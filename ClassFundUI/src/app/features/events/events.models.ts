import { AssignmentStatus, EventStatus } from '../../shared/models/labels';
import { MemberRole } from '../ledger/ledger.models';

export type ParticipationResponse = 'Pending' | 'Accepted' | 'Declined';

/** Tình trạng đóng tiền của người tham gia với cả sự kiện (FR-32). */
export type ParticipantPaymentStatus = 'Paid' | 'Pending' | 'Unpaid' | 'NoCharge';

export type ChargeStatus = 'Open' | 'Closed' | 'Cancelled';

export interface EventSummary {
  id: string;
  name: string;
  description: string | null;
  eventDate: string | null;
  status: EventStatus;
  invitedCount: number;
  participantCount: number;
  paidInFullCount: number;
  totalIncome: number;
  totalExpense: number;
  balance: number;
  myResponse: ParticipationResponse | null;
  createdAt: string;
  completedAt: string | null;
}

export interface MyEventCharge {
  chargeId: string;
  title: string;
  amountDue: number;
  dueDate: string;
  status: AssignmentStatus;
}

export interface EventCharge {
  id: string;
  title: string;
  purpose: string | null;
  amountPerPerson: number;
  dueDate: string;
  status: ChargeStatus;
  paidCount: number;
  pendingCount: number;
  unpaidCount: number;
}

export interface EventDetail {
  id: string;
  name: string;
  description: string | null;
  eventDate: string | null;
  status: EventStatus;
  createdAt: string;
  completedAt: string | null;
  invitedCount: number;
  acceptedCount: number;
  declinedCount: number;
  pendingCount: number;
  excludedCount: number;
  participantCount: number;
  totalIncome: number;
  totalExpense: number;
  balance: number;
  amountPerPerson: number;
  me: { response: ParticipationResponse; isFinal: boolean; exclusionReason: string | null } | null;
  myCharges: MyEventCharge[];
  charges: EventCharge[];
}

export interface EventParticipant {
  userId: string;
  fullName: string;
  role: MemberRole | null;
  paid: number;
  due: number;
  paymentStatus: ParticipantPaymentStatus;
  hasSelfDeclared: boolean;
  pendingPaymentCount: number;
}

export interface EventParticipants {
  items: EventParticipant[];
  total: number;
  paidCount: number;
  pendingCount: number;
  unpaidCount: number;
}

export interface CompletionPreview {
  excluded: { userId: string; fullName: string }[];
  underpaid: { userId: string; fullName: string; missing: number }[];
  pendingPaymentCount: number;
  remainingCount: number;
}

export interface SaveEventBody {
  name: string;
  description: string | null;
  eventDate: string;
}

export const EVENT_NAME_MAX = 150;
export const EVENT_DESCRIPTION_MAX = 1000;
export const CANCEL_REASON_MAX = 500;

export type Tone = 'event' | 'warning' | 'success' | 'neutral' | 'danger';

/** Nhãn trạng thái trên thẻ / tiêu đề sự kiện như canvas (b-ev / b-warn / b-ok / b-gen). */
export const EVENT_STATUS_BADGE: Record<EventStatus, { text: string; tone: Tone }> = {
  Open: { text: 'Đang mở đăng ký & thu chi', tone: 'event' },
  Completed: { text: 'Đã kết thúc – chờ quyết toán', tone: 'warning' },
  Settled: { text: 'Đã quyết toán', tone: 'success' },
  Cancelled: { text: 'Đã hủy', tone: 'neutral' },
};

/** Tab E-01: "Đang diễn ra" (đang mở + chờ quyết toán) / "Đã quyết toán / hủy". */
export function isActiveEvent(status: EventStatus): boolean {
  return status === 'Open' || status === 'Completed';
}

export const PAYMENT_STATUS_BADGE: Record<ParticipantPaymentStatus, { text: string; tone: Tone }> = {
  Paid: { text: 'Đã đóng', tone: 'success' },
  Pending: { text: 'Chờ xác nhận', tone: 'warning' },
  Unpaid: { text: 'Chưa đóng', tone: 'danger' },
  NoCharge: { text: 'Chưa có khoản thu', tone: 'neutral' },
};

export const MY_RESPONSE_BADGE: Record<ParticipationResponse, { text: string; tone: Tone }> = {
  Accepted: { text: 'Bạn: Tham gia', tone: 'success' },
  Declined: { text: 'Bạn: Không tham gia', tone: 'neutral' },
  Pending: { text: 'Bạn chưa trả lời', tone: 'warning' },
};

const MONTH_DAY = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Ô ngày trên thẻ E-01: "TH11" / "20". */
export function dateBox(date: string | null): { month: string; day: string } | null {
  const match = date ? MONTH_DAY.exec(date) : null;
  return match ? { month: `TH${match[2]}`, day: match[3] } : null;
}

const vnMoney = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
const money = (value: number) => `${vnMoney.format(value)} đ`;

/** Dòng số liệu trên thẻ E-01 (canvas: "30 tham gia · 20 đã đóng đủ · thu 10.700.000 đ · chi 7.200.000 đ"). */
export function eventStats(event: EventSummary): string {
  if (event.status === 'Cancelled') {
    return event.totalIncome === 0 && event.totalExpense === 0 ? 'Không phát sinh thu chi' : `thu ${money(event.totalIncome)} · chi ${money(event.totalExpense)}`;
  }
  const parts = [`${event.participantCount} tham gia`];
  if (event.status === 'Open') {
    parts.push(`${event.paidInFullCount} đã đóng đủ`);
  }
  parts.push(`thu ${money(event.totalIncome)}`, `chi ${money(event.totalExpense)}`);
  if (event.status !== 'Open' && event.balance > 0) {
    parts.push(`thừa ${money(event.balance)}`);
  }
  return parts.join(' · ');
}

/** "Thứ Sáu 20/11/2026". */
export function longDate(date: string | null): string {
  const match = date ? MONTH_DAY.exec(date) : null;
  if (!match) {
    return '';
  }
  const value = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  const weekday = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'][value.getDay()];
  return `${weekday} ${match[3]}/${match[2]}/${match[1]}`;
}
