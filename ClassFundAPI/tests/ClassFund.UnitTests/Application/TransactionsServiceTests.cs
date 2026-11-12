using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Transactions;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

/// <summary>Quy tắc của module Thu chi (M6): BR-02, BR-04 → BR-07, BR-11, BR-12, BR-14, NFR-08.</summary>
public class TransactionsServiceTests
{
    // 23:30 ngày 11/11 UTC = 06:30 ngày 12/11 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 23, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly VietnamToday = new(2026, 11, 12);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly User _teacher;
    private readonly User _treasurer;
    private readonly User _student;
    private readonly Fund _fund;
    private readonly Category _income;
    private readonly Category _expense;

    public TransactionsServiceTests()
    {
        _teacher = AddUser(UserKind.Teacher, "Cô Hạnh");
        _fund = new Fund(_teacher.Id, "Quỹ lớp 5A", Now.AddDays(-60));
        _db.Add(_fund);
        _income = AddCategory(TransactionType.Income, "Đóng quỹ");
        _expense = AddCategory(TransactionType.Expense, "Văn phòng phẩm");
        _treasurer = AddStudent("Nguyễn Minh Khang", treasurer: true);
        _student = AddStudent("Lê Gia Bảo", treasurer: false);
        SignIn(_treasurer);
    }

    private TransactionsService Service()
    {
        var clock = new FixedClock(Now);
        return new TransactionsService(_db, new AccessService(_db), _currentUser.Object, new AuditTrail(_db, new Mock<IRequestInfo>().Object, clock), clock);
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    private User AddUser(UserKind kind, string fullName)
    {
        var user = new User(kind, fullName, "hash", kind == UserKind.Teacher ? $"{Guid.NewGuid():N}@example.vn" : null,
            kind == UserKind.Student ? $"hs.{Guid.NewGuid():N}"[..12] : null, false, Now);
        _db.Add(user);
        return user;
    }

    private User AddStudent(string fullName, bool treasurer)
    {
        var user = AddUser(UserKind.Student, fullName);
        var student = new Student(_fund.Id, fullName, _teacher.Id, Now);
        student.LinkAccount(user.Id, Now);
        _db.Add(student);
        if (treasurer)
        {
            _db.Add(new TreasurerAssignment(_fund.Id, student.Id, _teacher.Id, Now));
        }

        return user;
    }

    private Category AddCategory(TransactionType type, string name)
    {
        var category = new Category(_fund.Id, type, name, Now);
        _db.Add(category);
        return category;
    }

    private static TransactionValues Values(Category category, long amount = 100_000, DateOnly? on = null, bool isPublic = true, string? note = null, string description = "Khoản") =>
        new(amount, on ?? VietnamToday, category.Id, description, note, isPublic);

    private Task<TransactionResponse> CreateAsync(TransactionType type, TransactionValues values, Guid? key = null) =>
        Service().CreateAsync(_fund.Id, type, values, key ?? Guid.NewGuid(), CancellationToken.None);

    private static TransactionFilter Filter(TransactionStatusFilter status = TransactionStatusFilter.Active, string? search = null) =>
        new(null, null, null, null, search, status, 1, 20);

    [Fact]
    public async Task Create_trims_text_records_creator_audit_and_allows_today_in_vietnam_time()
    {
        var saved = await CreateAsync(TransactionType.Income, Values(_income, description: "  Đóng quỹ  ", note: "   "));

        var transaction = Assert.Single(_db.Query<FundTransaction>());
        Assert.Equal("Đóng quỹ", transaction.Description);
        Assert.Null(transaction.Note);
        Assert.Equal(VietnamToday, transaction.OccurredOn);
        Assert.Equal(_treasurer.Id, transaction.CreatedBy);
        Assert.Equal("Nguyễn Minh Khang", saved.CreatedByName);
        Assert.False(saved.Replayed);
        var audit = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(TransactionsAuditActions.Create, audit.Action);
        Assert.Equal(transaction.Id.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Create_rejects_future_date_and_unusable_category()
    {
        var future = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(TransactionType.Expense, Values(_expense, on: VietnamToday.AddDays(1))));
        Assert.Equal(TransactionsErrors.FutureDateCode, future.Code);

        var wrongType = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(TransactionType.Income, Values(_expense)));
        Assert.Equal(TransactionsErrors.CategoryInvalidCode, wrongType.Code);

        _expense.Update(_expense.Name, isHidden: true, Now);
        var hidden = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(TransactionType.Expense, Values(_expense)));
        Assert.Equal(TransactionsErrors.CategoryInvalidCode, hidden.Code);
        Assert.Empty(_db.Query<FundTransaction>());
    }

    [Fact]
    public async Task Same_key_replays_the_saved_transaction()
    {
        var key = Guid.NewGuid();
        var first = await CreateAsync(TransactionType.Income, Values(_income), key);

        var again = await CreateAsync(TransactionType.Income, Values(_income, amount: 999), key);

        Assert.Equal(first.Id, again.Id);
        Assert.True(again.Replayed);
        Assert.Equal(100_000, again.Amount);
        Assert.Single(_db.Query<FundTransaction>());
    }

    [Fact]
    public async Task Expense_over_balance_is_saved_with_negative_flag_and_cancelled_items_do_not_count()
    {
        await CreateAsync(TransactionType.Income, Values(_income, amount: 100_000));
        var big = await CreateAsync(TransactionType.Expense, Values(_expense, amount: 150_000));
        Assert.True(big.IsNegativeBalance);

        var cancelled = await Service().CancelAsync(_fund.Id, big.Id, "Nhập nhầm", CancellationToken.None);

        Assert.False(cancelled.IsNegativeBalance);
    }

