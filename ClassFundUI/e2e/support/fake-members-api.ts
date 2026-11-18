import { Page, Route } from '@playwright/test';

export type Role = 'Owner' | 'Treasurer' | 'Student';

export interface FakeMember {
  id: string;
  userId: string;
  fullName: string;
  email: string;
  role: Role;
  status: 'Active' | 'Removed';
  mustChangePassword: boolean;
  joinedAt: string;
  removedAt: string | null;
  canManageAccount: boolean;
}

export const FUND_ID = 'f1';

export function member(partial: Partial<FakeMember> & Pick<FakeMember, 'fullName' | 'email'>): FakeMember {
  const slug = partial.email.split('@')[0];
  return {
    id: `m-${slug}`,
    userId: `u-${slug}`,
    role: 'Student',
    status: 'Active',
    mustChangePassword: false,
    joinedAt: '2026-09-02T03:00:00Z',
    removedAt: null,
    canManageAccount: true,
    ...partial,
  };
}

export interface Recorded {
  method: string;
  path: string;
  body: unknown;
  query: URLSearchParams;
}

/**
 * API thành viên giả lập trong bộ nhớ, mô phỏng hợp đồng của backend (docs/api/api-catalogue.md):
 * lọc / tìm / phân trang, kết quả từng email khi thêm, mã lỗi ProblemDetails.
 */
export class FakeMembersApi {
  members: FakeMember[];
  readonly requests: Recorded[] = [];
  /** Số khoản chưa đóng / số dư cho xem trước khi ngừng, theo email. */
  impact = new Map<string, { unpaidCount?: number; balance?: number; pendingPaymentCount?: number }>();
  archived = false;
  private counter = 0;

  constructor(private readonly page: Page, members: FakeMember[]) {
    this.members = members;
  }

  async install(): Promise<void> {
    await this.page.route(new RegExp(`/api/v1/funds/${FUND_ID}/members`), (route) => this.handle(route));
  }

  last(method: string, path: string): Recorded | undefined {
    return [...this.requests].reverse().find((item) => item.method === method && item.path === path);
  }

  private nextPassword(): string {
    this.counter += 1;
    return `Tam-${String(this.counter).padStart(4, '0')}-abcd`;
  }

  private problem(route: Route, status: number, code: string, title: string, errors?: Record<string, string[]>): Promise<void> {
    return route.fulfill({ status, contentType: 'application/problem+json', body: JSON.stringify({ status, code, title, errors }) });
  }

  private addOne(rawEmail: string, fullName: string | null): Record<string, unknown> {
    const email = rawEmail.trim().toLowerCase();
    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
      return { email: rawEmail.trim(), outcome: 'Invalid', member: null, temporaryPassword: null, message: 'Email không hợp lệ.' };
    }
    const existing = this.members.find((item) => item.email === email);
    if (existing?.status === 'Active') {
      return { email, outcome: 'AlreadyExists', member: existing, temporaryPassword: null, message: 'Học sinh này đã ở trong quỹ.' };
    }
    if (existing) {
      existing.status = 'Active';
      existing.removedAt = null;
      existing.role = 'Student';
      return { email, outcome: 'Reactivated', member: existing, temporaryPassword: null, message: null };
    }
    if (email.startsWith('co-tai-khoan')) {
      const added = member({ fullName: 'Đã Có Tài Khoản', email, canManageAccount: false });
      this.members.push(added);
      return { email, outcome: 'ExistingAdded', member: added, temporaryPassword: null, message: null };
    }
    const created = member({ fullName: fullName ?? email.split('@')[0], email, mustChangePassword: true });
    this.members.push(created);
    return { email, outcome: 'Created', member: created, temporaryPassword: this.nextPassword(), message: null };
  }

  private async handle(route: Route): Promise<void> {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(`/api/v1/funds/${FUND_ID}/members`, '') || '/';
    const method = request.method();
    const body = request.postData() ? JSON.parse(request.postData() as string) : null;
    this.requests.push({ method, path, body, query: url.searchParams });

    if (method === 'GET' && path === '/') {
      const status = url.searchParams.get('status') ?? 'Active';
      const search = (url.searchParams.get('search') ?? '').toLowerCase();
      const page = Number(url.searchParams.get('page') ?? 1);
      const pageSize = Number(url.searchParams.get('pageSize') ?? 20);
      const rank = { Owner: 0, Treasurer: 1, Student: 2 } as const;
      const rows = this.members
        .filter((item) => item.status === status)
        .filter((item) => !search || item.fullName.toLowerCase().includes(search) || item.email.includes(search))
        .sort((a, b) => rank[a.role] - rank[b.role] || a.fullName.localeCompare(b.fullName));
      return route.fulfill({ json: { items: rows.slice((page - 1) * pageSize, page * pageSize), total: rows.length, page, pageSize } });
    }

    if (method === 'POST' && path === '/') {
      if (this.archived) {
        return this.problem(route, 422, 'fund.archived', 'Quỹ đã tổng kết cuối năm, chỉ được xem.');
      }
      const result = this.addOne(body.email, body.fullName);
      if (result['outcome'] === 'AlreadyExists') {
        return this.problem(route, 409, 'member.already_exists', 'Học sinh này đã ở trong quỹ.');
      }
      return route.fulfill({ status: 201, json: result });
    }

    if (method === 'POST' && path === '/bulk') {
      const seen = new Set<string>();
      const emails = (body.emails as string[]).filter((email) => {
        const key = email.trim().toLowerCase();
        return key && !seen.has(key) && seen.add(key);
      });
      return route.fulfill({ json: { results: emails.map((email) => this.addOne(email, null)) } });
    }

    const memberMatch = /^\/([^/]+)(\/[a-z-]+)?$/.exec(path);
    const target = memberMatch ? this.members.find((item) => item.id === memberMatch[1]) : undefined;
    if (!target) {
      return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
    }
    const action = memberMatch?.[2] ?? '';

    if (method === 'PUT' && action === '') {
      if (!target.canManageAccount) {
        return this.problem(route, 422, 'member.account_not_managed', 'Tài khoản này do chính người dùng quản lý.');
      }
      target.fullName = body.fullName.trim();
      return route.fulfill({ json: target });
    }

    if (method === 'POST' && action === '/reset-password') {
      if (!target.canManageAccount) {
        return this.problem(route, 422, 'member.account_not_managed', 'Tài khoản này do chính người dùng quản lý.');
      }
      target.mustChangePassword = true;
      return route.fulfill({ json: { temporaryPassword: this.nextPassword() } });
    }

    const effect = this.impact.get(target.email) ?? {};
    if (method === 'GET' && action === '/removal-preview') {
      const balance = effect.balance ?? 0;
      const pending = effect.pendingPaymentCount ?? 0;
      const reason = balance > 0 ? 'Thành viên còn số dư cá nhân. Số dư phải về 0 (rút tiền) trước khi ngừng.' : null;
      return route.fulfill({
        json: { unpaidCount: effect.unpaidCount ?? 0, pendingPaymentCount: pending, balance, canRemove: !reason && pending === 0, blockedReason: reason },
      });
    }

    if (method === 'POST' && action === '/remove') {
      if ((effect.balance ?? 0) > 0) {
        return this.problem(route, 422, 'member.has_balance', 'Thành viên còn số dư cá nhân.');
      }
      target.status = 'Removed';
      target.removedAt = '2026-11-18T03:00:00Z';
      return route.fulfill({ json: { cancelledAssignments: effect.unpaidCount ?? 0 } });
    }

    return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
  }
}
