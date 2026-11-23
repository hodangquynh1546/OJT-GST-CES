import { MemberRole } from '../../core/auth/auth.models';
import { FinancialScope } from '../../shared/models/labels';

export type BalanceTransactionType = 'SettlementCredit' | 'ChargePayment' | 'ChargeRefund' | 'Withdrawal' | 'Adjustment' | 'ListCloseRefund';
export type WithdrawalStatus = 'Requested' | 'Paid' | 'Rejected';

/** Rút tối thiểu 10.000đ, bội của 1.000đ (khớp `WithdrawalRule`). */
export const WITHDRAW_MIN = 10_000;
export const WITHDRAW_STEP = 1_000;

export interface BalanceHistoryItem {
  id: string;
  type: BalanceTransactionType;
  amount: number;
  balanceAfter: number;
  createdAt: string;
  description: string;
  scope: FinancialScope | null;
  eventName: string | null;
}

/** B-01: học sinh chỉ xem số dư và lịch sử biến động. */
export interface MyBalance {
  fundName: string;
  balance: number;
  history: BalanceHistoryItem[];
}

/** B-02: một thành viên và số dư cá nhân của họ. */
export interface MemberBalance {
  userId: string;
  fullName: string;
  email: string;
  role: MemberRole;
  isActive: boolean;
  balance: number;
}

export interface MemberBalancesResult {
  items: MemberBalance[];
  total: number;
  page: number;
  pageSize: number;
  /** Tổng tiền thủ quỹ đang giữ hộ (tổng số dư mọi thành viên). */
  heldTotal: number;
}

/** Một lần rút đã thực hiện; dữ liệu cũ có thể ở trạng thái Rejected (yêu cầu bị hủy khi đổi cách rút). */
export interface Withdrawal {
  id: string;
  userId: string;
  fullName: string;
  amount: number;
  status: WithdrawalStatus;
  at: string;
  handledByName: string | null;
  note: string | null;
}

export const WITHDRAWAL_STATUS_BADGE: Record<WithdrawalStatus, { text: string; tone: 'warning' | 'success' | 'danger' }> = {
  Requested: { text: 'Đang chờ', tone: 'warning' },
  Paid: { text: 'Đã rút', tone: 'success' },
  Rejected: { text: 'Đã hủy', tone: 'danger' },
};

/** Kiểm tra số tiền rút ở FE (BE kiểm lại): trả thông báo lỗi hoặc null khi hợp lệ. */
export function withdrawError(amount: number | null, balance: number): string | null {
  if (amount === null || !Number.isFinite(amount)) {
    return 'Vui lòng nhập số tiền.';
  }
  if (!Number.isInteger(amount) || amount < WITHDRAW_MIN) {
    return 'Số tiền rút tối thiểu 10.000đ.';
  }
  if (amount % WITHDRAW_STEP !== 0) {
    return 'Số tiền rút phải là bội của 1.000đ (dạng xx.000).';
  }
  if (amount > balance) {
    return 'Số tiền rút vượt quá số dư hiện có.';
  }
  return null;
}

const vietnamTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** Thời điểm UTC → "17/11 10:05" giờ Việt Nam (như canvas). */
export function shortTime(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return '';
  }
  const parts = Object.fromEntries(vietnamTime.formatToParts(parsed).map((part) => [part.type, part.value]));
  return `${parts['day']}/${parts['month']} ${parts['hour']}:${parts['minute']}`;
}

/** Nhãn cột "Nguồn" ở lịch sử: phạm vi gốc, hoàn tiền khi đóng danh sách hoặc "Rút tiền". */
export function historySource(item: BalanceHistoryItem): { text: string; event: boolean } {
  if (item.type === 'Withdrawal') {
    return { text: 'Rút tiền', event: false };
  }
  if (item.type === 'ListCloseRefund') {
    return { text: item.eventName ? `Hoàn khi đóng danh sách: ${item.eventName}` : 'Hoàn khi đóng danh sách', event: true };
  }
  if (item.scope === 'Event') {
    return { text: item.eventName ? `Sự kiện: ${item.eventName}` : 'Sự kiện', event: true };
  }
  if (item.scope === 'General') {
    return { text: 'Quỹ chung', event: false };
  }
  return { text: 'Điều chỉnh', event: false };
}
