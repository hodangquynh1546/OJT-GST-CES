using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Balance;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>M10-T4: BalanceService + WithdrawalsService (FR-40 → FR-42, BR-25, BR-26, OQ-11).</summary>
public class BalanceServiceTests
{
    private readonly ServiceTestContext _ctx = new();

    private BalanceService Balances => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Audit, _ctx.Clock);

    private WithdrawalsService Withdrawals => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, Balances, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock);

    private long BalanceOf(User user) => _ctx.Db.Query<PersonalBalance>().SingleOrDefault(item => item.UserId == user.Id && item.FundId == _ctx.Fund.Id)?.Balance ?? 0;

    private Task Credit(User user, long amount) =>
        Balances.CreditAsync(_ctx.Fund.Id, user.Id, amount, BalanceTransactionType.SettlementCredit, null, _ctx.Owner.Id, default);

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
    public async Task Debit_never_goes_negative_and_payments_are_blocked_while_withdrawal_pending()
    {
        await Credit(_ctx.StudentA, 30_000);
        var short_ = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Balances.DebitAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 30_001, BalanceTransactionType.ChargePayment, null, _ctx.StudentA.Id, default));
        Assert.Equal(BalanceErrors.Insufficient, short_.Code);

        _ctx.ActAs(_ctx.StudentA);
        await Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default);
        var locked = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Balances.DebitAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 10_000, BalanceTransactionType.ChargePayment, null, _ctx.StudentA.Id, default));
        Assert.Equal(WithdrawalRule.BalanceLockedCode, locked.Code);
        Assert.Equal(30_000, BalanceOf(_ctx.StudentA));
    }

    [Fact]
    public async Task Withdraw_requests_the_whole_balance_once_OQ11()
    {
        _ctx.ActAs(_ctx.StudentA);
        var empty = await Assert.ThrowsAsync<BusinessRuleException>(() => Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default));
        Assert.Equal(WithdrawalRule.NothingToWithdrawCode, empty.Code);

        await Credit(_ctx.StudentA, 38_000);
        var mine = await Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default);

        Assert.Equal(38_000, mine.Balance); // giữ nguyên tới khi thủ quỹ trả
        Assert.Equal((38_000L, WithdrawalStatus.Requested), (mine.LatestWithdrawal!.Amount, mine.LatestWithdrawal.Status));
        Assert.Single(_ctx.AuditLogs, log => log.Action == BalanceAuditActions.WithdrawalRequest);

        var twice = await Assert.ThrowsAsync<BusinessRuleException>(() => Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default));
        Assert.Equal(WithdrawalRule.PendingExistsCode, twice.Code);
    }

    [Fact]
    public async Task Archived_fund_still_allows_withdrawal_BR37()
    {
        await Credit(_ctx.StudentA, 10_000);
        _ctx.Fund.Status = FundStatus.Archived;
        _ctx.ActAs(_ctx.StudentA);
        var mine = await Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default);
        Assert.Equal(WithdrawalStatus.Requested, mine.LatestWithdrawal!.Status);
    }

    [Fact]
    public async Task Paid_deducts_exactly_the_requested_amount_and_notifies()
    {
        await Credit(_ctx.StudentA, 38_000);
        _ctx.ActAs(_ctx.StudentA);
        var request = (await Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default)).LatestWithdrawal!;
        await Credit(_ctx.StudentA, 5_000); // tiền thừa mới cộng trong lúc chờ không bị rút theo

        _ctx.ActAs(_ctx.Treasurer);
        var paid = await Withdrawals.MarkPaidAsync(_ctx.Fund.Id, request.Id, default);

        Assert.Equal(WithdrawalStatus.Paid, paid.Status);
        Assert.Equal(_ctx.Treasurer.FullName, paid.HandledByName);
        Assert.Equal(5_000, BalanceOf(_ctx.StudentA));
        var withdrawal = _ctx.Db.Query<BalanceTransaction>().Single(item => item.Type == BalanceTransactionType.Withdrawal);
        Assert.Equal((-38_000L, 5_000L, request.Id), (withdrawal.Amount, withdrawal.BalanceAfter, withdrawal.ReferenceId!.Value));
        Assert.Empty(_ctx.Db.Query<LedgerEntry>()); // không ghi sổ thu chi (BR-26)
        Assert.Single(_ctx.Db.Query<Notification>(), item => item.UserId == _ctx.StudentA.Id && item.Type == NotificationType.WithdrawalProcessed);
        Assert.False(Assert.Single(_ctx.AuditLogs, log => log.Action == BalanceAuditActions.WithdrawalPaid).IsSelfAction);

        var again = await Assert.ThrowsAsync<ConflictException>(() => Withdrawals.MarkPaidAsync(_ctx.Fund.Id, request.Id, default));
        Assert.Equal(BalanceErrors.NotPending, again.Code);
    }

    [Fact]
    public async Task Reject_needs_reason_and_reopens_the_balance()
    {
        await Credit(_ctx.StudentA, 38_000);
        _ctx.ActAs(_ctx.StudentA);
        var request = (await Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default)).LatestWithdrawal!;

        _ctx.ActAs(_ctx.Owner);
        var noReason = await Assert.ThrowsAsync<BusinessRuleException>(() => Withdrawals.RejectAsync(_ctx.Fund.Id, request.Id, " ", default));
        Assert.Equal(BalanceErrors.ReasonRequired, noReason.Code);
        var rejected = await Withdrawals.RejectAsync(_ctx.Fund.Id, request.Id, " Đóng Dã ngoại trước ", default);

        Assert.Equal((WithdrawalStatus.Rejected, "Đóng Dã ngoại trước"), (rejected.Status, rejected.RejectionReason));
        Assert.Equal(38_000, BalanceOf(_ctx.StudentA));
        Assert.False(await Balances.HasPendingWithdrawalAsync(_ctx.Fund.Id, _ctx.StudentA.Id, default));

        _ctx.ActAs(_ctx.StudentA);
        var mine = await Balances.GetMineAsync(_ctx.Fund.Id, default);
        Assert.Equal("Đóng Dã ngoại trước", mine.LatestWithdrawal!.RejectionReason);
        Assert.Equal(WithdrawalStatus.Requested, (await Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default)).LatestWithdrawal!.Status);
    }

    [Fact]
    public async Task Treasurer_handling_own_request_is_a_self_action()
    {
        await Credit(_ctx.Treasurer, 20_000);
        _ctx.ActAs(_ctx.Treasurer);
        var request = (await Balances.RequestWithdrawalAsync(_ctx.Fund.Id, default)).LatestWithdrawal!;
        var list = await Withdrawals.ListAsync(_ctx.Fund.Id, WithdrawalStatus.Requested, default);
        Assert.True(Assert.Single(list.Items).IsMine);

        await Withdrawals.MarkPaidAsync(_ctx.Fund.Id, request.Id, default);
        Assert.True(Assert.Single(_ctx.AuditLogs, log => log.Action == BalanceAuditActions.WithdrawalPaid).IsSelfAction);
    }

    [Fact]
    public async Task Students_cannot_handle_requests()
    {
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Withdrawals.ListAsync(_ctx.Fund.Id, null, default));
    }

    [Fact]
    public async Task My_balance_history_describes_each_change_and_lists_other_funds()
    {
        var picnic = new Event(_ctx.Fund.Id, "Tham quan bảo tàng", null, null, _ctx.Owner.Id, ServiceTestContext.Start);
        var settlement = new Settlement(_ctx.Fund.Id, picnic.Id, 30, 6_000_000, 4_860_000, _ctx.Owner.Id, ServiceTestContext.Start);
        var line = new SettlementLine(settlement.Id, _ctx.StudentA.Id, 200_000, 38_000) { Status = SettlementLineStatus.CreditedToBalance };
        var other = new Fund("CLB Tiếng Anh", _ctx.Owner.Id, "2026-2027", ServiceTestContext.Start);
        _ctx.Db.Seed(picnic);
        _ctx.Db.Seed(settlement);
        _ctx.Db.Seed(line);
        _ctx.Db.Seed(other);
        _ctx.Db.Seed(new FundMember(other.Id, _ctx.StudentA.Id, MemberRole.Student, ServiceTestContext.Start));
        await Balances.CreditAsync(_ctx.Fund.Id, _ctx.StudentA.Id, 38_000, BalanceTransactionType.SettlementCredit, line.Id, _ctx.Owner.Id, default);

        _ctx.ActAs(_ctx.StudentA);
        var mine = await Balances.GetMineAsync(_ctx.Fund.Id, default);

        var item = Assert.Single(mine.History);
        Assert.Equal(("Tiền thừa quyết toán", (FinancialScope?)FinancialScope.Event, "Tham quan bảo tàng"), (item.Description, item.Scope, item.EventName));
        Assert.Equal(("CLB Tiếng Anh", 0L), (Assert.Single(mine.OtherFunds).FundName, mine.OtherFunds[0].Balance));
    }
}
