import { Page } from '@playwright/test';
import { mockReports } from './mock-reports';
import { FUND } from './mock-session';

/**
 * Dữ liệu giả cho S-01 Tổng quan (hợp đồng: docs/api/backend-api.md – `GET /funds/{id}`, `reports/yearly`; Canvas board `Overview`).
 * Chỉ giả lập overview (M7a). Biểu đồ 3 tháng đọc `reports/yearly` của M7b: dùng `mockReports()` và `yearlyReport()` trong
 * `mock-reports.ts` (hợp đồng yearly đã khóa: luôn đủ 12 tháng), không tự viết lại ở đây.
 */
export interface MockTransaction {
  id: string;
  type: 'Income' | 'Expense';
  amount: number;
  occurredOn: string;
  categoryId: string;
  categoryName: string | null;
  description: string;
  isCancelled: boolean;
  isPublic?: boolean;
  note?: string;
}

export interface MockOverview {
  role: string;
  permissions: string[];
  currentBalance: number;
  totalIncome: number;
  totalExpense: number;
  currentMonth: { income: number; expense: number; net: number };
  treasurerStatus: 'Unassigned' | 'Assigned';
  managers: { fullName: string; role: 'Teacher' | 'Treasurer' }[];
  recentTransactions: MockTransaction[];
}

const tx = (n: number, type: 'Income' | 'Expense', amount: number, occurredOn: string, categoryName: string, description: string): MockTransaction => ({
  id: `019a0000-0000-7000-8000-0000000003${String(n).padStart(2, '0')}`,
  type,
  amount,
  occurredOn,
  categoryId: `019a0000-0000-7000-8000-0000000002${String(n).padStart(2, '0')}`,
  categoryName,
  description,
  isCancelled: false,
});

const RECENT: MockTransaction[] = [
  tx(1, 'Income', 500_000, '2026-11-10', 'Đóng quỹ', 'Đóng quỹ tháng 11'),
  tx(2, 'Expense', 200_000, '2026-11-09', 'Văn phòng phẩm', 'Mua bút, giấy'),
  tx(3, 'Expense', 150_000, '2026-11-05', 'Sinh hoạt, liên hoan', 'Liên hoan 20/11'),
];

const MANAGERS: MockOverview['managers'] = [
  { fullName: 'Trần Văn Hùng', role: 'Teacher' },
  { fullName: 'Phạm Thu Trang', role: 'Treasurer' },
];

/** Quỹ có thủ quỹ, số dư dương: 1.500.000 thu − 350.000 chi (khoản đã hủy không tính). */
export const OVERVIEW_TEACHER: MockOverview = {
  role: 'Teacher',
  permissions: ['Fund.Manage', 'Fund.View', 'Ledger.ViewFull', 'Member.View', 'Student.Manage', 'Treasurer.Assign'],
  currentBalance: 1_150_000,
  totalIncome: 1_500_000,
  totalExpense: 350_000,
  currentMonth: { income: 500_000, expense: 350_000, net: 150_000 },
  treasurerStatus: 'Assigned',
  managers: MANAGERS,
  recentTransactions: RECENT,
};

export const OVERVIEW_TREASURER: MockOverview = {
  ...OVERVIEW_TEACHER,
  role: 'Treasurer',
  permissions: ['Category.Manage', 'Fund.View', 'Ledger.ViewFull', 'Member.View', 'Transaction.Manage'],
};

/** Học sinh / phụ huynh: cùng số tổng, danh sách gần đây chỉ khoản công khai và không có trường nội bộ. */
export const OVERVIEW_PUBLIC: MockOverview = {
  ...OVERVIEW_TEACHER,
  role: 'Student',
  permissions: ['Fund.View', 'Ledger.ViewPublic'],
  recentTransactions: [RECENT[0], RECENT[2]],
};

/** Số dư âm (BR-04) + chưa phân công thủ quỹ (BR-36). */
export const OVERVIEW_NEGATIVE_NO_TREASURER: MockOverview = {
  ...OVERVIEW_TEACHER,
  currentBalance: -150_000,
  totalIncome: 100_000,
  totalExpense: 250_000,
  currentMonth: { income: 100_000, expense: 250_000, net: -150_000 },
  treasurerStatus: 'Unassigned',
  managers: [MANAGERS[0]],
  recentTransactions: [tx(4, 'Expense', 250_000, '2026-11-03', 'Chi khác', 'Chi trước khi có thủ quỹ')],
};

/** Quỹ mới: chưa có giao dịch nào. */
export const OVERVIEW_EMPTY: MockOverview = {
  ...OVERVIEW_TEACHER,
  currentBalance: 0,
  totalIncome: 0,
  totalExpense: 0,
  currentMonth: { income: 0, expense: 0, net: 0 },
  managers: [MANAGERS[0]],
  treasurerStatus: 'Unassigned',
  recentTransactions: [],
};

/** Giả lập `GET /funds/{id}` (`fundId` là mẫu regex, mặc định quỹ `FUND`). Các method khác để route khác xử lý. */
export async function mockOverview(page: Page, overview: MockOverview = OVERVIEW_TEACHER, fundId: string = FUND): Promise<void> {
  await page.route(new RegExp(`/api/v1/funds/${fundId}$`), (route) =>
    route.request().method() === 'GET' ? route.fulfill({ json: overview }) : route.fallback(),
  );
}

/**
 * Mock tối thiểu cho trang S-01 (overview + yearly) – cho các spec chỉ đi qua trang chính của quỹ (khung ứng dụng, đăng nhập,
 * Quỹ của tôi). Chế độ full có backend thật: request không mock sẽ tới backend và bị 401, SPA chuyển về /login.
 */
export async function mockOverviewPage(page: Page, overview: MockOverview = OVERVIEW_TEACHER): Promise<void> {
  await mockOverview(page, overview, '[^/?]+'); // quỹ nào cũng được (ví dụ quỹ vừa tạo ở G-01)
  await mockReports(page, { fundId: '*' });
}
