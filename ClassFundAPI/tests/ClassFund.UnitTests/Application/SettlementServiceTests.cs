using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Balance;
using ClassFund.Application.Features.Settlements;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

/// <summary>M9-T2/T4: SettlementService (FR-35 → FR-39; BR-21 → BR-24; OQ-18, OQ-19).</summary>
public class SettlementServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private readonly ServiceTestContext _ctx = new();
    private readonly Event _event;
    private readonly Charge _charge;

    public SettlementServiceTests()
    {
        _event = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, Day, _ctx.Owner.Id, ServiceTestContext.Start) { Status = EventStatus.Completed };
        _ctx.Db.Seed(_event);
        _charge = new Charge(_ctx.Fund.Id, _event.Id, "Tiền xe", null, 100_000, Day, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(_charge);
        _ctx.ActAs(_ctx.Treasurer);
    }

    private SettlementService CreateService() =>
        new(
            _ctx.Db,
            _ctx.CurrentUser,
            _ctx.Access,
            new BalanceService(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Clock),
            _ctx.Audit,
            new Notifier(_ctx.Db, _ctx.Clock),
            _ctx.Clock);

    /// <summary>Người tham gia đã đóng <paramref name="paid"/> (0 = chưa đóng); thu vào sổ sự kiện khi đã đóng.</summary>
    private ChargeAssignment Join(User user, long paid, bool isFinal = true, PaymentStatus? status = null)
    {
        _ctx.Db.Seed(new EventParticipant(_event.Id, user.Id) { Response = ParticipationResponse.Accepted, IsFinal = isFinal });
        var assignment = new ChargeAssignment(_charge.Id, user.Id, 100_000) { Status = paid > 0 ? AssignmentStatus.Paid : AssignmentStatus.Unpaid };
        _ctx.Db.Seed(assignment);
        if (paid > 0 || status is not null)
        {
            var payment = new Payment(assignment.Id, user.Id, paid, PaymentMethod.Cash, ServiceTestContext.Start) { Status = status ?? PaymentStatus.Confirmed };
            _ctx.Db.Seed(payment);
            if (payment.Status == PaymentStatus.Confirmed)
            {
                _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, _event.Id, LedgerType.Income, LedgerSource.Payment, null, "Thu", null, paid, Day, _ctx.Owner.Id, ServiceTestContext.Start) { PaymentId = payment.Id });
            }
        }

        return assignment;
    }

    private void Spend(long amount) =>
        _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, _event.Id, LedgerType.Expense, LedgerSource.Manual, null, "Chi", null, amount, Day, _ctx.Owner.Id, ServiceTestContext.Start));

    private long BalanceOf(User user) => _ctx.Db.Query<PersonalBalance>().SingleOrDefault(item => item.UserId == user.Id)?.Balance ?? 0;

    [Fact]
    public async Task Preview_splits_the_surplus_equally_per_head()
    {
        Join(_ctx.StudentA, 100_000);
        Join(_ctx.StudentB, 100_000);
        Spend(140_000); // thu 200.000, chi 140.000, thừa 60.000

        var preview = await CreateService().PreviewAsync(_ctx.Fund.Id, _event.Id, default);

        Assert.Equal((200_000L, 140_000L, 60_000L, SettlementOutcome.Surplus, true), (preview.TotalCollected, preview.TotalSpent, preview.Surplus, preview.Outcome, preview.CanExecute));
        Assert.All(preview.Lines, line => Assert.Equal(30_000, line.Amount));
    }

    [Fact]
    public async Task The_share_does_not_depend_on_how_much_each_person_paid_and_leftover_below_1000_is_not_split()
    {
        Join(_ctx.StudentA, 100_000);
        Join(_ctx.StudentB, 100_000);
        Join(_ctx.Treasurer, 100_000);
        Spend(199_500); // thừa 100.500 → 100 đơn vị 1.000đ chia 3: 33.000 / 33.000 / 34.000

        var preview = await CreateService().PreviewAsync(_ctx.Fund.Id, _event.Id, default);

        Assert.Equal(100_000, preview.Lines.Sum(line => line.Amount));
        Assert.All(preview.Lines, line => Assert.Equal(0, line.Amount % 1_000));
    }

    [Fact]
    public async Task Crediting_to_balance_adds_each_share_settles_the_event_and_excludes_unpaid()
    {
        Join(_ctx.StudentA, 100_000);
        Join(_ctx.StudentB, 100_000);
        Join(_ctx.Treasurer, 0, isFinal: false); // đã bị loại khi đóng danh sách
        Spend(110_000);

        var result = await CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, SurplusMethod.PersonalBalance, default);

        Assert.Equal((SettlementOutcome.Surplus, 90_000L), (result.Outcome, result.Surplus));
        Assert.Equal(EventStatus.Settled, _event.Status);
        Assert.Equal(45_000, BalanceOf(_ctx.StudentA));
        Assert.Equal(45_000, BalanceOf(_ctx.StudentB));
        Assert.Equal(0, BalanceOf(_ctx.Treasurer));
        Assert.All(result.Lines, line => Assert.Equal(SettlementLineStatus.CreditedToBalance, line.Status));
        Assert.Equal(AssignmentStatus.Excluded, _ctx.Db.Query<ChargeAssignment>().Single(item => item.UserId == _ctx.Treasurer.Id).Status);
        Assert.Equal(ChargeStatus.Closed, _charge.Status);
        Assert.Equal(2, _ctx.Db.Query<BalanceTransaction>().Count(item => item.Type == BalanceTransactionType.SettlementCredit));
        Assert.Equal(2, _ctx.Db.Query<Notification>().Count(item => item.Type == NotificationType.BalanceCredited));
        Assert.Equal(2, _ctx.Db.Query<Notification>().Count(item => item.Type == NotificationType.SettlementResult));
        Assert.Contains(_ctx.AuditLogs, log => log.Action == SettlementAuditActions.Execute && log.Scope == FinancialScope.Event && log.EventId == _event.Id);
        Assert.DoesNotContain(_ctx.Db.Query<LedgerEntry>(), entry => entry.Source == LedgerSource.Refund);
    }

    [Fact]
    public async Task Direct_refund_waits_then_each_confirmation_writes_a_refund_expense()
    {
        Join(_ctx.StudentA, 100_000);
        Join(_ctx.StudentB, 100_000);
        Spend(100_000);

        var result = await CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, SurplusMethod.DirectRefund, default);

        Assert.All(result.Lines, line => Assert.Equal((SettlementLineStatus.PendingRefund, 50_000L), (line.Status, line.Amount)));
        Assert.Equal(0, BalanceOf(_ctx.StudentA));
        Assert.DoesNotContain(_ctx.Db.Query<LedgerEntry>(), entry => entry.Source == LedgerSource.Refund);

        var line = result.Lines.First();
        var done = await CreateService().ConfirmRefundAsync(_ctx.Fund.Id, _event.Id, line.Id!.Value, default);

        Assert.Equal(SettlementLineStatus.Refunded, done.Status);
        var entry = _ctx.Db.Query<LedgerEntry>().Single(item => item.Source == LedgerSource.Refund);
        Assert.Equal((LedgerType.Expense, 50_000L, _event.Id), (entry.Type, entry.Amount, entry.EventId));
        var again = await Assert.ThrowsAsync<ConflictException>(() => CreateService().ConfirmRefundAsync(_ctx.Fund.Id, _event.Id, line.Id!.Value, default));
        Assert.Equal(SettlementErrors.LineProcessedCode, again.Code);
    }

    [Fact]
    public async Task An_event_refund_can_still_be_confirmed_after_the_fund_is_archived_BR37()
    {
        Join(_ctx.StudentA, 100_000);
        Spend(50_000);
        var result = await CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, SurplusMethod.DirectRefund, default);
        _ctx.Fund.Status = FundStatus.Archived;

        var done = await CreateService().ConfirmRefundAsync(_ctx.Fund.Id, _event.Id, result.Lines.Single().Id!.Value, default);

        Assert.Equal(SettlementLineStatus.Refunded, done.Status);
    }

    [Fact]
    public async Task Exactly_balanced_needs_no_method_and_creates_no_lines()
    {
        Join(_ctx.StudentA, 100_000);
        Spend(100_000);

        var result = await CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, null, default);

        Assert.Equal((SettlementOutcome.Balanced, EventStatus.Settled), (result.Outcome, _event.Status));
        Assert.Empty(result.Lines);
        Assert.Empty(_ctx.Db.Query<SettlementLine>());
    }

    [Fact]
    public async Task A_surplus_needs_a_method()
    {
        Join(_ctx.StudentA, 100_000);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, null, default));

        Assert.Equal(SettlementErrors.MethodRequiredCode, error.Code);
        Assert.Equal(EventStatus.Completed, _event.Status);
    }

    [Theory]
    [InlineData(EventStatus.Open)]
    [InlineData(EventStatus.Cancelled)]
    public async Task Only_a_completed_event_can_be_settled(EventStatus status)
    {
        _event.Status = status;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, null, default));

        Assert.Equal(SettlementErrors.NotCompletedCode, error.Code);
    }

    [Fact]
    public async Task Settling_twice_conflicts()
    {
        Join(_ctx.StudentA, 100_000);
        Spend(100_000);
        await CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, null, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, null, default));

        Assert.Equal(SettlementErrors.AlreadySettledCode, error.Code);
    }

    [Fact]
    public async Task Excluded_people_do_not_share_the_surplus_and_nothing_touches_the_general_fund()
    {
        Join(_ctx.StudentA, 100_000);
        Join(_ctx.StudentB, 100_000, isFinal: false);
        Spend(50_000);

        var preview = await CreateService().PreviewAsync(_ctx.Fund.Id, _event.Id, default);

        Assert.Equal(_ctx.StudentA.Id, Assert.Single(preview.Lines).UserId);
        await CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, SurplusMethod.PersonalBalance, default);
        Assert.All(_ctx.Db.Query<LedgerEntry>(), entry => Assert.Equal(_event.Id, entry.EventId));
    }

    [Fact]
    public async Task Students_cannot_execute_and_only_see_their_own_line()
    {
        Join(_ctx.StudentA, 100_000);
        Join(_ctx.StudentB, 100_000);
        Spend(100_000);
        await CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, SurplusMethod.PersonalBalance, default);

        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().PreviewAsync(_ctx.Fund.Id, _event.Id, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().ExecuteAsync(_ctx.Fund.Id, _event.Id, null, default));

        var mine = await CreateService().GetAsync(_ctx.Fund.Id, _event.Id, default);
        Assert.Equal(_ctx.StudentA.Id, Assert.Single(mine.Lines).UserId);

        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAnyAsync<Exception>(() => CreateService().GetAsync(_ctx.Fund.Id, _event.Id, default));
    }
}
