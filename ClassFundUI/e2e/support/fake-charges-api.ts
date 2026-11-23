import { Page, Route } from '@playwright/test';

export const FUND_ID = 'f1';

export interface FakeCharge {
  id: string;
  title: string;
  purpose: string | null;
  amountPerPerson: number;
  dueDate: string | null;
  status: 'Open' | 'Closed' | 'Cancelled';
  createdAt: string;
  categoryId: string | null;
  categoryName: string | null;
  progress: { assignedCount: number; paidCount: number; collectedAmount: number } | null;
  mine: { assignmentId: string; status: string; amountDue: number } | null;
}

export interface FakeMine {
  assignmentId: string;
  chargeId: string;
  title: string;
  purpose: string | null;
  amountDue: number;
  dueDate: string | null;
  chargeStatus: 'Open' | 'Closed';
  status: 'Unpaid' | 'Paid';
  method: string | null;
  paidAt: string | null;
  isSelfDeclared: boolean;
  eventId?: string | null;
  eventName?: string | null;
}

/** Một dòng "ai nộp / chưa nộp" của một khoản thu (thủ quỹ). */
export interface FakeRow {
  assignmentId: string;
  userId: string;
  fullName: string;
  email: string;
  role: 'Student' | 'Treasurer' | 'Owner';
  amountDue: number;
  status: 'Unpaid' | 'Paid';
  isSelfDeclared: boolean;
  paymentId: string | null;
  method: string | null;
  paymentAt: string | null;
  hasProof: boolean;
  rejectionReason: string | null;
}

export interface Recorded {
  method: string;
  path: string;
  body: unknown;
}

/** API khoản thu / thu tiền giả lập, mô phỏng hợp đồng backend (docs/api/api-catalogue.md). */
export class FakeChargesApi {
  readonly requests: Recorded[] = [];
  /** Lỗi buộc POST collect / uncollect / remind trả về (mô phỏng xung đột). */
  failNext: { status: number; code: string; title: string } | null = null;

  constructor(
    private readonly page: Page,
    public charges: FakeCharge[],
    public mine: FakeMine[] = [],
    public rows: FakeRow[] = [],
    private readonly memberTotal = 32,
  ) {}

  async install(): Promise<void> {
    await this.page.route(new RegExp(`/api/v1/funds/${FUND_ID}/(charges|me/assignments|assignments|members|categories)`), (route) => this.handle(route));
  }

  last(method: string, path: string): Recorded | undefined {
    return [...this.requests].reverse().find((item) => item.method === method && item.path === path);
  }

  count(method: string, pathPart: string): number {
    return this.requests.filter((item) => item.method === method && item.path.includes(pathPart)).length;
  }

  private json(route: Route, body: unknown, status = 200): Promise<void> {
    return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
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
    this.requests.push({ method, path, body });

    if (method === 'GET' && path === '/categories') {
      return this.json(route, [{ id: 'cat1', name: 'Quỹ lớp', type: 'Income', isSystem: false, isActive: true }]);
    }
    if (method === 'GET' && path === '/members') {
      return this.json(route, { items: [], total: this.memberTotal, page: 1, pageSize: 1 });
    }
    if (method === 'GET' && path === '/charges') {
      const status = url.searchParams.get('status');
      const items = this.charges.filter((charge) => !status || charge.status === status);
      return this.json(route, { items, total: items.length, page: 1, pageSize: 100 });
    }
    if (method === 'POST' && path === '/charges') {
      const charge: FakeCharge = {
        id: `c${this.charges.length + 1}`,
        title: body.title,
        purpose: body.purpose,
        amountPerPerson: body.amountPerPerson,
        dueDate: body.dueDate,
        status: 'Open',
        createdAt: '2026-11-19T01:00:00Z',
        categoryId: body.categoryId,
        categoryName: 'Quỹ lớp',
        progress: { assignedCount: this.memberTotal, paidCount: 0, collectedAmount: 0 },
        mine: null,
      };
      this.charges.unshift(charge);
      return this.json(route, charge, 201);
    }
    const one = /^\/charges\/([^/]+)$/.exec(path);
    if (method === 'GET' && one) {
      const found = this.charges.find((charge) => charge.id === one[1]);
      return found ? this.json(route, found) : this.problem(route, 404, 'not_found', 'x');
    }
    if (method === 'GET' && /^\/charges\/[^/]+\/assignments$/.test(path)) {
      const status = url.searchParams.get('status');
      const items = this.rows.filter((row) => !status || row.status === status);
      return this.json(route, { items, total: items.length, page: 1, pageSize: 20 });
    }
    if (method === 'GET' && path === '/me/assignments') {
      return this.json(route, this.mine);
    }
    const action = /^\/assignments\/([^/]+)\/(collect|uncollect|remind)$/.exec(path);
    if (method === 'POST' && action) {
      if (this.failNext) {
        const { status, code, title } = this.failNext;
        this.failNext = null;
        return this.problem(route, status, code, title);
      }
      const row = this.rows.find((entry) => entry.assignmentId === action[1]);
      if (!row) {
        return this.problem(route, 404, 'not_found', 'Không tìm thấy');
      }
      if (action[2] === 'remind') {
        return this.json(route, { reminded: 1 });
      }
      const collect = action[2] === 'collect';
      row.status = collect ? 'Paid' : 'Unpaid';
      row.method = collect ? body.method : null;
      row.paymentAt = collect ? '2026-11-19T01:00:00Z' : null;
      return this.json(route, { id: 'p-new', assignmentId: row.assignmentId, amount: row.amountDue, method: body?.method ?? 'Cash', transferCode: body?.transferCode ?? null, status: 'Confirmed', confirmedAt: '2026-11-19T01:00:00Z', isSelfDeclared: false }, collect ? 201 : 200);
    }
    return route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ status: 404, code: 'not_found', title: 'x' }) });
  }
}
