namespace ClassFund.Domain.Enums;

/// <summary>Phạm vi tài chính: giao dịch/khoản thu/thông báo/audit chỉ thuộc đúng MỘT phạm vi (yêu cầu mục 2, 6.3).</summary>
public enum FinancialScope { General = 1, Event = 2, System = 3 }

public enum UserOrigin { SelfRegistered = 1, Provisioned = 2 }

public enum MemberRole { Owner = 1, Treasurer = 2, Student = 3 }

public enum MemberStatus { Active = 1, Removed = 2 }

public enum FundStatus { Active = 1, Archived = 2 }

/// <summary>Open → Completed (kết thúc) → Settled (đã quyết toán); Cancelled từ Open. Không có bước chốt danh sách (OQ-18).</summary>
public enum EventStatus { Open = 1, Completed = 3, Settled = 4, Cancelled = 5 }

public enum ParticipationResponse { Pending = 1, Accepted = 2, Declined = 3 }

/// <summary>Chi không vượt thu (BR-38) nên không còn khoản thu bổ sung khi quyết toán (OQ-18).</summary>
public enum ChargeKind { Regular = 1 }

public enum ChargeStatus { Open = 1, Closed = 2, Cancelled = 3 }

/// <summary>Trạng thái nghĩa vụ đóng tiền của một người với một khoản thu.</summary>
public enum AssignmentStatus { Unpaid = 1, AwaitingConfirmation = 2, Paid = 3, Excluded = 4, Cancelled = 5 }

public enum PaymentMethod { Cash = 1, BankTransfer = 2, PersonalBalance = 3 }

public enum PaymentStatus { Submitted = 1, Confirmed = 2, Rejected = 3 }

/// <summary>Verified: thủ quỹ khác/đúng người xác nhận cho người khác; SelfDeclared: thủ quỹ tự ghi nhận cho mình (mục 1.3).</summary>
public enum ConfirmationMode { Verified = 1, SelfDeclared = 2 }

public enum LedgerType { Income = 1, Expense = 2 }

public enum LedgerSource { Manual = 1, Payment = 2, Refund = 3, BalanceWithdrawal = 4 }

public enum AllocationRule { Equal = 1, Proportional = 2 }

public enum SurplusMethod { DirectRefund = 1, PersonalBalance = 2 }

/// <summary>Tổng chi luôn ≤ tổng thu (BR-38) nên quyết toán chỉ có vừa đủ hoặc thừa.</summary>
public enum SettlementOutcome { Balanced = 1, Surplus = 2 }

public enum SettlementLineStatus { PendingRefund = 1, Refunded = 2, CreditedToBalance = 3 }

public enum BalanceTransactionType { SettlementCredit = 1, ChargePayment = 2, Withdrawal = 3, Adjustment = 4, ChargeRefund = 5, ListCloseRefund = 6 }

public enum WithdrawalStatus { Requested = 1, Paid = 2, Rejected = 3 }

/// <summary>Receipt: ảnh hóa đơn/chứng từ chi; PaymentProof: bill chuyển khoản; Avatar: ảnh đại diện; Document: tài liệu.</summary>
public enum AttachmentKind { Receipt = 1, PaymentProof = 2, Avatar = 3, Document = 4 }

/// <summary>Nhắc đóng tiền: <c>Auto</c> = hệ thống nhắc trước hạn đóng danh sách; <c>Manual</c> = thủ quỹ bấm "Nhắc đóng".</summary>
public enum ReminderKind { Manual = 1, Auto = 2 }

public enum NotificationType
{
    EventCreated = 1,
    GeneralChargeCreated = 2,
    EventChargeCreated = 3,
    ChargeDueSoon = 4,

    /// <summary>Bị loại khỏi sự kiện khi kết thúc vì chưa đóng khoản nào (BR-19).</summary>
    EventParticipantExcluded = 5,
    SettlementResult = 6,
    BalanceCredited = 7,
    WithdrawalProcessed = 8,
    PaymentConfirmed = 9,
    PaymentRejected = 10,

    /// <summary>Sự kiện đã kết thúc, nhắc người chưa đóng đủ nộp phần còn lại (BR-19).</summary>
    EventUnpaidReminder = 11,
}
