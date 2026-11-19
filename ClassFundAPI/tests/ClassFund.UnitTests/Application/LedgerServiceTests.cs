using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Features.Attachments;
using ClassFund.Application.Features.Categories;
using ClassFund.Application.Features.Ledger;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.UnitTests.Application;

/// <summary>M7-T3: LedgerService (FR-26 → FR-29, BR-01, BR-06, BR-08, BR-09, BR-20, BR-38, OQ-14).</summary>
public class LedgerServiceTests
{
    private static readonly DateOnly Today = VietnamTime.DateOf(ServiceTestContext.Start);

    private readonly ServiceTestContext _ctx = new();
    private readonly Category _gift;
    private readonly Category _income;
    private readonly Event _picnic;

    public LedgerServiceTests()
    {
        _gift = new Category(_ctx.Fund.Id, "Quà tặng", LedgerType.Expense, isSystem: false);
        _income = new Category(_ctx.Fund.Id, "Đóng góp quỹ lớp", LedgerType.Income, isSystem: true);
        _picnic = new Event(_ctx.Fund.Id, "Dã ngoại Ba Vì", null, Today, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(_gift, _income);
        _ctx.Db.Seed(_picnic);
    }

    private LedgerService Service => new(
        _ctx.Db,
        _ctx.CurrentUser,
        _ctx.Access,
        new CategoriesService(_ctx.Db, _ctx.CurrentUser, _ctx.Audit),
        new AttachmentsService(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Storage, _ctx.Audit, _ctx.Clock),
        _ctx.Audit,
        _ctx.Clock);

    private LedgerEntry SeedIncome(long amount, Guid? eventId = null, LedgerSource source = LedgerSource.Payment)
    {
        var entry = new LedgerEntry(_ctx.Fund.Id, eventId, LedgerType.Income, source, _income.Id, "Thu: Quỹ lớp", null, amount, Today, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(entry);
        return entry;
    }

    private Attachment SeedReceipt(User? uploader = null)
    {
        var file = new Attachment(_ctx.Fund.Id, AttachmentKind.Receipt, "hoa-don.jpg", "image/jpeg", 1000, $"k/{Guid.NewGuid()}", (uploader ?? _ctx.Owner).Id, ServiceTestContext.Start);
        _ctx.Db.Seed(file);
        return file;
    }

    private static CreateExpenseInput Expense(long amount, Guid categoryId, Guid? eventId = null, DateOnly? date = null, params Guid[] files) =>
        new(eventId, categoryId, "  Mua hoa 20/11  ", "Tặng cô chủ nhiệm", amount, date ?? Today, " có hóa đơn ", files);

    [Fact]
    public async Task Creates_expense_within_balance_attaches_receipt_and_audits()
    {
        SeedIncome(1_000_000);
        var receipt = SeedReceipt();

        var detail = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(650_000, _gift.Id, files: receipt.Id), default);

        Assert.Equal(LedgerType.Expense, detail.Entry.Type);
        Assert.Equal(LedgerSource.Manual, detail.Entry.Source);
        Assert.Equal("Mua hoa 20/11", detail.Entry.Title);
        Assert.Equal("có hóa đơn", detail.Note);
        Assert.Equal("Quà tặng", detail.Entry.CategoryName);
        Assert.Equal(MemberRole.Owner, detail.PerformedByRole);
        Assert.Equal(detail.Entry.Id, receipt.OwnerId);
        Assert.Single(detail.Attachments);
        Assert.True(detail.CanReverse);

        var log = Assert.Single(_ctx.AuditLogs, item => item.Action == LedgerAuditActions.Create);
        Assert.Equal(FinancialScope.General, log.Scope);
        Assert.Contains("650000", log.AfterJson);
    }

    [Fact]
    public async Task Expense_over_scope_balance_is_rejected_BR38()
    {
        SeedIncome(300_000);
        SeedIncome(5_000_000, _picnic.Id); // tiền sự kiện không bù cho quỹ chung

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(300_001, _gift.Id), default));

