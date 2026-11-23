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

    private NotificationsService CreateService() => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock);

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
    public async Task An_event_charge_reminder_carries_the_event_scope_and_stops_after_settlement()
    {
        var open = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, Today, _ctx.Owner.Id, ServiceTestContext.Start);
        var settled = new Event(_ctx.Fund.Id, "Cắm trại", null, Today, _ctx.Owner.Id, ServiceTestContext.Start) { Status = EventStatus.Settled };
        _ctx.Db.Seed(open, settled);
        SeedCharge(_ctx.StudentA, Today.AddDays(1), eventId: open.Id);
        SeedCharge(_ctx.StudentA, Today.AddDays(1), eventId: settled.Id);

        await CreateService().UnreadCountAsync(_ctx.Fund.Id, default);

        var item = Assert.Single(DueSoon(_ctx.StudentA));
        Assert.Equal((FinancialScope.Event, open.Id), (item.Scope, item.EventId!.Value));
        Assert.StartsWith("[Sự kiện: Dã ngoại Ba Vì]", item.Title);
    }
}
