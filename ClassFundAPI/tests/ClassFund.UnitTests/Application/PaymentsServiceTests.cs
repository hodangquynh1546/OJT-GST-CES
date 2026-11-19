using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Attachments;
using ClassFund.Application.Features.Payments;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

/// <summary>M6a-T2/T4: PaymentsService (FR-20 → FR-23; BR-10 → BR-12, BR-14).</summary>
public class PaymentsServiceTests
{
    private readonly ServiceTestContext _ctx = new();
    private readonly Charge _charge;

    public PaymentsServiceTests()
    {
        _charge = new Charge(_ctx.Fund.Id, null, "Quỹ lớp tháng 12", "Đóng quỹ", 50_000, new DateOnly(2026, 12, 31), ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(_charge);
        foreach (var user in new[] { _ctx.Owner, _ctx.Treasurer, _ctx.StudentA, _ctx.StudentB })
        {
            _ctx.Db.Seed(new ChargeAssignment(_charge.Id, user.Id, 50_000));
        }

        _ctx.Db.Seed(new Category(_ctx.Fund.Id, "Đóng góp quỹ lớp", LedgerType.Income, true));
        _ctx.ActAs(_ctx.StudentA);
    }

    private PaymentsService CreateService() =>
        new(
            _ctx.Db,
            _ctx.CurrentUser,
            _ctx.Access,
            new AttachmentsService(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Storage, _ctx.Audit, _ctx.Clock),
            _ctx.Audit,
            new Notifier(_ctx.Db, _ctx.Clock),
            _ctx.Clock);

    private ChargeAssignment AssignmentOf(User user) =>
        _ctx.Db.Query<ChargeAssignment>().Single(item => item.ChargeId == _charge.Id && item.UserId == user.Id);

    private Attachment SeedProof(User uploader)
    {
        var proof = new Attachment(_ctx.Fund.Id, AttachmentKind.PaymentProof, "bill.jpg", "image/jpeg", 1000, "k/bill.jpg", uploader.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(proof);
        return proof;
    }

    private static SubmitPaymentInput Cash => new(PaymentMethod.Cash, null, null);

    // ---------- Nộp tiền ----------

    [Fact]
    public async Task Student_cash_payment_waits_for_confirmation_and_writes_no_ledger_entry()
    {
        var payment = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);

        Assert.Equal(PaymentStatus.Submitted, payment.Status);
        Assert.False(payment.IsSelfDeclared);
        Assert.Equal(AssignmentStatus.AwaitingConfirmation, AssignmentOf(_ctx.StudentA).Status);
        Assert.Empty(_ctx.Db.Query<LedgerEntry>());
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Submit && log.ActorUserId == _ctx.StudentA.Id);
    }

