using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Reports;

/// <inheritdoc />
/// <remarks>
/// TD-107: số tổng tháng cộng từ chính các dòng <c>byCategory</c> nên tổng các dòng luôn khớp số tổng; số dư đầu kỳ tính lại
/// từ mọi khoản trước ngày 1, không đọc báo cáo tháng trước.
/// TD-108: bản công khai chỉ nhóm khoản công khai theo danh mục; khoản không công khai chỉ được <c>SUM</c> theo loại trong
/// database, nên danh mục và số lượng của chúng không rời khỏi database (NFR-08).
/// TD-109: danh sách khoản dùng lại <see cref="ITransactionsService.ListAsync"/> – một nơi quyết định phạm vi công khai.
/// </remarks>
public sealed class ReportsService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    ITransactionsService transactions) : IReportsService
{
    public const string OtherCategoryName = "Khoản khác";

    private const int MonthsPerYear = 12;

    private sealed record TypeTotal(TransactionType Type, long Total);

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    public async Task<MonthlyReport> GetMonthlyAsync(Guid fundId, int year, int month, CancellationToken cancellationToken)
    {
        var fullView = await FullViewAsync(fundId, cancellationToken);

        var firstDay = new DateOnly(year, month, 1);
        var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var active = Active(fundId);
        var inMonth = active.Where(transaction => transaction.OccurredOn >= firstDay && transaction.OccurredOn <= lastDay);

        var opening = Net(await TotalsAsync(active.Where(transaction => transaction.OccurredOn < firstDay), cancellationToken));
        var lines = await ByCategoryAsync(inMonth, fullView, cancellationToken);
        var income = lines.Where(line => line.Type == TransactionType.Income).Sum(line => line.Amount);
        var expense = lines.Where(line => line.Type == TransactionType.Expense).Sum(line => line.Amount);

        var listed = await transactions.ListAsync(
            fundId,
            new TransactionFilter(null, null, firstDay, lastDay, null, TransactionStatusFilter.Active, 1, int.MaxValue),
            cancellationToken);

        return new MonthlyReport(
            year,
            month,
            opening,
            income,
            expense,
            income - expense,
            opening + income - expense,
            lines,
            listed.Items);
    }

    public async Task<YearlyReport> GetYearlyAsync(Guid fundId, int year, CancellationToken cancellationToken)
    {
        await FullViewAsync(fundId, cancellationToken);

        var firstDay = new DateOnly(year, 1, 1);
        var lastDay = new DateOnly(year, MonthsPerYear, 31);
        var active = Active(fundId);

        var balance = Net(await TotalsAsync(active.Where(transaction => transaction.OccurredOn < firstDay), cancellationToken));
        var totals = (await db.ToListAsync(
                active
                    .Where(transaction => transaction.OccurredOn >= firstDay && transaction.OccurredOn <= lastDay)
                    .GroupBy(transaction => new { transaction.OccurredOn.Month, transaction.Type })
                    .Select(group => new { group.Key.Month, group.Key.Type, Total = group.Sum(transaction => transaction.Amount) }),
                cancellationToken))
            .ToDictionary(item => (item.Month, item.Type), item => item.Total);

        var months = new List<MonthFigures>(MonthsPerYear);
        for (var month = 1; month <= MonthsPerYear; month++)
        {
            var income = totals.GetValueOrDefault((month, TransactionType.Income));
            var expense = totals.GetValueOrDefault((month, TransactionType.Expense));
            var closing = balance + income - expense;
            months.Add(new MonthFigures(month, balance, income, expense, income - expense, closing));
            balance = closing;
        }

        return new YearlyReport(year, months);
    }

    /// <summary>Thành viên quỹ (không phải → 404); <c>true</c> khi xem bản đầy đủ (<c>Ledger.ViewFull</c>).</summary>
    private async Task<bool> FullViewAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.FundView))
        {
            throw new ForbiddenAccessException();
        }

        return access.Permissions.Contains(Permissions.LedgerViewFull);
    }

    /// <summary>Khoản chưa hủy của quỹ – nguồn của mọi số liệu (BR-02).</summary>
    private IQueryable<FundTransaction> Active(Guid fundId) =>
        db.Query<FundTransaction>().Where(transaction => transaction.FundId == fundId && !transaction.IsCancelled);

    /// <summary>Gộp theo loại trong database; tập rỗng → không có dòng (tổng 0).</summary>
    private async Task<List<TypeTotal>> TotalsAsync(IQueryable<FundTransaction> query, CancellationToken cancellationToken) =>
        await db.ToListAsync(
            query
                .GroupBy(transaction => transaction.Type)
                .Select(group => new TypeTotal(group.Key, group.Sum(transaction => transaction.Amount))),
            cancellationToken);

    private static long Net(List<TypeTotal> totals) =>
        totals.Sum(total => total.Type == TransactionType.Income ? total.Total : -total.Total);

    private async Task<IReadOnlyList<CategoryReportLine>> ByCategoryAsync(
        IQueryable<FundTransaction> inMonth,
        bool fullView,
        CancellationToken cancellationToken)
    {
        var shown = fullView ? inMonth : inMonth.Where(transaction => transaction.IsPublic);
        var rows = await db.ToListAsync(
            from transaction in shown
            join category in db.Query<Category>() on transaction.CategoryId equals category.Id
            group transaction by new { transaction.Type, transaction.CategoryId, category.Name } into line
            select new
            {
                line.Key.Type,
                line.Key.CategoryId,
                line.Key.Name,
                Count = line.Count(),
                Amount = line.Sum(transaction => transaction.Amount),
            },
            cancellationToken);

        var lines = rows
            .Select(row => new CategoryReportLine(row.Type, row.CategoryId, row.Name, row.Count, row.Amount))
            .ToList();

        if (!fullView)
        {
            foreach (var hidden in await TotalsAsync(inMonth.Where(transaction => !transaction.IsPublic), cancellationToken))
            {
                if (hidden.Total > 0)
                {
                    lines.Add(new CategoryReportLine(hidden.Type, null, OtherCategoryName, null, hidden.Total));
                }
            }
        }

        return lines
            .OrderBy(line => line.Type)
            .ThenBy(line => line.CategoryId is null)
            .ThenByDescending(line => line.Amount)
            .ThenBy(line => line.CategoryName, StringComparer.CurrentCulture)
            .ToArray();
    }
}
