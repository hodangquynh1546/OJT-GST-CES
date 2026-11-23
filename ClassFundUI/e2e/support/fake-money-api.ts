import { Page, Route } from '@playwright/test';

import { AN, FakeAuthApi, FakeUser } from './fake-auth-api';

/** API giả lập M7 (sổ thu chi) + M8 (sự kiện), dữ liệu theo canvas LedgerBody / EventListBody / Event. */

export const FUND_ID = 'f1';
export const OWNER_PERMISSIONS = [
  'Fund.View', 'Fund.Manage', 'Member.Manage', 'Treasurer.Manage', 'Category.Manage', 'Event.Manage', 'Event.Participate',
  'Charge.Manage', 'Ledger.Manage', 'Payment.Submit', 'Payment.Confirm', 'Settlement.Execute', 'Balance.ViewOwn',
  'Balance.Withdraw', 'Balance.Handle', 'Report.View', 'Audit.View',
];
export const STUDENT_PERMISSIONS = ['Fund.View', 'Event.Participate', 'Payment.Submit', 'Balance.ViewOwn', 'Balance.Withdraw'];

export const owner: FakeUser = {
  ...AN,
  fullName: 'Nguyễn Minh Anh',
  memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role: 'Owner', permissions: OWNER_PERMISSIONS }],
};
export const student: FakeUser = {
  ...AN,
  fullName: 'Lê Thu Hà',
  memberships: [{ fundId: FUND_ID, fundName: 'Lớp 10A1', role: 'Student', permissions: STUDENT_PERMISSIONS }],
};

const TODAY = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
const MONTH = TODAY.slice(0, 8);

export interface Recorded {
  method: string;
  path: string;
  body: Record<string, unknown> | null;
  query: URLSearchParams;
}

interface Entry {
  id: string;
  eventId: string | null;
  eventName: string | null;
  type: 'Income' | 'Expense';
  source: 'Manual' | 'Payment';
  categoryId: string | null;
  categoryName: string | null;
  title: string;
  amount: number;
  transactionDate: string;
  performedByUserId: string;
  performedByName: string;
  isSelfDeclared: boolean;
  reversesEntryId: string | null;
  reversedByEntryId: string | null;
  attachmentCount: number;
  createdAt: string;
  purpose?: string | null;
  note?: string | null;
}

let seq = 0;
const id = (prefix: string) => `${prefix}-${++seq}`;

function entry(partial: Partial<Entry> & Pick<Entry, 'title' | 'amount' | 'type'>): Entry {
  return {
    id: id('le'),
    eventId: null,
    eventName: null,
    source: 'Manual',
    categoryId: 'c-gift',
    categoryName: 'Quà tặng',
    transactionDate: `${MONTH}12`,
    performedByUserId: 'u-anh',
    performedByName: 'Nguyễn Minh Anh',
    isSelfDeclared: false,
    reversesEntryId: null,
    reversedByEntryId: null,
    attachmentCount: 0,
    createdAt: '2026-11-16T11:42:00Z',
    ...partial,
  };
}

export const CATEGORIES = [
  { id: 'c-fund', name: 'Thu quỹ', type: 'Income', isSystem: true, isActive: true, usageCount: 2 },
  { id: 'c-gift', name: 'Quà tặng', type: 'Expense', isSystem: false, isActive: true, usageCount: 1 },
  { id: 'c-study', name: 'Học tập', type: 'Expense', isSystem: false, isActive: true, usageCount: 1 },
  { id: 'c-clean', name: 'Vệ sinh', type: 'Expense', isSystem: false, isActive: true, usageCount: 1 },
];

interface FakeEvent {
  id: string;
  name: string;
  description: string | null;
  eventDate: string;
  status: 'Open' | 'Completed' | 'Settled' | 'Cancelled';
  invitedCount: number;
  participantCount: number;
  paidInFullCount: number;
  myResponse: 'Pending' | 'Accepted' | 'Declined' | null;
  income: number;
  expense: number;
}

