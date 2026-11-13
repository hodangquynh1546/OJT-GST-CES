import { Page, Route } from '@playwright/test';

import { CATEGORY_DONG_QUY, CATEGORY_KHEN_THUONG, CATEGORY_SINH_HOAT, CATEGORY_THU_KHAC, CATEGORY_VAN_PHONG_PHAM } from './mock-categories';
import { FUND } from './mock-session';

/**
 * Dữ liệu giả cho `GET /funds/{id}/reports/monthly` và `…/reports/yearly` (hợp đồng: docs/api/backend-api.md – Funds;
 * snapshot backend `Contracts/snapshots/Reports.json`; số liệu theo Canvas `Reports`).
 * Dùng chung cho M7b (S-03) và M7a (S-01 – biểu đồ 3 tháng đọc `yearly`).
 *
 * Hợp đồng `yearly` đã khóa: **luôn đủ 12 tháng** (`month` 1 → 12), chỉ số tổng, cuối tháng trước = đầu tháng sau, giống nhau ở
 * mọi vai trò. Tháng chưa tới vẫn có trong mảng (thu / chi = 0, số dư mang sang).
 */
export type MockTxType = 'Income' | 'Expense';

export interface MockMonthFigures {
  month: number;
  openingBalance: number;
  totalIncome: number;
  totalExpense: number;
  netChange: number;
  closingBalance: number;
}

export interface MockYearlyReport {
  year: number;
  months: MockMonthFigures[];
}

export interface MockCategoryLine {
  type: MockTxType;
  categoryId: string | null;
  categoryName: string;
  count: number | null;
  amount: number;
}

export interface MockReportTx {
  id: string;
  type: MockTxType;
  amount: number;
  occurredOn: string;
  categoryId: string;
  categoryName: string;
  description: string;
  isCancelled: boolean;
  note?: string;
  isPublic?: boolean;
  createdByName?: string;
}

export interface MockMonthlyReport {
  year: number;
  month: number;
  openingBalance: number;
  totalIncome: number;
  totalExpense: number;
  netChange: number;
  closingBalance: number;
  byCategory: MockCategoryLine[];
  transactions: MockReportTx[];
}

/** Hôm nay theo giờ Việt Nam (`yyyy-MM-dd`). */
export const REPORT_TODAY = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
export const REPORT_YEAR = Number(REPORT_TODAY.slice(0, 4));
export const REPORT_MONTH = Number(REPORT_TODAY.slice(5, 7));

/** Thu / chi 3 tháng gần nhất theo Canvas (T9, T10, T11/2026), đặt vào 2 tháng trước, tháng trước và tháng hiện tại. */
const RECENT: [income: number, expense: number][] = [
  [4_800_000, 2_100_000],
  [4_500_000, 4_750_000],
  [3_500_000, 1_930_000],
];

/**
 * 12 tháng của `year`; ba tháng gần nhất (tính tới `currentMonth`) có số liệu Canvas, các tháng khác 0.
 * Mặc định: năm / tháng hiện tại theo giờ Việt Nam.
 */
export function yearlyReport(year = REPORT_YEAR, currentMonth = year === REPORT_YEAR ? REPORT_MONTH : 12, openingBalance = 0): MockYearlyReport {
  let balance = openingBalance;
  const months: MockMonthFigures[] = [];
  for (let month = 1; month <= 12; month++) {
    const offset = month - (currentMonth - RECENT.length + 1);
    const [income, expense] = offset >= 0 && offset < RECENT.length ? RECENT[offset] : [0, 0];
    const closing = balance + income - expense;
    months.push({ month, openingBalance: balance, totalIncome: income, totalExpense: expense, netChange: income - expense, closingBalance: closing });
    balance = closing;
  }
  return { year, months };
}

const day = (year: number, month: number, date: number) =>
  `${year}-${String(month).padStart(2, '0')}-${String(date).padStart(2, '0')}`;

const tx = (
  id: string,
  type: MockTxType,
  amount: number,
  occurredOn: string,
  category: { id: string; name: string },
  description: string,
  isPublic = true,
): MockReportTx => ({
  id,
  type,
  amount,
  occurredOn,
  categoryId: category.id,
  categoryName: category.name,
  description,
  isCancelled: false,
  note: '',
  isPublic,
  createdByName: 'Nguyễn Minh Khang',
});

/**
 * Báo cáo tháng bản đầy đủ (thủ quỹ, GVCN) – Canvas tháng 11: đầu 2.450.000, thu 3.500.000, chi 1.930.000, cuối 4.020.000;
 * "Thu khác" 300.000 và "Khen thưởng" 500.000 là khoản không công khai. `transactions` mới nhất trước như API.
 */
