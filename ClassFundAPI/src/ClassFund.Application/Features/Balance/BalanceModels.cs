using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Balance;

/// <summary>Một biến động số dư (B-01 "Lịch sử biến động"). <c>Amount</c> có dấu; <c>Scope</c> null với rút tiền / điều chỉnh.</summary>
public sealed record BalanceHistoryItemDto(
    Guid Id,
    BalanceTransactionType Type,
    long Amount,
    long BalanceAfter,
    DateTimeOffset CreatedAt,
    string Description,
    FinancialScope? Scope,
    string? EventName);

/// <summary>Số dư của tôi và lịch sử biến động (chỉ xem; học sinh không tự rút).</summary>
public sealed record MyBalanceDto(
    string FundName,
    long Balance,
    IReadOnlyList<BalanceHistoryItemDto> History);

/// <summary>Một thành viên và số dư cá nhân của họ (B-02 "Rút số dư").</summary>
public sealed record MemberBalanceDto(
    Guid UserId,
    string FullName,
    string Email,
    MemberRole Role,
    bool IsActive,
    long Balance);

/// <summary><see cref="HeldTotal"/> = tổng tiền thủ quỹ đang giữ hộ (tổng số dư mọi thành viên của quỹ).</summary>
public sealed record MemberBalancesResult(
    IReadOnlyList<MemberBalanceDto> Items,
    int Total,
    int Page,
    int PageSize,
    long HeldTotal);

/// <summary>Một lần rút số dư đã thực hiện (lịch sử rút). Dữ liệu cũ có thể ở trạng thái Rejected (yêu cầu bị hủy khi đổi cách rút).</summary>
public sealed record WithdrawalDto(
    Guid Id,
    Guid UserId,
    string FullName,
    long Amount,
    WithdrawalStatus Status,
    DateTimeOffset At,
    string? HandledByName,
    string? Note);
