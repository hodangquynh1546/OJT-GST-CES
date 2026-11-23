import { Page, Route } from '@playwright/test';

import { FakeAuthApi, FakeUser } from './fake-auth-api';
import { FUND_ID, owner, student } from './fake-money-api';

export { FUND_ID, owner, student };

/** API giả lập M10 (số dư, rút tiền) + M13 (tổng quan, báo cáo), dữ liệu theo canvas BalanceBody / Withdrawals / Overview / Reports. */

export interface Recorded {
  method: string;
  path: string;
  body: Record<string, unknown> | null;
  query: URLSearchParams;
}

interface MemberBalance {
  userId: string;
  fullName: string;
  email: string;
  role: 'Student' | 'Treasurer' | 'Owner';
  isActive: boolean;
  balance: number;
}

interface Withdrawal {
  id: string;
  userId: string;
  fullName: string;
  amount: number;
  status: 'Paid' | 'Rejected';
  at: string;
  handledByName: string | null;
  note: string | null;
}

export class FakeBalanceReportsApi {
  readonly requests: Recorded[] = [];
  balance = 38_000;
  readonly members: MemberBalance[] = [
    { userId: 'u-ha', fullName: 'Lê Thu Hà', email: 'ha.le@truong.edu.vn', role: 'Student', isActive: true, balance: 38_000 },
    { userId: 'u-linh', fullName: 'Đỗ Khánh Linh', email: 'linh.do@truong.edu.vn', role: 'Student', isActive: true, balance: 63_000 },
    { userId: 'u-long', fullName: 'Phạm Đức Long', email: 'long.pham@truong.edu.vn', role: 'Student', isActive: true, balance: 0 },
  ];
  readonly withdrawals: Withdrawal[] = [
    { id: 'w3', userId: 'u-long', fullName: 'Phạm Đức Long', amount: 38_000, status: 'Paid', at: '2026-11-10T03:00:00Z', handledByName: 'Trần Quang Huy', note: null },
  ];

  constructor(private readonly page: Page) {}

  static async open(page: Page, user: FakeUser, url: string): Promise<FakeBalanceReportsApi> {
    const auth = new FakeAuthApi(page);
    auth.addAccount(user, 'Passw0rd1');
    auth.signIn(user);
    await auth.install();
    const api = new FakeBalanceReportsApi(page);
    await page.route(new RegExp(`/api/v1/funds/${FUND_ID}/(me/balance|balances|withdrawals|reports)`), (route) => api.handle(route, user === student));
    await page.goto(url);
    return api;
  }

  last(method: string, path: string): Recorded | undefined {
    return [...this.requests].reverse().find((item) => item.method === method && item.path === path);
  }

  private problem(route: Route, status: number, code: string, title: string): Promise<void> {
    return route.fulfill({ status, contentType: 'application/problem+json', body: JSON.stringify({ status, code, title }) });
  }

  private mine() {
    return {
      fundName: 'Lớp 10A1',
      balance: this.balance,
      history: [
        { id: 'h1', type: 'SettlementCredit', amount: 38_000, balanceAfter: 38_000, createdAt: '2026-11-14T11:00:00Z', description: 'Tiền thừa quyết toán', scope: 'Event', eventName: 'Tham quan bảo tàng' },
        { id: 'h2', type: 'ChargePayment', amount: -25_000, balanceAfter: 0, createdAt: '2026-10-30T01:12:00Z', description: 'Đóng “Quỹ photo T10”', scope: 'General', eventName: null },
        { id: 'h4', type: 'ListCloseRefund', amount: 70_000, balanceAfter: 95_000, createdAt: '2026-11-20T01:00:00Z', description: 'Hoàn tiền đã đóng khi đóng danh sách', scope: 'Event', eventName: 'Dã ngoại Ba Vì' },
        { id: 'h3', type: 'SettlementCredit', amount: 25_000, balanceAfter: 25_000, createdAt: '2026-10-28T10:30:00Z', description: 'Tiền thừa quyết toán', scope: 'Event', eventName: 'Liên hoan giữa kỳ' },
      ],
    };
  }

  private balances(search: string | null) {
    const items = this.members.filter((member) => !search || member.fullName.toLowerCase().includes(search.toLowerCase()) || member.email.includes(search));
    return { items, total: items.length, page: 1, pageSize: 100, heldTotal: this.members.reduce((sum, member) => sum + member.balance, 0) };
  }

