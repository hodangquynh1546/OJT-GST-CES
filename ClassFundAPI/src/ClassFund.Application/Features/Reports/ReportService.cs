using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Reports;

/// <summary>Báo cáo tháng, thống kê 12 tháng (FR-08, FR-09; BR-02, BR-09, BR-10, BR-15, P1; NFR-08).</summary>
public sealed class ReportService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    TransactionService transactions)
{
    public const string OtherCategoryName = "Khoản khác";

    private const int MonthsPerYear = 12;

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    /// <exception cref="Common.Exceptions.NotFoundException">Không thuộc quỹ.</exception>
    public async Task<MonthlyReport> GetMonthlyAsync(Guid fundId, int year, int month, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        var fullView = access.Permissions.Contains(Permissions.LedgerViewFull);

        var firstDay = new DateOnly(year, month, 1);
        var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var active = db.ActiveTransactions(fundId);
        var inMonth = active.Where(transaction => transaction.OccurredOn >= firstDay && transaction.OccurredOn <= lastDay);

        var opening = (await db.TotalsAsync(active.Where(transaction => transaction.OccurredOn < firstDay), cancellationToken)).Net;
        var lines = await ByCategoryAsync(inMonth, fullView, cancellationToken);

        // Số tổng cộng từ chính các dòng thống kê nên tổng các dòng luôn khớp số tổng (BR-15).
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

    /// <exception cref="Common.Exceptions.NotFoundException">Không thuộc quỹ.</exception>
    public async Task<YearlyReport> GetYearlyAsync(Guid fundId, int year, CancellationToken cancellationToken)
    {
        await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);

        var firstDay = new DateOnly(year, 1, 1);
        var lastDay = new DateOnly(year, MonthsPerYear, 31);
        var active = db.ActiveTransactions(fundId);

        var balance = (await db.TotalsAsync(active.Where(transaction => transaction.OccurredOn < firstDay), cancellationToken)).Net;
        var totals = (await db.ToListAsync(
                active
                    .Where(transaction => transaction.OccurredOn >= firstDay && transaction.OccurredOn <= lastDay)
                    .GroupBy(transaction => new { transaction.OccurredOn.Month, transaction.Type })
                    .Select(group => new
                    {
                        group.Key.Month,
                        group.Key.Type,
                        Total = group.Sum(transaction => transaction.Amount),
                    }),
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

    /// <summary>
    /// Bản công khai chỉ gộp khoản công khai theo danh mục; khoản không công khai chỉ được cộng theo loại ngay trong
    /// database, nên danh mục và số lượng của chúng không rời khỏi database (NFR-08).
    /// </summary>
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
            var hidden = await db.TotalsAsync(inMonth.Where(transaction => !transaction.IsPublic), cancellationToken);
            AddOtherLine(lines, TransactionType.Income, hidden.Income);
            AddOtherLine(lines, TransactionType.Expense, hidden.Expense);
        }

        return lines
            .OrderBy(line => line.Type)
            .ThenBy(line => line.CategoryId is null)
            .ThenByDescending(line => line.Amount)
            .ThenBy(line => line.CategoryName, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static void AddOtherLine(List<CategoryReportLine> lines, TransactionType type, long amount)
    {
        if (amount > 0)
        {
            lines.Add(new CategoryReportLine(type, null, OtherCategoryName, null, amount));
        }
    }
}
