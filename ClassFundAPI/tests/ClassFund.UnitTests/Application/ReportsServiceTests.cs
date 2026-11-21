using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Events;
using ClassFund.Application.Features.Reports;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

/// <summary>M13-T3: ReportsService (FR-48 → FR-51, BR-02, BR-13).</summary>
public class ReportsServiceTests
{
    private static readonly DateOnly Nov = new(2026, 11, 10);
    private static readonly DateOnly Oct = new(2026, 10, 10);

    private readonly ServiceTestContext _ctx = new();
    private readonly Category _gift;
    private readonly Category _study;
    private readonly Event _picnic;

    public ReportsServiceTests()
    {
        _gift = new Category(_ctx.Fund.Id, "Quà tặng", LedgerType.Expense, isSystem: false);
        _study = new Category(_ctx.Fund.Id, "Học tập", LedgerType.Expense, isSystem: false);
        _picnic = new Event(_ctx.Fund.Id, "Dã ngoại", null, Nov, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(_gift, _study);
        _ctx.Db.Seed(_picnic);
    }

    private ReportsService Service => new(
        _ctx.Db,
        _ctx.CurrentUser,
        _ctx.Access,
        new EventsService(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock));

    private LedgerEntry Entry(LedgerType type, long amount, DateOnly date, Guid? eventId = null, Guid? categoryId = null, bool self = false, Guid? reverses = null, LedgerSource source = LedgerSource.Manual)
    {
        var entry = new LedgerEntry(_ctx.Fund.Id, eventId, type, source, categoryId, "x", null, amount, date, _ctx.Owner.Id, ServiceTestContext.Start)
        {
            IsSelfDeclared = self,
            ReversesEntryId = reverses,
        };
        _ctx.Db.Seed(entry);
        return entry;
    }

    private (Charge Charge, ChargeAssignment A, ChargeAssignment B) GeneralCharge()
    {
        var charge = new Charge(_ctx.Fund.Id, null, "Quỹ lớp HK1", null, 200_000, Nov, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        var a = new ChargeAssignment(charge.Id, _ctx.StudentA.Id, 200_000) { Status = AssignmentStatus.Paid };
        var b = new ChargeAssignment(charge.Id, _ctx.StudentB.Id, 200_000) { Status = AssignmentStatus.Unpaid };
        _ctx.Db.Seed(charge);
        _ctx.Db.Seed(a, b);
        _ctx.Db.Seed(new Payment(a.Id, _ctx.StudentA.Id, 200_000, PaymentMethod.Cash, ServiceTestContext.Start) { Status = PaymentStatus.Confirmed });
        return (charge, a, b);
    }

    [Fact]
    public async Task Overview_general_figures_exclude_events_and_split_self_declared_BR02_BR13()
    {
        Entry(LedgerType.Income, 1_000_000, Nov, source: LedgerSource.Payment);
        Entry(LedgerType.Income, 200_000, Nov, self: true, source: LedgerSource.Payment);
        var wrong = Entry(LedgerType.Expense, 120_000, Nov, categoryId: _gift.Id);
        Entry(LedgerType.Income, 120_000, Nov, categoryId: _gift.Id, reverses: wrong.Id);
        Entry(LedgerType.Expense, 300_000, Nov, categoryId: _study.Id);
        Entry(LedgerType.Income, 9_000_000, Nov, _picnic.Id, source: LedgerSource.Payment); // sự kiện – không tính
        GeneralCharge();

        var overview = await Service.GetOverviewAsync(_ctx.Fund.Id, default);

        Assert.Equal(new GeneralTotalsDto(900_000, 1_200_000, 300_000, 200_000, 2, 1), overview.General);
        Assert.Equal((4, 2), (overview.MemberCount, overview.TreasurerCount));
        Assert.Equal([_ctx.Owner.FullName, _ctx.Treasurer.FullName], overview.TreasurerNames);
        Assert.Equal(1, Assert.Single(overview.OpenCharges).PaidCount);
        Assert.Equal(5, overview.RecentEntries.Count);
        Assert.DoesNotContain(overview.RecentEntries, entry => entry.Amount == 9_000_000);
        Assert.NotNull(overview.Todo);
    }

    [Fact]
    public async Task Student_overview_has_my_part_but_no_recent_entries_or_todo()
    {
        Entry(LedgerType.Income, 1_000_000, Nov, source: LedgerSource.Payment);
        GeneralCharge();
        _ctx.Db.Seed(new PersonalBalance(_ctx.Fund.Id, _ctx.StudentB.Id) { Balance = 38_000 });
        _ctx.ActAs(_ctx.StudentB);

        var overview = await Service.GetOverviewAsync(_ctx.Fund.Id, default);

        Assert.Empty(overview.RecentEntries);
        Assert.Null(overview.Todo);
        Assert.Equal((200_000L, 1, "Quỹ lớp HK1", 38_000L), (overview.Me.DueAmount, overview.Me.DueCount, overview.Me.NearestDueTitle, overview.Me.Balance));
        Assert.Equal(1_000_000, overview.General.Balance);
    }

    [Fact]
    public async Task General_report_by_category_and_month_within_range_FR49()
    {
        Entry(LedgerType.Income, 1_600_000, Oct, source: LedgerSource.Payment);
        Entry(LedgerType.Expense, 480_000, Oct, categoryId: _study.Id);
        Entry(LedgerType.Income, 400_000, Nov, source: LedgerSource.Payment);
        var gift = Entry(LedgerType.Expense, 650_000, Nov, categoryId: _gift.Id);
        var wrong = Entry(LedgerType.Expense, 100_000, Nov, categoryId: _gift.Id);
        Entry(LedgerType.Income, 100_000, Nov, categoryId: _gift.Id, reverses: wrong.Id);
        Entry(LedgerType.Expense, 50_000, Nov, _picnic.Id, _gift.Id);
        _ = gift;

        var report = await Service.GetGeneralAsync(_ctx.Fund.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), default);

        Assert.Equal((400_000L, 650_000L, 870_000L), (report.TotalIncome, report.TotalExpense, report.Balance));
        Assert.Equal(new CategoryAmountDto(_gift.Id, "Quà tặng", 650_000), Assert.Single(report.ExpenseByCategory));
        Assert.Equal(new MonthRowDto(2026, 11, 400_000, 650_000, 870_000), Assert.Single(report.Months));

        var all = await Service.GetGeneralAsync(_ctx.Fund.Id, null, null, default);
        Assert.Equal([1_120_000L, 870_000L], all.Months.Select(month => month.ClosingBalance));
    }

    [Fact]
    public async Task Event_report_rows_use_only_that_event_FR50()
    {
        _ctx.Db.Seed(new EventParticipant(_picnic.Id, _ctx.StudentA.Id) { Response = ParticipationResponse.Accepted });
        var fee = new Charge(_ctx.Fund.Id, _picnic.Id, "Phí", null, 350_000, Nov, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        var assignment = new ChargeAssignment(fee.Id, _ctx.StudentA.Id, 350_000) { Status = AssignmentStatus.Paid };
        _ctx.Db.Seed(fee);
        _ctx.Db.Seed(assignment);
        _ctx.Db.Seed(new Payment(assignment.Id, _ctx.StudentA.Id, 200_000, PaymentMethod.Cash, ServiceTestContext.Start) { Status = PaymentStatus.Confirmed });
        Entry(LedgerType.Income, 200_000, Nov, _picnic.Id, source: LedgerSource.Payment);
        Entry(LedgerType.Expense, 50_000, Nov, _picnic.Id, _gift.Id);
        Entry(LedgerType.Income, 5_000_000, Nov, source: LedgerSource.Payment); // quỹ chung – không tính

        var row = Assert.Single(await Service.GetEventsAsync(_ctx.Fund.Id, default));

        Assert.Equal((1, 350_000L, 200_000L, 150_000L, 50_000L), (row.ParticipantCount, row.AmountDue, row.Collected, row.Uncollected, row.TotalExpense));
        Assert.Null(row.Surplus); // còn mở thì chưa có tiền thừa
    }

    [Fact]
    public async Task Student_report_and_students_see_only_their_own_row_FR51()
    {
        GeneralCharge();
        _ctx.Db.Seed(new PersonalBalance(_ctx.Fund.Id, _ctx.StudentA.Id) { Balance = 13_000 });
        _ctx.Db.Seed(
            new BalanceTransaction(_ctx.Fund.Id, _ctx.StudentA.Id, BalanceTransactionType.SettlementCredit, 38_000, 38_000, null, _ctx.Owner.Id, ServiceTestContext.Start),
            new BalanceTransaction(_ctx.Fund.Id, _ctx.StudentA.Id, BalanceTransactionType.ChargePayment, -25_000, 13_000, null, _ctx.StudentA.Id, ServiceTestContext.Start));

        var rows = await Service.GetStudentsAsync(_ctx.Fund.Id, default);
        Assert.Equal(4, rows.Count);
        var a = rows.Single(row => row.UserId == _ctx.StudentA.Id);
        Assert.Equal((200_000L, 38_000L, 25_000L, 13_000L), (a.PaidGeneral, a.Refunded, a.UsedBalance, a.Balance));
        var b = rows.Single(row => row.UserId == _ctx.StudentB.Id);
        Assert.Equal("Quỹ lớp HK1", Assert.Single(b.UnpaidGeneral).Title);

        _ctx.ActAs(_ctx.StudentB);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service.GetStudentsAsync(_ctx.Fund.Id, default));
        Assert.Equal(_ctx.StudentB.Id, (await Service.GetMineAsync(_ctx.Fund.Id, default)).UserId);
    }

    [Fact]
    public async Task Outsiders_get_404()
    {
        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetOverviewAsync(_ctx.Fund.Id, default));
    }
}