  private overview(isStudent: boolean) {
    return {
      fundName: 'Lớp 10A1',
      schoolYear: '2026-2027',
      treasurerNames: ['Nguyễn Minh Anh', 'Trần Quang Huy'],
      general: { balance: 4_820_000, totalIncome: 7_400_000, totalExpense: 2_580_000, selfDeclaredIncome: 200_000, incomeCount: 28, expenseCount: 9 },
      memberCount: 32,
      treasurerCount: 2,
      heldBalance: 0,
      recentEntries: isStudent
        ? []
        : [
            { id: 'le1', transactionDate: '2026-11-16', title: 'Mua hoa 20/11 tặng cô chủ nhiệm', categoryName: 'Quà tặng', type: 'Expense', source: 'Manual', amount: 650_000, performedByName: 'Trần Quang Huy', attachmentCount: 2, isSelfDeclared: false, paymentMethod: null, isReversed: false },
            { id: 'le2', transactionDate: '2026-11-15', title: 'Thu: Quỹ lớp HK1 – Lê Thu Hà', categoryName: 'Thu quỹ', type: 'Income', source: 'Payment', amount: 200_000, performedByName: 'Trần Quang Huy', attachmentCount: 0, isSelfDeclared: false, paymentMethod: 'BankTransfer', isReversed: false },
            { id: 'le3', transactionDate: '2026-11-15', title: 'Thu: Quỹ lớp HK1 – Nguyễn Minh Anh', categoryName: 'Thu quỹ', type: 'Income', source: 'Payment', amount: 200_000, performedByName: 'Nguyễn Minh Anh', attachmentCount: 0, isSelfDeclared: true, paymentMethod: 'Cash', isReversed: false },
            { id: 'le4', transactionDate: '2026-11-12', title: 'In tài liệu ôn tập giữa kỳ', categoryName: 'Học tập', type: 'Expense', source: 'Manual', amount: 180_000, performedByName: 'Nguyễn Minh Anh', attachmentCount: 1, isSelfDeclared: false, paymentMethod: null, isReversed: false },
          ],
      openCharges: [
        { id: 'c1', title: 'Quỹ lớp HK1', amountPerPerson: 200_000, dueDate: null, assignedCount: 32, paidCount: 26 },
        { id: 'c2', title: 'Áo lớp', amountPerPerson: 200_000, dueDate: null, assignedCount: 32, paidCount: 21 },
        { id: 'c3', title: 'Quỹ photo HK1', amountPerPerson: 30_000, dueDate: null, assignedCount: 32, paidCount: 10 },
      ],
      events: [
        { id: 'e1', name: 'Dã ngoại Ba Vì', eventDate: '2026-11-20', status: 'Open', participantCount: 30, paidInFullCount: 26, myResponse: 'Accepted' },
        { id: 'e2', name: 'Liên hoan giữa kỳ', eventDate: '2026-10-26', status: 'Completed', participantCount: 28, paidInFullCount: 28, myResponse: 'Accepted' },
      ],
      todo: isStudent ? null : { unpaidGeneralCount: 4, eventsToCloseList: [{ id: 'e1', name: 'Dã ngoại Ba Vì' }], eventsAwaitingSettlement: [{ id: 'e2', name: 'Liên hoan giữa kỳ' }] },
      me: {
        dueAmount: 580_000,
        dueCount: 3,
        nearestDueTitle: 'Dã ngoại Ba Vì',
        nearestDueDate: '2026-11-18',
        nearestDueAmount: 350_000,
        nearestDueEventName: 'Dã ngoại Ba Vì',
        balance: 38_000,
      },
      notifications: [
        { title: 'Nhắc đóng khoản thu', body: 'Áo lớp: bạn chưa nộp, hãy đưa tiền cho thủ quỹ', scope: 'General', eventName: null, createdAt: '2026-11-17T01:00:00Z', isRead: false },
        { title: 'Tiền thừa +38.000 đ', body: 'Đã cộng vào số dư cá nhân', scope: 'Event', eventName: 'Tham quan bảo tàng', createdAt: '2026-11-14T11:00:00Z', isRead: true },
      ],
    };
  }

