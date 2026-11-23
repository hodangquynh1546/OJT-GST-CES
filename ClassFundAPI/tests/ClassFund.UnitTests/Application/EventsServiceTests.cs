using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Balance;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>M8: EventsService + EventInvitationService (FR-30 → FR-34, BR-16 → BR-20); luồng mentor 2026-11: Đang mở → Đóng danh sách → Quyết toán.</summary>
public class EventsServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    /// <summary>Hạn đóng danh sách mặc định của test: sau "hôm nay" (18/11) nên đóng ngay là đóng sớm.</summary>
    private static readonly DateOnly Deadline = new(2026, 11, 25);

    private readonly ServiceTestContext _ctx = new();

    private BalanceService Balances => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Clock);

    private EventsService Service => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, Balances, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock);

    private EventInvitationService Invitations => new(_ctx.Db, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock));

    private IEnumerable<Notification> Notifications => _ctx.Db.Query<Notification>();

    private Task<EventDetailDto> CreatePicnic() =>
        Service.CreateAsync(_ctx.Fund.Id, new CreateEventInput("  Dã ngoại Ba Vì ", " Xe 45 chỗ ", Day, Deadline), default);

    private EventParticipant Participant(Guid eventId, User user) =>
        _ctx.Db.Query<EventParticipant>().Single(item => item.EventId == eventId && item.UserId == user.Id);

    private void Accept(Guid eventId, params User[] users)
    {
        foreach (var user in users)
        {
            Participant(eventId, user).Response = ParticipationResponse.Accepted;
        }
    }

    private Charge SeedCharge(Guid eventId, long amount, string title = "Phí dã ngoại")
    {
        var charge = new Charge(_ctx.Fund.Id, eventId, title, null, amount, Day, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(charge);
        return charge;
    }

    private ChargeAssignment Assign(Charge charge, User user, AssignmentStatus status = AssignmentStatus.Unpaid)
    {
        var assignment = new ChargeAssignment(charge.Id, user.Id, charge.AmountPerPerson) { Status = status };
        _ctx.Db.Seed(assignment);
        return assignment;
    }

    private void Pay(ChargeAssignment assignment, PaymentStatus status, ConfirmationMode? mode = null)
    {
        _ctx.Db.Seed(new Payment(assignment.Id, assignment.UserId, assignment.AmountDue, PaymentMethod.Cash, ServiceTestContext.Start)
        {
            Status = status,
            ConfirmationMode = mode ?? (status == PaymentStatus.Confirmed ? ConfirmationMode.Verified : null),
        });
        assignment.Status = status == PaymentStatus.Confirmed ? AssignmentStatus.Paid : assignment.Status;
        if (status == PaymentStatus.Confirmed)
        {
            _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, _ctx.Db.Query<Charge>().Single(item => item.Id == assignment.ChargeId).EventId, LedgerType.Income, LedgerSource.Payment, null, "Thu", null, assignment.AmountDue, Day, _ctx.Owner.Id, ServiceTestContext.Start));
        }
    }

    [Fact]
    public async Task Create_invites_every_active_member_and_notifies_others_BR17()
    {
        _ctx.Db.Query<FundMember>().Single(member => member.UserId == _ctx.StudentB.Id).Status = MemberStatus.Removed;

        var detail = await CreatePicnic();

        Assert.Equal("Dã ngoại Ba Vì", detail.Name);
        Assert.Equal("Xe 45 chỗ", detail.Description);
        Assert.Equal(EventStatus.Open, detail.Status);
        Assert.Equal(Deadline, detail.ListDeadline);
        Assert.Equal(3, detail.InvitedCount);
        Assert.Equal(3, detail.PendingCount);
        Assert.Equal(ParticipationResponse.Pending, detail.Me!.Response);

        var notified = Notifications.Select(item => item.UserId).Order().ToArray();
        Assert.Equal(new[] { _ctx.Treasurer.Id, _ctx.StudentA.Id }.Order(), notified);
        Assert.All(Notifications, item =>
        {
            Assert.Equal(NotificationType.EventCreated, item.Type);
            Assert.Equal(FinancialScope.Event, item.Scope);
            Assert.StartsWith("[Sự kiện: Dã ngoại Ba Vì]", item.Title);
        });

        var log = Assert.Single(_ctx.AuditLogs, item => item.Action == EventsAuditActions.Create);
        Assert.Equal(FinancialScope.Event, log.Scope);
        Assert.Equal(detail.Id, log.EventId);
    }

    [Fact]
    public async Task Students_cannot_manage_events()
    {
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(CreatePicnic);
    }

    [Fact]
    public async Task Students_see_only_events_they_were_invited_to()
    {
        var picnic = await CreatePicnic();
        var late = new User("late@x.com", "late", "h", UserOrigin.Provisioned, false, ServiceTestContext.Start);
        _ctx.Db.Seed(late);
        _ctx.Db.Seed(new FundMember(_ctx.Fund.Id, late.Id, MemberRole.Student, ServiceTestContext.Start));

        _ctx.ActAs(_ctx.StudentA);
        Assert.Equal(picnic.Id, Assert.Single(await Service.ListAsync(_ctx.Fund.Id, default)).Id);

        _ctx.ActAs(late);
        Assert.Empty(await Service.ListAsync(_ctx.Fund.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetAsync(_ctx.Fund.Id, picnic.Id, default));

        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.ListAsync(_ctx.Fund.Id, default));
    }

    [Fact]
    public async Task Respond_only_while_open_FR31()
    {
        var picnic = await CreatePicnic();

        _ctx.ActAs(_ctx.StudentA);
        var accepted = await Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Accepted, default);
        Assert.Equal(ParticipationResponse.Accepted, accepted.Me!.Response);
        Assert.Equal(1, accepted.AcceptedCount);
        Assert.Single(_ctx.AuditLogs, item => item.Action == EventsAuditActions.Respond);

        // Thủ quỹ tự trả lời, không cần ai duyệt.
        _ctx.ActAs(_ctx.Treasurer);
        Assert.Equal(2, (await Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Accepted, default)).AcceptedCount);

        _ctx.Db.Query<Event>().Single().Status = EventStatus.Completed;
        _ctx.ActAs(_ctx.StudentA);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Declined, default));
        Assert.Equal(EventsErrors.NotOpen, error.Code);
    }

    [Fact]
    public async Task Accepting_late_takes_on_open_charges_and_declining_drops_unpaid_ones_BR18()
    {
        var picnic = await CreatePicnic();
        var fee = SeedCharge(picnic.Id, 350_000);
        _ctx.ActAs(_ctx.StudentA);

        await Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Accepted, default);
        var assignment = Assert.Single(_ctx.Db.Query<ChargeAssignment>());
        Assert.Equal((fee.Id, _ctx.StudentA.Id, 350_000L, AssignmentStatus.Unpaid), (assignment.ChargeId, assignment.UserId, assignment.AmountDue, assignment.Status));

        var detail = await Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Declined, default);
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
        Assert.Empty(detail.MyCharges);

        await Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Accepted, default);
        Assert.Equal(AssignmentStatus.Unpaid, Assert.Single(_ctx.Db.Query<ChargeAssignment>()).Status);
    }

    [Fact]
    public async Task Cannot_decline_after_paying()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.StudentA);
        Pay(Assign(SeedCharge(picnic.Id, 350_000), _ctx.StudentA), PaymentStatus.Confirmed);

        _ctx.ActAs(_ctx.StudentA);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Declined, default));
        Assert.Equal(EventsErrors.ResponseLocked, error.Code);
    }

    [Fact]
    public async Task Participants_are_accepted_people_with_payment_status_FR32()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.Owner, _ctx.Treasurer, _ctx.StudentA); // StudentB chưa trả lời → không hiện
        var fee = SeedCharge(picnic.Id, 350_000);
        var lunch = SeedCharge(picnic.Id, 80_000, "Tiền ăn trưa");
        Pay(Assign(fee, _ctx.Owner), PaymentStatus.Confirmed, ConfirmationMode.SelfDeclared);
        Pay(Assign(lunch, _ctx.Owner), PaymentStatus.Confirmed, ConfirmationMode.SelfDeclared);
        Pay(Assign(fee, _ctx.Treasurer), PaymentStatus.Confirmed);
        Assign(lunch, _ctx.Treasurer);
        Assign(fee, _ctx.StudentA);
        Assign(lunch, _ctx.StudentA);

        var all = await Service.ListParticipantsAsync(_ctx.Fund.Id, picnic.Id, null, default);

        Assert.Equal(3, all.Total);
        Assert.Equal((1, 2), (all.PaidCount, all.UnpaidCount));
        var owner = all.Items[0];
        Assert.Equal((MemberRole.Owner, 430_000L, 430_000L, ParticipantPaymentStatus.Paid, true), (owner.Role, owner.Paid, owner.Due, owner.PaymentStatus, owner.HasSelfDeclared));
        var treasurer = all.Items.Single(item => item.UserId == _ctx.Treasurer.Id);
        Assert.Equal((350_000L, ParticipantPaymentStatus.Unpaid), (treasurer.Paid, treasurer.PaymentStatus));

        var unpaid = await Service.ListParticipantsAsync(_ctx.Fund.Id, picnic.Id, ParticipantPaymentStatus.Unpaid, default);
        Assert.Equal(2, unpaid.Items.Count);
        Assert.Equal(3, unpaid.Total);

        // Học sinh chỉ thấy tên, không thấy tiền của người khác.
        _ctx.ActAs(_ctx.StudentA);
        var names = await Service.ListParticipantsAsync(_ctx.Fund.Id, picnic.Id, null, default);
        Assert.All(names.Items, item => Assert.Equal((0L, ParticipantPaymentStatus.NoCharge), (item.Paid, item.PaymentStatus)));
    }

    // ---------- Đóng danh sách (BR-19) ----------

    /// <summary>Sự kiện có hai khoản thu: Owner đóng đủ, Treasurer chỉ đóng phí (350.000), StudentA chưa đóng gì, StudentB không tham gia.</summary>
    private async Task<(EventDetailDto Event, Charge Fee, Charge Lunch)> PicnicWithMixedPaymentsAsync()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.Owner, _ctx.Treasurer, _ctx.StudentA);
        Participant(picnic.Id, _ctx.StudentB).Response = ParticipationResponse.Declined;
        var fee = SeedCharge(picnic.Id, 350_000);
        var lunch = SeedCharge(picnic.Id, 80_000, "Tiền ăn trưa");
        Pay(Assign(fee, _ctx.Owner), PaymentStatus.Confirmed, ConfirmationMode.SelfDeclared);
        Pay(Assign(lunch, _ctx.Owner), PaymentStatus.Confirmed, ConfirmationMode.SelfDeclared);
        Pay(Assign(fee, _ctx.Treasurer), PaymentStatus.Confirmed);
        Assign(lunch, _ctx.Treasurer);
        Assign(fee, _ctx.StudentA);
        Assign(lunch, _ctx.StudentA);
        return (picnic, fee, lunch);
    }

    private long BalanceOf(User user) => _ctx.Db.Query<PersonalBalance>().SingleOrDefault(item => item.UserId == user.Id)?.Balance ?? 0;

    [Fact]
    public async Task Closing_the_list_excludes_the_underpaid_refunds_their_money_into_the_balance_and_records_the_history()
    {
        var (picnic, fee, _) = await PicnicWithMixedPaymentsAsync();

        var result = await Service.CloseListAsync(_ctx.Fund.Id, picnic.Id, confirmEarly: true, default);

        Assert.Equal((2, 1, 350_000L), (result.Excluded.Count, result.RemainingCount, result.TotalRefund));
        var treasurer = Participant(picnic.Id, _ctx.Treasurer);
        Assert.Equal((false, 350_000L, EventsErrors.ExclusionReasonUnderpaid), (treasurer.IsFinal, treasurer.RefundedAmount, treasurer.ExclusionReason));
        Assert.Equal(ServiceTestContext.Start, treasurer.ExcludedAt);
        var student = Participant(picnic.Id, _ctx.StudentA);
        Assert.Equal((false, 0L, EventsErrors.ExclusionReasonNothingPaid), (student.IsFinal, student.RefundedAmount, student.ExclusionReason));
        Assert.True(Participant(picnic.Id, _ctx.Owner).IsFinal);
        Assert.Null(Participant(picnic.Id, _ctx.StudentB).ExclusionReason);

        // Tiền đã đóng của người bị loại về số dư cá nhân; sổ sự kiện có dòng chi hoàn tiền tương ứng.
        Assert.Equal(350_000, BalanceOf(_ctx.Treasurer));
        Assert.Equal(0, BalanceOf(_ctx.StudentA));
        var credit = _ctx.Db.Query<BalanceTransaction>().Single();
        Assert.Equal((BalanceTransactionType.ListCloseRefund, picnic.Id), (credit.Type, credit.ReferenceId!.Value));
        var refund = _ctx.Db.Query<LedgerEntry>().Single(item => item.Source == LedgerSource.Refund);
        Assert.Equal((LedgerType.Expense, 350_000L, (Guid?)picnic.Id), (refund.Type, refund.Amount, refund.EventId));

        // Gỡ nghĩa vụ của người bị loại, người còn lại giữ nguyên.
        Assert.All(_ctx.Db.Query<ChargeAssignment>().Where(item => item.UserId == _ctx.Treasurer.Id || item.UserId == _ctx.StudentA.Id),
            item => Assert.Equal(AssignmentStatus.Excluded, item.Status));
        Assert.All(_ctx.Db.Query<ChargeAssignment>().Where(item => item.UserId == _ctx.Owner.Id), item => Assert.Equal(AssignmentStatus.Paid, item.Status));
        var closed = _ctx.Db.Query<Event>().Single();
        Assert.Equal((EventStatus.Completed, ServiceTestContext.Start), (closed.Status, closed.CompletedAt!.Value));
        Assert.NotNull(fee);

        // Thông báo và nhật ký rõ ràng.
        var excludedNotes = Notifications.Where(item => item.Type == NotificationType.EventParticipantExcluded).ToArray();
        Assert.Equal(2, excludedNotes.Length);
        Assert.Contains("350.000", excludedNotes.Single(item => item.UserId == _ctx.Treasurer.Id).Body);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == EventsAuditActions.CloseList && log.Scope == FinancialScope.Event && log.EventId == picnic.Id);
        Assert.Equal(2, _ctx.AuditLogs.Count(log => log.Action == EventsAuditActions.Exclude));

        // Trang sự kiện hiển thị lịch sử cho thủ quỹ, học sinh không thấy danh sách người bị loại.
        var detail = await Service.GetAsync(_ctx.Fund.Id, picnic.Id, default);
        Assert.Equal(2, detail.Exclusions.Count);
        Assert.Equal(350_000, detail.Exclusions.Single(item => item.UserId == _ctx.Treasurer.Id).RefundedAmount);
        _ctx.ActAs(_ctx.StudentA);
        Assert.Empty((await Service.GetAsync(_ctx.Fund.Id, picnic.Id, default)).Exclusions);
    }

    [Fact]
    public async Task The_preview_matches_what_closing_does_and_changes_nothing()
    {
        var (picnic, _, _) = await PicnicWithMixedPaymentsAsync();

        var preview = await Service.PreviewCloseListAsync(_ctx.Fund.Id, picnic.Id, default);

        Assert.Equal((2, 1, 2, 350_000L, true), (preview.Excluded.Count, preview.RemainingCount, preview.OpenChargeCount, preview.TotalRefund, preview.BeforeDeadline));
        Assert.Equal(EventStatus.Open, _ctx.Db.Query<Event>().Single().Status);
        Assert.Empty(_ctx.Db.Query<BalanceTransaction>());
        Assert.All(_ctx.Db.Query<EventParticipant>(), item => Assert.Null(item.ExcludedAt));
    }

    [Fact]
    public async Task Closing_before_the_deadline_needs_confirmation_but_not_from_the_deadline_on()
    {
        var (picnic, _, _) = await PicnicWithMixedPaymentsAsync();

        var early = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CloseListAsync(_ctx.Fund.Id, picnic.Id, confirmEarly: false, default));
        Assert.Equal(EventStatusRules.ListDeadlineNotReachedCode, early.Code);
        Assert.Equal(EventStatus.Open, _ctx.Db.Query<Event>().Single().Status);

        _ctx.Clock.Now = new DateTimeOffset(2026, 11, 25, 2, 0, 0, TimeSpan.Zero); // 25/11 giờ Việt Nam = hạn
        var onTime = await Service.CloseListAsync(_ctx.Fund.Id, picnic.Id, confirmEarly: false, default);

        Assert.False(onTime.BeforeDeadline);
        Assert.Equal(EventStatus.Completed, _ctx.Db.Query<Event>().Single().Status);
    }

    [Fact]
    public async Task An_event_without_open_charges_excludes_nobody_and_closed_charges_do_not_count()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.StudentA);

        var none = await Service.PreviewCloseListAsync(_ctx.Fund.Id, picnic.Id, default);
        Assert.Empty(none.Excluded);

        var oldFee = SeedCharge(picnic.Id, 50_000, "Phí đã đóng sổ");
        oldFee.Status = ChargeStatus.Closed;
        Assign(oldFee, _ctx.StudentA); // chưa đóng nhưng khoản thu đã đóng nên không tính
        var open = SeedCharge(picnic.Id, 100_000);
        Pay(Assign(open, _ctx.StudentA), PaymentStatus.Confirmed);

        var result = await Service.CloseListAsync(_ctx.Fund.Id, picnic.Id, confirmEarly: true, default);

        Assert.Empty(result.Excluded);
        Assert.True(Participant(picnic.Id, _ctx.StudentA).IsFinal);
    }

    [Fact]
    public async Task Only_the_open_event_of_a_manager_can_be_closed()
    {
        var (picnic, _, _) = await PicnicWithMixedPaymentsAsync();

        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service.CloseListAsync(_ctx.Fund.Id, picnic.Id, true, default));

        _ctx.ActAs(_ctx.Owner);
        await Service.CloseListAsync(_ctx.Fund.Id, picnic.Id, true, default);
        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CloseListAsync(_ctx.Fund.Id, picnic.Id, true, default));
        Assert.Equal(EventStatusRules.InvalidTransitionCode, again.Code);
    }

    [Fact]
    public async Task The_list_deadline_cannot_be_in_the_past_when_creating_or_changing_it()
    {
        var past = new DateOnly(2026, 11, 10);

        var create = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CreateAsync(_ctx.Fund.Id, new CreateEventInput("X", null, Day, past), default));
        Assert.Equal(EventsErrors.ListDeadlineInPast, create.Code);

        var picnic = await CreatePicnic();
        var update = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.UpdateAsync(_ctx.Fund.Id, picnic.Id, new UpdateEventInput("Dã ngoại", null, Day, past), default));
        Assert.Equal(EventsErrors.ListDeadlineInPast, update.Code);

        var later = await Service.UpdateAsync(_ctx.Fund.Id, picnic.Id, new UpdateEventInput("Dã ngoại", null, Day, Deadline.AddDays(3)), default);
        Assert.Equal(Deadline.AddDays(3), later.ListDeadline);
    }

    [Fact]
    public async Task Cancel_only_without_money_BR16()
    {
        var withMoney = await CreatePicnic();
        Accept(withMoney.Id, _ctx.StudentA);
        Pay(Assign(SeedCharge(withMoney.Id, 350_000), _ctx.StudentA), PaymentStatus.Confirmed);
        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CancelAsync(_ctx.Fund.Id, withMoney.Id, "Mưa", default));
        Assert.Equal(EventsErrors.HasMoney, blocked.Code);

        var picnic = await Service.CreateAsync(_ctx.Fund.Id, new CreateEventInput("Cắm trại", null, Day, Deadline), default);
        Accept(picnic.Id, _ctx.StudentA);
        var fee = SeedCharge(picnic.Id, 350_000, "Phí cắm trại");
        var assignment = Assign(fee, _ctx.StudentA);
        var cancelled = await Service.CancelAsync(_ctx.Fund.Id, picnic.Id, " Hủy do thời tiết ", default);

        Assert.Equal(EventStatus.Cancelled, cancelled.Status);
        Assert.Equal(ChargeStatus.Cancelled, fee.Status);
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
        Assert.Equal("Hủy do thời tiết", Assert.Single(_ctx.AuditLogs, item => item.Action == EventsAuditActions.Cancel).Note);

        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CancelAsync(_ctx.Fund.Id, picnic.Id, "x", default));
        Assert.Equal(EventStatusRules.InvalidTransitionCode, again.Code);
    }

    [Fact]
    public async Task Cancel_is_blocked_by_ledger_entries()
    {
        var picnic = await CreatePicnic();
        _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, picnic.Id, LedgerType.Income, LedgerSource.Payment, null, "Thu", null, 100_000, Day, _ctx.Owner.Id, ServiceTestContext.Start));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CancelAsync(_ctx.Fund.Id, picnic.Id, "x", default));
        Assert.Equal(EventsErrors.HasMoney, error.Code);
    }

    [Fact]
    public async Task Update_records_before_and_after_and_is_blocked_after_settlement()
    {
        var picnic = await CreatePicnic();
        var updated = await Service.UpdateAsync(_ctx.Fund.Id, picnic.Id, new UpdateEventInput("Dã ngoại Tam Đảo", null, Day.AddDays(1), Deadline), default);
        Assert.Equal(("Dã ngoại Tam Đảo", (string?)null, (DateOnly?)Day.AddDays(1)), (updated.Name, updated.Description, updated.EventDate));
        var log = Assert.Single(_ctx.AuditLogs, item => item.Action == EventsAuditActions.Update);
        Assert.Contains("Ba Vì", log.BeforeJson);

        _ctx.Db.Query<Event>().Single().Status = EventStatus.Settled;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.UpdateAsync(_ctx.Fund.Id, picnic.Id, new UpdateEventInput("X", null, Day, Deadline), default));
        Assert.Equal(EventsErrors.NotEditable, error.Code);
    }

    [Fact]
    public async Task List_shows_counts_money_and_my_response()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.Owner, _ctx.StudentA);
        var fee = SeedCharge(picnic.Id, 100_000);
        Pay(Assign(fee, _ctx.Owner), PaymentStatus.Confirmed);
        Assign(fee, _ctx.StudentA);

        var card = Assert.Single(await Service.ListAsync(_ctx.Fund.Id, default));

        Assert.Equal((4, 2, 1), (card.InvitedCount, card.ParticipantCount, card.PaidInFullCount));
        Assert.Equal((100_000L, 0L, 100_000L), (card.TotalIncome, card.TotalExpense, card.Balance));
        Assert.Equal(ParticipationResponse.Accepted, card.MyResponse);
    }

    [Fact]
    public async Task New_member_is_invited_to_open_events_only_TD10()
    {
        var open = await CreatePicnic();
        var done = await Service.CreateAsync(_ctx.Fund.Id, new CreateEventInput("Liên hoan", null, Day, Deadline), default);
        _ctx.Db.Query<Event>().Single(item => item.Id == done.Id).Status = EventStatus.Completed;
        var late = new User("late@x.com", "late", "h", UserOrigin.Provisioned, false, ServiceTestContext.Start);
        _ctx.Db.Seed(late);

        var count = await Invitations.InviteToOpenEventsAsync(_ctx.Fund.Id, late.Id, _ctx.Owner.Id, default);
        var again = await Invitations.InviteToOpenEventsAsync(_ctx.Fund.Id, late.Id, _ctx.Owner.Id, default);

        Assert.Equal((1, 0), (count, again));
        Assert.Equal(open.Id, Assert.Single(_ctx.Db.Query<EventParticipant>(), item => item.UserId == late.Id).EventId);
        Assert.Single(Notifications, item => item.UserId == late.Id && item.Type == NotificationType.EventCreated);
        Assert.Single(_ctx.AuditLogs, item => item.Action == EventsAuditActions.Invite);
    }

    [Fact]
    public void Notification_titles_fit_the_column()
    {
        var title = EventsService.NotificationTitle(new string('x', 150), "Bạn không còn trong danh sách tham gia");
        Assert.True(title.Length <= 200);
        Assert.EndsWith("Bạn không còn trong danh sách tham gia", title);
        Assert.Equal("[Sự kiện: A] Lời mời tham gia", EventsService.NotificationTitle("A", "Lời mời tham gia"));
    }
}
