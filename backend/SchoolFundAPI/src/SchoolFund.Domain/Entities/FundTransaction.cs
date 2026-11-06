using SchoolFund.Domain.Common;
using SchoolFund.Domain.Enums;
using SchoolFund.Domain.Exceptions;

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
    public const string CancelledCode = "transaction.cancelled";
    public const string CancelReasonRequiredCode = "transaction.cancel_reason_required";

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

    /// <summary>Khoản đã hủy không sửa và không hủy lại được (BR-05, BR-06).</summary>
    public void EnsureActive()
    {
        if (IsCancelled)
        {
            throw new BusinessRuleException(CancelledCode, "Khoản này đã bị hủy.");
        }
    }

    /// <summary>Không có tham số loại: thu không đổi được thành chi và ngược lại (BR-05).</summary>
    public void Update(
        long amount,
        DateOnly occurredOn,
        Guid categoryId,
        string description,
        string? note,
        bool isPublic,
        Guid updatedBy,
        DateTimeOffset updatedAt)
    {
        EnsureActive();
        Amount = amount;
        OccurredOn = occurredOn;
        CategoryId = categoryId;
        Description = description;
        Note = note;
        IsPublic = isPublic;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    public void Cancel(string reason, Guid cancelledBy, DateTimeOffset cancelledAt)
    {
        EnsureActive();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException(CancelReasonRequiredCode, "Hủy khoản thu chi phải có lý do.");
        }

        IsCancelled = true;
        CancelReason = reason;
        CancelledBy = cancelledBy;
        CancelledAt = cancelledAt;
    }
}
