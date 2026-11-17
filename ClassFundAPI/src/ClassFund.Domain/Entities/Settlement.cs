using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>
/// Quyết toán một phạm vi tài chính, làm đúng MỘT lần:
/// EventId có giá trị = quyết toán sự kiện (người tham gia thực tế);
/// EventId null = tổng kết cuối năm quỹ chung (chia cho cả lớp, OQ-13) – sau đó quỹ chuyển Archived.
/// Hai phạm vi không bao giờ chuyển tiền cho nhau.
/// </summary>
public sealed class Settlement(Guid fundId, Guid? eventId, int participantCount, long totalCollected, long totalSpent, Guid settledByUserId, DateTimeOffset settledAt) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public Guid? EventId { get; init; } = eventId;

    public int ParticipantCount { get; init; } = participantCount;

    public long TotalCollected { get; init; } = totalCollected;

    public long TotalSpent { get; init; } = totalSpent;

    /// <summary>Thu − chi: dương = thừa, âm = thiếu.</summary>
    public long Difference => TotalCollected - TotalSpent;

    public SettlementOutcome Outcome { get; set; }

    public SurplusMethod? SurplusMethod { get; set; }

    public AllocationRule? AllocationRule { get; set; }

    /// <summary>Khoản thu bổ sung được tạo khi thiếu tiền (cùng phạm vi với quyết toán).</summary>
    public Guid? SupplementaryChargeId { get; set; }

    public Guid SettledByUserId { get; init; } = settledByUserId;

    public DateTimeOffset SettledAt { get; init; } = settledAt;
}

/// <summary>Phần tiền thừa chia cho một người (người tham gia thực tế của sự kiện, hoặc thành viên lớp khi tổng kết cuối năm).</summary>
public sealed class SettlementLine(Guid settlementId, Guid userId, long contributed, long refundAmount) : Entity
{
    public Guid SettlementId { get; init; } = settlementId;

    public Guid UserId { get; init; } = userId;

    public long Contributed { get; init; } = contributed;

    public long RefundAmount { get; init; } = refundAmount;

    public SettlementLineStatus Status { get; set; } = SettlementLineStatus.PendingRefund;

    public Guid? ConfirmedByUserId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
}

/// <summary>Số dư cá nhân của một người trong một quỹ; độc lập với quỹ chung. Version chống ghi đè đồng thời.</summary>
public sealed class PersonalBalance(Guid fundId, Guid userId) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public Guid UserId { get; init; } = userId;

    public long Balance { get; set; }

    public uint Version { get; set; }
}

/// <summary>Lịch sử biến động số dư (append-only). Amount có dấu: cộng (+) / trừ (−).</summary>
public sealed class BalanceTransaction(Guid fundId, Guid userId, BalanceTransactionType type, long amount, long balanceAfter, Guid? referenceId, Guid createdByUserId, DateTimeOffset createdAt) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public Guid UserId { get; init; } = userId;

    public BalanceTransactionType Type { get; init; } = type;

    public long Amount { get; init; } = amount;

    public long BalanceAfter { get; init; } = balanceAfter;

    /// <summary>SettlementLine / Payment / WithdrawalRequest tương ứng.</summary>
    public Guid? ReferenceId { get; init; } = referenceId;

    public Guid CreatedByUserId { get; init; } = createdByUserId;

    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}

/// <summary>
/// Yêu cầu rút TOÀN BỘ số dư (OQ-11): Amount = số dư tại lúc yêu cầu; trong khi chờ, số dư bị giữ (không dùng đóng khoản thu).
/// Mỗi người chỉ có một yêu cầu đang chờ trong một quỹ. Thủ quỹ liên hệ, trả tiền mặt, xác nhận Paid.
/// </summary>
public sealed class WithdrawalRequest(Guid fundId, Guid userId, long amount, DateTimeOffset requestedAt) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public Guid UserId { get; init; } = userId;

    public long Amount { get; init; } = amount;

    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Requested;

    public DateTimeOffset RequestedAt { get; init; } = requestedAt;

    public Guid? HandledByUserId { get; set; }

    public DateTimeOffset? HandledAt { get; set; }

    public string? RejectionReason { get; set; }
}
