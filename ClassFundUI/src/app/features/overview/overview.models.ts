import { FundRole } from '../../core/auth/auth.models';
import { Permission } from '../../core/auth/permissions';
import { TreasurerStatus } from '../funds/funds.models';
import { Transaction } from '../transactions/transactions.models';

/** Hợp đồng M7a: `GET /funds/{fundId}` (docs/api/backend-api.md – Funds; snapshot Contracts/snapshots/Funds.json). */
export interface MonthTotals {
  income: number;
  expense: number;
  net: number;
}

/** Họ tên và vai trò của GVCN / thủ quỹ đang hiệu lực – không email, tên đăng nhập (BR-23). */
export interface FundManager {
  fullName: string;
  role: FundRole;
}

export interface FundOverview {
  role: FundRole;
  permissions: Permission[];
  currentBalance: number;
  totalIncome: number;
  totalExpense: number;
  currentMonth: MonthTotals;
  treasurerStatus: TreasurerStatus;
  managers: FundManager[];
  /** 5 khoản đang hiệu lực mới nhất, theo phạm vi người xem (bản công khai chỉ khoản công khai). */
  recentTransactions: Transaction[];
}

/** Một cột nhóm của biểu đồ "Thu chi 3 tháng gần nhất". */
export interface TrendPoint {
  label: string;
  income: number;
  expense: number;
}