        Assert.Equal(SpendingRule.ExceedsBalanceCode, error.Code);
        Assert.DoesNotContain(_ctx.Db.Query<LedgerEntry>(), entry => entry.Type == LedgerType.Expense);
    }

    [Fact]
    public async Task Expense_equal_to_balance_is_allowed()
    {
        SeedIncome(300_000);
        var detail = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(300_000, _gift.Id), default);
        Assert.Equal(300_000, detail.Entry.Amount);
    }

    [Fact]
    public async Task Over_500k_requires_evidence_BR06()
    {
        SeedIncome(2_000_000);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(500_001, _gift.Id), default));
        Assert.Equal(EvidenceRule.RequiredCode, error.Code);

        var ok = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(500_000, _gift.Id), default);
        Assert.Empty(ok.Attachments);
    }

    [Fact]
    public async Task Future_date_is_rejected_BR09()
    {
        SeedIncome(1_000_000);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id, date: Today.AddDays(1)), default));
        Assert.Equal(LedgerErrors.FutureDate, error.Code);
    }

    [Fact]
    public async Task Income_category_or_inactive_category_is_unusable()
    {
        SeedIncome(1_000_000);
        var wrongType = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _income.Id), default));
        Assert.Equal(CategoriesErrors.Unusable, wrongType.Code);

        _gift.IsActive = false;
        var inactive = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id), default));
        Assert.Equal(CategoriesErrors.Unusable, inactive.Code);
    }

    [Fact]
    public async Task Event_expense_uses_event_balance_and_scope()
    {
        SeedIncome(400_000, _picnic.Id);

        var detail = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(400_000, _gift.Id, _picnic.Id), default);

        Assert.Equal(_picnic.Id, detail.Entry.EventId);
        Assert.Equal("Dã ngoại Ba Vì", detail.Entry.EventName);
        var log = Assert.Single(_ctx.AuditLogs, item => item.Action == LedgerAuditActions.Create);
        Assert.Equal(FinancialScope.Event, log.Scope);
        Assert.Equal(_picnic.Id, log.EventId);
    }

    [Theory]
    [InlineData(EventStatus.Completed)]
    [InlineData(EventStatus.Settled)]
    [InlineData(EventStatus.Cancelled)]
    public async Task Closed_event_accepts_no_new_expense_BR20(EventStatus status)
    {
        SeedIncome(400_000, _picnic.Id);
        _picnic.Status = status;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id, _picnic.Id), default));
        Assert.Equal(LedgerErrors.EventClosed, error.Code);
    }

    [Fact]
    public async Task Event_of_another_fund_is_not_found()
    {
        var other = new Event(Guid.NewGuid(), "Khác", null, Today, _ctx.Owner.Id, ServiceTestContext.Start);
        _ctx.Db.Seed(other);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id, other.Id), default));
    }

    [Fact]
    public async Task Archived_fund_is_read_only()
    {
        SeedIncome(1_000_000);
        _ctx.Fund.Status = FundStatus.Archived;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id), default));
        Assert.Equal(YearEndRule.FundArchivedCode, error.Code);
    }

    [Fact]
    public async Task Students_cannot_write_OQ14()
    {
        SeedIncome(1_000_000);
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id), default));
    }

    [Fact]
    public async Task Receipt_uploaded_by_someone_else_is_unusable()
    {
        SeedIncome(1_000_000);
        var receipt = SeedReceipt(_ctx.Treasurer);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id, files: receipt.Id), default));
        Assert.Equal(AttachmentsErrors.Unusable, error.Code);
    }

    [Fact]
    public async Task General_list_and_totals_exclude_event_entries_BR01()
    {
        SeedIncome(1_000_000);
        SeedIncome(700_000, _picnic.Id);
        await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(200_000, _gift.Id), default);

        var general = await Service.ListAsync(_ctx.Fund.Id, new LedgerFilter(null, null, null, null, null, 1, 20), default);
        Assert.Equal(2, general.Total);
        Assert.All(general.Items, item => Assert.Null(item.EventId));
        Assert.Equal(new LedgerSummaryDto(800_000, 1_000_000, 200_000), general.Summary);

        var picnic = await Service.ListAsync(_ctx.Fund.Id, new LedgerFilter(_picnic.Id, null, null, null, null, 1, 20), default);
        Assert.Equal(700_000, picnic.Summary.Balance);
    }

    [Fact]
    public async Task List_filters_by_type_category_and_dates()
    {
        SeedIncome(1_000_000);
        await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id, date: Today.AddDays(-10)), default);
        await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(50_000, _gift.Id), default);

        var expenses = await Service.ListAsync(_ctx.Fund.Id, new LedgerFilter(null, LedgerType.Expense, null, null, null, 1, 20), default);
        Assert.Equal(2, expenses.Total);

        var recent = await Service.ListAsync(_ctx.Fund.Id, new LedgerFilter(null, null, _gift.Id, Today.AddDays(-1), Today, 1, 20), default);
        var only = Assert.Single(recent.Items);
        Assert.Equal(50_000, only.Amount);
        // Số dư là toàn thời gian; tổng thu / chi chỉ trong khoảng ngày đang lọc (khoản chi 10 ngày trước bị loại).
        Assert.Equal(new LedgerSummaryDto(850_000, 1_000_000, 50_000), recent.Summary);
    }

    [Fact]
    public async Task Reversal_offsets_the_expense_once_BR08()
    {
        SeedIncome(1_000_000);
        var expense = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(650_000, _gift.Id, files: SeedReceipt().Id), default);

        var reversal = await Service.ReverseAsync(_ctx.Fund.Id, expense.Entry.Id, "  Ghi nhầm số tiền ", default);

        Assert.Equal(LedgerType.Income, reversal.Entry.Type);
        Assert.Equal(650_000, reversal.Entry.Amount);
        Assert.Equal(expense.Entry.Id, reversal.Entry.ReversesEntryId);
        Assert.Equal("Đảo: Mua hoa 20/11", reversal.Entry.Title);
        Assert.Equal("Ghi nhầm số tiền", reversal.Note);
        Assert.Equal(expense.Entry.Id, reversal.Reverses!.Id);
        Assert.False(reversal.CanReverse);

        var original = await Service.GetAsync(_ctx.Fund.Id, expense.Entry.Id, default);
        Assert.Equal(reversal.Entry.Id, original.ReversedBy!.Id);
        Assert.Equal("Ghi nhầm số tiền", original.ReversedBy.Reason);
        Assert.False(original.CanReverse);
        Assert.Equal([LedgerAuditActions.Create, LedgerAuditActions.Reverse], original.History.Select(item => item.Action));

        var totals = await LedgerBalances.ForScopeAsync(_ctx.Db, _ctx.Fund.Id, null, default);
        Assert.Equal(new ScopeTotals(1_000_000, 0), totals);

        var again = await Assert.ThrowsAsync<ConflictException>(() => Service.ReverseAsync(_ctx.Fund.Id, expense.Entry.Id, "lần hai", default));
        Assert.Equal(LedgerErrors.AlreadyReversed, again.Code);
    }

    [Fact]
    public async Task Only_manual_expenses_can_be_reversed()
    {
        var income = SeedIncome(1_000_000);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.ReverseAsync(_ctx.Fund.Id, income.Id, "sai", default));
        Assert.Equal(LedgerErrors.NotReversible, error.Code);

        var expense = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id), default);
        var reversal = await Service.ReverseAsync(_ctx.Fund.Id, expense.Entry.Id, "sai", default);
        var nested = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.ReverseAsync(_ctx.Fund.Id, reversal.Entry.Id, "sai", default));
        Assert.Equal(LedgerErrors.NotReversible, nested.Code);
    }

    [Fact]
    public async Task Settled_event_ledger_is_locked_for_reversal()
    {
        SeedIncome(400_000, _picnic.Id);
        var expense = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(100_000, _gift.Id, _picnic.Id), default);
        _picnic.Status = EventStatus.Settled;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service.ReverseAsync(_ctx.Fund.Id, expense.Entry.Id, "sai", default));
        Assert.Equal(LedgerErrors.ScopeLocked, error.Code);
    }

    [Fact]
    public async Task Students_see_totals_and_expenses_but_not_who_paid_OQ14()
    {
        var payment = SeedIncome(1_000_000);
        var expense = await Service.CreateExpenseAsync(_ctx.Fund.Id, Expense(200_000, _gift.Id), default);
        _ctx.ActAs(_ctx.StudentA);

        var list = await Service.ListAsync(_ctx.Fund.Id, new LedgerFilter(null, null, null, null, null, 1, 20), default);

        Assert.Equal(expense.Entry.Id, Assert.Single(list.Items).Id);
        Assert.Equal(800_000, list.Summary.Balance);
        Assert.DoesNotContain(list.Items, item => item.Source == LedgerSource.Payment);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetAsync(_ctx.Fund.Id, payment.Id, default));
        var detail = await Service.GetAsync(_ctx.Fund.Id, expense.Entry.Id, default);
        Assert.False(detail.CanReverse);
        Assert.Empty(detail.History);
    }

    [Fact]
    public async Task Event_ledger_is_visible_to_accepted_participants_only()
    {
        SeedIncome(400_000, _picnic.Id);
        _ctx.Db.Seed(
            new EventParticipant(_picnic.Id, _ctx.StudentA.Id) { Response = ParticipationResponse.Accepted },
            new EventParticipant(_picnic.Id, _ctx.StudentB.Id) { Response = ParticipationResponse.Declined });
        var filter = new LedgerFilter(_picnic.Id, null, null, null, null, 1, 20);

        _ctx.ActAs(_ctx.StudentA);
        Assert.Equal(400_000, (await Service.ListAsync(_ctx.Fund.Id, filter, default)).Summary.Balance);

        _ctx.ActAs(_ctx.StudentB);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.ListAsync(_ctx.Fund.Id, filter, default));

        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAsync<NotFoundException>(() => Service.ListAsync(_ctx.Fund.Id, filter, default));
    }

    [Fact]
    public void Long_titles_are_cut_for_reversals()
    {
        var title = LedgerService.ReversalTitle(new string('a', 150));
        Assert.Equal(LedgerService.ReversalTitleMaxLength, title.Length);
        Assert.StartsWith(LedgerService.ReversalTitlePrefix, title);
        Assert.EndsWith("…", title);
    }
}