  async handle(route: Route, isStudent: boolean): Promise<void> {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname.replace(`/api/v1/funds/${FUND_ID}`, '');
    const method = request.method();
    const raw = request.postData();
    const body = raw ? (JSON.parse(raw) as Record<string, unknown>) : null;
    this.requests.push({ method, path, body, query: url.searchParams });

    if (method === 'GET' && path === '/me/balance') {
      return route.fulfill({ json: this.mine() });
    }
    if (method === 'GET' && path === '/balances') {
      return route.fulfill({ json: this.balances(url.searchParams.get('search')) });
    }
    const withdraw = /^\/balances\/([^/]+)\/withdraw$/.exec(path);
    if (method === 'POST' && withdraw) {
      const member = this.members.find((row) => row.userId === withdraw[1])!;
      const amount = Number(body!['amount']);
      if (amount > member.balance) {
        return this.problem(route, 422, 'balance.insufficient', 'Số dư cá nhân không đủ.');
      }
      member.balance -= amount;
      this.withdrawals.unshift({ id: `w${this.withdrawals.length + 10}`, userId: member.userId, fullName: member.fullName, amount, status: 'Paid', at: '2026-11-20T03:00:00Z', handledByName: 'Nguyễn Minh Anh', note: null });
      return route.fulfill({ json: member });
    }
    if (method === 'GET' && path === '/withdrawals') {
      return route.fulfill({ json: { items: this.withdrawals, total: this.withdrawals.length, page: 1, pageSize: 50 } });
    }
    if (method === 'GET' && path === '/reports/overview') {
      return route.fulfill({ json: this.overview(isStudent) });
    }
    if (method === 'GET' && path === '/reports/general') {
      return route.fulfill({
        json: {
          from: url.searchParams.get('from'),
          to: url.searchParams.get('to'),
          totalIncome: 7_400_000,
          totalExpense: 2_580_000,
          selfDeclaredIncome: 200_000,
          balance: 4_820_000,
          openChargeCount: 3,
          expenseByCategory: [
            { categoryId: 'g', categoryName: 'Quà tặng', amount: 1_050_000 },
            { categoryId: 's', categoryName: 'Học tập', amount: 680_000 },
            { categoryId: 'l', categoryName: 'Sinh hoạt lớp', amount: 500_000 },
            { categoryId: 'v', categoryName: 'Vệ sinh', amount: 350_000 },
          ],
          months: [
            { year: 2026, month: 9, income: 1_600_000, expense: 480_000, closingBalance: 1_120_000 },
            { year: 2026, month: 10, income: 400_000, expense: 1_150_000, closingBalance: 370_000 },
            { year: 2026, month: 11, income: 5_400_000, expense: 950_000, closingBalance: 4_820_000 },
          ],
        },
      });
    }
    if (method === 'GET' && path === '/reports/events') {
      return route.fulfill({
        json: [
          { id: 'e1', name: 'Dã ngoại Ba Vì', status: 'Open', participantCount: 30, finalCount: null, amountDue: 12_900_000, collected: 10_700_000, uncollected: 2_200_000, totalExpense: 7_200_000, surplus: null, surplusMethod: null, refunded: 0, creditedToBalance: 0 },
          { id: 'e2', name: 'Liên hoan giữa kỳ', status: 'Completed', participantCount: 29, finalCount: 28, amountDue: 4_200_000, collected: 4_150_000, uncollected: 50_000, totalExpense: 4_000_000, surplus: 150_000, surplusMethod: null, refunded: 0, creditedToBalance: 0 },
          { id: 'e3', name: 'Tham quan bảo tàng', status: 'Settled', participantCount: 31, finalCount: 30, amountDue: 6_000_000, collected: 6_000_000, uncollected: 0, totalExpense: 4_860_000, surplus: 1_140_000, surplusMethod: 'PersonalBalance', refunded: 0, creditedToBalance: 1_140_000 },
          { id: 'e4', name: 'Cắm trại hè', status: 'Cancelled', participantCount: 0, finalCount: null, amountDue: 0, collected: 0, uncollected: 0, totalExpense: 0, surplus: null, surplusMethod: null, refunded: 0, creditedToBalance: 0 },
        ],
      });
    }
    if (method === 'GET' && path === '/reports/students') {
      return route.fulfill({
        json: [
          { userId: 'u-ha', fullName: 'Lê Thu Hà', role: 'Student', paidGeneral: 400_000, unpaidGeneral: [{ title: 'Áo lớp', status: 'Unpaid' }, { title: 'Photo HK1', status: 'Unpaid' }], paidEvents: 350_000, refunded: 63_000, usedBalance: 25_000, balance: 38_000 },
          { userId: 'u-long', fullName: 'Phạm Đức Long', role: 'Student', paidGeneral: 600_000, unpaidGeneral: [{ title: 'HK1', status: 'Unpaid' }], paidEvents: 850_000, refunded: 38_000, usedBalance: 0, balance: 0 },
          { userId: 'u-linh', fullName: 'Đỗ Khánh Linh', role: 'Student', paidGeneral: 830_000, unpaidGeneral: [], paidEvents: 500_000, refunded: 63_000, usedBalance: 200_000, balance: 63_000 },
        ],
      });
    }
    return this.problem(route, 404, 'not_found', 'Không tìm thấy dữ liệu.');
  }
}
