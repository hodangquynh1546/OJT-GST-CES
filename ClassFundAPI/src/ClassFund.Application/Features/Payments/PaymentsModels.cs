using ClassFund.Application.Features.Attachments;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Payments;

/// <summary>Một khoản cần đóng của chính người dùng (H-03). Chỉ có nghĩa vụ của mình (AZ-04).</summary>
public sealed record MyAssignmentItemDto(
    Guid AssignmentId,
    Guid ChargeId,
    string Title,
    string? Purpose,
    long AmountDue,
    DateOnly DueDate,
    ChargeStatus ChargeStatus,
    AssignmentStatus Status,
    Guid? PaymentId,
    PaymentMethod? Method,
    PaymentStatus? PaymentStatus,
    string? RejectionReason,
    bool IsSelfDeclared,
    Guid? EventId = null,
    string? EventName = null);

public sealed record SubmitPaymentInput(PaymentMethod Method, string? TransferCode, Guid? AttachmentId);

/// <summary>Kết quả nộp tiền. Thủ quỹ tự nộp cho mình thì <see cref="Status"/> = Confirmed và <see cref="IsSelfDeclared"/> = true.</summary>
public sealed record PaymentDto(
    Guid Id,
    Guid AssignmentId,
    long Amount,
    PaymentMethod Method,
    string? TransferCode,
    PaymentStatus Status,
    DateTimeOffset SubmittedAt,
    bool IsSelfDeclared);

/// <summary>Một dòng trong hàng chờ xác nhận (H-04) kèm ảnh minh chứng.</summary>
public sealed record PaymentQueueItemDto(
    Guid Id,
    Guid AssignmentId,
    Guid ChargeId,
    string ChargeTitle,
    Guid PayerUserId,
    string PayerName,
    string PayerEmail,
    long Amount,
    PaymentMethod Method,
    string? TransferCode,
    PaymentStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ConfirmedAt,
    bool IsSelfDeclared,
    string? RejectionReason,
    IReadOnlyList<AttachmentDto> Attachments);

/// <summary><see cref="Status"/> mặc định <c>Submitted</c> (chờ xác nhận).</summary>
public sealed record PaymentFilter(PaymentStatus? Status, int Page, int PageSize);
