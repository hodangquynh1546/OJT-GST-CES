using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Features.Categories;
using ClassFund.Application.Features.Charges;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

/// <summary>M6a-T1/T4: ChargesService (FR-18, FR-24; BR-37).</summary>
public class ChargesServiceTests
{
    private static readonly DateOnly Due = new(2026, 12, 31);

    private readonly ServiceTestContext _ctx = new();

    private readonly Category _income;
    private readonly Category _expense;

    public ChargesServiceTests()
    {
        _income = new Category(_ctx.Fund.Id, "Quỹ lớp", LedgerType.Income, false);
        _expense = new Category(_ctx.Fund.Id, "In ấn", LedgerType.Expense, false);
        _ctx.Db.Seed(_income, _expense);
    }

    private ChargesService CreateService() =>
        new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, new CategoriesService(_ctx.Db, _ctx.CurrentUser, _ctx.Audit), _ctx.Audit, new Notifier(_ctx.Db, _ctx.Clock), _ctx.Clock);

    private CreateChargeInput Input(long amount = 50_000, DateOnly? due = null, Guid? categoryId = null) =>
        new("Quỹ lớp tháng 12", "Đóng quỹ", amount, due ?? Due, categoryId ?? _income.Id);

    [Fact]
    public async Task Create_assigns_every_active_member_including_treasurers_and_notifies_them()
    {
        var removed = new User("old@x.com", "old", "hash", UserOrigin.Provisioned, false, ServiceTestContext.Start);
        _ctx.Db.Seed(removed);
        _ctx.Db.Seed(new FundMember(_ctx.Fund.Id, removed.Id, MemberRole.Student, ServiceTestContext.Start) { Status = MemberStatus.Removed });
        _ctx.ActAs(_ctx.Treasurer);

        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);

        var assignments = _ctx.Db.Query<ChargeAssignment>().Where(item => item.ChargeId == charge.Id).ToList();
        Assert.Equal(4, assignments.Count);
        Assert.DoesNotContain(assignments, item => item.UserId == removed.Id);
        Assert.All(assignments, item => Assert.Equal((AssignmentStatus.Unpaid, 50_000L), (item.Status, item.AmountDue)));
        Assert.Equal(4, _ctx.Db.Query<Notification>().Count(item => item.Type == NotificationType.GeneralChargeCreated));
        Assert.Contains(_ctx.AuditLogs, log => log.Action == ChargesAuditActions.Create && log.ActorUserId == _ctx.Treasurer.Id);
        Assert.Equal(ChargeStatus.Open, charge.Status);
    }

    [Fact]
    public async Task Create_is_forbidden_for_students()
    {
        _ctx.ActAs(_ctx.StudentA);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().CreateAsync(_ctx.Fund.Id, Input(), default));
    }

    [Fact]
    public async Task Create_rejects_past_due_date_and_non_positive_amount()
    {
        var past = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateAsync(_ctx.Fund.Id, Input(due: new DateOnly(2026, 11, 1)), default));
        var amount = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateAsync(_ctx.Fund.Id, Input(amount: 0), default));

        Assert.Equal(ChargesErrors.DueDateInPastCode, past.Code);
        Assert.Equal("money.invalid", amount.Code);
    }

    [Fact]
    public async Task Create_is_blocked_when_the_fund_is_archived()
    {
        _ctx.Fund.Status = FundStatus.Archived;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateAsync(_ctx.Fund.Id, Input(), default));

        Assert.Equal("fund.archived", error.Code);
    }

    [Fact]
    public async Task List_shows_progress_to_treasurers_only_and_my_assignment_to_everyone()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);
        _ctx.Db.Query<ChargeAssignment>().Single(item => item.ChargeId == charge.Id && item.UserId == _ctx.StudentA.Id).Status = AssignmentStatus.Paid;

        var asTreasurer = (await CreateService().ListAsync(_ctx.Fund.Id, new ChargeFilter(null, 1, 20), default)).Items.Single();
        _ctx.ActAs(_ctx.StudentA);
        var asStudent = (await CreateService().ListAsync(_ctx.Fund.Id, new ChargeFilter(null, 1, 20), default)).Items.Single();

        Assert.Equal(new ChargeProgressDto(4, 1, 0, 50_000), asTreasurer.Progress);
        Assert.Null(asStudent.Progress);
        Assert.Equal(AssignmentStatus.Paid, asStudent.Mine?.Status);
    }

    [Fact]
    public async Task Assignments_list_is_for_treasurers_and_marks_self_declared()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);
        var mine = _ctx.Db.Query<ChargeAssignment>().Single(item => item.ChargeId == charge.Id && item.UserId == _ctx.Owner.Id);
        mine.Status = AssignmentStatus.Paid;
        _ctx.Db.Seed(new Payment(mine.Id, _ctx.Owner.Id, 50_000, PaymentMethod.Cash, ServiceTestContext.Start)
        {
            Status = PaymentStatus.Confirmed,
            ConfirmationMode = ConfirmationMode.SelfDeclared,
        });

        var rows = (await CreateService().ListAssignmentsAsync(_ctx.Fund.Id, charge.Id, new AssignmentFilter(null, null, 1, 20), default)).Items;

        Assert.True(rows.Single(row => row.UserId == _ctx.Owner.Id).IsSelfDeclared);
        Assert.Equal(AssignmentStatus.Paid, rows[^1].Status); // chưa đóng lên trước
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateService().ListAssignmentsAsync(_ctx.Fund.Id, charge.Id, new AssignmentFilter(null, null, 1, 20), default));
    }

    [Fact]
    public async Task Close_stops_collecting_and_cannot_be_repeated()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);

        var closed = await CreateService().CloseAsync(_ctx.Fund.Id, charge.Id, default);
        var again = await Assert.ThrowsAsync<ConflictException>(() => CreateService().CloseAsync(_ctx.Fund.Id, charge.Id, default));

        Assert.Equal(ChargeStatus.Closed, closed.Status);
        Assert.Equal(ChargesErrors.InvalidStatusCode, again.Code);
    }

    [Fact]
    public async Task Cancel_cancels_every_assignment_when_nobody_paid()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);

        var cancelled = await CreateService().CancelAsync(_ctx.Fund.Id, charge.Id, default);

        Assert.Equal(ChargeStatus.Cancelled, cancelled.Status);
        Assert.All(_ctx.Db.Query<ChargeAssignment>().Where(item => item.ChargeId == charge.Id), item => Assert.Equal(AssignmentStatus.Cancelled, item.Status));
        Assert.Contains(_ctx.AuditLogs, log => log.Action == ChargesAuditActions.Cancel);
    }

    [Fact]
    public async Task Cancel_is_blocked_once_someone_has_paid()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);
        var assignment = _ctx.Db.Query<ChargeAssignment>().First(item => item.ChargeId == charge.Id);
        _ctx.Db.Seed(new Payment(assignment.Id, assignment.UserId, 50_000, PaymentMethod.Cash, ServiceTestContext.Start));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CancelAsync(_ctx.Fund.Id, charge.Id, default));

        Assert.Equal(ChargesErrors.HasPaymentsCode, error.Code);
    }

    [Fact]
    public async Task A_charge_of_another_fund_is_not_found()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetAsync(Guid.NewGuid(), charge.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetAsync(_ctx.Fund.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Remind_notifies_only_people_who_have_not_paid_and_is_limited_to_once_an_hour()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);
        var rows = _ctx.Db.Query<ChargeAssignment>().Where(item => item.ChargeId == charge.Id).ToList();
        rows.Single(item => item.UserId == _ctx.StudentA.Id).Status = AssignmentStatus.Paid;
        rows.Single(item => item.UserId == _ctx.StudentB.Id).Status = AssignmentStatus.AwaitingConfirmation;
        var before = _ctx.Db.Query<Notification>().Count();

        var result = await CreateService().RemindAsync(_ctx.Fund.Id, charge.Id, default);

        Assert.Equal(2, result.Reminded); // chủ quỹ + thủ quỹ chưa đóng
        var reminders = _ctx.Db.Query<Notification>().Where(item => item.Type == NotificationType.ChargeDueSoon).ToList();
        Assert.Equal(2, reminders.Count);
        Assert.DoesNotContain(reminders, item => item.UserId == _ctx.StudentA.Id || item.UserId == _ctx.StudentB.Id);
        Assert.Equal(before + 2, _ctx.Db.Query<Notification>().Count());

        var again = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RemindAsync(_ctx.Fund.Id, charge.Id, default));
        Assert.Equal(ChargesErrors.RemindTooSoonCode, again.Code);

        _ctx.Clock.Now = ServiceTestContext.Start.AddHours(2);
        Assert.Equal(2, (await CreateService().RemindAsync(_ctx.Fund.Id, charge.Id, default)).Reminded);
    }

    [Fact]
    public async Task Remind_needs_an_open_charge_somebody_unpaid_and_the_manager_permission()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().RemindAsync(_ctx.Fund.Id, charge.Id, default));

        _ctx.ActAs(_ctx.Owner);
        foreach (var item in _ctx.Db.Query<ChargeAssignment>().Where(item => item.ChargeId == charge.Id))
        {
            item.Status = AssignmentStatus.Paid;
        }

        var nobody = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RemindAsync(_ctx.Fund.Id, charge.Id, default));
        Assert.Equal(ChargesErrors.NothingToRemindCode, nobody.Code);

        await CreateService().CloseAsync(_ctx.Fund.Id, charge.Id, default);
        await Assert.ThrowsAsync<ConflictException>(() => CreateService().RemindAsync(_ctx.Fund.Id, charge.Id, default));
    }

    [Fact]
    public async Task Create_stores_the_chosen_income_category_and_returns_its_name()
    {
        var charge = await CreateService().CreateAsync(_ctx.Fund.Id, Input(), default);

        Assert.Equal((_income.Id, "Quỹ lớp"), (charge.CategoryId, charge.CategoryName));
        Assert.Equal(_income.Id, _ctx.Db.Query<Charge>().Single().CategoryId);
    }

    [Fact]
    public async Task Create_rejects_an_expense_inactive_or_unknown_category()
    {
        var inactive = new Category(_ctx.Fund.Id, "Cũ", LedgerType.Income, false) { IsActive = false };
        _ctx.Db.Seed(inactive);

        foreach (var id in new[] { _expense.Id, inactive.Id, Guid.NewGuid() })
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateAsync(_ctx.Fund.Id, Input(categoryId: id), default));
            Assert.Equal(CategoriesErrors.Unusable, error.Code);
        }

        Assert.Empty(_ctx.Db.Query<Charge>());
    }

    // ---------- M6b: khoản thu sự kiện (BR-18) ----------

    private Event SeedEvent(EventStatus status = EventStatus.Open)
    {
        var fundEvent = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, new DateOnly(2026, 12, 20), _ctx.Owner.Id, ServiceTestContext.Start) { Status = status };
        _ctx.Db.Seed(fundEvent);
        _ctx.Db.Seed(new EventParticipant(fundEvent.Id, _ctx.StudentA.Id) { Response = ParticipationResponse.Accepted });
        _ctx.Db.Seed(new EventParticipant(fundEvent.Id, _ctx.StudentB.Id) { Response = ParticipationResponse.Declined });
        _ctx.Db.Seed(new EventParticipant(fundEvent.Id, _ctx.Treasurer.Id));
        return fundEvent;
    }

    [Fact]
    public async Task Event_charge_is_assigned_only_to_accepted_participants_and_notifies_only_them()
    {
        var fundEvent = SeedEvent();
        _ctx.ActAs(_ctx.Treasurer);

        var charge = await CreateService().CreateEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, Input(70_000), default);

        var assignment = Assert.Single(_ctx.Db.Query<ChargeAssignment>().Where(item => item.ChargeId == charge.Id));
        Assert.Equal((_ctx.StudentA.Id, 70_000L), (assignment.UserId, assignment.AmountDue));
        Assert.Equal(fundEvent.Id, _ctx.Db.Query<Charge>().Single(item => item.Id == charge.Id).EventId);
        var notification = Assert.Single(_ctx.Db.Query<Notification>().Where(item => item.Type == NotificationType.EventChargeCreated));
        Assert.Equal(_ctx.StudentA.Id, notification.UserId);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == ChargesAuditActions.EventCreate && log.Scope == FinancialScope.Event && log.EventId == fundEvent.Id);
    }

    [Fact]
    public async Task Event_charge_cannot_be_created_after_the_event_has_ended()
    {
        var fundEvent = SeedEvent(EventStatus.Completed);
        _ctx.ActAs(_ctx.Treasurer);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CreateEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, Input(), default));

        Assert.Empty(_ctx.Db.Query<Charge>());
    }

    [Fact]
    public async Task Event_charge_can_be_listed_closed_and_cancelled_only_through_its_own_event()
    {
        var fundEvent = SeedEvent();
        var other = SeedEvent();
        _ctx.ActAs(_ctx.Treasurer);
        var charge = await CreateService().CreateEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, Input(), default);

        var rows = await CreateService().ListEventAssignmentsAsync(_ctx.Fund.Id, fundEvent.Id, charge.Id, new AssignmentFilter(null, null, 1, 50), default);
        Assert.Equal(1, rows.Total);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService().ListEventAssignmentsAsync(_ctx.Fund.Id, other.Id, charge.Id, new AssignmentFilter(null, null, 1, 50), default));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().CloseEventChargeAsync(_ctx.Fund.Id, other.Id, charge.Id, default));

        var closed = await CreateService().CloseEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, charge.Id, default);
        Assert.Equal(ChargeStatus.Closed, closed.Status);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == ChargesAuditActions.EventClose && log.Scope == FinancialScope.Event);

        var second = await CreateService().CreateEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, Input(), default);
        var cancelled = await CreateService().CancelEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, second.Id, default);
        Assert.Equal(ChargeStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Reminding_an_event_participant_notifies_only_that_person_and_is_throttled()
    {
        var fundEvent = SeedEvent();
        _ctx.ActAs(_ctx.Treasurer);
        await CreateService().CreateEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, Input(70_000), default);

        var result = await CreateService().RemindEventParticipantAsync(_ctx.Fund.Id, fundEvent.Id, _ctx.StudentA.Id, default);

        Assert.Equal(1, result.Reminded);
        var reminder = Assert.Single(_ctx.Db.Query<Notification>().Where(item => item.Type == NotificationType.EventUnpaidReminder));
        Assert.Equal((_ctx.StudentA.Id, FinancialScope.Event, fundEvent.Id), (reminder.UserId, reminder.Scope, reminder.EventId!.Value));
        Assert.StartsWith("[Sự kiện: Dã ngoại Ba Vì]", reminder.Title);
        Assert.Contains("70.000", reminder.Body);
        Assert.Contains(_ctx.AuditLogs, log => log.Action == ChargesAuditActions.EventRemind && log.Scope == FinancialScope.Event);

        var tooSoon = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RemindEventParticipantAsync(_ctx.Fund.Id, fundEvent.Id, _ctx.StudentA.Id, default));
        Assert.Equal(ChargesErrors.RemindTooSoonCode, tooSoon.Code);

        _ctx.Clock.Now = ServiceTestContext.Start.AddHours(2);
        await CreateService().RemindEventParticipantAsync(_ctx.Fund.Id, fundEvent.Id, _ctx.StudentA.Id, default);
        Assert.Equal(2, _ctx.Db.Query<Notification>().Count(item => item.Type == NotificationType.EventUnpaidReminder));
    }

    [Fact]
    public async Task Reminding_needs_something_unpaid_and_a_counted_participant()
    {
        var fundEvent = SeedEvent();
        _ctx.ActAs(_ctx.Treasurer);
        await CreateService().CreateEventChargeAsync(_ctx.Fund.Id, fundEvent.Id, Input(70_000), default);
        _ctx.Db.Query<ChargeAssignment>().Single(item => item.UserId == _ctx.StudentA.Id).Status = AssignmentStatus.Paid;

        var nothing = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RemindEventParticipantAsync(_ctx.Fund.Id, fundEvent.Id, _ctx.StudentA.Id, default));
        Assert.Equal(ChargesErrors.NothingToRemindCode, nothing.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().RemindEventParticipantAsync(_ctx.Fund.Id, fundEvent.Id, _ctx.StudentB.Id, default)); // đã từ chối tham gia
        Assert.Empty(_ctx.Db.Query<Notification>().Where(item => item.Type == NotificationType.EventUnpaidReminder));

        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().RemindEventParticipantAsync(_ctx.Fund.Id, fundEvent.Id, _ctx.StudentA.Id, default));
    }
}