    [Fact]
    public async Task Bank_transfer_needs_proof_and_attaches_it_to_the_payment()
    {
        var withoutProof = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.BankTransfer, "FT123", null), default));
        var proof = SeedProof(_ctx.StudentA);

        var payment = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.BankTransfer, " FT123 ", proof.Id), default);

        Assert.Equal(PaymentsErrors.ProofRequiredCode, withoutProof.Code);
        Assert.Equal(payment.Id, proof.OwnerId);
        Assert.Equal("FT123", payment.TransferCode);
    }

    [Fact]
    public async Task Someone_elses_proof_cannot_be_used()
    {
        var proof = SeedProof(_ctx.StudentB);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.BankTransfer, null, proof.Id), default));

        Assert.Equal("attachment.unusable", error.Code);
    }

    [Fact]
    public async Task Paying_from_personal_balance_is_not_available_yet()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.PersonalBalance, null, null), default));

        Assert.Equal(PaymentsErrors.MethodUnavailableCode, error.Code);
    }

    [Fact]
    public async Task A_student_cannot_pay_someone_elses_obligation()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentB).Id, Cash, default));
    }

    [Fact]
    public async Task A_second_submission_for_the_same_obligation_conflicts()
    {
        await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default));

        Assert.Equal(PaymentsErrors.NotPayableCode, error.Code);
    }

    [Fact]
    public async Task A_closed_charge_no_longer_takes_payments()
    {
        _charge.Status = ChargeStatus.Closed;

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default));

        Assert.Equal(PaymentsErrors.ChargeNotOpenCode, error.Code);
    }

    [Fact]
    public async Task An_archived_fund_takes_no_payments()
    {
        _ctx.Fund.Status = FundStatus.Archived;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default));

        Assert.Equal("fund.archived", error.Code);
    }

    [Fact]
    public async Task A_treasurer_paying_for_themselves_is_confirmed_at_once_and_marked_self_declared()
    {
        _ctx.ActAs(_ctx.Treasurer);

        var payment = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.Treasurer).Id, Cash, default);

        Assert.Equal(PaymentStatus.Confirmed, payment.Status);
        Assert.True(payment.IsSelfDeclared);
        Assert.Equal(AssignmentStatus.Paid, AssignmentOf(_ctx.Treasurer).Status);
        var entry = Assert.Single(_ctx.Db.Query<LedgerEntry>());
        Assert.Equal((LedgerType.Income, LedgerSource.Payment, true, 50_000L), (entry.Type, entry.Source, entry.IsSelfDeclared, entry.Amount));
        Assert.Equal(payment.Id, entry.PaymentId);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Confirm && log.IsSelfAction);
        Assert.Empty(_ctx.Db.Query<Notification>());
    }

    [Fact]
    public async Task My_assignments_lists_only_my_own_with_the_latest_payment_state()
    {
        var assignment = AssignmentOf(_ctx.StudentA);
        await CreateService().SubmitAsync(_ctx.Fund.Id, assignment.Id, Cash, default);

        var mine = await CreateService().ListMineAsync(_ctx.Fund.Id, default);

        var item = Assert.Single(mine);
        Assert.Equal((assignment.Id, AssignmentStatus.AwaitingConfirmation, PaymentStatus.Submitted), (item.AssignmentId, item.Status, item.PaymentStatus));
    }

    // ---------- Xác nhận / từ chối ----------

    [Fact]
    public async Task Confirm_writes_one_income_entry_marks_paid_and_notifies_the_payer()
    {
        var submitted = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);
        _ctx.ActAs(_ctx.Treasurer);

        var confirmed = await CreateService().ConfirmAsync(_ctx.Fund.Id, submitted.Id, default);

        Assert.Equal(PaymentStatus.Confirmed, confirmed.Status);
        Assert.False(confirmed.IsSelfDeclared);
        Assert.Equal(AssignmentStatus.Paid, AssignmentOf(_ctx.StudentA).Status);
        var entry = Assert.Single(_ctx.Db.Query<LedgerEntry>());
        Assert.Equal((LedgerSource.Payment, false, _ctx.Treasurer.Id), (entry.Source, entry.IsSelfDeclared, entry.PerformedByUserId));
        Assert.NotNull(entry.CategoryId);
        var note = Assert.Single(_ctx.Db.Query<Notification>(), item => item.Type == NotificationType.PaymentConfirmed);
        Assert.Equal(_ctx.StudentA.Id, note.UserId);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Confirm && !log.IsSelfAction);
    }

    [Fact]
    public async Task Confirming_twice_conflicts_and_writes_only_one_entry()
    {
        var submitted = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);
        _ctx.ActAs(_ctx.Treasurer);
        await CreateService().ConfirmAsync(_ctx.Fund.Id, submitted.Id, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().ConfirmAsync(_ctx.Fund.Id, submitted.Id, default));

        Assert.Equal(PaymentsErrors.AlreadyProcessedCode, error.Code);
        Assert.Single(_ctx.Db.Query<LedgerEntry>());
    }

    [Fact]
    public async Task Students_can_neither_confirm_reject_nor_see_the_queue()
    {
        var submitted = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);
        _ctx.ActAs(_ctx.StudentB);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().ConfirmAsync(_ctx.Fund.Id, submitted.Id, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().RejectAsync(_ctx.Fund.Id, submitted.Id, "x", default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().ListAsync(_ctx.Fund.Id, new PaymentFilter(null, 1, 20), default));
    }

    [Fact]
    public async Task Reject_needs_a_reason_returns_the_obligation_and_allows_paying_again()
    {
        var submitted = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);
        _ctx.ActAs(_ctx.Treasurer);

        var blank = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RejectAsync(_ctx.Fund.Id, submitted.Id, "  ", default));
        var rejected = await CreateService().RejectAsync(_ctx.Fund.Id, submitted.Id, " Chưa nhận được tiền ", default);

        Assert.Equal(PaymentsErrors.ReasonRequiredCode, blank.Code);
        Assert.Equal((PaymentStatus.Rejected, "Chưa nhận được tiền"), (rejected.Status, rejected.RejectionReason));
        Assert.Equal(AssignmentStatus.Unpaid, AssignmentOf(_ctx.StudentA).Status);
        Assert.Empty(_ctx.Db.Query<LedgerEntry>());
        Assert.Contains(_ctx.Db.Query<Notification>(), item => item.Type == NotificationType.PaymentRejected && item.UserId == _ctx.StudentA.Id);

        _ctx.ActAs(_ctx.StudentA);
        var again = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);
        Assert.Equal(PaymentStatus.Submitted, again.Status);
        var mine = Assert.Single(await CreateService().ListMineAsync(_ctx.Fund.Id, default));
        Assert.Equal(again.Id, mine.PaymentId);
    }

    [Fact]
    public async Task The_queue_defaults_to_submitted_and_carries_the_proof()
    {
        var proof = SeedProof(_ctx.StudentA);
        var submitted = await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, new(PaymentMethod.BankTransfer, "FT1", proof.Id), default);
        _ctx.ActAs(_ctx.Treasurer);

        var queue = await CreateService().ListAsync(_ctx.Fund.Id, new PaymentFilter(null, 1, 20), default);
        await CreateService().ConfirmAsync(_ctx.Fund.Id, submitted.Id, default);
        var after = await CreateService().ListAsync(_ctx.Fund.Id, new PaymentFilter(null, 1, 20), default);
        var history = await CreateService().ListAsync(_ctx.Fund.Id, new PaymentFilter(PaymentStatus.Confirmed, 1, 20), default);

        var item = Assert.Single(queue.Items);
        Assert.Equal((_ctx.StudentA.Id, "Quỹ lớp tháng 12"), (item.PayerUserId, item.ChargeTitle));
        Assert.Equal(proof.Id, Assert.Single(item.Attachments).Id);
        Assert.Empty(after.Items);
        Assert.Single(history.Items);
    }

    // ---------- Thủ quỹ ghi nhận tiền mặt thay học sinh ----------

    [Fact]
    public async Task A_treasurer_records_cash_for_a_student_which_is_verified_not_self_declared()
    {
        _ctx.ActAs(_ctx.Treasurer);

        var payment = await CreateService().RecordCashAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, default);

        Assert.Equal((PaymentStatus.Confirmed, PaymentMethod.Cash, false), (payment.Status, payment.Method, payment.IsSelfDeclared));
        Assert.Equal(AssignmentStatus.Paid, AssignmentOf(_ctx.StudentA).Status);
        var entry = Assert.Single(_ctx.Db.Query<LedgerEntry>());
        Assert.Equal((LedgerSource.Payment, false, _ctx.Treasurer.Id), (entry.Source, entry.IsSelfDeclared, entry.PerformedByUserId));
        Assert.Contains(_ctx.Db.Query<Notification>(), item => item.Type == NotificationType.PaymentConfirmed && item.UserId == _ctx.StudentA.Id);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == PaymentsAuditActions.Confirm && !log.IsSelfAction && log.ActorUserId == _ctx.Treasurer.Id);
    }

    [Fact]
    public async Task Recording_cash_for_yourself_is_self_declared_and_students_cannot_record()
    {
        _ctx.ActAs(_ctx.Treasurer);
        var self = await CreateService().RecordCashAsync(_ctx.Fund.Id, AssignmentOf(_ctx.Treasurer).Id, default);
        Assert.True(self.IsSelfDeclared);

        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().RecordCashAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentB).Id, default));
    }

    [Fact]
    public async Task Recording_cash_conflicts_when_already_paid_or_waiting_or_charge_closed()
    {
        await CreateService().SubmitAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, Cash, default);
        _ctx.ActAs(_ctx.Treasurer);

        var waiting = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RecordCashAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentA).Id, default));
        Assert.Equal(PaymentsErrors.NotPayableCode, waiting.Code);

        _charge.Status = ChargeStatus.Closed;
        var closed = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RecordCashAsync(_ctx.Fund.Id, AssignmentOf(_ctx.StudentB).Id, default));
        Assert.Equal(PaymentsErrors.ChargeNotOpenCode, closed.Code);
        Assert.Empty(_ctx.Db.Query<LedgerEntry>());
    }
}
