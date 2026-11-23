using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Reports;

/// <summary>
/// Số liệu quỹ chung (chỉ <c>EventId = null</c>, BR-02). <c>SelfDeclaredIncome</c> = thu thủ quỹ tự ghi nhận, tách riêng (BR-13);
/// <c>IncomeCount</c> = lượt thanh toán đã xác nhận còn hiệu lực; <c>ExpenseCount</c> = khoản chi chưa bị đảo.
/// </summary>
public sealed record GeneralTotalsDto(long Balance, long TotalIncome, long TotalExpense, long SelfDeclaredIncome, int IncomeCount, int ExpenseCount);

/// <summary>Một giao dịch gần đây ở S-01 (chỉ thủ quỹ thấy).</summary>
public sealed record OverviewEntryDto(
    Guid Id,
    DateOnly TransactionDate,
    string Title,
    string? CategoryName,
    LedgerType Type,
    LedgerSource Source,
    long Amount,
    string PerformedByName,
    int AttachmentCount,
    bool IsSelfDeclared,
    PaymentMethod? PaymentMethod,
    bool IsReversed);

/// <summary>Khoản thu chung đang mở và tiến độ (S-01 "Khoản thu chưa hoàn thành").</summary>
public sealed record OpenChargeDto(Guid Id, string Title, long AmountPerPerson, DateOnly? DueDate, int AssignedCount, int PaidCount);

public sealed record OverviewEventDto(
    Guid Id,
    string Name,
    DateOnly? EventDate,
    EventStatus Status,
    int ParticipantCount,
    int PaidInFullCount,
    ParticipationResponse? MyResponse);

public sealed record NamedItemDto(Guid Id, string Name);

/// <summary>"Việc cần làm" của thủ quỹ: khoản thu chung còn người chưa đóng, sự kiện tới hạn cần đóng danh sách, sự kiện chờ quyết toán.</summary>
public sealed record ManagerTodoDto(int UnpaidGeneralCount, IReadOnlyList<NamedItemDto> EventsToCloseList, IReadOnlyList<NamedItemDto> EventsAwaitingSettlement);

/// <summary>Phần của tôi (S-01 học sinh): cần đóng, số dư, khoản gần hạn nhất.</summary>
public sealed record MyOverviewDto(
    long DueAmount,
    int DueCount,
    string? NearestDueTitle,
    DateOnly? NearestDueDate,
    long NearestDueAmount,
    string? NearestDueEventName,
    long Balance);

public sealed record OverviewNotificationDto(string Title, string Body, FinancialScope Scope, string? EventName, DateTimeOffset CreatedAt, bool IsRead);

public sealed record FundOverviewDto(
    string FundName,
    string SchoolYear,
    IReadOnlyList<string> TreasurerNames,
    GeneralTotalsDto General,
    int MemberCount,
    int TreasurerCount,
    long HeldBalance,
    IReadOnlyList<OverviewEntryDto> RecentEntries,
    IReadOnlyList<OpenChargeDto> OpenCharges,
    IReadOnlyList<OverviewEventDto> Events,
    ManagerTodoDto? Todo,
    MyOverviewDto Me,
    IReadOnlyList<OverviewNotificationDto> Notifications);

public sealed record CategoryAmountDto(Guid? CategoryId, string CategoryName, long Amount);

/// <summary>Một tháng ở "Số dư theo tháng": thu / chi trong tháng và số dư quỹ chung cuối tháng.</summary>
public sealed record MonthRowDto(int Year, int Month, long Income, long Expense, long ClosingBalance);

/// <summary>S-02 tab Thu chi chung (FR-49). Tổng thu / chi trong khoảng ngày; số dư toàn thời gian.</summary>
public sealed record GeneralReportDto(
    DateOnly? From,
    DateOnly? To,
    long TotalIncome,
    long TotalExpense,
    long SelfDeclaredIncome,
    long Balance,
    int OpenChargeCount,
    IReadOnlyList<CategoryAmountDto> ExpenseByCategory,
    IReadOnlyList<MonthRowDto> Months);

/// <summary>S-02 tab Sự kiện (FR-50): một dòng mỗi sự kiện.</summary>
public sealed record EventReportRowDto(
    Guid Id,
    string Name,
    EventStatus Status,
    int ParticipantCount,
    int? FinalCount,
    long AmountDue,
    long Collected,
    long Uncollected,
    long TotalExpense,
    long? Surplus,
    SurplusMethod? SurplusMethod,
    long Refunded,
    long CreditedToBalance);

public sealed record UnpaidChargeDto(string Title, AssignmentStatus Status);

/// <summary>S-04 (FR-51): một học sinh. Học sinh chỉ xem dòng của mình (<c>/reports/me</c>).</summary>
public sealed record StudentReportRowDto(
    Guid UserId,
    string FullName,
    MemberRole Role,
    long PaidGeneral,
    IReadOnlyList<UnpaidChargeDto> UnpaidGeneral,
    long PaidEvents,
    long Refunded,
    long UsedBalance,
    long Balance);
