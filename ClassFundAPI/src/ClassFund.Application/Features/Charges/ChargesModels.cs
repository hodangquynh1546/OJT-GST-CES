using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Charges;

/// <summary>Tiến độ thu của một khoản (chỉ trả cho người có <c>Charge.Manage</c>). Không tính nghĩa vụ đã hủy.</summary>
public sealed record ChargeProgressDto(int AssignedCount, int PaidCount, int AwaitingCount, long CollectedAmount);

/// <summary>Tình trạng đóng của chính người đang xem.</summary>
public sealed record MyAssignmentDto(Guid AssignmentId, AssignmentStatus Status, long AmountDue);

/// <summary>Một khoản thu chung. <see cref="Progress"/> chỉ có với thủ quỹ; <see cref="Mine"/> null nếu người xem không có nghĩa vụ.</summary>
public sealed record ChargeDto(
    Guid Id,
    string Title,
    string? Purpose,
    long AmountPerPerson,
    DateOnly DueDate,
    ChargeStatus Status,
    DateTimeOffset CreatedAt,
    ChargeProgressDto? Progress,
    MyAssignmentDto? Mine);

public sealed record CreateChargeInput(string Title, string? Purpose, long AmountPerPerson, DateOnly DueDate);

public sealed record ChargeFilter(ChargeStatus? Status, int Page, int PageSize);

/// <summary>
/// Một dòng "đã đóng / chưa đóng" của khoản thu (FR-24). <see cref="IsSelfDeclared"/>: thủ quỹ tự ghi nhận (FR-22).
/// <see cref="PaymentId"/> là lần nộp hiện hành (chờ xác nhận / đã xác nhận); <see cref="RejectionReason"/> là lý do
/// lần nộp gần nhất bị từ chối khi người này chưa nộp lại.
/// </summary>
public sealed record AssignmentRowDto(
    Guid AssignmentId,
    Guid UserId,
    string FullName,
    string Email,
    MemberRole Role,
    long AmountDue,
    AssignmentStatus Status,
    bool IsSelfDeclared,
    Guid? PaymentId,
    PaymentMethod? Method,
    DateTimeOffset? PaymentAt,
    bool HasProof,
    string? RejectionReason);

public sealed record AssignmentFilter(AssignmentStatus? Status, string? Search, int Page, int PageSize);

/// <summary>Số người đã được nhắc đóng.</summary>
public sealed record RemindResult(int Reminded);
