import { Transaction, TransactionType } from '../transactions/transactions.models';

/** Hợp đồng M7b (docs/api/backend-api.md – Funds `reports/*`; snapshot Contracts/snapshots/Reports.json). */

/** Dòng theo danh mục. Bản công khai: dòng "Khoản khác" có `categoryId` và `count` null (P1). */
export interface CategoryReportLine {
  type: TransactionType;
  categoryId: string | null;
  categoryName: string;
  count: number | null;
  amount: number;
}

/** Báo cáo tháng: số tổng giống nhau ở mọi vai trò; `transactions` chỉ khoản đang hiệu lực, theo phạm vi người xem. */
export interface MonthlyReport {
  year: number;
  month: number;
  openingBalance: number;
  totalIncome: number;
  totalExpense: number;
  netChange: number;
  closingBalance: number;
  byCategory: CategoryReportLine[];
  transactions: Transaction[];
}

export interface MonthFigures {
  month: number;
  openingBalance: number;
  totalIncome: number;
  totalExpense: number;
  netChange: number;
  closingBalance: number;
}

/** Luôn đủ 12 tháng (tháng 1 → 12), chỉ số tổng, giống nhau ở mọi vai trò. */
export interface YearlyReport {
  year: number;
  months: MonthFigures[];
}

/** Tên dòng gộp khoản không công khai do backend trả (`ReportsService.OtherCategoryName`). */
export const OTHER_CATEGORY_NAME = 'Khoản khác';
