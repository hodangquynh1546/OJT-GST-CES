using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Features.Reports;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;

namespace SchoolFund.UnitTests.Application;

/// <summary>Công thức số dư và dòng "Khoản khác" (BR-02, BR-09, BR-10, BR-15, P1, NFR-08).</summary>
public class ReportServiceTests
{
    private readonly ReportFixture _world = new();

    private static User Viewer(ReportFixture world, string viewer) =>
        viewer == nameof(ReportFixture.Parent) ? world.Parent : world.Student;

    public static TheoryData<string> PublicViewers => new() { nameof(ReportFixture.Parent), nameof(ReportFixture.Student) };

    private static CategoryReportLine OtherLine(MonthlyReport report, TransactionType type) =>
        Assert.Single(report.ByCategory, line => line.Type == type && line.CategoryId is null);

    [Fact]
    public async Task Monthly_report_computes_opening_and_closing_balance()
    {
        _world.Add(_world.Dues, 500_000, "2026-09-30");
        _world.Add(_world.Supplies, 100_000, "2026-09-15");
        _world.Add(_world.Dues, 200_000, "2026-10-01");
        _world.Add(_world.Supplies, 50_000, "2026-10-31");
        _world.Add(_world.Dues, 999_000, "2026-11-01");

        var report = await _world.MonthlyAsync(2026, 10);

        Assert.Equal(400_000, report.OpeningBalance);
        Assert.Equal(200_000, report.TotalIncome);
        Assert.Equal(50_000, report.TotalExpense);
        Assert.Equal(150_000, report.NetChange);
        Assert.Equal(550_000, report.ClosingBalance);
        Assert.Equal(2, report.Transactions.Count);
    }

    [Fact]
    public async Task Opening_balance_equals_closing_balance_of_the_previous_month_across_a_year_end()
    {
        _world.Add(_world.Dues, 700_000, "2025-11-10");
        _world.Add(_world.Supplies, 900_000, "2025-12-31");
        _world.Add(_world.Dues, 300_000, "2026-01-01");
        _world.Add(_world.Supplies, 10_000, "2026-02-28");

        var december = await _world.MonthlyAsync(2025, 12);
        var january = await _world.MonthlyAsync(2026, 1);
        var february = await _world.MonthlyAsync(2026, 2);
        var march = await _world.MonthlyAsync(2026, 3);

        Assert.Equal(-200_000, december.ClosingBalance);
        Assert.Equal(december.ClosingBalance, january.OpeningBalance);
        Assert.Equal(january.ClosingBalance, february.OpeningBalance);
        Assert.Equal(february.ClosingBalance, march.OpeningBalance);
        Assert.Equal(march.OpeningBalance, march.ClosingBalance);
    }

    [Fact]
    public async Task Cancelling_an_old_transaction_moves_every_later_month_and_keeps_the_chain()
    {
        var old = _world.Add(_world.Dues, 300_000, "2026-08-10");
        _world.Add(_world.Dues, 100_000, "2026-09-10");
        _world.Add(_world.Supplies, 40_000, "2026-10-10");

        Assert.Equal(400_000, (await _world.MonthlyAsync(2026, 10)).OpeningBalance);

        _world.Cancel(old);

        var august = await _world.MonthlyAsync(2026, 8);
        var september = await _world.MonthlyAsync(2026, 9);
        var october = await _world.MonthlyAsync(2026, 10);
        var overview = await _world.OverviewAsync();

        Assert.Equal(0, august.TotalIncome);
        Assert.Empty(august.Transactions);
        Assert.Empty(august.ByCategory);
        Assert.Equal(august.ClosingBalance, september.OpeningBalance);
        Assert.Equal(100_000, october.OpeningBalance);
        Assert.Equal(september.ClosingBalance, october.OpeningBalance);
        Assert.Equal(60_000, october.ClosingBalance);
        Assert.Equal(october.ClosingBalance, overview.CurrentBalance);
    }

