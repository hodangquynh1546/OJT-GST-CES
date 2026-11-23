using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Settlements;

/// <summary>Phần tiền thừa của một người. <see cref="Id"/> và <see cref="Status"/> chỉ có sau khi quyết toán (xem trước thì null).</summary>
public sealed record SettlementLineDto(
    Guid? Id,
    Guid UserId,
    string FullName,
    long Contributed,
    long Amount,
    SettlementLineStatus? Status);

/// <summary>Số liệu xem trước quyết toán (E-04): tổng hợp, người bị đóng nghĩa vụ, chặn nếu còn thanh toán chờ.</summary>
public sealed record SettlementPreviewDto(
    Guid EventId,
    string EventName,
    EventStatus Status,
    int ParticipantCount,
    long TotalCollected,
    long TotalSpent,
    long Surplus,
    SettlementOutcome Outcome,
    int PendingPaymentCount,
    int UnpaidObligationCount,
    bool CanExecute,
    IReadOnlyList<SettlementLineDto> Lines);

/// <summary>Kết quả quyết toán đã thực hiện. Học sinh chỉ nhận dòng của chính mình.</summary>
public sealed record SettlementResultDto(
    Guid Id,
    Guid EventId,
    string EventName,
    SettlementOutcome Outcome,
    SurplusMethod? SurplusMethod,
    int ParticipantCount,
    long TotalCollected,
    long TotalSpent,
    long Surplus,
    DateTimeOffset SettledAt,
    string SettledByName,
    IReadOnlyList<SettlementLineDto> Lines);

/// <summary>
/// Số liệu xem trước tổng kết cuối năm quỹ chung (S-05): chỉ tính giao dịch quỹ chung; phần chia cho mọi thành viên đang hoạt động
/// theo tỷ lệ tổng đã đóng các khoản thu chung cả năm (OQ-19).
/// </summary>
public sealed record YearEndPreviewDto(
    string FundName,
    string SchoolYear,
    FundStatus Status,
    int MemberCount,
    long TotalCollected,
    long TotalSpent,
    long Surplus,
    SettlementOutcome Outcome,
    int UnsettledEventCount,
    int PendingPaymentCount,
    int OpenChargeCount,
    int UnpaidObligationCount,
    bool CanExecute,
    IReadOnlyList<SettlementLineDto> Lines);

/// <summary>Kết quả tổng kết cuối năm. Học sinh chỉ nhận dòng của chính mình.</summary>
public sealed record YearEndResultDto(
    Guid Id,
    string FundName,
    string SchoolYear,
    SettlementOutcome Outcome,
    SurplusMethod? SurplusMethod,
    int MemberCount,
    long TotalCollected,
    long TotalSpent,
    long Surplus,
    DateTimeOffset SettledAt,
    string SettledByName,
    IReadOnlyList<SettlementLineDto> Lines);
