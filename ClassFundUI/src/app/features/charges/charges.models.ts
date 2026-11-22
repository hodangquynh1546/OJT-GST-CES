import { AssignmentStatus, PaymentMethod, PaymentStatus } from '../../shared/models/labels';
import { MemberRole } from '../../core/auth/auth.models';
import { Attachment } from '../attachments/attachments.models';

export type ChargeStatus = 'Open' | 'Closed' | 'Cancelled';

export const CHARGE_STATUS_LABEL: Record<ChargeStatus, string> = {
  Open: 'Đang mở',
  Closed: 'Đã đóng',
  Cancelled: 'Đã hủy',
};

/** Tiến độ thu (chỉ có với người có `Charge.Manage`). */
export interface ChargeProgress {
  assignedCount: number;
  paidCount: number;
  awaitingCount: number;
  collectedAmount: number;
}

export interface MyAssignment {
  assignmentId: string;
  status: AssignmentStatus;
  amountDue: number;
}

/** Khớp `ChargeDto`. */
export interface Charge {
  id: string;
  title: string;
  purpose: string | null;
  amountPerPerson: number;
  dueDate: string;
  status: ChargeStatus;
  createdAt: string;
  categoryId: string | null;
  categoryName: string | null;
  progress: ChargeProgress | null;
  mine: MyAssignment | null;
}

export interface CreateChargeRequest {
  title: string;
  purpose: string | null;
  amountPerPerson: number;
  dueDate: string;
  categoryId: string;
}

/** Một dòng đã/chưa đóng (khớp `AssignmentRowDto`). */
export interface AssignmentRow {
  assignmentId: string;
  userId: string;
  fullName: string;
  email: string;
  role: MemberRole;
  amountDue: number;
  status: AssignmentStatus;
  isSelfDeclared: boolean;
  paymentId: string | null;
  method: PaymentMethod | null;
  paymentAt: string | null;
  hasProof: boolean;
  rejectionReason: string | null;
}

/** Khoản cần đóng của chính mình (khớp `MyAssignmentItemDto`). */
export interface MyAssignmentItem {
  assignmentId: string;
  chargeId: string;
  title: string;
  purpose: string | null;
  amountDue: number;
  dueDate: string;
  chargeStatus: ChargeStatus;
  status: AssignmentStatus;
  paymentId: string | null;
  method: PaymentMethod | null;
  paymentStatus: PaymentStatus | null;
  rejectionReason: string | null;
  isSelfDeclared: boolean;
  eventId: string | null;
  eventName: string | null;
  /** Số dư cá nhân đã dùng cho khoản này (đóng một phần bằng số dư). */
  balanceApplied: number;
}

export interface SubmitPaymentRequest {
  method: Extract<PaymentMethod, 'Cash' | 'BankTransfer' | 'PersonalBalance'>;
  transferCode: string | null;
  attachmentId: string | null;
  /** Dùng số dư cá nhân trước, phần còn thiếu đóng bằng `method`. */
  useBalance?: boolean;
}

export interface SubmittedPayment {
  id: string;
  assignmentId: string;
  amount: number;
  method: PaymentMethod;
  transferCode: string | null;
  status: PaymentStatus;
  submittedAt: string;
  isSelfDeclared: boolean;
  balanceApplied: number;
}

/** Một dòng hàng chờ xác nhận (khớp `PaymentQueueItemDto`). */
export interface PaymentQueueItem {
  id: string;
  assignmentId: string;
  chargeId: string;
  chargeTitle: string;
  payerUserId: string;
  payerName: string;
  payerEmail: string;
  amount: number;
  method: PaymentMethod;
  transferCode: string | null;
  status: PaymentStatus;
  submittedAt: string;
  confirmedAt: string | null;
  isSelfDeclared: boolean;
  rejectionReason: string | null;
  attachments: Attachment[];
  /** Phần đã trừ từ số dư cá nhân; thủ quỹ chỉ cần nhận `amount - balanceApplied`. */
  balanceApplied: number;
  /** Khoản thu của sự kiện nào (null = quỹ chung). */
  eventId: string | null;
  eventName: string | null;
}
