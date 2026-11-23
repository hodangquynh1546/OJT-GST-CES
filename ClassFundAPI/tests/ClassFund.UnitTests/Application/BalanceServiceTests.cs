using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Balance;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>M10 (mentor, 2026-11): số dư chỉ vào từ quyết toán / hoàn tiền khi đóng danh sách, chỉ ra khi thủ quỹ rút (BR-25, BR-26).</summary>
public class BalanceServiceTests
{
    private readonly ServiceTestContext _ctx = new();

    private BalanceService Balances => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Clock);

    private WithdrawalsService Withdrawals => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, Balances, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock);

    private long BalanceOf(User user) => _ctx.Db.Query<PersonalBalance>().SingleOrDefault(item => item.UserId == user.Id && item.FundId == _ctx.Fund.Id)?.Balance ?? 0;

    private Task Credit(User user, long amount, BalanceTransactionType type = BalanceTransactionType.SettlementCredit, Guid? reference = null) =>
        Balances.CreditAsync(_ctx.Fund.Id, user.Id, amount, type, reference, _ctx.Owner.Id, default);

    // ---------- Cộng / trừ ----------

    [Fact]
    public async Task Credit_creates_balance_and_history_with_balance_after_BR25()
    {
        await Credit(_ctx.StudentA, 38_000);
        var after = await Balances.CreditAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 25_000, BalanceTransactionType.SettlementCredit, null, _ctx.Owner.Id, default);

        Assert.Equal(63_000, after);
        Assert.Equal(63_000, BalanceOf(_ctx.StudentA));
        Assert.Equal([38_000L, 63_000L], _ctx.Db.Query<BalanceTransaction>().Select(item => item.BalanceAfter));
    }

    [Fact]
    public async Task Debit_never_goes_negative()
    {
        await Credit(_ctx.StudentA, 30_000);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Balances.DebitAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 30_001, BalanceTransactionType.Withdrawal, null, _ctx.Owner.Id, default));

        Assert.Equal(BalanceErrors.Insufficient, error.Code);
        Assert.Equal(30_000, BalanceOf(_ctx.StudentA));
    }

    // ---------- Xem số dư của tôi ----------

    [Fact]
    public async Task A_student_sees_only_their_balance_and_history_with_the_refund_event_named()
    {
        var picnic = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, new DateOnly(2026, 12, 20), _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(picnic);
        await Credit(_ctx.StudentA, 70_000, BalanceTransactionType.ListCloseRefund, picnic.Id);
        await Credit(_ctx.StudentB, 5_000);
        _ctx.ActAs(_ctx.StudentA);

        var mine = await Balances.GetMineAsync(_ctx.Fund.Id, default);

        Assert.Equal(70_000, mine.Balance);
        var row = Assert.Single(mine.History);
        Assert.Equal(("Hoàn tiền đã đóng khi đóng danh sách", FinancialScope.Event, "Dã ngoại Ba Vì"), (row.Description, row.Scope, row.EventName));
    }

    // ---------- Rút số dư (thủ quỹ) ----------

    [Fact]
    public async Task A_treasurer_withdraws_part_of_a_balance_with_history_audit_and_notification()
    {
        await Credit(_ctx.StudentA, 80_000);
        _ctx.ActAs(_ctx.Treasurer);

        var row = await Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 30_000, default);

        Assert.Equal((50_000L, _ctx.StudentA.Id), (row.Balance, row.UserId));
        Assert.Equal(50_000, BalanceOf(_ctx.StudentA));
        var debit = _ctx.Db.Query<BalanceTransaction>().Single(item => item.Type == BalanceTransactionType.Withdrawal);
        Assert.Equal((-30_000L, 50_000L), (debit.Amount, debit.BalanceAfter));
        var record = Assert.Single(_ctx.Db.Query<WithdrawalRequest>());
        Assert.Equal((WithdrawalStatus.Paid, 30_000L, _ctx.Treasurer.Id), (record.Status, record.Amount, record.HandledByUserId));
        Assert.Equal(record.Id, debit.ReferenceId);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == BalanceAuditActions.Withdraw && log.Scope == FinancialScope.System && !log.IsSelfAction);
        Assert.Equal(NotificationType.WithdrawalProcessed, Assert.Single(_ctx.Db.Query<Notification>()).Type);
        Assert.Empty(_ctx.Db.Query<LedgerEntry>());
    }

    [Fact]
    public async Task The_whole_balance_can_be_withdrawn_and_a_treasurers_own_withdrawal_is_self_declared()
    {
        await Credit(_ctx.Treasurer, 38_000);
        _ctx.ActAs(_ctx.Treasurer);

        var row = await Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.Treasurer.Id, 38_000, default);

        Assert.Equal(0, row.Balance);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == BalanceAuditActions.Withdraw && log.IsSelfAction);
    }

    [Theory]
    [InlineData(9_000)]
    [InlineData(10_500)]
    [InlineData(0)]
    public async Task A_withdrawal_must_be_at_least_10k_and_a_multiple_of_1k(long amount)
    {
        await Credit(_ctx.StudentA, 80_000);
        _ctx.ActAs(_ctx.Treasurer);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.StudentA.Id, amount, default));

        Assert.Equal(WithdrawalRule.InvalidAmountCode, error.Code);
        Assert.Equal(80_000, BalanceOf(_ctx.StudentA));
    }

    [Fact]
    public async Task A_withdrawal_cannot_exceed_the_balance_and_changes_nothing_when_refused()
    {
        await Credit(_ctx.StudentA, 20_000);
        _ctx.ActAs(_ctx.Treasurer);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 21_000, default));

        Assert.Equal(BalanceErrors.Insufficient, error.Code);
        Assert.Equal(20_000, BalanceOf(_ctx.StudentA));
        Assert.Empty(_ctx.Db.Query<WithdrawalRequest>());
    }

    [Fact]
    public async Task Students_cannot_withdraw_or_list_balances_and_strangers_are_not_members()
    {
        await Credit(_ctx.StudentA, 80_000);

        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 10_000, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Withdrawals.ListBalancesAsync(_ctx.Fund.Id, null, 1, 20, default));

        _ctx.ActAs(_ctx.Treasurer);
        await Assert.ThrowsAsync<NotFoundException>(() => Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.Outsider.Id, 10_000, default));
    }

    [Fact]
    public async Task Withdrawing_is_still_possible_after_the_year_end_BR37()
    {
        await Credit(_ctx.StudentA, 80_000);
        _ctx.Fund.Status = FundStatus.Archived;
        _ctx.ActAs(_ctx.Treasurer);

        var row = await Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 80_000, default);

        Assert.Equal(0, row.Balance);
    }

    // ---------- Danh sách số dư và lịch sử rút ----------

    [Fact]
    public async Task The_balance_list_shows_every_member_with_the_biggest_balance_first_and_the_held_total()
    {
        await Credit(_ctx.StudentA, 38_000);
        await Credit(_ctx.StudentB, 63_000);
        _ctx.ActAs(_ctx.Treasurer);

        var all = await Withdrawals.ListBalancesAsync(_ctx.Fund.Id, null, 1, 20, default);
        var filtered = await Withdrawals.ListBalancesAsync(_ctx.Fund.Id, "treasurer", 1, 20, default);

        Assert.Equal(4, all.Total);
        Assert.Equal([_ctx.StudentB.Id, _ctx.StudentA.Id], all.Items.Take(2).Select(item => item.UserId));
        Assert.Equal(101_000, all.HeldTotal);
        Assert.Equal(_ctx.Treasurer.Id, Assert.Single(filtered.Items).UserId);
    }

    [Fact]
    public async Task The_withdrawal_history_lists_newest_first_with_who_handled_it()
    {
        await Credit(_ctx.StudentA, 80_000);
        _ctx.ActAs(_ctx.Treasurer);
        await Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 10_000, default);
        _ctx.Clock.Now = ServiceTestContext.Start.AddHours(1);
        await Withdrawals.WithdrawAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 20_000, default);

        var history = await Withdrawals.HistoryAsync(_ctx.Fund.Id, 1, 20, default);

        Assert.Equal([20_000L, 10_000L], history.Items.Select(item => item.Amount));
        Assert.All(history.Items, item => Assert.Equal(_ctx.Treasurer.FullName, item.HandledByName));
    }
}
