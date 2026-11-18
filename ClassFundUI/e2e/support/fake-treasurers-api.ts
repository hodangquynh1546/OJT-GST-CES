import { Page, Route } from '@playwright/test';

export const FUND_ID = 'f1';

export interface FakePerson {
  memberId: string;
  userId: string;
  fullName: string;
  email: string;
  role: 'Owner' | 'Treasurer' | 'Student';
  joinedAt: string;
  grantedAt: string | null;
}

export function person(partial: Partial<FakePerson> & Pick<FakePerson, 'fullName' | 'email'>): FakePerson {
  const slug = partial.email.split('@')[0];
  return { memberId: `m-${slug}`, userId: `u-${slug}`, role: 'Student', joinedAt: '2026-09-02T03:00:00Z', grantedAt: null, ...partial };
}

export interface Recorded {
  method: string;
  path: string;
  body: unknown;
  query: URLSearchParams;
}

/** API đội thủ quỹ giả lập + danh sách học sinh để chọn (GET /members?role=Student), mô phỏng hợp đồng backend. */
export class FakeTreasurersApi {
  readonly requests: Recorded[] = [];
  /** Email → mã lỗi buộc API trả về khi phân quyền (mô phỏng xung đột). */
  failGrant = new Map<string, { status: number; code: string; title: string }>();
  private clock = 0;

  constructor(
    private readonly page: Page,
    public people: FakePerson[],
  ) {}

  async install(): Promise<void> {
    await this.page.route(new RegExp(`/api/v1/funds/${FUND_ID}/(treasurers|members)`), (route) => this.handle(route));
  }

  last(method: string, path: string): Recorded | undefined {
    return [...this.requests].reverse().find((item) => item.method === method && item.path === path);
  }

  private problem(route: Route, status: number, code: string, title: string): Promise<void> {
    return route.fulfill({ status, contentType: 'application/problem+json', body: JSON.stringify({ status, code, title }) });
  }

  private async handle(route: Route): Promise<void> {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(`/api/v1/funds/${FUND_ID}`, '');
    const method = request.method();
    const body = request.postData() ? JSON.parse(request.postData() as string) : null;
    this.requests.push({ method, path, body, query: url.searchParams });

    if (method === 'GET' && path === '/members') {
      const role = url.searchParams.get('role');
      const search = (url.searchParams.get('search') ?? '').toLowerCase();
      const rows = this.people
        .filter((item) => !role || item.role === role)
        .filter((item) => !search || item.fullName.toLowerCase().includes(search) || item.email.includes(search))
        .map((item) => ({
          id: item.memberId,
          userId: item.userId,
          fullName: item.fullName,
          email: item.email,
          role: item.role,
          status: 'Active',
          mustChangePassword: false,
          joinedAt: item.joinedAt,
          removedAt: null,
          canManageAccount: true,
        }));
      return route.fulfill({ json: { items: rows, total: rows.length, page: 1, pageSize: 50 } });
    }

    if (method === 'GET' && path === '/treasurers') {
      const rank = { Owner: 0, Treasurer: 1, Student: 2 } as const;
      const team = this.people.filter((item) => item.role !== 'Student').sort((a, b) => rank[a.role] - rank[b.role]);
      return route.fulfill({ json: team });
    }

    if (method === 'POST' && path === '/treasurers') {
      const target = this.people.find((item) => item.memberId === body.memberId);
      if (!target) {
        return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
      }
      const failure = this.failGrant.get(target.email);
      if (failure) {
        return this.problem(route, failure.status, failure.code, failure.title);
      }
      if (target.role !== 'Student') {
        return this.problem(route, 409, 'treasurer.already_treasurer', 'Người này đã là thủ quỹ của quỹ.');
      }
      this.clock += 1;
      target.role = 'Treasurer';
      target.grantedAt = `2026-11-1${this.clock}T03:00:00Z`;
      return route.fulfill({ status: 201, json: target });
    }

    const revoke = /^\/treasurers\/([^/]+)\/revoke$/.exec(path);
    if (method === 'POST' && revoke) {
      const target = this.people.find((item) => item.memberId === revoke[1]);
      if (!target) {
        return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
      }
      if (target.role === 'Owner') {
        return this.problem(route, 422, 'treasurer.cannot_revoke_owner', 'Không thể thu hồi quyền của thủ quỹ chính.');
      }
      target.role = 'Student';
      target.grantedAt = null;
      return route.fulfill({ json: target });
    }

    return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
  }
}
