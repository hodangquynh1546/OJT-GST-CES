import { APIRequestContext, expect, request as playwrightRequest } from '@playwright/test';

/**
 * Chế độ **full** (`E2E_FULL=1`): gọi API thật (qua proxy Angular → ClassFund.Api → PostgreSQL) để seed dữ liệu
 * cho test, giống cách SPA gọi: cookie phiên + header X-XSRF-TOKEN; token mới lấy từ response có `antiforgeryToken`.
 * Seed bằng chính API của các module đã xong (ví dụ đăng ký GVCN → tạo quỹ), không ghi thẳng DB.
 */
export class RealApi {
  private token: string | null = null;

  constructor(private readonly request: APIRequestContext) {}

  async get<T>(path: string): Promise<T> {
    const response = await this.request.get(`/api/v1${path}`);
    expect(response.ok(), `${path}: ${response.status()} ${await response.text()}`).toBeTruthy();
    return (await response.json()) as T;
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    return this.send<T>('POST', path, body);
  }

  async put<T>(path: string, body: unknown): Promise<T> {
    return this.send<T>('PUT', path, body);
  }

  private async send<T>(method: 'POST' | 'PUT', path: string, body: unknown): Promise<T> {
    const response = await this.request.fetch(`/api/v1${path}`, {
      method,
      data: body,
      headers: { 'X-XSRF-TOKEN': await this.antiforgery() },
    });
    expect(response.ok(), `${method} ${path}: ${response.status()} ${await response.text()}`).toBeTruthy();
    const text = await response.text();
    const json = text ? (JSON.parse(text) as T & { antiforgeryToken?: string }) : (undefined as T);
    if (json && typeof json === 'object' && 'antiforgeryToken' in json && json.antiforgeryToken) {
      this.token = json.antiforgeryToken;
    }
    return json;
  }

  private async antiforgery(): Promise<string> {
    if (!this.token) {
      this.token = (await this.get<{ antiforgeryToken: string }>('/auth/antiforgery')).antiforgeryToken;
    }
    return this.token;
  }
}

/** Tài khoản thật tạo bằng API (M1). `login` là email (giáo viên, phụ huynh) hoặc tên đăng nhập (học sinh). */
export interface RealAccount {
  login: string;
  password: string;
  fullName: string;
}

export const E2E_PASSWORD = 'Matkhau123';

/**
 * M1: đăng ký GVCN mới bằng `POST /auth/register-teacher` – `api` được đăng nhập luôn bằng tài khoản này
 * (cookie phiên + token antiforgery mới), dùng tiếp để seed (ví dụ M2: `api.post('/funds', { name })`).
 */
export async function registerTeacher(api: RealApi, fullName = 'Cô Hạnh', password = E2E_PASSWORD): Promise<RealAccount> {
  const login = `${unique('gv')}@example.vn`;
  await api.post('/auth/register-teacher', { fullName, email: login, password, confirmPassword: password });
  return { login, password, fullName };
}

/** M1: đăng nhập bằng `POST /auth/login` (không có vai trò); `api` chuyển sang phiên của tài khoản này. */
export async function login(api: RealApi, account: Pick<RealAccount, 'login' | 'password'>): Promise<void> {
  await api.post('/auth/login', { login: account.login, password: account.password });
}

/** Học sinh / phụ huynh seed bằng API; `fundId` là quỹ GVCN (`api`) vừa tạo. */
export interface RealStudent extends RealAccount {
  studentId: string;
}

/** M3: GVCN (`api`) thêm học sinh vào quỹ (chưa có tài khoản); có `parentEmail` thì liên kết phụ huynh luôn. */
export async function createStudent(
  api: RealApi,
  fundId: string,
  fullName = `Học sinh ${unique('')}`,
  parentEmail?: string,
): Promise<{ studentId: string; fullName: string }> {
  const created = await api.post<{ id: string }>(`/funds/${fundId}/students`, { fullName, parentEmail });
  return { studentId: created.id, fullName };
}

