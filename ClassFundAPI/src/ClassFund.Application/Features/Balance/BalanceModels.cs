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

/// <summary>Yêu cầu rút gần nhất của tôi (đang chờ, hoặc kết quả lần trước để hiện thông báo trên B-01).</summary>
public sealed record MyWithdrawalDto(
    Guid Id,
    long Amount,
    WithdrawalStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? HandledAt,
    string? HandledByName,
    string? RejectionReason);

/// <summary>Số dư của tôi ở quỹ khác (B-01 "Số dư ở các quỹ khác").</summary>
public sealed record OtherFundBalanceDto(Guid FundId, string FundName, bool IsArchived, long Balance);

public sealed record MyBalanceDto(
    string FundName,
    long Balance,
    MyWithdrawalDto? LatestWithdrawal,
    IReadOnlyList<BalanceHistoryItemDto> History,
    IReadOnlyList<OtherFundBalanceDto> OtherFunds);

/// <summary>Một yêu cầu rút ở B-02.</summary>
public sealed record WithdrawalRequestDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    long Amount,
    WithdrawalStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? HandledAt,
    string? HandledByName,
    string? RejectionReason,
    bool IsMine);

public sealed record WithdrawalListResult(
    IReadOnlyList<WithdrawalRequestDto> Items,
    int RequestedCount,
    int PaidCount,
    int RejectedCount);
