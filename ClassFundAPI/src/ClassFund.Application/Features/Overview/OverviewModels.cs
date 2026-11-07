using ClassFund.Application.Features.Funds;
using ClassFund.Application.Features.Reports;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Authorization;

namespace ClassFund.Application.Features.Overview;

/// <summary>Chỉ họ tên và vai trò: không id, email, tên đăng nhập hay liên hệ (FR-12, BR-23).</summary>
public sealed record FundMemberResponse(string FullName, FundRole Role);

/// <summary>Số liệu giống nhau ở mọi vai trò (BR-15); <c>recentTransactions</c> theo phạm vi người xem.</summary>
public sealed record FundOverviewResponse(
    FundRole Role,
    IReadOnlyList<string> Permissions,
    long CurrentBalance,
    long TotalIncome,
    long TotalExpense,
    PeriodTotals CurrentMonth,
    TreasurerStatus TreasurerStatus,
    IReadOnlyList<FundMemberResponse> Managers,
    IReadOnlyList<TransactionResponse> RecentTransactions);
