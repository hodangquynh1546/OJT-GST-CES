using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Charges;
using ClassFund.Application.Features.Payments;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>Thu tiền (mentor, 2026-11): học sinh chỉ xem; thủ quỹ tích "đã thu" Tiền mặt / Chuyển khoản, hủy khi tích nhầm, nhắc đóng (BR-11, BR-12).</summary>
public class PaymentsServiceTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly Charge _charge;
    private readonly Category _custom;

    public PaymentsServiceTests()
    {
        _charge = new Charge(_ctx.Fund.Id, null, "Quỹ lớp tháng 12", "Đóng quỹ", 50_000, null, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(_charge);
        foreach (var user in new[] { _ctx.Owner, _ctx.Treasurer, _ctx.StudentA, _ctx.StudentB })
        {
            _ctx.Db.Seed(new ChargeAssignment(_charge.Id, user.Id, 50_000));
        }

        _custom = new Category(_ctx.Fund.Id, "Quỹ vệ sinh", LedgerType.Income, false);
        _ctx.Db.Seed(_custom);
        _charge.CategoryId = _custom.Id;
        _ctx.ActAs(_ctx.Treasurer);
    }

    private PaymentsService CreateService() =>
        new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock);

    private ChargeAssignment AssignmentOf(User user, Charge? charge = null) =>
        _ctx.Db.Query<ChargeAssignment>().Single(item => item.ChargeId == (charge ?? _charge).Id && item.UserId == user.Id);

    private static CollectPaymentInput Cash => new(PaymentMethod.Cash, null);

    private (Event Event, Charge Charge) SeedEventCharge(EventStatus status = EventStatus.Open)
    {
        var fundEvent = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, new DateOnly(2026, 12, 20), _ctx.Owner.Id, ServiceTestContext.Start) { Status = status };
        var charge = new Charge(_ctx.Fund.Id, fundEvent.Id, "Tiền xe", null, 70_000, null, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start) { CategoryId = _custom.Id };
        _ctx.Db.Seed(fundEvent);
        _ctx.Db.Seed(charge);
        _ctx.Db.Seed(new ChargeAssignment(charge.Id, _ctx.StudentA.Id, 70_000));
        return (fundEvent, charge);
    }

    // ---------- Học sinh chỉ xem ----------

    [Fact]
    public async Task A_student_sees_only_their_own_obligations_as_paid_or_unpaid_with_unpaid_first()
    {
        var (_, eventCharge) = SeedEventCharge();
        await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.BankTransfer, "FT123"), default);
        var cancelled = new Charge(_ctx.Fund.Id, null, "Đã hủy", null, 1_000, null, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(cancelled);
        _ctx.Db.Seed(new ChargeAssignment(cancelled.Id, _ctx.StudentA.Id, 1_000) { Status = AssignmentStatus.Cancelled });

        _ctx.ActAs(_ctx.StudentA);
        var mine = await CreateService().ListMineAsync(_ctx.Fund.Id, default);

        Assert.Equal(["Tiền xe", "Quỹ lớp tháng 12"], mine.Select(item => item.Title));
        Assert.Equal((AssignmentStatus.Unpaid, "Dã ngoại Ba Vì"), (mine[0].Status, mine[0].EventName));
        Assert.Equal((AssignmentStatus.Paid, PaymentMethod.BankTransfer), (mine[1].Status, mine[1].Method));
        Assert.Null(mine[1].EventName);
        Assert.DoesNotContain(mine, item => item.Title == "Đã hủy");
        Assert.NotNull(eventCharge);
    }

    [Fact]
    public async Task A_student_cannot_collect_remind_or_uncollect()
    {
        _ctx.ActAs(_ctx.StudentA);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().UncollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, "x", default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().RemindAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentB).Id, default));
    }

    // ---------- Thủ quỹ tích đã thu ----------

    [Fact]
    public async Task Collecting_cash_confirms_at_once_writes_the_income_in_the_charge_scope_and_tells_the_student()
    {
        var payment = await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);

        Assert.Equal((PaymentStatus.Confirmed, PaymentMethod.Cash, 50_000L, false), (payment.Status, payment.Method, payment.Amount, payment.IsSelfDeclared));
        Assert.Equal(AssignmentStatus.Paid, AssignmentOf(_ctx.StudentA).Status);
        var entry = _ctx.Db.Query<LedgerEntry>().Single();
        Assert.Equal((LedgerType.Income, LedgerSource.Payment, 50_000L, _custom.Id, payment.Id), (entry.Type, entry.Source, entry.Amount, entry.CategoryId, entry.PaymentId));
        Assert.Null(entry.EventId);
        Assert.False(entry.IsSelfDeclared);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Collect && log.ActorUserId == _ctx.Treasurer.Id && !log.IsSelfAction);
        var note = Assert.Single(_ctx.Db.Query<Notification>());
        Assert.Equal((_ctx.StudentA.Id, NotificationType.PaymentConfirmed), (note.UserId, note.Type));
    }

    [Fact]
    public async Task Bank_transfer_keeps_the_transfer_code_and_needs_no_proof()
    {
        var payment = await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.BankTransfer, " FT123 "), default);

        Assert.Equal((PaymentMethod.BankTransfer, "FT123"), (payment.Method, payment.TransferCode));
        Assert.Equal("FT123", _ctx.Db.Query<LedgerEntry>().Single().TransferCode);
    }

    [Fact]
    public async Task The_balance_is_not_a_way_to_pay_a_charge()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.PersonalBalance, null), default));

        Assert.Equal(PaymentsErrors.MethodInvalidCode, error.Code);
        Assert.Equal(AssignmentStatus.Unpaid, AssignmentOf(_ctx.StudentA).Status);
    }

    [Fact]
    public async Task A_treasurer_collecting_for_themselves_is_self_declared_BR12()
    {
        var payment = await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.Treasurer).Id, Cash, default);

        Assert.True(payment.IsSelfDeclared);
        Assert.True(_ctx.Db.Query<LedgerEntry>().Single().IsSelfDeclared);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Collect && log.IsSelfAction);
        Assert.Empty(_ctx.Db.Query<Notification>());
    }

    [Fact]
    public async Task Collecting_the_same_obligation_twice_conflicts()
    {
        await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default));

        Assert.Equal(PaymentsErrors.NotPayableCode, error.Code);
        Assert.Single(_ctx.Db.Query<LedgerEntry>());
    }

    [Fact]
    public async Task A_closed_charge_or_an_archived_fund_takes_no_collection()
    {
        _charge.Status = ChargeStatus.Closed;
        var closed = await Assert.ThrowsAsync<ConflictException>(() => CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default));
        Assert.Equal(PaymentsErrors.ChargeNotOpenCode, closed.Code);

        _charge.Status = ChargeStatus.Open;
        _ctx.Fund.Status = FundStatus.Archived;
        var archived = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default));
        Assert.Equal(YearEndRule.FundArchivedCode, archived.Code);
    }

    [Fact]
    public async Task An_event_charge_is_collected_in_the_event_scope_only_while_the_list_is_open()
    {
        var (fundEvent, eventCharge) = SeedEventCharge();
        var assignment = AssignmentOf(_ctx.StudentA, eventCharge);

        await CreateService().CollectAsync(_ctx.Fund.Id, assignment.Id, Cash, default);

        var entry = _ctx.Db.Query<LedgerEntry>().Single();
        Assert.Equal(fundEvent.Id, entry.EventId);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Collect && log.Scope == FinancialScope.Event && log.EventId == fundEvent.Id);

        var (closedEvent, closedCharge) = SeedEventCharge(EventStatus.Completed);
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA, closedCharge).Id, Cash, default));
        Assert.Equal(PaymentsErrors.ChargeNotOpenCode, error.Code);
        Assert.NotNull(closedEvent);
    }

    // ---------- Hủy ghi nhận thu (tích nhầm) ----------

    [Fact]
    public async Task Uncollecting_reverses_the_income_and_lets_the_obligation_be_collected_again()
    {
        var first = await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);

        var undone = await CreateService().UncollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, "Tích nhầm người", default);

        Assert.Equal(PaymentStatus.Rejected, undone.Status);
        Assert.Equal(AssignmentStatus.Unpaid, AssignmentOf(_ctx.StudentA).Status);
        var reversal = _ctx.Db.Query<LedgerEntry>().Single(item => item.ReversesEntryId != null);
        Assert.Equal((LedgerType.Expense, 50_000L, "Tích nhầm người"), (reversal.Type, reversal.Amount, reversal.Note));
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Uncollect);

        var second = await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.BankTransfer, null), default);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(AssignmentStatus.Paid, AssignmentOf(_ctx.StudentA).Status);
    }

    [Fact]
    public async Task Uncollecting_needs_a_reason_and_something_collected()
    {
        var noReason = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().UncollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, " ", default));
        Assert.Equal(PaymentsErrors.ReasonRequiredCode, noReason.Code);

        var notCollected = await Assert.ThrowsAsync<ConflictException>(() => CreateService().UncollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, "lý do", default));
        Assert.Equal(PaymentsErrors.NotCollectedCode, notCollected.Code);
    }

    // ---------- Nhắc đóng ----------

    [Fact]
    public async Task Reminding_a_student_notifies_only_them_logs_the_reminder_and_is_throttled()
    {
        var result = await CreateService().RemindAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentB).Id, default);

        Assert.Equal(1, result.Reminded);
        var note = Assert.Single(_ctx.Db.Query<Notification>());
        Assert.Equal((_ctx.StudentB.Id, NotificationType.ChargeDueSoon), (note.UserId, note.Type));
        Assert.StartsWith("[Quỹ chung] Nhắc đóng: Quỹ lớp tháng 12", note.Title);
        var log = Assert.Single(_ctx.Db.Query<PaymentReminder>());
        Assert.Equal((_ctx.StudentB.Id, ReminderKind.Manual, _ctx.Treasurer.Id, _charge.Id), (log.UserId, log.Kind, log.SentByUserId, log.ChargeId));
        Assert.Contains(_ctx.AuditLogs, entry => entry.Action == PaymentsAuditActions.Remind);

        var tooSoon = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RemindAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentB).Id, default));
        Assert.Equal(ChargesErrors.RemindTooSoonCode, tooSoon.Code);

        _ctx.Clock.Now = ServiceTestContext.Start.AddHours(2);
        await CreateService().RemindAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentB).Id, default);
        Assert.Equal(2, _ctx.Db.Query<PaymentReminder>().Count());
    }

    [Fact]
    public async Task Reminding_an_event_participant_uses_the_event_scope_and_a_paid_person_needs_no_reminder()
    {
        var (fundEvent, eventCharge) = SeedEventCharge();

        await CreateService().RemindAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA, eventCharge).Id, default);

        var note = Assert.Single(_ctx.Db.Query<Notification>());
        Assert.Equal((NotificationType.EventUnpaidReminder, FinancialScope.Event, fundEvent.Id), (note.Type, note.Scope, note.EventId!.Value));
        Assert.StartsWith("[Sự kiện: Dã ngoại Ba Vì]", note.Title);

        await CreateService().CollectAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);
        var paid = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RemindAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, default));
        Assert.Equal(ChargesErrors.NothingToRemindCode, paid.Code);
    }
}
