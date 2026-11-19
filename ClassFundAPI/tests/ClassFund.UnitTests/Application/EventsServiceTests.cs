using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>M8-T3: EventsService + EventInvitationService (FR-30 → FR-34, BR-16 → BR-20, OQ-18).</summary>
public class EventsServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private readonly ServiceTestContext _ctx = new();

    private EventsService Service => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock);

    private EventInvitationService Invitations => new(_ctx.Db, _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock));

    private IEnumerable<Notification> Notifications => _ctx.Db.Query<Notification>();

    private Task<EventDetailDto> CreatePicnic() =>
        Service.CreateAsync(_ctx.Fund.Id, new CreateEventInput("  Dã ngoại Ba Vì ", " Xe 45 chỗ ", Day), default);

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
        assignment.Status = status switch
        {
            PaymentStatus.Confirmed => AssignmentStatus.Paid,
            PaymentStatus.Submitted => AssignmentStatus.AwaitingConfirmation,
            _ => assignment.Status,
        };
    }

    [Fact]
    public async Task Create_invites_every_active_member_and_notifies_others_BR17()
    {
        _ctx.Db.Query<FundMember>().Single(member => member.UserId == _ctx.StudentB.Id).Status = MemberStatus.Removed;

        var detail = await CreatePicnic();

        Assert.Equal("Dã ngoại Ba Vì", detail.Name);
        Assert.Equal("Xe 45 chỗ", detail.Description);
        Assert.Equal(EventStatus.Open, detail.Status);
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
        Pay(Assign(SeedCharge(picnic.Id, 350_000), _ctx.StudentA), PaymentStatus.Submitted);

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
        Pay(Assign(lunch, _ctx.Treasurer), PaymentStatus.Submitted);
        Assign(fee, _ctx.StudentA);
        Assign(lunch, _ctx.StudentA);

        var all = await Service.ListParticipantsAsync(_ctx.Fund.Id, picnic.Id, null, default);

        Assert.Equal(3, all.Total);
        Assert.Equal((1, 1, 1), (all.PaidCount, all.PendingCount, all.UnpaidCount));
        var owner = all.Items[0];
        Assert.Equal((MemberRole.Owner, 430_000L, 430_000L, ParticipantPaymentStatus.Paid, true), (owner.Role, owner.Paid, owner.Due, owner.PaymentStatus, owner.HasSelfDeclared));
        var treasurer = all.Items.Single(item => item.UserId == _ctx.Treasurer.Id);
        Assert.Equal((350_000L, ParticipantPaymentStatus.Pending, 1), (treasurer.Paid, treasurer.PaymentStatus, treasurer.PendingPaymentCount));

        var unpaid = await Service.ListParticipantsAsync(_ctx.Fund.Id, picnic.Id, ParticipantPaymentStatus.Unpaid, default);
        Assert.Equal(_ctx.StudentA.Id, Assert.Single(unpaid.Items).UserId);
        Assert.Equal(3, unpaid.Total);

        // Học sinh chỉ thấy tên, không thấy tiền của người khác.
        _ctx.ActAs(_ctx.StudentA);
        var names = await Service.ListParticipantsAsync(_ctx.Fund.Id, picnic.Id, null, default);
        Assert.All(names.Items, item => Assert.Equal((0L, ParticipantPaymentStatus.NoCharge), (item.Paid, item.PaymentStatus)));
    }

    [Fact]
    public async Task Complete_excludes_people_who_paid_nothing_and_reminds_the_underpaid_BR19()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.Owner, _ctx.Treasurer, _ctx.StudentA, _ctx.StudentB);
        var fee = SeedCharge(picnic.Id, 350_000);
        var lunch = SeedCharge(picnic.Id, 80_000, "Tiền ăn trưa");
        Pay(Assign(fee, _ctx.Owner), PaymentStatus.Confirmed);
        Pay(Assign(lunch, _ctx.Owner), PaymentStatus.Confirmed);            // đủ
        Pay(Assign(fee, _ctx.Treasurer), PaymentStatus.Confirmed);
        var treasurerLunch = Assign(lunch, _ctx.Treasurer);                  // thiếu 80.000 → nhắc
        Pay(Assign(fee, _ctx.StudentA), PaymentStatus.Submitted);            // đang chờ → giữ
        var aLunch = Assign(lunch, _ctx.StudentA);
        var bFee = Assign(fee, _ctx.StudentB);                               // chưa đóng gì → loại
        var bLunch = Assign(lunch, _ctx.StudentB);

        var preview = await Service.PreviewCompletionAsync(_ctx.Fund.Id, picnic.Id, default);
        Assert.Equal(_ctx.StudentB.Id, Assert.Single(preview.Excluded).UserId);
        Assert.Equal(new[] { _ctx.Treasurer.Id, _ctx.StudentA.Id }.Order(), preview.Underpaid.Select(person => person.UserId).Order());
        Assert.Equal(80_000, preview.Underpaid.Single(person => person.UserId == _ctx.Treasurer.Id).Missing);
        Assert.Equal((1, 3), (preview.PendingPaymentCount, preview.RemainingCount));
        Assert.Equal(EventStatus.Open, _ctx.Db.Query<Event>().Single().Status); // xem trước không ghi gì

        var before = Notifications.Count();
        var result = await Service.CompleteAsync(_ctx.Fund.Id, picnic.Id, default);

        Assert.Equal(preview.Excluded, result.Excluded);
        Assert.Equal(preview.Underpaid, result.Underpaid);
        Assert.Equal((preview.PendingPaymentCount, preview.RemainingCount), (result.PendingPaymentCount, result.RemainingCount));
        var fundEvent = _ctx.Db.Query<Event>().Single();
        Assert.Equal(EventStatus.Completed, fundEvent.Status);
        Assert.Equal(ServiceTestContext.Start, fundEvent.CompletedAt);
        Assert.False(Participant(picnic.Id, _ctx.StudentB).IsFinal);
        Assert.Equal(EventsErrors.ExclusionReason, Participant(picnic.Id, _ctx.StudentB).ExclusionReason);
        Assert.True(Participant(picnic.Id, _ctx.StudentA).IsFinal);
        Assert.Equal((AssignmentStatus.Excluded, AssignmentStatus.Excluded), (bFee.Status, bLunch.Status));
        Assert.Equal((AssignmentStatus.Unpaid, AssignmentStatus.Unpaid), (treasurerLunch.Status, aLunch.Status)); // vẫn nộp được

        var sent = Notifications.Skip(before).ToArray();
        Assert.Equal(_ctx.StudentB.Id, Assert.Single(sent, item => item.Type == NotificationType.EventParticipantExcluded).UserId);
        Assert.Equal(2, sent.Count(item => item.Type == NotificationType.EventUnpaidReminder));
        Assert.Contains("80.000 đ", sent.Single(item => item.UserId == _ctx.Treasurer.Id).Body);

        var detail = await Service.GetAsync(_ctx.Fund.Id, picnic.Id, default);
        Assert.Equal((3, 1), (detail.ParticipantCount, detail.ExcludedCount));
        var participants = await Service.ListParticipantsAsync(_ctx.Fund.Id, picnic.Id, null, default);
        Assert.DoesNotContain(participants.Items, item => item.UserId == _ctx.StudentB.Id);

        Assert.Single(_ctx.AuditLogs, item => item.Action == EventsAuditActions.Complete);
        var twice = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CompleteAsync(_ctx.Fund.Id, picnic.Id, default));
        Assert.Equal(EventStatusRules.InvalidTransitionCode, twice.Code);
    }

    [Fact]
    public async Task Complete_excludes_everyone_who_paid_nothing_even_without_assigned_charges()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.StudentA, _ctx.Treasurer);
        var fee = SeedCharge(picnic.Id, 350_000);
        SeedCharge(picnic.Id, 80_000, "Tiền ăn trưa");
        Pay(Assign(fee, _ctx.Treasurer), PaymentStatus.Confirmed); // đóng 1/2 khoản → vẫn ở lại

        var result = await Service.CompleteAsync(_ctx.Fund.Id, picnic.Id, default);

        // StudentA nhận tham gia nhưng chưa có / chưa đóng khoản nào → bị loại.
        Assert.Equal(_ctx.StudentA.Id, Assert.Single(result.Excluded).UserId);
        Assert.Equal(1, result.RemainingCount);
        Assert.True(Participant(picnic.Id, _ctx.Treasurer).IsFinal);
        Assert.False(Participant(picnic.Id, _ctx.StudentB).IsFinal); // chưa trả lời → không tính
    }

    [Fact]
    public async Task Late_acceptance_while_open_can_pay_and_stay_but_not_after_completion()
    {
        var picnic = await CreatePicnic();
        var fee = SeedCharge(picnic.Id, 350_000);
        SeedCharge(picnic.Id, 80_000, "Tiền ăn trưa");
        _ctx.ActAs(_ctx.StudentA);
        await Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Accepted, default);
        Assert.Equal(2, _ctx.Db.Query<ChargeAssignment>().Count(item => item.UserId == _ctx.StudentA.Id));

        Pay(_ctx.Db.Query<ChargeAssignment>().Single(item => item.UserId == _ctx.StudentA.Id && item.ChargeId == fee.Id), PaymentStatus.Confirmed);
        _ctx.ActAs(_ctx.Owner);
        var result = await Service.CompleteAsync(_ctx.Fund.Id, picnic.Id, default);
        Assert.Empty(result.Excluded);
        Assert.Equal(80_000, Assert.Single(result.Underpaid).Missing); // chưa đóng đủ vẫn ở lại

        _ctx.ActAs(_ctx.StudentB);
        var late = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.RespondAsync(_ctx.Fund.Id, picnic.Id, ParticipationResponse.Accepted, default));
        Assert.Equal(EventsErrors.NotOpen, late.Code);
    }

    [Fact]
    public async Task Cancel_only_without_money_BR16()
    {
        var picnic = await CreatePicnic();
        Accept(picnic.Id, _ctx.StudentA);
        var fee = SeedCharge(picnic.Id, 350_000);
        var assignment = Assign(fee, _ctx.StudentA);
        Pay(assignment, PaymentStatus.Submitted);

        var blocked = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.CancelAsync(_ctx.Fund.Id, picnic.Id, "Mưa", default));
        Assert.Equal(EventsErrors.HasMoney, blocked.Code);

        _ctx.Db.Query<Payment>().Single().Status = PaymentStatus.Rejected;
        assignment.Status = AssignmentStatus.Unpaid;
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
        var updated = await Service.UpdateAsync(_ctx.Fund.Id, picnic.Id, new UpdateEventInput("Dã ngoại Tam Đảo", null, Day.AddDays(1)), default);
        Assert.Equal(("Dã ngoại Tam Đảo", (string?)null, (DateOnly?)Day.AddDays(1)), (updated.Name, updated.Description, updated.EventDate));
        var log = Assert.Single(_ctx.AuditLogs, item => item.Action == EventsAuditActions.Update);
        Assert.Contains("Ba Vì", log.BeforeJson);

        _ctx.Db.Query<Event>().Single().Status = EventStatus.Settled;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.UpdateAsync(_ctx.Fund.Id, picnic.Id, new UpdateEventInput("X", null, Day), default));
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
        _ctx.Db.Seed(new LedgerEntry(_ctx.Fund.Id, picnic.Id, LedgerType.Income, LedgerSource.Payment, null, "Thu", null, 100_000, Day, _ctx.Owner.Id, ServiceTestContext.Start));

        var card = Assert.Single(await Service.ListAsync(_ctx.Fund.Id, default));

        Assert.Equal((4, 2, 1), (card.InvitedCount, card.ParticipantCount, card.PaidInFullCount));
        Assert.Equal((100_000L, 0L, 100_000L), (card.TotalIncome, card.TotalExpense, card.Balance));
        Assert.Equal(ParticipationResponse.Accepted, card.MyResponse);
    }

    [Fact]
    public async Task New_member_is_invited_to_open_events_only_TD10()
    {
        var open = await CreatePicnic();
        var done = await Service.CreateAsync(_ctx.Fund.Id, new CreateEventInput("Liên hoan", null, Day), default);
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
