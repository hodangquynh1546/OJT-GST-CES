import { AssignmentStatus, EventStatus, FinancialScope, PaymentMethod } from '../../shared/models/labels';
import { LedgerType } from '../categories/categories.models';
import { ParticipationResponse } from '../events/events.models';
import { LedgerSource, MemberRole } from '../ledger/ledger.models';

export interface GeneralTotals {
  balance: number;
  totalIncome: number;
  totalExpense: number;
  selfDeclaredIncome: number;
  incomeCount: number;
  expenseCount: number;
}

export interface OverviewEntry {
  id: string;
  transactionDate: string;
  title: string;
  categoryName: string | null;
  type: LedgerType;
  source: LedgerSource;
  amount: number;
  performedByName: string;
  attachmentCount: number;
  isSelfDeclared: boolean;
  paymentMethod: PaymentMethod | null;
  isReversed: boolean;
}

export interface OpenCharge {
  id: string;
  title: string;
  amountPerPerson: number;
  dueDate: string | null;
  assignedCount: number;
  paidCount: number;
}

export interface OverviewEvent {
  id: string;
  name: string;
  eventDate: string | null;
  status: EventStatus;
  participantCount: number;
  paidInFullCount: number;
  myResponse: ParticipationResponse | null;
}

export interface FundOverview {
  fundName: string;
  schoolYear: string;
  treasurerNames: string[];
  general: GeneralTotals;
  memberCount: number;
  treasurerCount: number;
  heldBalance: number;
  recentEntries: OverviewEntry[];
  openCharges: OpenCharge[];
  events: OverviewEvent[];
  todo: { unpaidGeneralCount: number; eventsToCloseList: { id: string; name: string }[]; eventsAwaitingSettlement: { id: string; name: string }[] } | null;
  me: {
    dueAmount: number;
    dueCount: number;
    nearestDueTitle: string | null;
    nearestDueDate: string | null;
    nearestDueAmount: number;
    nearestDueEventName: string | null;
    balance: number;
  };
  notifications: { title: string; body: string; scope: FinancialScope; eventName: string | null; createdAt: string; isRead: boolean }[];
}

export interface GeneralReport {
  from: string | null;
  to: string | null;
  totalIncome: number;
  totalExpense: number;
  selfDeclaredIncome: number;
  balance: number;
  openChargeCount: number;
  expenseByCategory: { categoryId: string | null; categoryName: string; amount: number }[];
  months: { year: number; month: number; income: number; expense: number; closingBalance: number }[];
}

export interface EventReportRow {
  id: string;
  name: string;
  status: EventStatus;
  participantCount: number;
  finalCount: number | null;
  amountDue: number;
  collected: number;
  uncollected: number;
  totalExpense: number;
  surplus: number | null;
  surplusMethod: 'DirectRefund' | 'PersonalBalance' | null;
  refunded: number;
  creditedToBalance: number;
}

export interface StudentReportRow {
  userId: string;
  fullName: string;
  role: MemberRole;
  paidGeneral: number;
  unpaidGeneral: { title: string; status: AssignmentStatus }[];
  paidEvents: number;
  refunded: number;
  usedBalance: number;
  balance: number;
}

const group = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/** Số không kèm "đ" (bảng báo cáo của canvas): 1.600.000. */
export function plain(value: number): string {
  return group.format(value);
}

/** Phần trăm cho thanh tiến độ, 0–100. */
export function percent(part: number, total: number): number {
  return total > 0 ? Math.min(100, Math.round((part / total) * 100)) : 0;
}

/** Cột "Xử lý" của báo cáo sự kiện. */
export function surplusHandling(row: EventReportRow): string {
  if (row.surplus === null || row.status === 'Open' || row.status === 'Cancelled') {
    return '–';
  }
  if (row.status === 'Completed') {
    return 'Chưa xử lý';
  }
  if (row.surplusMethod === 'PersonalBalance') {
    return `Cộng số dư ${plain(row.creditedToBalance)}`;
  }
  if (row.surplusMethod === 'DirectRefund') {
    return `Hoàn trực tiếp ${plain(row.refunded)}`;
  }
  return row.surplus === 0 ? 'Vừa đủ' : '–';
}

/** Ký tự đặc biệt của CSV: bọc ngoặc kép khi có dấu phẩy, ngoặc kép hoặc xuống dòng. */
function csvCell(value: string | number): string {
  const text = String(value);
  return /[",\n;]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/** Xuất bảng ra CSV (UTF-8 có BOM để Excel đọc đúng tiếng Việt). */
export function toCsv(header: readonly string[], rows: readonly (readonly (string | number)[])[]): string {
  return '﻿' + [header, ...rows].map((row) => row.map(csvCell).join(',')).join('\r\n');
}
