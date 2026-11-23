import { AssignmentStatus, PaymentMethod, PaymentStatus } from '../../shared/models/labels';
import { MemberRole } from '../../core/auth/auth.models';

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
  /** Khoản thu chung không có hạn (null): thủ quỹ nhắc đóng đến khi mọi người đã nộp. */
  dueDate: string | null;
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
  dueDate: string | null;
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

/** Khoản cần đóng của chính mình (khớp `MyAssignmentItemDto`) – chỉ xem: Đã nộp / Chưa nộp. */
export interface MyAssignmentItem {
  assignmentId: string;
  chargeId: string;
  title: string;
  purpose: string | null;
  amountDue: number;
  dueDate: string | null;
  chargeStatus: ChargeStatus;
  status: AssignmentStatus;
  method: PaymentMethod | null;
  paidAt: string | null;
  isSelfDeclared: boolean;
  eventId: string | null;
  eventName: string | null;
}

/** Thủ quỹ chỉ được chọn Tiền mặt hoặc Chuyển khoản (số dư không phải phương thức thanh toán). */
export type CollectMethod = Extract<PaymentMethod, 'Cash' | 'BankTransfer'>;

export interface CollectPaymentRequest {
  method: CollectMethod;
  transferCode?: string | null;
}

/** Khớp `PaymentDto`. */
export interface CollectedPayment {
  id: string;
  assignmentId: string;
  amount: number;
  method: PaymentMethod;
  transferCode: string | null;
  status: PaymentStatus;
  confirmedAt: string | null;
  isSelfDeclared: boolean;
}
