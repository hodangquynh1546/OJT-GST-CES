import { Page, Route } from '@playwright/test';

export interface FakeUser {
  id: string;
  email: string;
  fullName: string;
  mustChangePassword: boolean;
  accountPermissions: string[];
  memberships: {
    fundId: string;
    fundName: string;
    role: 'Owner' | 'Treasurer' | 'Student';
    permissions: string[];
  }[];
}

export const AN: FakeUser = {
  id: 'u1',
  email: 'an@example.com',
  fullName: 'Nguyễn An',
  mustChangePassword: false,
  accountPermissions: ['Fund.Create'],
  memberships: [],
};

export interface Recorded {
  method: string;
  path: string;
  body: unknown;
  csrf: string | null;
}

/**
 * API xác thực giả lập trong bộ nhớ (cookie phiên ≈ biến `session`). Mô phỏng đúng hợp đồng của backend:
 * mã lỗi, ProblemDetails, 401 khi chưa đăng nhập, 403 password.change_required khi còn mật khẩu tạm.
 */
export class FakeAuthApi {
  session: FakeUser | null = null;
  readonly requests: Recorded[] = [];
  /** Tài khoản có thể đăng nhập: email → { password, user }. */
  accounts = new Map<string, { password: string; user: FakeUser }>();

  constructor(private readonly page: Page) {}

  async install(): Promise<void> {
    await this.page.route('**/api/v1/auth/**', (route) => this.handle(route));
    // Trang đích sau đăng nhập (F-01, M2) gọi GET /funds; header/A-04 có thể gọi ảnh đại diện (M5).
    await this.page.route('**/api/v1/funds', (route) =>
      this.session ? route.fulfill({ json: [] }) : this.problem(route, 401, 'unauthorized', 'Bạn chưa đăng nhập.'),
    );
    await this.page.route('**/api/v1/users/*/avatar', (route) => this.problem(route, 404, 'not_found', 'Không tìm thấy.'));
  }

  addAccount(user: FakeUser, password: string): void {
    this.accounts.set(user.email, { password, user });
  }

  signIn(user: FakeUser): void {
    this.session = user;
  }

  last(method: string, path: string): Recorded | undefined {
    return [...this.requests].reverse().find((item) => item.method === method && item.path === path);
  }

  private problem(route: Route, status: number, code: string, title: string, errors?: Record<string, string[]>): Promise<void> {
    return route.fulfill({
      status,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status, code, title, errors }),
    });
  }

  private async handle(route: Route): Promise<void> {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace('/api/v1/auth', '');
    const method = request.method();
    const body = request.postData() ? JSON.parse(request.postData() as string) : null;
    const csrf = request.headers()['x-xsrf-token'] ?? null;
    this.requests.push({ method, path, body, csrf });

    if (path === '/antiforgery') {
      return route.fulfill({ json: { antiforgeryToken: 'anon-token' } });
    }
    if (method !== 'GET' && !csrf) {
      return this.problem(route, 400, 'antiforgery_failed', 'Thiếu token.');
    }

    switch (`${method} ${path}`) {
      case 'GET /me':
        return this.session ? route.fulfill({ json: this.session }) : this.problem(route, 401, 'unauthorized', 'Bạn chưa đăng nhập.');

      case 'POST /login': {
        const account = this.accounts.get(String(body.email).trim().toLowerCase());
        if (!account || account.password !== body.password) {
          return this.problem(route, 401, 'auth.invalid_credentials', 'Email hoặc mật khẩu không đúng.');
        }
        this.session = account.user;
        return route.fulfill({ json: { antiforgeryToken: 'user-token', user: account.user } });
      }

      case 'POST /register': {
        if (this.accounts.has(String(body.email).trim().toLowerCase())) {
          return this.problem(route, 409, 'auth.email_exists', 'Email này đã được đăng ký.');
        }
        const user: FakeUser = { ...AN, email: body.email, fullName: body.fullName };
        this.addAccount(user, body.password);
        this.session = user;
        return route.fulfill({ status: 201, json: { antiforgeryToken: 'user-token', user } });
      }

      case 'POST /logout':
        this.session = null;
        return route.fulfill({ status: 204 });

      case 'PUT /me': {
        if (!this.session) {
          return this.problem(route, 401, 'unauthorized', 'Bạn chưa đăng nhập.');
        }
        if (this.session.mustChangePassword) {
          return this.problem(route, 403, 'password.change_required', 'Bạn cần đặt mật khẩu mới.');
        }
        this.session = { ...this.session, fullName: body.fullName };
        return route.fulfill({ json: this.session });
      }

      case 'POST /change-password': {
        if (!this.session) {
          return this.problem(route, 401, 'unauthorized', 'Bạn chưa đăng nhập.');
        }
        const account = this.accounts.get(this.session.email);
        if (!account || account.password !== body.currentPassword) {
          return this.problem(route, 422, 'auth.current_password_incorrect', 'Mật khẩu hiện tại không đúng.');
        }
        account.password = body.newPassword;
        this.session = { ...this.session, mustChangePassword: false };
        account.user = this.session;
        return route.fulfill({ json: { antiforgeryToken: 'user-token-2', user: this.session } });
      }

      default:
        return this.problem(route, 404, 'not_found', 'Không tìm thấy.');
    }
  }
}