/** M3: GVCN cấp tài khoản – học sinh ở trạng thái "Chờ kích hoạt", đăng nhập bằng `login` + mật khẩu tạm `password`. */
export async function grantAccount(api: RealApi, fundId: string, studentId: string, fullName = 'Học sinh', username?: string): Promise<RealStudent> {
  const credential = await api.post<{ login: string; temporaryPassword: string }>(`/funds/${fundId}/students/${studentId}/account`, { username });
  return { studentId, fullName, login: credential.login, password: credential.temporaryPassword };
}

/**
 * M3: học sinh đã **kích hoạt** (đã đổi mật khẩu tạm sang `E2E_PASSWORD`) – điều kiện để M4 phân công thủ quỹ.
 * Đăng nhập bằng một phiên riêng nên phiên của GVCN (`api`) không bị đổi.
 */
export async function createActiveStudent(api: RealApi, fundId: string, fullName = `Học sinh ${unique('')}`): Promise<RealStudent> {
  const { studentId } = await createStudent(api, fundId, fullName);
  const pending = await grantAccount(api, fundId, studentId, fullName);
  const context = await playwrightRequest.newContext({ baseURL: 'http://localhost:4200' });
  try {
    const studentApi = new RealApi(context);
    await login(studentApi, pending);
    await studentApi.post('/auth/change-password', { currentPassword: pending.password, newPassword: E2E_PASSWORD });
  } finally {
    await context.dispose();
  }
  return { ...pending, password: E2E_PASSWORD };
}

/** M4: GVCN (`api`) phân công học sinh đã kích hoạt làm thủ quỹ; trả về id phân công. */
export async function assignTreasurer(api: RealApi, fundId: string, studentId: string): Promise<string> {
  return (await api.post<{ id: string }>(`/funds/${fundId}/treasurers`, { studentId })).id;
}

export interface NewTransaction {
  type: 'Income' | 'Expense';
  amount: number;
  description: string;
  /** Tên danh mục (mặc định "Đóng quỹ" / "Chi khác" – danh mục mặc định khi tạo quỹ) hoặc `categoryId`. */
  category?: string;
  categoryId?: string;
  /** `yyyy-MM-dd`, mặc định hôm nay theo giờ Việt Nam. */
  occurredOn?: string;
  isPublic?: boolean;
  note?: string;
}

/**
 * M6: thủ quỹ (`api` đang đăng nhập bằng thủ quỹ) ghi một khoản thu / chi bằng `POST /transactions`.
 * Danh mục tìm theo tên qua `GET /categories` (M5). Trả về khoản đã lưu (bản đầy đủ, có `isNegativeBalance`).
 */
export async function createTransaction(api: RealApi, fundId: string, input: NewTransaction): Promise<{ id: string; isNegativeBalance: boolean }> {
  let categoryId = input.categoryId;
  if (!categoryId) {
    const name = input.category ?? (input.type === 'Income' ? 'Đóng quỹ' : 'Chi khác');
    const categories = await api.get<{ id: string; type: string; name: string }[]>(`/funds/${fundId}/categories`);
    categoryId = categories.find((category) => category.type === input.type && category.name === name)?.id;
    expect(categoryId, `Không có danh mục "${name}" (${input.type})`).toBeTruthy();
  }
  const occurredOn = input.occurredOn ?? new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
  return api.post(`/funds/${fundId}/transactions`, {
    type: input.type,
    amount: input.amount,
    occurredOn,
    categoryId,
    description: input.description,
    note: input.note ?? null,
    isPublic: input.isPublic ?? true,
    idempotencyKey: crypto.randomUUID(),
  });
}

/** Email / tên không trùng giữa các lần chạy (DB e2e dùng chung). */
export function unique(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

export const isFullE2E = process.env['E2E_FULL'] === '1';
