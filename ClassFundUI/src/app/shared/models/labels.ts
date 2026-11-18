/** Phạm vi tài chính: giao dịch/khoản thu/thông báo chỉ thuộc MỘT phạm vi (khớp FinancialScope ở backend). */
export type FinancialScope = 'General' | 'Event' | 'System';

export const SCOPE_LABEL: Record<FinancialScope, string> = {
  General: 'Quỹ chung',
  Event: 'Sự kiện',
  System: 'Hệ thống',
};

/** Vòng đời sự kiện (OQ-18): Open → Completed → Settled; Cancelled từ Open. */
export type EventStatus = 'Open' | 'Completed' | 'Settled' | 'Cancelled';

export const EVENT_STATUS_LABEL: Record<EventStatus, string> = {
  Open: 'Đang mở',
  Completed: 'Đã kết thúc',
  Settled: 'Đã quyết toán',
  Cancelled: 'Đã hủy',
};

export type PaymentMethod = 'Cash' | 'BankTransfer' | 'PersonalBalance';

export const PAYMENT_METHOD_LABEL: Record<PaymentMethod, string> = {
  Cash: 'Tiền mặt',
  BankTransfer: 'Chuyển khoản',
  PersonalBalance: 'Số dư cá nhân',
};

export type PaymentStatus = 'Submitted' | 'Confirmed' | 'Rejected';

export const PAYMENT_STATUS_LABEL: Record<PaymentStatus, string> = {
  Submitted: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Rejected: 'Bị từ chối',
};

export type AssignmentStatus = 'Unpaid' | 'AwaitingConfirmation' | 'Paid' | 'Excluded' | 'Cancelled';

export const ASSIGNMENT_STATUS_LABEL: Record<AssignmentStatus, string> = {
  Unpaid: 'Chưa đóng',
  AwaitingConfirmation: 'Chờ xác nhận',
  Paid: 'Đã đóng',
  Excluded: 'Bị loại khỏi sự kiện',
  Cancelled: 'Đã hủy',
};