interface Participant {
  userId: string;
  fullName: string;
  role: 'Owner' | 'Treasurer' | 'Student';
  paid: number;
  due: number;
  paymentStatus: 'Paid' | 'Pending' | 'Unpaid' | 'NoCharge';
  hasSelfDeclared: boolean;
  pendingPaymentCount: number;
}

export class FakeMoneyApi {
  readonly requests: Recorded[] = [];
  private readonly reminded = new Set<string>();
  readonly entries: Entry[];
  readonly events: FakeEvent[];
  readonly people: Participant[];

  constructor(private readonly page: Page) {
    const flowers = entry({ title: 'Mua hoa 20/11 tặng cô chủ nhiệm', amount: 650_000, type: 'Expense', transactionDate: `${MONTH}16`, attachmentCount: 2, performedByName: 'Trần Quang Huy', purpose: 'Hoa và thiệp tặng cô Lan', note: 'Mua tại shop hoa cổng trường' });
    const broom = entry({ title: 'Mua chổi, khăn lau', amount: 120_000, type: 'Expense', categoryId: 'c-clean', categoryName: 'Vệ sinh', transactionDate: `${MONTH}12` });
    const reversal = entry({ title: 'Đảo: Mua chổi, khăn lau', amount: 120_000, type: 'Income', categoryId: 'c-clean', categoryName: 'Vệ sinh', transactionDate: `${MONTH}13`, reversesEntryId: broom.id, note: 'Ghi trùng' });
    broom.reversedByEntryId = reversal.id;
    this.entries = [
      flowers,
      entry({ title: 'Thu: Quỹ lớp HK1 – Lê Thu Hà', amount: 200_000, type: 'Income', source: 'Payment', categoryId: 'c-fund', categoryName: 'Thu quỹ', transactionDate: `${MONTH}15`, performedByName: 'Trần Quang Huy' }),
      entry({ title: 'Thu: Quỹ lớp HK1 – Nguyễn Minh Anh', amount: 5_200_000, type: 'Income', source: 'Payment', categoryId: 'c-fund', categoryName: 'Thu quỹ', transactionDate: `${MONTH}15`, isSelfDeclared: true }),
      reversal,
      broom,
      entry({ title: 'In tài liệu ôn tập giữa kỳ', amount: 180_000, type: 'Expense', categoryId: 'c-study', categoryName: 'Học tập', attachmentCount: 1 }),
      entry({ title: 'Đặt xe 45 chỗ', amount: 4_800_000, type: 'Expense', eventId: 'e1', eventName: 'Dã ngoại Ba Vì', attachmentCount: 2, transactionDate: `${MONTH}17` }),
      entry({ title: 'Thu: Phí dã ngoại', amount: 9_100_000, type: 'Income', source: 'Payment', eventId: 'e1', eventName: 'Dã ngoại Ba Vì', transactionDate: `${MONTH}16` }),
    ];
    this.events = [
      { id: 'e1', name: 'Dã ngoại Ba Vì', description: 'Đi xe 45 chỗ, vé vào cổng, ăn trưa', eventDate: '2026-11-20', status: 'Open', invitedCount: 32, participantCount: 6, paidInFullCount: 2, myResponse: 'Pending', income: 9_100_000, expense: 4_800_000 },
      { id: 'e2', name: 'Liên hoan giữa kỳ', description: 'Liên hoan cuối đợt thi giữa kỳ', eventDate: '2026-10-26', status: 'Completed', invitedCount: 32, participantCount: 28, paidInFullCount: 28, myResponse: 'Accepted', income: 4_150_000, expense: 4_000_000 },
      { id: 'e3', name: 'Tham quan bảo tàng', description: 'Bảo tàng Lịch sử Quốc gia', eventDate: '2026-11-08', status: 'Settled', invitedCount: 32, participantCount: 30, paidInFullCount: 30, myResponse: 'Accepted', income: 3_000_000, expense: 1_860_000 },
      { id: 'e4', name: 'Cắm trại hè', description: 'Hủy do thời tiết', eventDate: '2026-09-30', status: 'Cancelled', invitedCount: 32, participantCount: 0, paidInFullCount: 0, myResponse: 'Declined', income: 0, expense: 0 },
    ];
    this.people = [
      { userId: 'u-anh', fullName: 'Nguyễn Minh Anh', role: 'Owner', paid: 430_000, due: 430_000, paymentStatus: 'Paid', hasSelfDeclared: true, pendingPaymentCount: 0 },
      { userId: 'u-ha', fullName: 'Lê Thu Hà', role: 'Student', paid: 430_000, due: 430_000, paymentStatus: 'Paid', hasSelfDeclared: false, pendingPaymentCount: 0 },
      { userId: 'u-bao', fullName: 'Hoàng Gia Bảo', role: 'Student', paid: 350_000, due: 430_000, paymentStatus: 'Pending', hasSelfDeclared: false, pendingPaymentCount: 1 },
      { userId: 'u-long', fullName: 'Phạm Đức Long', role: 'Student', paid: 350_000, due: 430_000, paymentStatus: 'Unpaid', hasSelfDeclared: false, pendingPaymentCount: 0 },
      { userId: 'u-my', fullName: 'Bùi Thảo My', role: 'Student', paid: 0, due: 430_000, paymentStatus: 'Unpaid', hasSelfDeclared: false, pendingPaymentCount: 0 },
      { userId: 'u-ngoc', fullName: 'Trịnh Bảo Ngọc', role: 'Student', paid: 0, due: 430_000, paymentStatus: 'Unpaid', hasSelfDeclared: false, pendingPaymentCount: 0 },
    ];
  }

