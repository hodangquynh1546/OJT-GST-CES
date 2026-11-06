using System.Net;
using System.Text.Json;
using Moq;
using SchoolFund.Application.Common.Auditing;
using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Paging;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Application.Common.Security;
using SchoolFund.Application.Features.Transactions;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;
using SchoolFund.Domain.Exceptions;

namespace SchoolFund.UnitTests.Application;

/// <summary>Quy tắc BR-01 → BR-08, BR-12, BR-14, D-18, NFR-08 của sổ thu chi.</summary>
public class TransactionServiceTests
{
    // 2026-11-05 18:00 UTC = 2026-11-06 01:00 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly TodayInVietnam = new(2026, 11, 6);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NoRequestInfo : IRequestInfo
    {
        public string? CorrelationId => null;

        public IPAddress? IpAddress => null;
    }

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TransactionService _service;

    private readonly User _teacher;
    private readonly User _treasurer;
    private readonly User _student;
    private readonly User _parent;
    private readonly Fund _fund;
    private readonly Fund _otherFund;
    private readonly Category _income;
    private readonly Category _expense;

    public TransactionServiceTests()
    {
        var time = new FixedTime();
        _service = new TransactionService(
            _db,
            new AccessService(_db),
            _currentUser.Object,
            new AuditTrail(_db, new NoRequestInfo(), time),
            time);

        _teacher = AddUser(UserKind.Teacher, "Cô Hạnh");
        _treasurer = AddUser(UserKind.Student, "Minh Khang");
        _student = AddUser(UserKind.Student, "Bảo Anh");
        _parent = AddUser(UserKind.Parent, "Phụ huynh");
        _fund = AddFund();
        _otherFund = AddFund();
        _income = AddCategory("Đóng quỹ", TransactionType.Income);
        _expense = AddCategory("Photo", TransactionType.Expense);

        var treasurerStudent = AddStudent(_treasurer);
        _db.Add(new TreasurerAssignment(_fund.Id, treasurerStudent.Id, _teacher.Id, Now));
        var child = AddStudent(_student);
        _db.Add(new ParentLink(child.Id, _parent.Id, _teacher.Id, Now));

        SignIn(_treasurer);
    }

    public static TheoryData<string> PublicViewers => new() { nameof(_parent), nameof(_student) };

    private User AddUser(UserKind kind, string name)
    {
        var user = new User(kind, name, "hash", $"{Guid.NewGuid():N}@example.vn", null, false, Now);
        _db.Add(user);
        return user;
    }

    private Fund AddFund()
    {
        var fund = new Fund(_teacher.Id, "Quỹ lớp", Now);
        _db.Add(fund);
        return fund;
    }

    private Student AddStudent(User account)
    {
        var student = new Student(_fund.Id, "Học sinh", _teacher.Id, Now);
        student.LinkAccount(account.Id, Now);
        _db.Add(student);
        return student;
    }

