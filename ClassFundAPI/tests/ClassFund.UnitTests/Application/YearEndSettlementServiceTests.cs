using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Balance;
using ClassFund.Application.Features.Settlements;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>M14-T4: YearEndSettlementService (FR-53, FR-54; BR-34 → BR-37; OQ-13, OQ-19).</summary>
public class YearEndSettlementServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private readonly ServiceTestContext _ctx = new();
    private readonly Charge _charge;

    public YearEndSettlementServiceTests()
    {
        _charge = new Charge(_ctx.Fund.Id, null, "Quỹ lớp HK1", null, 100_000, Day, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(_charge);
        _ctx.ActAs(_ctx.Treasurer);
    }

    private YearEndSettlementService CreateService() =>
        new(
            _ctx.Db,
            _ctx.CurrentUser,
            _ctx.Access,
            new BalanceService(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Audit, _ctx.Clock),
            _ctx.Audit,
            new Notifier(_ctx.Db, _ctx.Clock),
            _ctx.Clock);

    private ChargeAssignment Assign(User user, long paid, PaymentStatus? status = null)
    {
        var assignment = new ChargeAssignment(_charge.Id, user.Id, 100_000) { Status = paid > 0 ? AssignmentStatus.Paid : AssignmentStatus.Unpaid };
        _ctx.Db.Seed(assignment);
        if (paid > 0 || status is not null)
        {
            var payment = new Payment(assignment.Id, user.Id, paid, PaymentMethod.Cash, ServiceTestContext.Start) { Status = status ?? PaymentStatus.Confirmed };
            _ctx.Db.Seed(payment);
            if (payment.Status == PaymentStatus.Confirmed)
            {
                _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, null, LedgerType.Income, LedgerSource.Payment, null, "Thu", null, paid, Day, _ctx.Owner.Id, ServiceTestContext.Start) { PaymentId = payment.Id });
            }
        }

        return assignment;
    }

    private void SpendGeneral(long amount) =>
        _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, null, LedgerType.Expense, LedgerSource.Manual, null, "Chi", null, amount, Day, _ctx.Owner.Id, ServiceTestContext.Start));

    private Event SeedEvent(EventStatus status)
    {
        var fundEvent = new Event(_ctx.Fund.Id, "Dã ngoại", null, Day, _ctx.Owner.Id, ServiceTestContext.Start) { Status = status };
        _ctx.Db.Seed(fundEvent);
        return fundEvent;
    }

    private long BalanceOf(User user) => _ctx.Db.Query<PersonalBalance>().SingleOrDefault(item => item.UserId == user.Id)?.Balance ?? 0;

    /// <summary>A đóng 100.000, B đóng 50.000; chi 90.000 → thừa 60.000 (A 40.000, B 20.000).</summary>
    private void StandardYear()
    {
        Assign(_ctx.StudentA, 100_000);
        Assign(_ctx.StudentB, 50_000);
        Assign(_ctx.Treasurer, 0);
        SpendGeneral(90_000);
    }

    [Fact]
    public async Task Preview_splits_the_surplus_among_every_active_member_in_proportion_and_ignores_event_money()
    {
        StandardYear();
        var fundEvent = SeedEvent(EventStatus.Settled);
        _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, fundEvent.Id, LedgerType.Income, LedgerSource.Payment, null, "Thu sự kiện", null, 500_000, Day, _ctx.Owner.Id, ServiceTestContext.Start));

        var preview = await CreateService().PreviewAsync(_ctx.Fund.Id, default);

        Assert.Equal((150_000L, 90_000L, 60_000L, 4, true), (preview.TotalCollected, preview.TotalSpent, preview.Surplus, preview.MemberCount, preview.CanExecute));
        Assert.Equal(40_000, preview.Lines.Single(line => line.UserId == _ctx.StudentA.Id).Amount);
        Assert.Equal(20_000, preview.Lines.Single(line => line.UserId == _ctx.StudentB.Id).Amount);
        Assert.Equal(0, preview.Lines.Single(line => line.UserId == _ctx.Treasurer.Id).Amount);
        Assert.Equal(60_000, preview.Lines.Sum(line => line.Amount));
        Assert.Equal((1, 1), (preview.OpenChargeCount, preview.UnpaidObligationCount));
    }

    [Fact]
    public async Task Crediting_the_balance_archives_the_fund_closes_charges_and_excludes_the_unpaid()
    {
        StandardYear();

        var result = await CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.PersonalBalance, default);

        Assert.Equal((SettlementOutcome.Surplus, 60_000L), (result.Outcome, result.Surplus));
        Assert.Equal(FundStatus.Archived, _ctx.Fund.Status);
        Assert.Equal(ChargeStatus.Closed, _charge.Status);
        Assert.Equal(AssignmentStatus.Excluded, _ctx.Db.Query<ChargeAssignment>().Single(item => item.UserId == _ctx.Treasurer.Id).Status);
        Assert.Equal(40_000, BalanceOf(_ctx.StudentA));
        Assert.Equal(20_000, BalanceOf(_ctx.StudentB));
        Assert.All(result.Lines, line => Assert.Equal(SettlementLineStatus.CreditedToBalance, line.Status));
        Assert.Equal(4, _ctx.Db.Query<Notification>().Count(item => item.Type == NotificationType.SettlementResult && item.Scope == FinancialScope.General));
        Assert.Equal(2, _ctx.Db.Query<Notification>().Count(item => item.Type == NotificationType.BalanceCredited));
        Assert.Contains(_ctx.AuditLogs, log => log.Action == SettlementAuditActions.YearEndSettle && log.Scope == FinancialScope.General && log.EventId == null);
        Assert.DoesNotContain(_ctx.Db.Query<LedgerEntry>(), entry => entry.Source == LedgerSource.Refund);
    }

    [Fact]
    public async Task Direct_refund_writes_a_general_refund_expense_even_after_the_fund_is_archived()
    {
        Assign(_ctx.StudentA, 100_000);
        Assign(_ctx.StudentB, 100_000);
        SpendGeneral(100_000);
        var result = await CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.DirectRefund, default);
        Assert.Equal(FundStatus.Archived, _ctx.Fund.Status);
        var line = result.Lines.First(item => item.Amount > 0);
        Assert.Equal(SettlementLineStatus.PendingRefund, line.Status);

        var done = await CreateService().ConfirmRefundAsync(_ctx.Fund.Id, line.Id!.Value, default);

        Assert.Equal(SettlementLineStatus.Refunded, done.Status);
        var entry = _ctx.Db.Query<LedgerEntry>().Single(item => item.Source == LedgerSource.Refund);
        Assert.Equal((LedgerType.Expense, 50_000L, (Guid?)null), (entry.Type, entry.Amount, entry.EventId));
        var again = await Assert.ThrowsAsync<ConflictException>(() => CreateService().ConfirmRefundAsync(_ctx.Fund.Id, line.Id!.Value, default));
        Assert.Equal(SettlementErrors.LineProcessedCode, again.Code);
    }

    [Theory]
    [InlineData(EventStatus.Open)]
    [InlineData(EventStatus.Completed)]
    public async Task An_event_that_is_not_settled_or_cancelled_blocks_the_year_end(EventStatus status)
    {
        StandardYear();
        SeedEvent(status);

        var preview = await CreateService().PreviewAsync(_ctx.Fund.Id, default);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.PersonalBalance, default));

        Assert.Equal((1, false), (preview.UnsettledEventCount, preview.CanExecute));
        Assert.Equal(YearEndRule.OpenEventsCode, error.Code);
        Assert.Equal(FundStatus.Active, _ctx.Fund.Status);
    }

    [Theory]
    [InlineData(EventStatus.Settled)]
    [InlineData(EventStatus.Cancelled)]
    public async Task Settled_or_cancelled_events_do_not_block(EventStatus status)
    {
        StandardYear();
        SeedEvent(status);

        await CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.PersonalBalance, default);

        Assert.Equal(FundStatus.Archived, _ctx.Fund.Status);
    }

    [Fact]
    public async Task A_payment_waiting_for_confirmation_blocks_the_year_end()
    {
        StandardYear();
        Assign(_ctx.Owner, 0, PaymentStatus.Submitted);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.PersonalBalance, default));

        Assert.Equal(SettlementErrors.YearEndPendingPaymentsCode, error.Code);
        Assert.Equal(FundStatus.Active, _ctx.Fund.Status);
    }

    [Fact]
    public async Task The_year_end_can_only_be_done_once()
    {
        StandardYear();
        await CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.PersonalBalance, default);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.PersonalBalance, default));

        Assert.Equal(YearEndRule.AlreadyClosedCode, error.Code);
        Assert.Equal(1, _ctx.Db.Query<Settlement>().Count(item => item.EventId == null));
        Assert.Equal(40_000, BalanceOf(_ctx.StudentA));
    }

    [Fact]
    public async Task A_surplus_needs_a_method_and_an_exact_balance_does_not()
    {
        StandardYear();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, null, default));
        Assert.Equal(SettlementErrors.MethodRequiredCode, error.Code);

        SpendGeneral(60_000); // chi hết số thừa
        var result = await CreateService().ExecuteAsync(_ctx.Fund.Id, null, default);

        Assert.Equal((SettlementOutcome.Balanced, FundStatus.Archived), (result.Outcome, _ctx.Fund.Status));
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task Students_cannot_preview_or_execute_and_only_see_their_own_line_afterwards()
    {
        StandardYear();
        await CreateService().ExecuteAsync(_ctx.Fund.Id, SurplusMethod.PersonalBalance, default);

        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().PreviewAsync(_ctx.Fund.Id, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, null, default));

        var mine = await CreateService().GetAsync(_ctx.Fund.Id, default);
        Assert.Equal(_ctx.StudentA.Id, Assert.Single(mine.Lines).UserId);
    }

    [Fact]
    public async Task Get_before_the_year_end_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetAsync(_ctx.Fund.Id, default));
    }
}