  static async open(page: Page, user: FakeUser, url: string): Promise<FakeMoneyApi> {
    const auth = new FakeAuthApi(page);
    auth.addAccount(user, 'Passw0rd1');
    auth.signIn(user);
    await auth.install();
    const api = new FakeMoneyApi(page);
    await api.install(user === student);
    await page.goto(url);
    return api;
  }

  private isStudent = false;

  async install(isStudent = false): Promise<void> {
    this.isStudent = isStudent;
    await this.page.route(new RegExp(`/api/v1/funds/${FUND_ID}/(ledger-entries|events|categories|attachments)`), (route) => this.handle(route));
  }

  last(method: string, path: string): Recorded | undefined {
    return [...this.requests].reverse().find((item) => item.method === method && item.path === path);
  }

  private problem(route: Route, status: number, code: string, title: string): Promise<void> {
    return route.fulfill({ status, contentType: 'application/problem+json', body: JSON.stringify({ status, code, title }) });
  }

  private balance(eventId: string | null): number {
    return this.entries.filter((item) => item.eventId === eventId).reduce((sum, item) => sum + (item.type === 'Income' ? item.amount : -item.amount), 0);
  }

  private detail(item: Entry) {
    return {
      entry: item,
      purpose: item.purpose ?? null,
      note: item.note ?? null,
      performedByRole: 'Treasurer',
      attachments: Array.from({ length: item.attachmentCount }, (_, index) => ({
        id: `${item.id}-f${index}`,
        kind: 'Receipt',
        fileName: index === 0 ? 'hoa-don-hoa.jpg' : 'thiep.pdf',
        contentType: index === 0 ? 'image/jpeg' : 'application/pdf',
        sizeBytes: index === 0 ? 1_258_291 : 348_160,
        uploadedAt: item.createdAt,
      })),
      reversedBy: item.reversedByEntryId
        ? (() => {
            const reversal = this.entries.find((other) => other.id === item.reversedByEntryId)!;
            return { id: reversal.id, amount: reversal.amount, transactionDate: reversal.transactionDate, reason: reversal.note, performedByName: reversal.performedByName, createdAt: reversal.createdAt };
          })()
        : null,
      reverses: item.reversesEntryId
        ? (() => {
            const original = this.entries.find((other) => other.id === item.reversesEntryId)!;
            return { id: original.id, title: original.title, transactionDate: original.transactionDate };
          })()
        : null,
      canReverse: !this.isStudent && item.type === 'Expense' && item.source === 'Manual' && !item.reversedByEntryId && !item.reversesEntryId,
      history: this.isStudent ? [] : [{ action: 'ledger.create', actorName: item.performedByName, at: item.createdAt }],
    };
  }