    private Category AddCategory(string name, TransactionType type, bool hidden = false, Fund? fund = null)
    {
        var category = new Category((fund ?? _fund).Id, type, name, Now);
        if (hidden)
        {
            category.Update(name, isHidden: true, Now);
        }

        _db.Add(category);
        return category;
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    private void SignInPublicViewer(string viewer) => SignIn(viewer == nameof(_parent) ? _parent : _student);

    private static TransactionValues Values(
        Category category,
        long amount = 100_000,
        string description = "Thu tháng 11",
        string? note = null,
        bool isPublic = true,
        DateOnly? occurredOn = null) =>
        new(amount, occurredOn ?? new DateOnly(2026, 11, 5), category.Id, description, note, isPublic);

    private Task<TransactionResponse> CreateAsync(TransactionValues values, TransactionType type = TransactionType.Income, Guid? key = null) =>
        _service.CreateAsync(_fund.Id, type, values, key ?? Guid.NewGuid(), CancellationToken.None);

    private Task<TransactionResponse> CreateIncomeAsync(
        string description = "Thu tháng 11",
        string? note = null,
        bool isPublic = true,
        long amount = 100_000) =>
        CreateAsync(Values(_income, amount, description, note, isPublic));

    private Task<TransactionResponse> CreateExpenseAsync(long amount) =>
        CreateAsync(Values(_expense, amount), TransactionType.Expense);

    private Task<TransactionResponse> UpdateAsync(TransactionResponse transaction, TransactionValues values) =>
        _service.UpdateAsync(_fund.Id, transaction.Id, values, CancellationToken.None);

    private Task<TransactionResponse> CancelAsync(TransactionResponse transaction, string reason = "Nhập nhầm") =>
        _service.CancelAsync(_fund.Id, transaction.Id, reason, CancellationToken.None);

    private static TransactionFilter Filter(
        TransactionStatusFilter status = TransactionStatusFilter.Active,
        TransactionType? type = null,
        Guid? categoryId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        string? search = null,
        int page = 1,
        int pageSize = 20) =>
        new(type, categoryId, from, to, search, status, page, pageSize);

    private Task<PagedResult<TransactionResponse>> ListAsync(TransactionFilter? filter = null) =>
        _service.ListAsync(_fund.Id, filter ?? Filter(), CancellationToken.None);

    private int AuditCount(string action) => _db.Query<AuditLog>().Count(log => log.Action == action);

    private static async Task VerifyRuleAsync(string code, Func<Task> act) =>
        Assert.Equal(code, (await Assert.ThrowsAsync<BusinessRuleException>(act)).Code);

    private static string[] JsonFields(TransactionResponse response)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonSerializerOptions.Web));
        return json.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public async Task Treasurer_creates_a_transaction_with_trimmed_text_and_audit()
    {
        var created = await CreateAsync(Values(_income, description: "  Thu tháng 11  ", note: "  Ghi chú  "));

        Assert.False(created.Replayed);
        Assert.False(created.IsNegativeBalance);
        Assert.Equal("Thu tháng 11", created.Description);
        Assert.Equal("Ghi chú", created.Note);
        Assert.True(created.IsPublic);
        Assert.Equal("Đóng quỹ", created.CategoryName);
        Assert.Equal("Minh Khang", created.CreatedByName);
        Assert.Equal(1, AuditCount("Income.Create"));
        Assert.Equal(_treasurer.Id, _db.Query<AuditLog>().Single().ActorUserId);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_first_transaction_without_saving_again()
    {
        var key = Guid.NewGuid();
        var first = await CreateAsync(Values(_income, amount: 100_000), key: key);

        var replay = await CreateAsync(Values(_income, amount: 999_999), key: key);

        Assert.True(replay.Replayed);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(100_000, replay.Amount);
        Assert.Single(_db.Query<FundTransaction>());
        Assert.Equal(1, AuditCount("Income.Create"));
    }

    [Fact]
    public async Task Same_key_in_another_fund_is_a_new_transaction()
    {
        var key = Guid.NewGuid();
        _db.Add(new FundTransaction(_otherFund.Id, TransactionType.Income, 1, new DateOnly(2026, 11, 1), Guid.NewGuid(), "Khác", null, true, _teacher.Id, key, Now));

        var created = await CreateAsync(Values(_income), key: key);

        Assert.False(created.Replayed);
    }

    [Fact]
    public async Task Concurrent_request_that_hits_the_unique_index_is_answered_as_a_replay()
    {
        var key = Guid.NewGuid();
        var winner = new FundTransaction(_fund.Id, TransactionType.Income, 500, new DateOnly(2026, 11, 1), _income.Id, "Đã lưu", null, true, _treasurer.Id, key, Now);
        var db = new RacingDb(_db, winner);
        var time = new FixedTime();
        var service = new TransactionService(db, new AccessService(_db), _currentUser.Object, new AuditTrail(db, new NoRequestInfo(), time), time);

        var result = await service.CreateAsync(_fund.Id, TransactionType.Income, Values(_income), key, CancellationToken.None);

        Assert.True(result.Replayed);
        Assert.Equal(winner.Id, result.Id);
    }

    [Fact]
    public async Task Expense_over_balance_is_saved_with_a_warning()
    {
        await CreateIncomeAsync(amount: 100_000);

        var created = await CreateExpenseAsync(150_000);

        Assert.True(created.IsNegativeBalance);
        Assert.Equal(2, _db.Query<FundTransaction>().Count());
    }

    [Fact]
    public async Task Cancelled_transactions_do_not_count_in_the_balance()
    {
        var income = await CreateIncomeAsync(amount: 100_000);
        var expense = await CreateExpenseAsync(150_000);

        var cancelled = await CancelAsync(expense);
        Assert.False(cancelled.IsNegativeBalance);

        await CreateExpenseAsync(50_000);
        Assert.True((await CancelAsync(income)).IsNegativeBalance);
    }

    [Fact]
    public async Task Date_is_checked_in_vietnam_time()
    {
        var today = await CreateAsync(Values(_income, occurredOn: TodayInVietnam));
        Assert.Equal(TodayInVietnam, today.OccurredOn);

        await VerifyRuleAsync(
            TransactionService.FutureDateCode,
            () => CreateAsync(Values(_income, occurredOn: TodayInVietnam.AddDays(1))));
    }

    [Fact]
    public async Task Category_must_belong_to_the_fund_and_match_the_type()
    {
        var foreign = AddCategory("Quỹ khác", TransactionType.Income, fund: _otherFund);

        await VerifyRuleAsync(TransactionService.CategoryInvalidCode, () => CreateAsync(Values(foreign)));
        await VerifyRuleAsync(TransactionService.CategoryInvalidCode, () => CreateAsync(Values(_expense), TransactionType.Income));
        Assert.Empty(_db.Query<FundTransaction>());
    }

    [Fact]
    public async Task Hidden_category_cannot_be_chosen_but_existing_transaction_keeps_it()
    {
        var created = await CreateIncomeAsync();
        _income.Update(_income.Name, isHidden: true, Now);

        await VerifyRuleAsync(TransactionService.CategoryInvalidCode, () => CreateIncomeAsync());

        var updated = await UpdateAsync(created, Values(_income, amount: 200_000));
        Assert.Equal(200_000, updated.Amount);

        var other = AddCategory("Thu khác", TransactionType.Income);
        var hidden = AddCategory("Đã ẩn", TransactionType.Income, hidden: true);
        await UpdateAsync(created, Values(other));
        await VerifyRuleAsync(TransactionService.CategoryInvalidCode, () => UpdateAsync(created, Values(hidden)));
    }

    [Fact]
    public async Task Update_changes_values_records_who_and_keeps_old_and_new_in_audit()
    {
        var created = await CreateIncomeAsync();
        var otherCategory = AddCategory("Thu khác", TransactionType.Income);

        var updated = await UpdateAsync(created, Values(otherCategory, 250_000, "Đã sửa", "Ghi chú", isPublic: false));

        Assert.Equal(250_000, updated.Amount);
        Assert.Equal("Thu khác", updated.CategoryName);
        Assert.Equal("Đã sửa", updated.Description);
        Assert.Equal("Ghi chú", updated.Note);
        Assert.False(updated.IsPublic);
        Assert.Equal("Minh Khang", updated.UpdatedByName);
        Assert.Equal(Now, updated.UpdatedAt);

        var log = _db.Query<AuditLog>().Single(item => item.Action == "Income.Update");
        Assert.Contains("100000", log.OldValues);
        Assert.Contains("250000", log.NewValues);
    }

    [Fact]
    public async Task Update_never_changes_the_type()
    {
        var created = await CreateIncomeAsync();

        await VerifyRuleAsync(TransactionService.CategoryInvalidCode, () => UpdateAsync(created, Values(_expense)));

        Assert.Equal(TransactionType.Income, _db.Query<FundTransaction>().Single().Type);
    }

    [Fact]
    public async Task Update_without_changes_writes_nothing()
    {
        var created = await CreateIncomeAsync();

        var result = await UpdateAsync(created, Values(_income, description: " Thu tháng 11 "));

        Assert.Null(result.UpdatedAt);
        Assert.Equal(0, AuditCount("Income.Update"));
    }

    [Fact]
    public async Task Update_to_a_future_date_is_rejected()
    {
        var created = await CreateIncomeAsync();

        await VerifyRuleAsync(
            TransactionService.FutureDateCode,
            () => UpdateAsync(created, Values(_income, occurredOn: TodayInVietnam.AddDays(1))));
    }

    [Fact]
    public async Task Cancel_stores_reason_and_who_and_keeps_the_row()
    {
        var created = await CreateIncomeAsync();

        var cancelled = await CancelAsync(created, "  Nhập nhầm  ");

        Assert.True(cancelled.IsCancelled);
        Assert.Equal("Nhập nhầm", cancelled.CancelReason);
        Assert.Equal("Minh Khang", cancelled.CancelledByName);
        Assert.Equal(TodayInVietnam, cancelled.CancelledOn);
        Assert.Single(_db.Query<FundTransaction>());
        Assert.Equal(1, AuditCount("Income.Cancel"));
    }

    [Fact]
    public async Task Cancel_without_reason_is_rejected()
    {
        var created = await CreateIncomeAsync();

        await VerifyRuleAsync(FundTransaction.CancelReasonRequiredCode, () => CancelAsync(created, "   "));

        Assert.False(_db.Query<FundTransaction>().Single().IsCancelled);
    }

    [Fact]
    public async Task Cancelled_transaction_cannot_be_changed_or_cancelled_again()
    {
        var created = await CreateIncomeAsync();
        await CancelAsync(created);

        await VerifyRuleAsync(FundTransaction.CancelledCode, () => UpdateAsync(created, Values(_income, amount: 5)));
        await VerifyRuleAsync(FundTransaction.CancelledCode, () => CancelAsync(created, "Lần hai"));

        Assert.Equal(1, AuditCount("Income.Cancel"));
    }

    [Fact]
    public async Task Writes_to_a_transaction_of_another_fund_are_not_found()
    {
        var created = await CreateIncomeAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(_otherFund.Id, created.Id, Values(_income), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CancelAsync(_otherFund.Id, created.Id, "Lý do", CancellationToken.None));
    }

    [Fact]
    public async Task List_returns_active_transactions_by_default_newest_first()
    {
        var old = await CreateAsync(Values(_income, occurredOn: new DateOnly(2026, 10, 1)));
        var recent = await CreateAsync(Values(_income, occurredOn: new DateOnly(2026, 11, 1)));
        var cancelled = await CreateIncomeAsync();
        await CancelAsync(cancelled);

        var result = await ListAsync();

        Assert.Equal([recent.Id, old.Id], result.Items.Select(item => item.Id));
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task List_filters_by_status_type_category_and_dates()
    {
        var income = await CreateAsync(Values(_income, occurredOn: new DateOnly(2026, 10, 1)));
        var expense = await CreateAsync(Values(_expense, occurredOn: new DateOnly(2026, 11, 1)), TransactionType.Expense);
        await CancelAsync(income);

        Assert.Equal(income.Id, Assert.Single((await ListAsync(Filter(TransactionStatusFilter.Cancelled))).Items).Id);
        Assert.Equal(2, (await ListAsync(Filter(TransactionStatusFilter.All))).Total);
        Assert.Equal(expense.Id, Assert.Single((await ListAsync(Filter(type: TransactionType.Expense))).Items).Id);
        Assert.Equal(expense.Id, Assert.Single((await ListAsync(Filter(categoryId: _expense.Id))).Items).Id);
        Assert.Equal(expense.Id, Assert.Single((await ListAsync(Filter(TransactionStatusFilter.All, from: new DateOnly(2026, 10, 15)))).Items).Id);
        Assert.Equal(income.Id, Assert.Single((await ListAsync(Filter(TransactionStatusFilter.All, to: new DateOnly(2026, 10, 15)))).Items).Id);
    }

    [Fact]
    public async Task List_pages_and_reports_the_total()
    {
        for (var day = 1; day <= 5; day++)
        {
            await CreateAsync(Values(_income, occurredOn: new DateOnly(2026, 11, day)));
        }

        var result = await ListAsync(Filter(page: 2, pageSize: 2));

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal([new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 2)], result.Items.Select(item => item.OccurredOn));
    }

    [Fact]
    public async Task Full_view_searches_description_and_note_ignoring_case()
    {
        var byDescription = await CreateIncomeAsync(description: "Mua PHONG BÌ");
        var byNote = await CreateIncomeAsync(description: "Khác", note: "phong bì cô giáo");
        await CreateIncomeAsync(description: "Không liên quan");

        var result = await ListAsync(Filter(search: " phong bì "));

        Assert.Equal(
            new HashSet<Guid> { byDescription.Id, byNote.Id },
            result.Items.Select(item => item.Id).ToHashSet());
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_list_hides_private_transactions_before_counting_and_paging(string viewer)
    {
        var shown = await CreateIncomeAsync("Công khai");
        await CreateIncomeAsync("Hoa và phong bì", isPublic: false);
        await CreateIncomeAsync("Khoản riêng", isPublic: false);
        SignInPublicViewer(viewer);

        var result = await ListAsync(Filter(TransactionStatusFilter.All, pageSize: 1));

        Assert.Equal(1, result.Total);
        Assert.Equal(shown.Id, Assert.Single(result.Items).Id);
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_search_never_matches_notes_or_private_transactions(string viewer)
    {
        await CreateIncomeAsync("Công khai", note: "phong bì");
        await CreateIncomeAsync("Hoa và phong bì", isPublic: false);
        SignInPublicViewer(viewer);

        var result = await ListAsync(Filter(TransactionStatusFilter.All, search: "phong bì"));

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_json_has_only_the_allowed_fields(string viewer)
    {
        await CreateIncomeAsync("Công khai", note: "Ghi chú nội bộ");
        SignInPublicViewer(viewer);

        var item = Assert.Single((await ListAsync()).Items);

        Assert.Equal(
            ["amount", "categoryId", "categoryName", "description", "id", "isCancelled", "occurredOn", "type"],
            JsonFields(item));
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_viewer_sees_a_cancelled_public_transaction_with_only_status_and_date(string viewer)
    {
        var created = await CreateIncomeAsync("Công khai");
        await CancelAsync(created, "Lý do nội bộ");
        var privateOne = await CreateIncomeAsync("Khoản riêng", isPublic: false);
        await CancelAsync(privateOne);
        SignInPublicViewer(viewer);

        var item = Assert.Single((await ListAsync(Filter(TransactionStatusFilter.Cancelled))).Items);

        Assert.True(item.IsCancelled);
        Assert.Equal(TodayInVietnam, item.CancelledOn);
        Assert.Equal(
            ["amount", "cancelledOn", "categoryId", "categoryName", "description", "id", "isCancelled", "occurredOn", "type"],
            JsonFields(item));
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Private_transaction_detail_is_not_found_for_public_viewer(string viewer)
    {
        var hidden = await CreateIncomeAsync(isPublic: false);
        var shown = await CreateIncomeAsync("Công khai", note: "Ghi chú nội bộ");
        SignInPublicViewer(viewer);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_fund.Id, hidden.Id, CancellationToken.None));

        var detail = await _service.GetAsync(_fund.Id, shown.Id, CancellationToken.None);
        Assert.Null(detail.Note);
        Assert.Null(detail.CreatedByName);
    }

    [Fact]
    public async Task Teacher_sees_full_detail_of_a_private_transaction()
    {
        var hidden = await CreateIncomeAsync(note: "Ghi chú", isPublic: false);
        SignIn(_teacher);

        var detail = await _service.GetAsync(_fund.Id, hidden.Id, CancellationToken.None);

        Assert.Equal("Ghi chú", detail.Note);
        Assert.Equal("Minh Khang", detail.CreatedByName);
    }

    [Fact]
    public async Task Outsider_gets_not_found()
    {
        var created = await CreateIncomeAsync();
        SignIn(AddUser(UserKind.Parent, "Người ngoài"));

        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_fund.Id, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task History_lists_changes_in_order_with_actor_and_values()
    {
        var created = await CreateIncomeAsync();
        await UpdateAsync(created, Values(_income, amount: 200_000));
        await CancelAsync(created, "Nhập nhầm");

        var history = await _service.GetHistoryAsync(_fund.Id, created.Id, CancellationToken.None);

        Assert.Equal(["Income.Create", "Income.Update", "Income.Cancel"], history.Select(item => item.Action));
        Assert.All(history, item => Assert.Equal("Minh Khang", item.ActorName));
        Assert.Null(history[0].OldValues);
        Assert.Equal(100_000, history[1].OldValues!.Value.GetProperty("amount").GetInt64());
        Assert.Equal(200_000, history[1].NewValues!.Value.GetProperty("amount").GetInt64());
        Assert.Equal("Nhập nhầm", history[2].NewValues!.Value.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task History_of_a_transaction_in_another_fund_is_not_found()
    {
        var created = await CreateIncomeAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetHistoryAsync(_otherFund.Id, created.Id, CancellationToken.None));
    }

    /// <summary>Giả lập request khác lưu cùng khóa ngay trước lần SaveChanges của request này.</summary>
    private sealed class RacingDb(FakeAppDb inner, FundTransaction winner) : IAppDb
    {
        private bool _saved;

        public IQueryable<T> Query<T>() where T : class => inner.Query<T>();

        public void Add<T>(T entity) where T : class
        {
            if (entity is not FundTransaction and not AuditLog)
            {
                inner.Add(entity);
            }
        }

        public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => inner.ToListAsync(query, cancellationToken);

        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => inner.FirstOrDefaultAsync(query, cancellationToken);

        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => inner.CountAsync(query, cancellationToken);

        public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => inner.AnyAsync(query, cancellationToken);

        public Task<T?> LockAsync<T>(Guid id, CancellationToken cancellationToken) where T : SchoolFund.Domain.Common.Entity =>
            inner.LockAsync<T>(id, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (!_saved)
            {
                _saved = true;
                inner.Add(winner);
            }

            throw new UniqueConstraintException("UQ_FundTransaction_FundId_IdempotencyKey", new InvalidOperationException());
        }

        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
            inner.InTransactionAsync(work, cancellationToken);
    }
}
