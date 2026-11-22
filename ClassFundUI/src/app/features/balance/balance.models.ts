import { FinancialScope } from '../../shared/models/labels';

export type BalanceTransactionType = 'SettlementCredit' | 'ChargePayment' | 'ChargeRefund' | 'Withdrawal' | 'Adjustment';
export type WithdrawalStatus = 'Requested' | 'Paid' | 'Rejected';

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

export interface MyWithdrawal {
  id: string;
  amount: number;
  status: WithdrawalStatus;
  requestedAt: string;
  handledAt: string | null;
  handledByName: string | null;
  rejectionReason: string | null;
}

export interface MyBalance {
  fundName: string;
  balance: number;
  latestWithdrawal: MyWithdrawal | null;
  history: BalanceHistoryItem[];
  otherFunds: { fundId: string; fundName: string; isArchived: boolean; balance: number }[];
}

export interface WithdrawalRequest {
  id: string;
  userId: string;
  fullName: string;
  email: string;
  amount: number;
  status: WithdrawalStatus;
  requestedAt: string;
  handledAt: string | null;
  handledByName: string | null;
  rejectionReason: string | null;
  isMine: boolean;
}

export interface WithdrawalList {
  items: WithdrawalRequest[];
  requestedCount: number;
  paidCount: number;
  rejectedCount: number;
}

export const WITHDRAWAL_STATUS_BADGE: Record<WithdrawalStatus, { text: string; tone: 'warning' | 'success' | 'danger' }> = {
  Requested: { text: 'Đang chờ', tone: 'warning' },
  Paid: { text: 'Đã trả', tone: 'success' },
  Rejected: { text: 'Từ chối', tone: 'danger' },
};

export const REJECT_REASON_MAX = 500;

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

/** Nhãn cột "Nguồn" ở lịch sử: phạm vi gốc hoặc "Rút tiền". */
export function historySource(item: BalanceHistoryItem): { text: string; event: boolean } {
  if (item.type === 'Withdrawal') {
    return { text: 'Rút tiền', event: false };
  }
  if (item.scope === 'Event') {
    return { text: item.eventName ? `Sự kiện: ${item.eventName}` : 'Sự kiện', event: true };
  }
  if (item.scope === 'General') {
    return { text: 'Quỹ chung', event: false };
  }
  return { text: 'Điều chỉnh', event: false };
}
