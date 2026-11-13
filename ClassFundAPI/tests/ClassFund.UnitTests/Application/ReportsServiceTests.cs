using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Reports;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

/// <summary>Quy tắc của module Báo cáo (M7b): BR-02, BR-10, BR-15, P1, NFR-08; TD-107, TD-108.</summary>
public class ReportsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly User _teacher;
    private readonly User _student;
    private readonly Fund _fund;
    private readonly Category _dues;
    private readonly Category _otherIncome;
    private readonly Category _stationery;
    private readonly Category _gifts;

    public ReportsServiceTests()
    {
        _teacher = AddUser(UserKind.Teacher, "Cô Hạnh");
        _fund = new Fund(_teacher.Id, "Quỹ lớp 5A", Now.AddDays(-90));
        _db.Add(_fund);
        _dues = AddCategory(TransactionType.Income, "Đóng quỹ");
        _otherIncome = AddCategory(TransactionType.Income, "Thu khác");
        _stationery = AddCategory(TransactionType.Expense, "Văn phòng phẩm");
        _gifts = AddCategory(TransactionType.Expense, "Khen thưởng");
        _student = AddStudent("Lê Gia Bảo");
        SignIn(_teacher);
    }

    [Fact]
    public async Task Monthly_opening_is_everything_before_the_first_day_and_cancelled_transactions_are_ignored()
    {
        Add(_dues, 1_000_000, new DateOnly(2026, 10, 31));
        Add(_stationery, 200_000, new DateOnly(2026, 10, 1));
        Add(_dues, 999_000, new DateOnly(2026, 10, 15), cancelled: true);
        Add(_dues, 300_000, new DateOnly(2026, 11, 1));
        Add(_stationery, 50_000, new DateOnly(2026, 11, 30));
        Add(_stationery, 70_000, new DateOnly(2026, 12, 1));

        var report = await Service().GetMonthlyAsync(_fund.Id, 2026, 11, CancellationToken.None);

        Assert.Equal(800_000, report.OpeningBalance);
        Assert.Equal(300_000, report.TotalIncome);
        Assert.Equal(50_000, report.TotalExpense);
        Assert.Equal(250_000, report.NetChange);
        Assert.Equal(1_050_000, report.ClosingBalance);
        Assert.Equal(2, report.Transactions.Count);
    }

    [Fact]
    public async Task Public_view_merges_hidden_amounts_into_other_lines_without_category_or_count()
    {
        var on = new DateOnly(2026, 11, 5);
        Add(_dues, 3_200_000, on);
        Add(_otherIncome, 300_000, on, isPublic: false);
        Add(_stationery, 180_000, on);
        Add(_stationery, 20_000, on, isPublic: false);
        Add(_gifts, 500_000, on, isPublic: false);

        var full = await Service().GetMonthlyAsync(_fund.Id, 2026, 11, CancellationToken.None);
        SignIn(_student);
        var shown = await Service().GetMonthlyAsync(_fund.Id, 2026, 11, CancellationToken.None);

        Assert.Equal(
            ["Đóng quỹ", "Thu khác", "Khen thưởng", "Văn phòng phẩm"],
            full.ByCategory.Select(line => line.CategoryName));
        Assert.Equal(200_000, full.ByCategory.Single(line => line.CategoryId == _stationery.Id).Amount);
        Assert.Equal(2, full.ByCategory.Single(line => line.CategoryId == _stationery.Id).Count);

        Assert.Equal(
            [
                new CategoryReportLine(TransactionType.Income, _dues.Id, "Đóng quỹ", 1, 3_200_000),
                new CategoryReportLine(TransactionType.Income, null, ReportsService.OtherCategoryName, null, 300_000),
                new CategoryReportLine(TransactionType.Expense, _stationery.Id, "Văn phòng phẩm", 1, 180_000),
                new CategoryReportLine(TransactionType.Expense, null, ReportsService.OtherCategoryName, null, 520_000),
            ],
            shown.ByCategory);
        Assert.Equal(full.TotalIncome, shown.TotalIncome);
        Assert.Equal(full.TotalExpense, shown.TotalExpense);
        Assert.Equal(full.ClosingBalance, shown.ClosingBalance);
        Assert.Equal(2, shown.Transactions.Count);
        Assert.All(shown.Transactions, item => Assert.Null(item.IsPublic));
    }

    [Fact]
    public async Task Public_view_has_no_other_line_when_every_transaction_is_public()
    {
        Add(_dues, 100_000, new DateOnly(2026, 11, 5));
        SignIn(_student);

        var report = await Service().GetMonthlyAsync(_fund.Id, 2026, 11, CancellationToken.None);

        Assert.DoesNotContain(report.ByCategory, line => line.CategoryId is null);
    }

    [Fact]
    public async Task Yearly_has_twelve_months_where_each_opening_equals_previous_closing()
    {
        Add(_dues, 500_000, new DateOnly(2025, 12, 31));
        Add(_dues, 4_800_000, new DateOnly(2026, 9, 5));
        Add(_gifts, 2_100_000, new DateOnly(2026, 9, 6), isPublic: false);
        Add(_stationery, 4_750_000, new DateOnly(2026, 10, 6));
        Add(_dues, 4_500_000, new DateOnly(2026, 10, 2));
        Add(_dues, 10_000, new DateOnly(2027, 1, 1));

        var report = await Service().GetYearlyAsync(_fund.Id, 2026, CancellationToken.None);

        Assert.Equal(2026, report.Year);
        Assert.Equal(Enumerable.Range(1, 12), report.Months.Select(month => month.Month));
        Assert.Equal(500_000, report.Months[0].OpeningBalance);
        Assert.Equal(new MonthFigures(9, 500_000, 4_800_000, 2_100_000, 2_700_000, 3_200_000), report.Months[8]);
        Assert.Equal(new MonthFigures(10, 3_200_000, 4_500_000, 4_750_000, -250_000, 2_950_000), report.Months[9]);
        Assert.Equal(2_950_000, report.Months[11].ClosingBalance);
        for (var index = 1; index < 12; index++)
        {
            Assert.Equal(report.Months[index - 1].ClosingBalance, report.Months[index].OpeningBalance);
        }

        SignIn(_student);
        Assert.Equal(report.Months, (await Service().GetYearlyAsync(_fund.Id, 2026, CancellationToken.None)).Months);
    }

    [Fact]
    public async Task Non_member_gets_not_found()
    {
        SignIn(AddUser(UserKind.Teacher, "Cô Lan"));

        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetMonthlyAsync(_fund.Id, 2026, 11, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetYearlyAsync(_fund.Id, 2026, CancellationToken.None));
    }

    private ReportsService Service()
    {
        var clock = new FixedClock(Now);
        var access = new AccessService(_db);
        var transactions = new TransactionsService(
            _db, access, _currentUser.Object, new AuditTrail(_db, new Mock<IRequestInfo>().Object, clock), clock);
        return new ReportsService(_db, access, _currentUser.Object, transactions);
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    private User AddUser(UserKind kind, string fullName)
    {
        var user = new User(kind, fullName, "hash", kind == UserKind.Teacher ? $"{Guid.NewGuid():N}@example.vn" : null,
            kind == UserKind.Student ? $"hs.{Guid.NewGuid():N}"[..12] : null, false, Now);
        _db.Add(user);
        return user;
    }

    private User AddStudent(string fullName)
    {
        var user = AddUser(UserKind.Student, fullName);
        var student = new Student(_fund.Id, fullName, _teacher.Id, Now);
        student.LinkAccount(user.Id, Now);
        _db.Add(student);
        return user;
    }

    private Category AddCategory(TransactionType type, string name)
    {
        var category = new Category(_fund.Id, type, name, Now);
        _db.Add(category);
        return category;
    }

    private void Add(Category category, long amount, DateOnly on, bool isPublic = true, bool cancelled = false)
    {
        var transaction = new FundTransaction(
            _fund.Id, category.Type, amount, on, category.Id, "Khoản", null, isPublic, _teacher.Id, Guid.NewGuid(), Now);
        if (cancelled)
        {
            transaction.Cancel("Nhập nhầm", _teacher.Id, Now);
        }

        _db.Add(transaction);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
