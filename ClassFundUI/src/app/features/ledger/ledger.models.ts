import { Attachment } from '../attachments/attachments.models';
import { LedgerType } from '../categories/categories.models';

/** Khớp LedgerSource ở backend: Manual = chi ghi tay / bút toán đảo; Payment = thu từ thanh toán đã xác nhận (M6). */
export type LedgerSource = 'Manual' | 'Payment' | 'Refund' | 'BalanceWithdrawal';

export type MemberRole = 'Owner' | 'Treasurer' | 'Student';

export const MEMBER_ROLE_LABEL: Record<MemberRole, string> = {
  Owner: 'Thủ quỹ chính',
  Treasurer: 'Thủ quỹ',
  Student: 'Học sinh',
};

export interface LedgerEntry {
  id: string;
  eventId: string | null;
  eventName: string | null;
  type: LedgerType;
  source: LedgerSource;
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
}

/** KPI: số dư toàn thời gian; tổng thu / chi trong khoảng ngày đang lọc. */
export interface LedgerSummary {
  balance: number;
  totalIncome: number;
  totalExpense: number;
}

export interface LedgerList {
  items: LedgerEntry[];
  total: number;
  page: number;
  pageSize: number;
  summary: LedgerSummary;
}

export interface LedgerReversal {
  id: string;
  amount: number;
  transactionDate: string;
  reason: string | null;
  performedByName: string;
  createdAt: string;
}

export interface LedgerHistoryItem {
  action: string;
  actorName: string;
  at: string;
}

export interface LedgerEntryDetail {
  entry: LedgerEntry;
  purpose: string | null;
  note: string | null;
  performedByRole: MemberRole | null;
  attachments: Attachment[];
  reversedBy: LedgerReversal | null;
  reverses: { id: string; title: string; transactionDate: string } | null;
  canReverse: boolean;
  history: LedgerHistoryItem[];
}

export interface LedgerQuery {
  eventId?: string | null;
  type?: LedgerType | null;
  categoryId?: string | null;
  from?: string | null;
  to?: string | null;
  page: number;
  pageSize: number;
}

export interface CreateExpenseBody {
  eventId: string | null;
  categoryId: string;
  title: string;
  purpose: string | null;
  amount: number;
  transactionDate: string;
  note: string | null;
  attachmentIds: string[];
}

export const LEDGER_PAGE_SIZE = 20;
export const EXPENSE_TITLE_MAX = 150;
export const LEDGER_TEXT_MAX = 1000;
export const REVERSE_REASON_MAX = 500;
/** Khoản chi trên mức này bắt buộc có chứng từ (BR-06, khớp EvidenceRule ở backend). */
export const EVIDENCE_REQUIRED_ABOVE = 500_000;
export const MAX_AMOUNT = 100_000_000_000;

export type LedgerTypeFilter = 'all' | LedgerType;

/** Tag dưới tên giao dịch ở L-01 (canvas: "2 chứng từ", "Từ thanh toán đã xác nhận", "Tự ghi nhận", "Bút toán đảo", "Đã đảo"). */
export interface EntryTag {
  text: string;
  tone: 'neutral' | 'warning' | 'danger';
}

export function entryTags(entry: LedgerEntry): EntryTag[] {
  const tags: EntryTag[] = [];
  if (entry.reversesEntryId) {
    tags.push({ text: 'Bút toán đảo', tone: 'neutral' });
  }
  if (entry.reversedByEntryId) {
    tags.push({ text: 'Đã đảo', tone: 'danger' });
  }
  if (entry.source === 'Payment') {
    tags.push(entry.isSelfDeclared ? { text: 'Tự ghi nhận', tone: 'warning' } : { text: 'Từ thanh toán đã xác nhận', tone: 'neutral' });
  }
  if (entry.attachmentCount > 0) {
    tags.push({ text: `${entry.attachmentCount} chứng từ`, tone: 'neutral' });
  }
  return tags;
}

/** Số tiền có dấu theo loại: thu "+", chi "−". */
export function signedAmount(entry: Pick<LedgerEntry, 'type' | 'amount'>): number {
  return entry.type === 'Income' ? entry.amount : -entry.amount;
}

/** "650.000" / "650000" / "650 000" → 650000; rỗng hoặc sai → null. */
export function parseMoney(value: string | null | undefined): number | null {
  const digits = (value ?? '').replace(/\D/g, '');
  if (!digits) {
    return null;
  }
  const amount = Number(digits);
  return Number.isSafeInteger(amount) ? amount : null;
}

const groupFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/** 650000 → "650.000" (ô nhập tiền, canvas `.money`). */
export function formatMoneyInput(amount: number | null): string {
  return amount === null ? '' : groupFormatter.format(amount);
}

/** Mức chi còn được phép trong phạm vi (BR-38); không âm. */
export function spendable(balance: number): number {
  return Math.max(0, balance);
}
