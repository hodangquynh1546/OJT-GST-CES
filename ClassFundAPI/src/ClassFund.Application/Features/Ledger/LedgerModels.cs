using ClassFund.Application.Features.Attachments;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Ledger;

/// <summary>Lọc sổ thu chi của MỘT phạm vi: <see cref="EventId"/> null = quỹ chung, có giá trị = sự kiện đó (BR-01).</summary>
public sealed record LedgerFilter(
    Guid? EventId,
    LedgerType? Type,
    Guid? CategoryId,
    DateOnly? From,
    DateOnly? To,
    int Page,
    int PageSize);

/// <summary>
/// Ghi một khoản chi (FR-27). Thu không ghi tay (BR-39): thu đi qua khoản thu M6.
/// <see cref="EventId"/> null = quỹ chung; có giá trị = sự kiện đang mở của quỹ.
/// </summary>
public sealed record CreateExpenseInput(
    Guid? EventId,
    Guid CategoryId,
    string Title,
    string? Purpose,
    long Amount,
    DateOnly TransactionDate,
    string? Note,
    IReadOnlyList<Guid> AttachmentIds);

/// <summary>Tổng của một phạm vi. Bút toán đảo triệt tiêu dòng gốc nên không làm phồng tổng thu / tổng chi.</summary>
public sealed record ScopeTotals(long TotalIncome, long TotalExpense)
{
    public static readonly ScopeTotals Zero = new(0, 0);

    /// <summary>Số dư hiện có = mức chi tối đa còn lại (BR-38).</summary>
    public long Balance => TotalIncome - TotalExpense;
}

/// <summary>
/// KPI của L-01 / tab Thu chi sự kiện: <see cref="Balance"/> là số dư toàn thời gian của phạm vi;
/// <see cref="TotalIncome"/> / <see cref="TotalExpense"/> tính trong khoảng ngày đang lọc (không lọc ngày = toàn bộ).
/// </summary>
public sealed record LedgerSummaryDto(long Balance, long TotalIncome, long TotalExpense);

public sealed record LedgerEntryDto(
    Guid Id,
    Guid? EventId,
    string? EventName,
    LedgerType Type,
    LedgerSource Source,
    Guid? CategoryId,
    string? CategoryName,
    string Title,
    long Amount,
    DateOnly TransactionDate,
    Guid PerformedByUserId,
    string PerformedByName,
    bool IsSelfDeclared,
    Guid? ReversesEntryId,
    Guid? ReversedByEntryId,
    int AttachmentCount,
    DateTimeOffset CreatedAt);

public sealed record LedgerListResult(
    IReadOnlyList<LedgerEntryDto> Items,
    int Total,
    int Page,
    int PageSize,
    LedgerSummaryDto Summary);

/// <summary>Bút toán đảo của một dòng (L-03: "Đã đảo bởi bút toán ngày … Lý do …").</summary>
public sealed record LedgerReversalDto(Guid Id, long Amount, DateOnly TransactionDate, string? Reason, string PerformedByName, DateTimeOffset CreatedAt);

/// <summary>Dòng gốc mà bút toán đảo này triệt tiêu.</summary>
public sealed record LedgerLinkDto(Guid Id, string Title, DateOnly TransactionDate);

/// <summary>Một thao tác trong nhật ký liên quan tới giao dịch (khối "Lịch sử thao tác" ở L-03, chỉ người có <c>Audit.View</c>).</summary>
public sealed record LedgerHistoryDto(string Action, string ActorName, DateTimeOffset At);

public sealed record LedgerEntryDetailDto(
    LedgerEntryDto Entry,
    string? Purpose,
    string? Note,
    MemberRole? PerformedByRole,
    IReadOnlyList<AttachmentDto> Attachments,
    LedgerReversalDto? ReversedBy,
    LedgerLinkDto? Reverses,
    bool CanReverse,
    IReadOnlyList<LedgerHistoryDto> History);