export function monthlyFull(year = REPORT_YEAR, month = REPORT_MONTH): MockMonthlyReport {
  const transactions = [
    tx('r5', 'Income', 300_000, day(year, month, 12), CATEGORY_THU_KHAC, 'Ủng hộ của phụ huynh em Minh', false),
    tx('r4', 'Expense', 500_000, day(year, month, 10), CATEGORY_KHEN_THUONG, 'Quà tặng cô giáo 20/11', false),
    tx('r3', 'Expense', 1_250_000, day(year, month, 8), CATEGORY_SINH_HOAT, 'Liên hoan 20/11'),
    tx('r2', 'Expense', 180_000, day(year, month, 5), CATEGORY_VAN_PHONG_PHAM, 'Mua giấy A4, bút dạ'),
    tx('r1', 'Income', 3_200_000, day(year, month, 2), CATEGORY_DONG_QUY, 'Đóng quỹ tháng 11 – 32 phụ huynh'),
  ];
  return {
    year,
    month,
    openingBalance: 2_450_000,
    totalIncome: 3_500_000,
    totalExpense: 1_930_000,
    netChange: 1_570_000,
    closingBalance: 4_020_000,
    byCategory: [
      { type: 'Income', categoryId: CATEGORY_DONG_QUY.id, categoryName: CATEGORY_DONG_QUY.name, count: 1, amount: 3_200_000 },
      { type: 'Income', categoryId: CATEGORY_THU_KHAC.id, categoryName: CATEGORY_THU_KHAC.name, count: 1, amount: 300_000 },
      { type: 'Expense', categoryId: CATEGORY_SINH_HOAT.id, categoryName: CATEGORY_SINH_HOAT.name, count: 1, amount: 1_250_000 },
      { type: 'Expense', categoryId: CATEGORY_KHEN_THUONG.id, categoryName: CATEGORY_KHEN_THUONG.name, count: 1, amount: 500_000 },
      { type: 'Expense', categoryId: CATEGORY_VAN_PHONG_PHAM.id, categoryName: CATEGORY_VAN_PHONG_PHAM.name, count: 1, amount: 180_000 },
    ],
    transactions,
  };
}

/**
 * Bản công khai (học sinh chưa phân công, phụ huynh) của cùng tháng: số tổng giữ nguyên; khoản không công khai gộp "Khoản khác"
 * (`categoryId` / `count` null); `transactions` chỉ khoản công khai, không trường nội bộ (`note`, `isPublic`, `createdByName`).
 */
export function monthlyPublic(year = REPORT_YEAR, month = REPORT_MONTH): MockMonthlyReport {
  const full = monthlyFull(year, month);
  return {
    ...full,
    byCategory: [
      { type: 'Income', categoryId: CATEGORY_DONG_QUY.id, categoryName: CATEGORY_DONG_QUY.name, count: 1, amount: 3_200_000 },
      { type: 'Income', categoryId: null, categoryName: 'Khoản khác', count: null, amount: 300_000 },
      { type: 'Expense', categoryId: CATEGORY_SINH_HOAT.id, categoryName: CATEGORY_SINH_HOAT.name, count: 1, amount: 1_250_000 },
      { type: 'Expense', categoryId: CATEGORY_VAN_PHONG_PHAM.id, categoryName: CATEGORY_VAN_PHONG_PHAM.name, count: 1, amount: 180_000 },
      { type: 'Expense', categoryId: null, categoryName: 'Khoản khác', count: null, amount: 500_000 },
    ],
    transactions: full.transactions
      .filter((item) => item.isPublic)
      .map(({ note: _note, isPublic: _isPublic, createdByName: _createdByName, ...item }) => item),
  };
}

/** Tháng không có giao dịch: số dư mang sang, mọi tổng = 0. */
export function monthlyEmpty(year = REPORT_YEAR, month = REPORT_MONTH, balance = 4_020_000): MockMonthlyReport {
  return {
    year,
    month,
    openingBalance: balance,
    totalIncome: 0,
    totalExpense: 0,
    netChange: 0,
    closingBalance: balance,
    byCategory: [],
    transactions: [],
  };
}

export interface MockReportsOptions {
  /** Báo cáo tháng theo `year` / `month` của request; mặc định Canvas (đầy đủ hoặc công khai theo `publicView`). */
  monthly?: (year: number, month: number) => MockMonthlyReport;
  /** Báo cáo năm theo `year` của request; mặc định {@link yearlyReport}. */
  yearly?: (year: number) => MockYearlyReport;
  publicView?: boolean;
  fundId?: string;
}

/** Giả lập hai endpoint báo cáo; trả về danh sách query đã gọi để test kiểm tra tham số. */
export async function mockReports(page: Page, options: MockReportsOptions = {}): Promise<URLSearchParams[]> {
  const fundId = options.fundId ?? FUND;
  const calls: URLSearchParams[] = [];
  const monthly = options.monthly ?? (options.publicView ? monthlyPublic : monthlyFull);
  const yearly = options.yearly ?? ((year: number) => yearlyReport(year));
  const answer = (route: Route, build: (query: URLSearchParams) => object) => {
    const query = new URL(route.request().url()).searchParams;
    calls.push(query);
    return route.fulfill({ json: build(query) });
  };
  await page.route(`**/api/v1/funds/${fundId}/reports/monthly?**`, (route) =>
    answer(route, (query) => monthly(Number(query.get('year')), Number(query.get('month')))),
  );
  await page.route(`**/api/v1/funds/${fundId}/reports/yearly?**`, (route) =>
    answer(route, (query) => yearly(Number(query.get('year')))),
  );
  return calls;
}
