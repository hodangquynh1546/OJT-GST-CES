using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Notifications;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

/// <summary>M11-T3: NotificationsService (FR-43, FR-44; OQ-12).</summary>
public class NotificationsServiceTests
{
    // Start = 2026-11-18 10:00 giờ Việt Nam.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private readonly ServiceTestContext _ctx = new();

    public NotificationsServiceTests() => _ctx.ActAs(_ctx.StudentA);

    private NotificationsService CreateService() => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, new Notifier(_ctx.Db, _ctx.Clock), new ReminderOptions(), _ctx.Clock);

    private Notification Seed(User user, string title, DateTimeOffset at, bool read = false, FinancialScope scope = FinancialScope.General, Guid? fundId = null)
    {
        var item = new Notification(user.Id, fundId ?? _ctx.Fund.Id, scope, null, NotificationType.GeneralChargeCreated, title, "nội dung", at) { IsRead = read };
        _ctx.Db.Seed(item);
        return item;
    }

    private (Charge Charge, ChargeAssignment Assignment) SeedCharge(User user, DateOnly due, AssignmentStatus status = AssignmentStatus.Unpaid, Guid? eventId = null, ChargeStatus chargeStatus = ChargeStatus.Open)
    {
        var charge = new Charge(_ctx.Fund.Id, eventId, "Quỹ HK1", null, 50_000, due, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start) { Status = chargeStatus };
        var assignment = new ChargeAssignment(charge.Id, user.Id, 50_000) { Status = status };
        _ctx.Db.Seed(charge);
        _ctx.Db.Seed(assignment);
        return (charge, assignment);
    }

    private IEnumerable<Notification> DueSoon(User user) =>
        _ctx.Db.Query<Notification>().Where(item => item.UserId == user.Id && item.Type == NotificationType.ChargeDueSoon);

    // ---------- Danh sách / đã đọc ----------

    [Fact]
    public async Task List_shows_only_my_notifications_newest_first_with_the_unread_count()
    {
        var t = ServiceTestContext.Start;
        Seed(_ctx.StudentA, "cũ", t.AddHours(-3), read: true);
        Seed(_ctx.StudentA, "mới", t.AddHours(-1));
        Seed(_ctx.StudentB, "của người khác", t);
        var otherFund = new Fund("Lớp khác", _ctx.Owner.Id, "2026-2027", t);
        _ctx.Db.Seed(otherFund);
        Seed(_ctx.StudentA, "quỹ khác", t, fundId: otherFund.Id);

        var result = await CreateService().ListAsync(_ctx.Fund.Id, false, 1, 20, default);

        Assert.Equal(["mới", "cũ"], result.Items.Select(item => item.Title));
        Assert.Equal((2, 1), (result.Total, result.UnreadCount));
    }

    [Fact]
    public async Task Unread_only_and_paging_work()
    {
        for (var i = 0; i < 5; i++)
        {
            Seed(_ctx.StudentA, $"tb{i}", ServiceTestContext.Start.AddMinutes(-i), read: i % 2 == 0);
        }

        var unread = await CreateService().ListAsync(_ctx.Fund.Id, true, 1, 20, default);
        var page2 = await CreateService().ListAsync(_ctx.Fund.Id, false, 2, 2, default);

        Assert.Equal(["tb1", "tb3"], unread.Items.Select(item => item.Title));
        Assert.Equal((5, 2, "tb2"), (page2.Total, page2.Items.Count, page2.Items[0].Title));
    }

    [Fact]
    public async Task Mark_read_changes_only_my_own_notification()
    {
        var mine = Seed(_ctx.StudentA, "của tôi", ServiceTestContext.Start);
        var theirs = Seed(_ctx.StudentB, "của họ", ServiceTestContext.Start);

        await CreateService().MarkReadAsync(_ctx.Fund.Id, mine.Id, default);
        await Assert.ThrowsAsync<ClassFund.Application.Common.Exceptions.NotFoundException>(() => CreateService().MarkReadAsync(_ctx.Fund.Id, theirs.Id, default));

        Assert.True(mine.IsRead);
        Assert.False(theirs.IsRead);
        Assert.Equal(0, await CreateService().UnreadCountAsync(_ctx.Fund.Id, default));
    }

    [Fact]
    public async Task Mark_all_read_marks_only_mine_in_this_fund()
    {
        var a1 = Seed(_ctx.StudentA, "1", ServiceTestContext.Start);
        var a2 = Seed(_ctx.StudentA, "2", ServiceTestContext.Start);
        var b1 = Seed(_ctx.StudentB, "3", ServiceTestContext.Start);

        var marked = await CreateService().MarkAllReadAsync(_ctx.Fund.Id, default);

        Assert.Equal(2, marked);
        Assert.All([a1, a2], item => Assert.True(item.IsRead));
        Assert.False(b1.IsRead);
    }

    [Fact]
    public async Task Outsiders_cannot_read_a_funds_notifications()
    {
        _ctx.ActAs(_ctx.Outsider);

        await Assert.ThrowsAnyAsync<Exception>(() => CreateService().ListAsync(_ctx.Fund.Id, false, 1, 20, default));
        await Assert.ThrowsAnyAsync<Exception>(() => CreateService().UnreadCountAsync(_ctx.Fund.Id, default));
    }

    // ---------- Nhắc hạn (OQ-12) ----------

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(-1, false)]
    public async Task Only_charges_due_within_two_days_are_reminded(int daysAhead, bool reminded)
    {
        SeedCharge(_ctx.StudentA, Today.AddDays(daysAhead));

        var count = await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Equal(reminded ? 1 : 0, count);
        Assert.Equal(reminded ? 1 : 0, DueSoon(_ctx.StudentA).Count());
    }

    [Fact]
    public async Task The_reminder_names_the_scope_and_is_not_repeated_the_same_day_but_returns_the_next_day()
    {
        var (_, assignment) = SeedCharge(_ctx.StudentA, Today.AddDays(2));

        await CreateService().ListAsync(_ctx.Fund.Id, false, 1, 20, default);
        await CreateService().ListAsync(_ctx.Fund.Id, false, 1, 20, default);
        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        var first = Assert.Single(DueSoon(_ctx.StudentA));
        Assert.StartsWith("[Quỹ chung] Sắp đến hạn đóng: Quỹ HK1", first.Title);
        Assert.Contains("Còn 2 ngày", first.Body);
        Assert.Equal((nameof(ChargeAssignment), assignment.Id), (first.ReferenceType, first.ReferenceId!.Value));

        _ctx.Clock.Now = ServiceTestContext.Start.AddDays(1);
        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Equal(2, DueSoon(_ctx.StudentA).Count());
    }

    [Fact]
    public async Task Paid_cancelled_or_closed_obligations_are_not_reminded_and_others_are_not_notified()
    {
        SeedCharge(_ctx.StudentA, Today.AddDays(1), AssignmentStatus.Paid);
        SeedCharge(_ctx.StudentA, Today.AddDays(1), AssignmentStatus.AwaitingConfirmation);
        SeedCharge(_ctx.StudentA, Today.AddDays(1), chargeStatus: ChargeStatus.Closed);
        SeedCharge(_ctx.StudentB, Today.AddDays(1));

        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Empty(DueSoon(_ctx.StudentA));
        Assert.Empty(DueSoon(_ctx.StudentB));
    }

    [Fact]
    public async Task Event_charges_no_longer_get_the_per_charge_due_date_reminder()
    {
        var open = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, Today, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(open);
        SeedCharge(_ctx.StudentA, Today.AddDays(1), eventId: open.Id);

        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Empty(DueSoon(_ctx.StudentA));
    }

    // ---------- Nhắc tự động trước hạn đóng danh sách (D-3 … D-1) ----------

    private Event SeedEventWithDeadline(DateOnly? deadline, EventStatus status = EventStatus.Open, string name = "Dã ngoại Ba Vì")
    {
        var fundEvent = new Event(_ctx.Fund.Id, name, null, Today.AddDays(20), _ctx.Owner.Id, ServiceTestContext.Start) { Status = status, ListDeadline = deadline };
        _ctx.Db.Seed(fundEvent);
        return fundEvent;
    }

    private ChargeAssignment SeedEventAssignment(Event fundEvent, User user, long amount = 70_000, AssignmentStatus status = AssignmentStatus.Unpaid)
    {
        var charge = new Charge(_ctx.Fund.Id, fundEvent.Id, "Tiền xe", null, amount, null, ChargeKind.Regular, _ctx.Owner.Id, ServiceTestContext.Start);
        var assignment = new ChargeAssignment(charge.Id, user.Id, amount) { Status = status };
        _ctx.Db.Seed(charge);
        _ctx.Db.Seed(assignment);
        return assignment;
    }

    private IEnumerable<PaymentReminder> Reminders(User user) => _ctx.Db.Query<PaymentReminder>().Where(item => item.UserId == user.Id);

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public async Task The_unpaid_are_reminded_only_in_the_last_three_days_before_the_list_deadline(int daysAhead, bool reminded)
    {
        var fundEvent = SeedEventWithDeadline(Today.AddDays(daysAhead));
        SeedEventAssignment(fundEvent, _ctx.StudentA);

        var count = await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Equal(reminded ? 1 : 0, count);
        Assert.Equal(reminded ? 1 : 0, Reminders(_ctx.StudentA).Count());
    }

    [Fact]
    public async Task The_automatic_reminder_names_the_event_the_amount_and_the_consequence_and_is_logged()
    {
        var fundEvent = SeedEventWithDeadline(Today.AddDays(2));
        SeedEventAssignment(fundEvent, _ctx.StudentA, 70_000);
        SeedEventAssignment(fundEvent, _ctx.StudentA, 30_000);

        await CreateService().ListAsync(_ctx.Fund.Id, false, 1, 20, default);

        var note = Assert.Single(_ctx.Db.Query<Notification>().Where(item => item.Type == NotificationType.EventUnpaidReminder));
        Assert.Equal((FinancialScope.Event, fundEvent.Id, _ctx.StudentA.Id), (note.Scope, note.EventId!.Value, note.UserId));
        Assert.StartsWith("[Sự kiện: Dã ngoại Ba Vì]", note.Title);
        Assert.Contains("Còn 2 ngày", note.Body);
        Assert.Contains("100.000", note.Body);
        Assert.Contains("hoàn vào số dư", note.Body);
        var log = Assert.Single(Reminders(_ctx.StudentA));
        Assert.Equal((ReminderKind.Auto, (Guid?)null, fundEvent.Id, Today), (log.Kind, log.SentByUserId, log.EventId!.Value, log.ReminderDate));
    }

    [Fact]
    public async Task The_automatic_reminder_is_sent_once_a_day_and_again_the_next_day()
    {
        var fundEvent = SeedEventWithDeadline(Today.AddDays(3));
        SeedEventAssignment(fundEvent, _ctx.StudentA);

        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);
        await CreateService().ListAsync(_ctx.Fund.Id, false, 1, 20, default);
        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);
        Assert.Single(Reminders(_ctx.StudentA));

        _ctx.Clock.Now = ServiceTestContext.Start.AddDays(1);
        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Equal([Today, Today.AddDays(1)], Reminders(_ctx.StudentA).Select(item => item.ReminderDate).Order());
    }

    [Fact]
    public async Task The_paid_other_people_and_events_that_are_closed_get_no_automatic_reminder()
    {
        var open = SeedEventWithDeadline(Today.AddDays(2));
        var closed = SeedEventWithDeadline(Today.AddDays(2), EventStatus.Completed, "Đã đóng danh sách");
        var settled = SeedEventWithDeadline(Today.AddDays(2), EventStatus.Settled, "Đã quyết toán");
        SeedEventAssignment(open, _ctx.StudentA, status: AssignmentStatus.Paid);
        SeedEventAssignment(open, _ctx.StudentB);
        SeedEventAssignment(closed, _ctx.StudentA);
        SeedEventAssignment(settled, _ctx.StudentA);
        SeedEventWithDeadline(null, name: "Không có hạn");

        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Empty(Reminders(_ctx.StudentA));
        Assert.Empty(Reminders(_ctx.StudentB));
    }

    [Fact]
    public async Task The_number_of_reminder_days_is_configurable()
    {
        var fundEvent = SeedEventWithDeadline(Today.AddDays(5));
        SeedEventAssignment(fundEvent, _ctx.StudentA);

        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);
        Assert.Empty(Reminders(_ctx.StudentA));

        var wider = new NotificationsService(_ctx.Db, _ctx.CurrentUser, _ctx.Access, new Notifier(_ctx.Db, _ctx.Clock), new ReminderOptions { ListDeadlineDays = 5 }, _ctx.Clock);
        await wider.UnreadCountAsync(_ctx.Fund.Id, default);

        Assert.Single(Reminders(_ctx.StudentA));
    }
}
