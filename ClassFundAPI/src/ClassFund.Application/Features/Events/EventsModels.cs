using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Events;

public sealed record CreateEventInput(string Name, string? Description, DateOnly EventDate, DateOnly ListDeadline);

public sealed record UpdateEventInput(string Name, string? Description, DateOnly EventDate, DateOnly ListDeadline);

/// <summary>Tình trạng đóng tiền của một người tham gia với cả sự kiện (FR-32): Đã đóng / Chưa đóng (thủ quỹ ghi nhận khi nhận tiền).</summary>
public enum ParticipantPaymentStatus
{
    /// <summary>Đã đóng đủ mọi khoản của sự kiện.</summary>
    Paid = 1,

    /// <summary>Còn khoản chưa đóng.</summary>
    Unpaid = 3,

    /// <summary>Sự kiện chưa có khoản thu nào áp cho người này.</summary>
    NoCharge = 4,
}

/// <summary>Thẻ sự kiện ở E-01.</summary>
public sealed record EventSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? EventDate,
    DateOnly? ListDeadline,
    EventStatus Status,
    int InvitedCount,
    int ParticipantCount,
    int PaidInFullCount,
    long TotalIncome,
    long TotalExpense,
    long Balance,
    ParticipationResponse? MyResponse,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Phần của tôi trong sự kiện (học sinh và thủ quỹ đều có).</summary>
public sealed record MyParticipationDto(ParticipationResponse Response, bool IsFinal, string? ExclusionReason);

/// <summary>Một nghĩa vụ của tôi với khoản thu của sự kiện (E-03 học sinh: "Phần của bạn").</summary>
public sealed record MyEventChargeDto(Guid ChargeId, string Title, long AmountDue, DateOnly? DueDate, AssignmentStatus Status);

/// <summary>Khoản thu của sự kiện kèm số người đã đóng / chờ / chưa đóng (tab Khoản thu sự kiện).</summary>
public sealed record EventChargeDto(
    Guid Id,
    string Title,
    string? Purpose,
    long AmountPerPerson,
    DateOnly? DueDate,
    ChargeStatus Status,
    int PaidCount,
    int UnpaidCount);

/// <summary>Một người bị loại khi đóng danh sách (E-03 "Lịch sử đóng danh sách"): lý do, số tiền hoàn vào số dư, thời điểm.</summary>
public sealed record EventExclusionDto(Guid UserId, string FullName, string? Reason, long RefundedAmount, DateTimeOffset? ExcludedAt);

/// <summary>E-03. <c>AmountPerPerson</c> = tổng mỗi người phải đóng theo các khoản thu đang mở (KPI "430.000 đ/người").</summary>
public sealed record EventDetailDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? EventDate,
    DateOnly? ListDeadline,
    EventStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int InvitedCount,
    int AcceptedCount,
    int DeclinedCount,
    int PendingCount,
    int ExcludedCount,
    int ParticipantCount,
    long TotalIncome,
    long TotalExpense,
    long Balance,
    long AmountPerPerson,
    MyParticipationDto? Me,
    IReadOnlyList<MyEventChargeDto> MyCharges,
    IReadOnlyList<EventChargeDto> Charges,
    IReadOnlyList<EventExclusionDto> Exclusions);

/// <summary>
/// Một người tham gia (FR-32). Thủ quỹ thấy đủ cột; học sinh chỉ thấy tên (các cột tiền = 0, trạng thái NoCharge).
/// <see cref="Paid"/> tính cả thanh toán thủ quỹ tự ghi nhận; <see cref="HasSelfDeclared"/> để hiện tag "Tự ghi nhận".
/// </summary>
public sealed record EventParticipantDto(
    Guid UserId,
    string FullName,
    MemberRole? Role,
    long Paid,
    long Due,
    ParticipantPaymentStatus PaymentStatus,
    bool HasSelfDeclared);

public sealed record EventParticipantsResult(
    IReadOnlyList<EventParticipantDto> Items,
    int Total,
    int PaidCount,
    int UnpaidCount);

/// <summary>Người sẽ bị loại khi đóng danh sách: số tiền đã đóng (hoàn vào số dư cá nhân) và lý do.</summary>
public sealed record ExcludedPersonDto(Guid UserId, string FullName, long Paid, long Refund, string Reason);

/// <summary>
/// Xem trước khi đóng danh sách (hộp thoại "Đóng danh sách?"): ai bị loại (chưa đóng đủ tất cả khoản thu đang mở) kèm số tiền hoàn,
/// còn lại bao nhiêu người, hạn đóng danh sách và có đang đóng sớm hơn hạn không.
/// </summary>
public sealed record EventCloseListPreviewDto(
    IReadOnlyList<ExcludedPersonDto> Excluded,
    int RemainingCount,
    int OpenChargeCount,
    long TotalRefund,
    DateOnly? ListDeadline,
    bool BeforeDeadline);
