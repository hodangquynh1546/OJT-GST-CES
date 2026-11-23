using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Payments;

/// <summary>
/// Một khoản cần đóng của chính người dùng (H-03, chỉ xem): Đã nộp / Chưa nộp (<see cref="Status"/>). Chỉ có nghĩa vụ của mình (AZ-04).
/// <see cref="Method"/> / <see cref="PaidAt"/> có khi đã được thủ quỹ ghi nhận đã thu.
/// </summary>
public sealed record MyAssignmentItemDto(
    Guid AssignmentId,
    Guid ChargeId,
    string Title,
    string? Purpose,
    long AmountDue,
    DateOnly? DueDate,
    ChargeStatus ChargeStatus,
    AssignmentStatus Status,
    PaymentMethod? Method,
    DateTimeOffset? PaidAt,
    bool IsSelfDeclared,
    Guid? EventId = null,
    string? EventName = null);

/// <summary>Thủ quỹ ghi nhận đã thu: chỉ Tiền mặt hoặc Chuyển khoản (số dư cá nhân không dùng để đóng khoản thu).</summary>
public sealed record CollectPaymentInput(PaymentMethod Method, string? TransferCode);

/// <summary>Kết quả ghi nhận thu. Thủ quỹ tích cho chính mình thì <see cref="IsSelfDeclared"/> = true.</summary>
public sealed record PaymentDto(
    Guid Id,
    Guid AssignmentId,
    long Amount,
    PaymentMethod Method,
    string? TransferCode,
    PaymentStatus Status,
    DateTimeOffset? ConfirmedAt,
    bool IsSelfDeclared);