    [Fact]
    public async Task Moving_a_transaction_to_another_month_keeps_the_chain()
    {
        var moved = _world.Add(_world.Dues, 300_000, "2026-10-10");

        moved.Update(300_000, new DateOnly(2026, 8, 10), _world.Dues.Id, "Nội dung", null, true, _world.Treasurer.Id, ReportFixture.Now);

        var august = await _world.MonthlyAsync(2026, 8);
        var september = await _world.MonthlyAsync(2026, 9);
        var october = await _world.MonthlyAsync(2026, 10);

        Assert.Equal(300_000, august.ClosingBalance);
        Assert.Equal(august.ClosingBalance, september.OpeningBalance);
        Assert.Equal(september.ClosingBalance, october.OpeningBalance);
        Assert.Equal(0, october.TotalIncome);
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_viewer_sees_hidden_part_as_one_other_line_per_type(string viewer)
    {
        _world.Add(_world.Dues, 100_000, "2026-10-05");
        _world.Add(_world.Dues, 20_000, "2026-10-06", isPublic: false);
        _world.Add(_world.Sponsor, 30_000, "2026-10-07", isPublic: false);
        _world.Add(_world.Supplies, 15_000, "2026-10-08");
        _world.Add(_world.Rewards, 5_000, "2026-10-09", isPublic: false, description: "Hoa và phong bì");
        _world.Add(_world.Rewards, 7_000, "2026-10-10", isPublic: false);

        var full = await _world.MonthlyAsync(2026, 10);
        _world.SignIn(Viewer(_world, viewer));
        var report = await _world.MonthlyAsync(2026, 10);

        var income = OtherLine(report, TransactionType.Income);
        Assert.Equal(ReportService.OtherCategoryName, income.CategoryName);
        Assert.Null(income.Count);
        Assert.Equal(50_000, income.Amount);

        var expense = OtherLine(report, TransactionType.Expense);
        Assert.Null(expense.Count);
        Assert.Equal(12_000, expense.Amount);

        // Danh mục có cả hai loại khoản: chỉ phần công khai được tính vào dòng của danh mục.
        var dues = Assert.Single(report.ByCategory, line => line.CategoryId == _world.Dues.Id);
        Assert.Equal(100_000, dues.Amount);
        Assert.Equal(1, dues.Count);

        Assert.DoesNotContain(report.ByCategory, line => line.CategoryId == _world.Rewards.Id || line.CategoryId == _world.Sponsor.Id);
        Assert.DoesNotContain(report.ByCategory, line => line.CategoryName is "Khen thưởng" or "Tài trợ");
        Assert.Equal(2, report.Transactions.Count);
        Assert.All(report.Transactions, transaction => Assert.Null(transaction.Note));
        Assert.DoesNotContain(report.Transactions, transaction => transaction.Description == "Hoa và phong bì");

        Assert.Equal(full.TotalIncome, report.TotalIncome);
        Assert.Equal(full.TotalExpense, report.TotalExpense);
        Assert.Equal(full.ClosingBalance, report.ClosingBalance);
        Assert.Equal(
            report.TotalIncome,
            report.ByCategory.Where(line => line.Type == TransactionType.Income).Sum(line => line.Amount));
        Assert.Equal(
            report.TotalExpense,
            report.ByCategory.Where(line => line.Type == TransactionType.Expense).Sum(line => line.Amount));
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_viewer_has_no_other_line_when_nothing_is_hidden_in_that_type(string viewer)
    {
        _world.Add(_world.Dues, 100_000, "2026-10-05");
        _world.Add(_world.Rewards, 5_000, "2026-10-09", isPublic: false);
        var cancelled = _world.Add(_world.Sponsor, 8_000, "2026-10-09", isPublic: false);
        _world.Cancel(cancelled);

        _world.SignIn(Viewer(_world, viewer));
        var report = await _world.MonthlyAsync(2026, 10);

        Assert.DoesNotContain(report.ByCategory, line => line.Type == TransactionType.Income && line.CategoryId is null);
        Assert.Equal(5_000, OtherLine(report, TransactionType.Expense).Amount);
    }

    [Fact]
    public async Task Public_viewer_does_not_see_cancelled_public_transactions_in_the_report()
    {
        _world.Cancel(_world.Add(_world.Dues, 100_000, "2026-10-05"));

        _world.SignIn(_world.Parent);
        var report = await _world.MonthlyAsync(2026, 10);

        Assert.Empty(report.Transactions);
        Assert.Empty(report.ByCategory);
        Assert.Equal(0, report.TotalIncome);
    }

    [Fact]
    public async Task Full_viewer_sees_real_categories_with_counts_and_no_other_line()
    {
        _world.Add(_world.Dues, 100_000, "2026-10-05");
        _world.Add(_world.Dues, 20_000, "2026-10-06", isPublic: false);
        _world.Add(_world.Rewards, 5_000, "2026-10-09", isPublic: false);

        _world.SignIn(_world.Teacher);
        var report = await _world.MonthlyAsync(2026, 10);

        Assert.Equal(2, report.ByCategory.Count);
        Assert.Equal(2, report.ByCategory.Single(line => line.CategoryId == _world.Dues.Id).Count);
        Assert.Equal(120_000, report.ByCategory.Single(line => line.CategoryId == _world.Dues.Id).Amount);
        Assert.Equal("Khen thưởng", report.ByCategory.Single(line => line.Type == TransactionType.Expense).CategoryName);
        Assert.Equal(3, report.Transactions.Count);
    }

    [Fact]
    public async Task By_category_lists_income_first_and_other_line_last_of_its_type()
    {
        _world.Add(_world.Supplies, 900_000, "2026-10-05");
        _world.Add(_world.Dues, 1_000, "2026-10-05");
        _world.Add(_world.Sponsor, 5_000, "2026-10-05");
        _world.Add(_world.Dues, 100_000, "2026-10-05", isPublic: false);

        _world.SignIn(_world.Parent);
        var report = await _world.MonthlyAsync(2026, 10);

        Assert.Equal(
            ["Tài trợ", "Đóng quỹ", ReportService.OtherCategoryName, "Văn phòng phẩm"],
            report.ByCategory.Select(line => line.CategoryName));
    }

    [Fact]
    public async Task Report_only_counts_the_requested_fund()
    {
        var other = _world.AddOtherFund();
        _world.Add(_world.Dues, 100_000, "2026-09-05", fund: other);
        _world.Add(_world.Dues, 100_000, "2026-10-05", fund: other);

        var report = await _world.MonthlyAsync(2026, 10);

        Assert.Equal(0, report.OpeningBalance);
        Assert.Equal(0, report.TotalIncome);
        Assert.Empty(report.Transactions);
    }

    [Fact]
    public async Task Month_without_transactions_returns_zero_figures()
    {
        var report = await _world.MonthlyAsync(2020, 2);

        Assert.Equal(0, report.ClosingBalance);
        Assert.Empty(report.ByCategory);
        Assert.Empty(report.Transactions);
    }

    [Fact]
    public async Task Leap_day_belongs_to_february()
    {
        _world.Add(_world.Dues, 100_000, "2024-02-29");

        Assert.Equal(100_000, (await _world.MonthlyAsync(2024, 2)).TotalIncome);
        Assert.Equal(100_000, (await _world.MonthlyAsync(2024, 3)).OpeningBalance);
    }

    [Fact]
    public async Task Yearly_report_has_twelve_chained_months_that_match_the_monthly_report()
    {
        _world.Add(_world.Dues, 500_000, "2025-12-31");
        _world.Add(_world.Dues, 200_000, "2026-03-15");
        _world.Add(_world.Supplies, 800_000, "2026-03-16");
        _world.Add(_world.Dues, 100_000, "2026-11-30", isPublic: false);
        _world.Add(_world.Dues, 50_000, "2027-01-01");
        _world.Cancel(_world.Add(_world.Dues, 999_000, "2026-05-01"));

        var yearly = await _world.YearlyAsync(2026);

        Assert.Equal(2026, yearly.Year);
        Assert.Equal(Enumerable.Range(1, 12), yearly.Months.Select(month => month.Month));
        Assert.Equal(500_000, yearly.Months[0].OpeningBalance);
        Assert.Equal(-600_000, yearly.Months[2].NetChange);
        Assert.Equal(0, yearly.Months[4].TotalIncome);
        Assert.Equal(0, yearly.Months[11].ClosingBalance);

        for (var index = 1; index < 12; index++)
        {
            Assert.Equal(yearly.Months[index - 1].ClosingBalance, yearly.Months[index].OpeningBalance);
        }

        var march = await _world.MonthlyAsync(2026, 3);
        Assert.Equal(march.OpeningBalance, yearly.Months[2].OpeningBalance);
        Assert.Equal(march.ClosingBalance, yearly.Months[2].ClosingBalance);
    }

    [Fact]
    public async Task Yearly_report_is_the_same_for_every_role()
    {
        _world.Add(_world.Dues, 500_000, "2026-03-15", isPublic: false);
        _world.Add(_world.Supplies, 80_000, "2026-03-16");

        var full = await _world.YearlyAsync(2026);
        _world.SignIn(_world.Parent);
        var publicView = await _world.YearlyAsync(2026);

        Assert.Equal(full.Months, publicView.Months);
    }

    [Fact]
    public async Task Outsider_gets_not_found()
    {
        _world.SignIn(_world.Outsider);

        await Assert.ThrowsAsync<NotFoundException>(() => _world.MonthlyAsync(2026, 10));
        await Assert.ThrowsAsync<NotFoundException>(() => _world.YearlyAsync(2026));
    }
}