  private eventDetail(event: FakeEvent) {
    const manage = !this.isStudent;
    return {
      id: event.id,
      name: event.name,
      description: event.description,
      eventDate: event.eventDate,
      status: event.status,
      createdAt: '2026-11-01T03:00:00Z',
      completedAt: event.status === 'Open' ? null : '2026-11-18T03:00:00Z',
      invitedCount: event.invitedCount,
      acceptedCount: event.participantCount,
      declinedCount: 2,
      pendingCount: 0,
      excludedCount: event.status === 'Open' ? 0 : 2,
      participantCount: event.participantCount,
      totalIncome: event.income,
      totalExpense: event.expense,
      balance: event.income - event.expense,
      amountPerPerson: 430_000,
      me: event.myResponse ? { response: event.myResponse, isFinal: event.status !== 'Open', exclusionReason: null } : null,
      myCharges:
        event.myResponse === 'Accepted'
          ? [
              { chargeId: 'ch1', title: 'Phí dã ngoại', amountDue: 350_000, dueDate: '2026-11-18', status: 'Unpaid' },
              { chargeId: 'ch2', title: 'Tiền ăn trưa', amountDue: 80_000, dueDate: '2026-11-19', status: 'Paid' },
            ]
          : [],
      charges: manage
        ? [
            { id: 'ch1', title: 'Phí dã ngoại', purpose: 'Xe 45 chỗ, vé vào cổng', amountPerPerson: 350_000, dueDate: '2026-11-18', status: 'Open', paidCount: 4, pendingCount: 1, unpaidCount: 2 },
            { id: 'ch2', title: 'Tiền ăn trưa', purpose: null, amountPerPerson: 80_000, dueDate: '2026-11-19', status: 'Open', paidCount: 2, pendingCount: 0, unpaidCount: 4 },
          ]
        : [],
    };
  }

