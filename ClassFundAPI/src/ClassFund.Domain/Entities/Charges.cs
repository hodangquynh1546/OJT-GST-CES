using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>Khoản thu. EventId null = khoản thu chung của lớp; có giá trị = khoản thu của sự kiện (không bao giờ cả hai).</summary>
public sealed class Charge(Guid fundId, Guid? eventId, string title, string? purpose, long amountPerPerson, DateOnly dueDate, ChargeKind kind, Guid createdByUserId, DateTimeOffset createdAt) : Entity
{
    public Guid FundId { get; init; } = fundId;

    public Guid? EventId { get; init; } = eventId;

    public string Title { get; set; } = title;

    public string? Purpose { get; set; } = purpose;

    /// <summary>Số tiền mỗi người phải đóng (VND, số nguyên).</summary>
    public long AmountPerPerson { get; set; } = amountPerPerson;

    public DateOnly DueDate { get; set; } = dueDate;

    public ChargeKind Kind { get; init; } = kind;

    /// <summary>Danh mục thu do thủ quỹ chọn khi tạo; bút toán thu sinh từ khoản này mang danh mục đó. Null ở khoản thu tạo trước khi có cột này.</summary>
    public Guid? CategoryId { get; set; }

    public ChargeStatus Status { get; set; } = ChargeStatus.Open;

    public Guid CreatedByUserId { get; init; } = createdByUserId;

    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}

/// <summary>Nghĩa vụ đóng của một người với một khoản thu.</summary>
public sealed class ChargeAssignment(Guid chargeId, Guid userId, long amountDue) : Entity
{
    public Guid ChargeId { get; init; } = chargeId;

    public Guid UserId { get; init; } = userId;

    public long AmountDue { get; set; } = amountDue;

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Unpaid;
}

/// <summary>Một lần nộp tiền (người nộp xác nhận đã nộp). Thủ quỹ xác nhận/từ chối; thủ quỹ tự nộp thì tự ghi nhận.</summary>
public sealed class Payment(Guid assignmentId, Guid payerUserId, long amount, PaymentMethod method, DateTimeOffset submittedAt) : Entity
{
    public Guid AssignmentId { get; init; } = assignmentId;

    public Guid PayerUserId { get; init; } = payerUserId;

    public long Amount { get; init; } = amount;

    public PaymentMethod Method { get; init; } = method;

    /// <summary>Mã giao dịch chuyển khoản (nếu có).</summary>
    public string? TransferCode { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Submitted;

    public DateTimeOffset SubmittedAt { get; init; } = submittedAt;

    public Guid? ConfirmedByUserId { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public ConfirmationMode? ConfirmationMode { get; set; }

    public string? RejectionReason { get; set; }
}
