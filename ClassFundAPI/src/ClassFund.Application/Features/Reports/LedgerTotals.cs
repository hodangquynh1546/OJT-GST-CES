using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Reports;

/// <summary>Tổng thu, tổng chi và chênh lệch của một kỳ.</summary>
public sealed record PeriodTotals(long Income, long Expense)
{
    public long Net => Income - Expense;
}

/// <summary>Mọi số liệu đều tính động từ các khoản chưa hủy, không có bảng số dư riêng (BR-02).</summary>
internal static class LedgerTotals
{
    public static IQueryable<FundTransaction> ActiveTransactions(this IAppDb db, Guid fundId) =>
        db.Query<FundTransaction>().Where(transaction => transaction.FundId == fundId && !transaction.IsCancelled);

    /// <summary>Gộp theo loại trong database; không dùng <c>Sum</c> trực tiếp vì tập rỗng phải cho kết quả 0.</summary>
    public static async Task<PeriodTotals> TotalsAsync(
        this IAppDb db,
        IQueryable<FundTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var totals = await db.ToListAsync(
            transactions
                .GroupBy(transaction => transaction.Type)
                .Select(group => new { Type = group.Key, Total = group.Sum(transaction => transaction.Amount) }),
            cancellationToken);

        return new PeriodTotals(
            totals.Where(item => item.Type == TransactionType.Income).Sum(item => item.Total),
            totals.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Total));
    }
}
