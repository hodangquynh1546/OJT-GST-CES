using SchoolFund.Domain.Common;
using SchoolFund.Domain.Enums;

namespace SchoolFund.Domain.Entities;

/// <summary>Khoản thu hoặc chi; hủy là đánh dấu, số dư luôn tính từ các khoản chưa hủy.</summary>
public sealed class FundTransaction(
    Guid fundId,
    TransactionType type,
    long amount,
    DateOnly occurredOn,
    Guid categoryId,
    string description,
    string? note,
    bool isPublic,
    Guid createdBy,
    Guid idempotencyKey,
    DateTimeOffset createdAt) : Entity
{
    public Guid FundId { get; private set; } = fundId;

    public TransactionType Type { get; private set; } = type;

    public long Amount { get; private set; } = amount;

    public DateOnly OccurredOn { get; private set; } = occurredOn;

    public Guid CategoryId { get; private set; } = categoryId;

    public string Description { get; private set; } = description;

    public string? Note { get; private set; } = note;

    public bool IsPublic { get; private set; } = isPublic;

    public bool IsCancelled { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelReason { get; private set; }

    public Guid CreatedBy { get; private set; } = createdBy;

    public Guid? UpdatedBy { get; private set; }

    public Guid? CancelledBy { get; private set; }

    public Guid IdempotencyKey { get; private set; } = idempotencyKey;

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;

    public DateTimeOffset? UpdatedAt { get; private set; }
}