  private async handle(route: Route): Promise<void> {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(`/api/v1/funds/${FUND_ID}`, '');
    const method = request.method();
    const raw = request.postData();
    const body = raw && request.headers()['content-type']?.includes('json') ? (JSON.parse(raw) as Record<string, unknown>) : null;
    this.requests.push({ method, path, body, query: url.searchParams });
    const query = url.searchParams;

    if (method === 'GET' && path === '/categories') {
      const type = query.get('type');
      return route.fulfill({ json: CATEGORIES.filter((item) => !type || item.type === type) });
    }

    if (method === 'POST' && path === '/attachments') {
      return route.fulfill({
        status: 201,
        json: { id: id('att'), kind: 'Receipt', fileName: 'hoa-don.png', contentType: 'image/png', sizeBytes: 2048, uploadedAt: '2026-11-18T03:00:00Z' },
      });
    }

    if (method === 'GET' && /^\/attachments\//.test(path)) {
      return route.fulfill({ status: 200, contentType: 'image/png', body: Buffer.from('89504e470d0a1a0a', 'hex') });
    }

    if (method === 'GET' && path === '/ledger-entries') {
      const eventId = query.get('eventId');
      const scope = this.entries.filter((item) => item.eventId === eventId);
      const visible = this.isStudent ? scope.filter((item) => item.source === 'Manual') : scope;
      const items = visible
        .filter((item) => !query.get('type') || item.type === query.get('type'))
        .filter((item) => !query.get('categoryId') || item.categoryId === query.get('categoryId'))
        .filter((item) => !query.get('from') || item.transactionDate >= query.get('from')!)
        .filter((item) => !query.get('to') || item.transactionDate <= query.get('to')!)
        .sort((a, b) => b.transactionDate.localeCompare(a.transactionDate));
      const income = scope.filter((item) => item.type === 'Income' && !item.reversesEntryId).reduce((sum, item) => sum + item.amount, 0);
      const reversedExpense = scope.filter((item) => item.reversesEntryId).reduce((sum, item) => sum + item.amount, 0);
      const expense = scope.filter((item) => item.type === 'Expense').reduce((sum, item) => sum + item.amount, 0) - reversedExpense;
      return route.fulfill({
        json: { items, total: items.length, page: 1, pageSize: 20, summary: { balance: this.balance(eventId), totalIncome: income, totalExpense: expense } },
      });
    }

    if (method === 'POST' && path === '/ledger-entries') {
      const amount = Number(body!['amount']);
      const eventId = (body!['eventId'] as string | null) ?? null;
      if (amount > this.balance(eventId)) {
        return this.problem(route, 422, 'ledger.exceeds_balance', 'Số tiền chi vượt số dư hiện có.');
      }
      const category = CATEGORIES.find((item) => item.id === body!['categoryId']);
      const created = entry({
        title: String(body!['title']),
        amount,
        type: 'Expense',
        eventId,
        eventName: eventId ? (this.events.find((item) => item.id === eventId)?.name ?? null) : null,
        categoryId: category?.id ?? null,
        categoryName: category?.name ?? null,
        transactionDate: String(body!['transactionDate']),
        attachmentCount: (body!['attachmentIds'] as string[]).length,
      });
      this.entries.unshift(created);
      const event = this.events.find((item) => item.id === eventId);
      if (event) {
        event.expense += amount;
      }
      return route.fulfill({ status: 201, json: this.detail(created) });
    }

    const entryMatch = /^\/ledger-entries\/([^/]+)(\/reverse)?$/.exec(path);
    if (entryMatch) {
      const item = this.entries.find((other) => other.id === entryMatch[1]);
      if (!item) {
        return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
      }
      if (method === 'GET') {
        return route.fulfill({ json: this.detail(item) });
      }
      if (item.reversedByEntryId) {
        return this.problem(route, 409, 'ledger.already_reversed', 'Giao dịch này đã được đảo.');
      }
      const reversal = entry({ title: `Đảo: ${item.title}`, amount: item.amount, type: 'Income', reversesEntryId: item.id, transactionDate: TODAY, note: String(body!['reason']), categoryId: item.categoryId, categoryName: item.categoryName });
      item.reversedByEntryId = reversal.id;
      this.entries.unshift(reversal);
      return route.fulfill({ status: 201, json: this.detail(reversal) });
    }

    if (method === 'GET' && path === '/events') {
      const list = this.isStudent ? this.events.filter((item) => item.myResponse !== null) : this.events;
      return route.fulfill({
        json: list.map((item) => ({
          id: item.id,
          name: item.name,
          description: item.description,
          eventDate: item.eventDate,
          status: item.status,
          invitedCount: item.invitedCount,
          participantCount: item.participantCount,
          paidInFullCount: item.paidInFullCount,
          totalIncome: item.income,
          totalExpense: item.expense,
          balance: item.income - item.expense,
          myResponse: item.myResponse,
          createdAt: '2026-11-01T03:00:00Z',
          completedAt: null,
        })),
      });
    }

    if (method === 'POST' && path === '/events') {
      const created: FakeEvent = {
        id: id('e'),
        name: String(body!['name']),
        description: (body!['description'] as string | null) ?? null,
        eventDate: String(body!['eventDate']),
        status: 'Open',
        invitedCount: 32,
        participantCount: 0,
        paidInFullCount: 0,
        myResponse: 'Pending',
        income: 0,
        expense: 0,
      };
      this.events.unshift(created);
      return route.fulfill({ status: 201, json: this.eventDetail(created) });
    }

    const eventMatch = /^\/events\/([^/]+)(\/.*)?$/.exec(path);
    if (eventMatch) {
      const event = this.events.find((item) => item.id === eventMatch[1]);
      if (!event) {
        return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
      }
      const rest = eventMatch[2] ?? '';
      if (method === 'GET' && rest === '') {
        return route.fulfill({ json: this.eventDetail(event) });
      }
      if (method === 'PUT' && rest === '') {
        event.name = String(body!['name']);
        return route.fulfill({ json: this.eventDetail(event) });
      }
      if (method === 'PUT' && rest === '/me/response') {
        if (event.status !== 'Open') {
          return this.problem(route, 422, 'event.not_open', 'Sự kiện không còn mở, không đổi được câu trả lời tham gia.');
        }
        event.myResponse = body!['response'] as FakeEvent['myResponse'];
        return route.fulfill({ json: this.eventDetail(event) });
      }
      if (method === 'GET' && rest === '/participants') {
        const visible = event.status === 'Open' ? this.people : this.people.filter((item) => item.paid > 0 || item.paymentStatus === 'Pending');
        const status = query.get('paymentStatus');
        const items = status ? visible.filter((item) => item.paymentStatus === status) : visible;
        const count = (value: string) => visible.filter((item) => item.paymentStatus === value).length;
        return route.fulfill({ json: { items, total: visible.length, paidCount: count('Paid'), pendingCount: count('Pending'), unpaidCount: count('Unpaid') } });
      }
      const remind = /^\/participants\/([^/]+)\/remind$/.exec(rest);
      if (method === 'POST' && remind) {
        const person = this.people.find((item) => item.userId === remind[1]);
        if (!person || person.paymentStatus !== 'Unpaid') {
          return this.problem(route, 422, 'charge.nothing_to_remind', 'Người này không còn khoản nào chưa đóng.');
        }
        if (this.reminded.has(person.userId)) {
          return this.problem(route, 409, 'charge.remind_too_soon', 'Vừa nhắc xong. Vui lòng chờ ít nhất 1 giờ rồi nhắc lại.');
        }
        this.reminded.add(person.userId);
        return route.fulfill({ json: { reminded: 1 } });
      }
      if (method === 'GET' && rest === '/completion-preview') {
        return route.fulfill({ json: this.preview() });
      }
      if (method === 'POST' && rest === '/complete') {
        const preview = this.preview();
        event.status = 'Completed';
        event.participantCount = preview.remainingCount;
        return route.fulfill({ json: preview });
      }
      if (method === 'POST' && rest === '/cancel') {
        if (event.income > 0 || event.expense > 0) {
          return this.problem(route, 422, 'event.has_money', 'Sự kiện đã có thu tiền hoặc ghi chi, không hủy được. Hãy kết thúc rồi quyết toán.');
        }
        event.status = 'Cancelled';
        return route.fulfill({ json: this.eventDetail(event) });
      }
    }

    return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
  }

  private preview() {
    return {
      excluded: this.people.filter((item) => item.paid === 0 && item.paymentStatus === 'Unpaid').map(({ userId, fullName }) => ({ userId, fullName })),
      underpaid: this.people.filter((item) => item.paid > 0 && item.paymentStatus === 'Unpaid').map(({ userId, fullName, due, paid }) => ({ userId, fullName, missing: due - paid })),
      pendingPaymentCount: this.people.reduce((sum, item) => sum + item.pendingPaymentCount, 0),
      remainingCount: this.people.filter((item) => item.paid > 0 || item.paymentStatus === 'Pending').length,
    };
  }
}