    [Fact]
    public async Task Update_keeps_type_records_updater_and_skips_audit_when_nothing_changes()
    {
        var created = await CreateAsync(TransactionType.Expense, Values(_expense, amount: 1_200_000));

        var same = await Service().UpdateAsync(_fund.Id, created.Id, Values(_expense, amount: 1_200_000), CancellationToken.None);
        Assert.Null(same.UpdatedByName);
        Assert.Single(_db.Query<AuditLog>());

        var updated = await Service().UpdateAsync(_fund.Id, created.Id, Values(_expense, amount: 1_250_000, isPublic: false), CancellationToken.None);
        Assert.Equal(TransactionType.Expense, updated.Type);
        Assert.Equal(1_250_000, updated.Amount);
        Assert.False(updated.IsPublic);
        Assert.Equal("Nguyễn Minh Khang", updated.UpdatedByName);
        var audit = _db.Query<AuditLog>().Single(log => log.Action == TransactionsAuditActions.Update);
        Assert.Contains("1200000", audit.OldValues);
        Assert.Contains("1250000", audit.NewValues);
    }

    [Fact]
    public async Task Hidden_category_already_used_can_be_kept_when_editing()
    {
        var created = await CreateAsync(TransactionType.Expense, Values(_expense));
        _expense.Update(_expense.Name, isHidden: true, Now);

        var updated = await Service().UpdateAsync(_fund.Id, created.Id, Values(_expense, amount: 200_000), CancellationToken.None);

        Assert.Equal(200_000, updated.Amount);
    }

    [Fact]
    public async Task Cancelled_transaction_cannot_be_cancelled_again_or_edited()
    {
        var created = await CreateAsync(TransactionType.Expense, Values(_expense));
        var cancelled = await Service().CancelAsync(_fund.Id, created.Id, "  Ghi trùng  ", CancellationToken.None);
        Assert.Equal("Ghi trùng", cancelled.CancelReason);
        Assert.Equal("Nguyễn Minh Khang", cancelled.CancelledByName);
        Assert.Equal(VietnamToday, cancelled.CancelledOn);

        var again = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().CancelAsync(_fund.Id, created.Id, "Lần 2", CancellationToken.None));
        Assert.Equal(FundTransaction.CancelledCode, again.Code);
        var edit = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().UpdateAsync(_fund.Id, created.Id, Values(_expense), CancellationToken.None));
        Assert.Equal(FundTransaction.CancelledCode, edit.Code);
    }

    [Fact]
    public async Task Public_viewer_gets_only_public_items_without_internal_fields_and_cannot_search_notes()
    {
        var publicItem = await CreateAsync(TransactionType.Income, Values(_income, description: "Đóng quỹ tháng 11", note: "Bí mật"));
        var privateItem = await CreateAsync(TransactionType.Expense, Values(_expense, isPublic: false, description: "Quà tặng"));
        SignIn(_student);

        var list = await Service().ListAsync(_fund.Id, Filter(TransactionStatusFilter.All), CancellationToken.None);

        var item = Assert.Single(list.Items);
        Assert.Equal(1, list.Total);
        Assert.Equal(publicItem.Id, item.Id);
        Assert.Null(item.Note);
        Assert.Null(item.IsPublic);
        Assert.Null(item.CreatedByName);
        Assert.Null(item.CreatedAt);
        Assert.Equal(0, (await Service().ListAsync(_fund.Id, Filter(search: "bí mật"), CancellationToken.None)).Total);
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAsync(_fund.Id, privateItem.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().GetHistoryAsync(_fund.Id, publicItem.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateAsync(TransactionType.Income, Values(_income)));
    }

    [Fact]
    public async Task Teacher_has_full_view_and_history_but_cannot_write()
    {
        var created = await CreateAsync(TransactionType.Income, Values(_income, note: "Ghi chú"));
        SignIn(_teacher);

        var detail = await Service().GetAsync(_fund.Id, created.Id, CancellationToken.None);
        Assert.Equal("Ghi chú", detail.Note);
        var history = await Service().GetHistoryAsync(_fund.Id, created.Id, CancellationToken.None);
        Assert.Equal("Nguyễn Minh Khang", Assert.Single(history).ActorName);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateAsync(TransactionType.Income, Values(_income)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().CancelAsync(_fund.Id, created.Id, "x", CancellationToken.None));
    }

    [Fact]
    public async Task List_orders_newest_first_and_pages_after_filtering()
    {
        for (var day = 1; day <= 5; day++)
        {
            await CreateAsync(TransactionType.Expense, Values(_expense, on: new DateOnly(2026, 11, day), description: $"Ngày {day}"));
        }

        await CreateAsync(TransactionType.Income, Values(_income, on: new DateOnly(2026, 11, 3), description: "Thu"));

        var page = await Service().ListAsync(
            _fund.Id, new TransactionFilter(TransactionType.Expense, null, new DateOnly(2026, 11, 2), null, null, TransactionStatusFilter.Active, 2, 2), CancellationToken.None);

        Assert.Equal(4, page.Total);
        Assert.Equal(["Ngày 3", "Ngày 2"], page.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task Unique_key_race_returns_winner_as_replay()
    {
        // Request khác cùng khóa lưu xong giữa lúc kiểm tra và lúc lưu → UNIQUE báo trùng → trả khoản đã có, không lỗi.
        _db.SaveFailure = new UniqueConstraintException("UQ_FundTransaction_FundId_IdempotencyKey", new InvalidOperationException());

        var replayed = await CreateAsync(TransactionType.Income, Values(_income));

        Assert.True(replayed.Replayed);
        Assert.Equal(Assert.Single(_db.Query<FundTransaction>()).Id, replayed.Id);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
