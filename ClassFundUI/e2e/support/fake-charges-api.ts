import { Page, Route } from '@playwright/test';

export const FUND_ID = 'f1';

export interface FakeCharge {
  id: string;
  title: string;
  purpose: string | null;
  amountPerPerson: number;
  dueDate: string;
  status: 'Open' | 'Closed' | 'Cancelled';
  createdAt: string;
  categoryId: string | null;
  categoryName: string | null;
  progress: { assignedCount: number; paidCount: number; awaitingCount: number; collectedAmount: number } | null;
  mine: { assignmentId: string; status: string; amountDue: number } | null;
}

export interface FakeMine {
  assignmentId: string;
  chargeId: string;
  title: string;
  purpose: string | null;
  amountDue: number;
  dueDate: string;
  chargeStatus: 'Open' | 'Closed';
  status: 'Unpaid' | 'AwaitingConfirmation' | 'Paid';
  paymentId: string | null;
  method: string | null;
  paymentStatus: string | null;
  rejectionReason: string | null;
  isSelfDeclared: boolean;
  eventId?: string | null;
  eventName?: string | null;
  balanceApplied?: number;
}

export interface FakeQueueItem {
  id: string;
  assignmentId: string;
  chargeId: string;
  chargeTitle: string;
  payerUserId: string;
  payerName: string;
  payerEmail: string;
  amount: number;
  method: 'Cash' | 'BankTransfer';
  transferCode: string | null;
  status: 'Submitted' | 'Confirmed' | 'Rejected';
  submittedAt: string;
  confirmedAt: string | null;
  isSelfDeclared: boolean;
  rejectionReason: string | null;
  attachments: unknown[];
  eventId?: string | null;
  eventName?: string | null;
}

export interface Recorded {
  method: string;
  path: string;
  body: unknown;
}

/** API khoản thu / thanh toán giả lập, mô phỏng hợp đồng backend M6a (docs/api/api-catalogue.md). */
export class FakeChargesApi {
  readonly requests: Recorded[] = [];
  /** Mã lỗi buộc POST nộp tiền / xác nhận trả về (mô phỏng xung đột). */
  failNext: { status: number; code: string; title: string } | null = null;
  /** Số dư cá nhân của người đang đăng nhập; 0 = chưa có. `withdrawalPending` mô phỏng yêu cầu rút đang chờ. */
  balance = 0;
  withdrawalPending = false;

  constructor(
    private readonly page: Page,
    public charges: FakeCharge[],
    public mine: FakeMine[] = [],
    public queue: FakeQueueItem[] = [],
    private readonly memberTotal = 32,
  ) {}

  async install(): Promise<void> {
    await this.page.route(new RegExp(`/api/v1/funds/${FUND_ID}/(charges|me/assignments|me/balance|payments|members|categories)`), (route) => this.handle(route));
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
        progress: { assignedCount: this.memberTotal, paidCount: 0, awaitingCount: 0, collectedAmount: 0 },
        mine: null,
      };
      this.charges.unshift(charge);
      return this.json(route, charge, 201);
    }
    if (method === 'GET' && path === '/me/balance') {
      const latestWithdrawal = this.withdrawalPending
        ? { id: 'w1', amount: this.balance, status: 'Requested', requestedAt: '2026-11-20T01:00:00Z', handledAt: null, handledByName: null, rejectionReason: null }
        : null;
      return this.json(route, { fundName: 'Lớp 10A1', balance: this.balance, latestWithdrawal, history: [], otherFunds: [] });
    }
    if (method === 'GET' && path === '/me/assignments') {
      return this.json(route, this.mine);
    }
    const submit = /^\/me\/assignments\/([^/]+)\/payments$/.exec(path);
    if (method === 'POST' && submit) {
      if (this.failNext) {
        const { status, code, title } = this.failNext;
        this.failNext = null;
        return this.problem(route, status, code, title);
      }
      const item = this.mine.find((entry) => entry.assignmentId === submit[1]);
      if (item) {
        const instant = body.method === 'PersonalBalance';
        if (body.useBalance && !instant) {
          item.balanceApplied = Math.min(this.balance, item.amountDue);
          this.balance -= item.balanceApplied;
        }
        item.status = instant ? 'Paid' : 'AwaitingConfirmation';
        item.method = body.method;
        item.paymentStatus = instant ? 'Confirmed' : 'Submitted';
        if (instant) {
          this.balance -= item.amountDue;
        }
      }
      const status = body.method === 'PersonalBalance' ? 'Confirmed' : 'Submitted';
      return this.json(route, { id: 'p-new', assignmentId: submit[1], amount: item?.amountDue ?? 0, method: body.method, transferCode: body.transferCode, status, submittedAt: '2026-11-19T01:00:00Z', isSelfDeclared: false }, 201);
    }
    if (method === 'GET' && path === '/payments') {
      const status = url.searchParams.get('status') ?? 'Submitted';
      const items = this.queue.filter((entry) => entry.status === status);
      return this.json(route, { items, total: items.length, page: 1, pageSize: 100 });
    }
    const action = /^\/payments\/([^/]+)\/(confirm|reject)$/.exec(path);
    if (method === 'POST' && action) {
      if (this.failNext) {
        const { status, code, title } = this.failNext;
        this.failNext = null;
        return this.problem(route, status, code, title);
      }
      const entry = this.queue.find((item) => item.id === action[1]);
      if (!entry) {
        return this.problem(route, 404, 'not_found', 'Không tìm thấy');
      }
      entry.status = action[2] === 'confirm' ? 'Confirmed' : 'Rejected';
      entry.rejectionReason = body?.reason ?? null;
      return this.json(route, entry);
    }
    return route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ status: 404, code: 'not_found', title: 'x' }) });
  }
}
