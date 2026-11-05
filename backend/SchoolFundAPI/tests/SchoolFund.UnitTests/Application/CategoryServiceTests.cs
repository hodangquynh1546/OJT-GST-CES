using System.Text.Json;
using Moq;
using SchoolFund.Application.Common.Auditing;
using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Application.Common.Security;
using SchoolFund.Application.Features.Categories;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;
using SchoolFund.Domain.Exceptions;

namespace SchoolFund.UnitTests.Application;

/// <summary>Quy tắc BR-08, BR-25, X-01, NFR-08 của danh mục.</summary>
public class CategoryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IAuditWriter> _audit = new();
    private readonly CategoryService _service;

    private readonly User _teacher;
    private readonly User _treasurer;
    private readonly User _student;
    private readonly User _parent;
    private readonly Fund _fund;
    private readonly Fund _otherFund;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_db, new AccessService(_db), _currentUser.Object, _audit.Object, new FixedTime());

        _teacher = AddUser(UserKind.Teacher);
        _treasurer = AddUser(UserKind.Student);
        _student = AddUser(UserKind.Student);
        _parent = AddUser(UserKind.Parent);
        _fund = AddFund();
        _otherFund = AddFund();

        var treasurerStudent = AddStudent(_treasurer);
        _db.Add(new TreasurerAssignment(_fund.Id, treasurerStudent.Id, _teacher.Id, Now));
        var child = AddStudent(_student);
        _db.Add(new ParentLink(child.Id, _parent.Id, _teacher.Id, Now));

        SignIn(_treasurer);
    }

    private User AddUser(UserKind kind)
    {
        var user = new User(kind, "Người dùng", "hash", $"{Guid.NewGuid():N}@example.vn", null, false, Now);
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

    private Category AddCategory(string name, TransactionType type = TransactionType.Expense, bool hidden = false, Fund? fund = null)
    {
        var category = new Category((fund ?? _fund).Id, type, name, Now);
        if (hidden)
        {
            category.Update(name, isHidden: true, Now);
        }

        _db.Add(category);
        return category;
    }

    private FundTransaction AddTransaction(Category category, bool isPublic = true, Fund? fund = null)
    {
        var transaction = new FundTransaction(
            (fund ?? _fund).Id,
            category.Type,
            1000,
            new DateOnly(2026, 11, 5),
            category.Id,
            "Nội dung",
            null,
            isPublic,
            _treasurer.Id,
            Guid.NewGuid(),
            Now);
        _db.Add(transaction);
        return transaction;
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    private Task<IReadOnlyList<CategoryResponse>> ListAsync(TransactionType? type = null, bool includeHidden = false) =>
        _service.ListAsync(_fund.Id, type, includeHidden, CancellationToken.None);

    private Task<CategoryCreatedResponse> CreateAsync(string name, TransactionType type = TransactionType.Expense) =>
        _service.CreateAsync(_fund.Id, type, name, CancellationToken.None);

    private Task UpdateAsync(Category category, string name, bool isHidden) =>
        _service.UpdateAsync(_fund.Id, category.Id, name, isHidden, CancellationToken.None);

    private static void VerifyNameTaken(BusinessRuleException exception) =>
        Assert.Equal(CategoryService.NameTakenCode, exception.Code);

    private void VerifyAuditCount(string action, int times) => _audit.Verify(
        audit => audit.Record(AuditCategory.Change, action, nameof(Category), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>()),
        Times.Exactly(times));

    [Fact]
    public async Task Treasurer_creates_a_category_and_it_is_audited()
    {
        var created = await CreateAsync("  Văn phòng phẩm  ");

        var category = Assert.Single(_db.Query<Category>());
        Assert.Equal(created.Id, category.Id);
        Assert.Equal(_fund.Id, category.FundId);
        Assert.Equal(TransactionType.Expense, category.Type);
        Assert.Equal("Văn phòng phẩm", category.Name);
        Assert.False(category.IsHidden);
        Assert.Equal(Now, category.CreatedAt);
        VerifyAuditCount(CategoryService.CreateAction, 1);
    }

    [Theory]
    [InlineData("Đóng quỹ")]
    [InlineData("đóng QUỸ")]
    [InlineData("  ĐÓNG QUỸ ")]
    public async Task Name_is_unique_in_fund_and_type_ignoring_case(string name)
    {
        AddCategory("Đóng quỹ", TransactionType.Income);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(name, TransactionType.Income));

        VerifyNameTaken(exception);
        Assert.Single(_db.Query<Category>());
        VerifyAuditCount(CategoryService.CreateAction, 0);
    }

    [Fact]
    public async Task Hidden_category_still_holds_its_name()
    {
        AddCategory("Photo", hidden: true);

        VerifyNameTaken(await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync("photo")));
    }

    [Fact]
    public async Task Same_name_is_allowed_in_other_type_and_other_fund()
    {
        AddCategory("Khác", TransactionType.Income);
        AddCategory("Khác", fund: _otherFund);

        await CreateAsync("Khác");

        Assert.Equal(3, _db.Query<Category>().Count());
    }

    [Fact]
    public async Task Concurrent_create_that_hits_the_unique_index_returns_name_taken()
    {
        _db.SaveFailure = new UniqueConstraintException("UQ_Category_Fund_Type_Name", new InvalidOperationException());

        VerifyNameTaken(await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync("Liên hoan")));
    }

    [Fact]
    public async Task Other_unique_violation_is_not_reported_as_name_taken()
    {
        _db.SaveFailure = new UniqueConstraintException("PK_Category", new InvalidOperationException());

        await Assert.ThrowsAsync<UniqueConstraintException>(() => CreateAsync("Liên hoan"));
    }

    [Fact]
    public async Task Treasurer_renames_a_category()
    {
        var category = AddCategory("Liên hoan");

        await UpdateAsync(category, " Sinh hoạt, liên hoan ", isHidden: false);

        Assert.Equal("Sinh hoạt, liên hoan", category.Name);
        Assert.Equal(Now, category.UpdatedAt);
        VerifyAuditCount(CategoryService.UpdateAction, 1);
    }

    [Fact]
    public async Task Treasurer_hides_and_shows_a_category_without_deleting_it()
    {
        var category = AddCategory("Photo");

        await UpdateAsync(category, "Photo", isHidden: true);
        Assert.True(category.IsHidden);
        Assert.Single(_db.Query<Category>());

        await UpdateAsync(category, "Photo", isHidden: false);
        Assert.False(category.IsHidden);
        Assert.Single(_db.Query<Category>());
        VerifyAuditCount(CategoryService.UpdateAction, 2);
    }

    [Fact]
    public async Task Category_can_change_only_the_case_of_its_own_name()
    {
        var category = AddCategory("photo");

        await UpdateAsync(category, "Photo", isHidden: false);

        Assert.Equal("Photo", category.Name);
    }

    [Fact]
    public async Task Rename_to_a_name_in_use_is_rejected()
    {
        AddCategory("Khen thưởng");
        var category = AddCategory("Photo");

        VerifyNameTaken(await Assert.ThrowsAsync<BusinessRuleException>(() => UpdateAsync(category, "KHEN THƯỞNG", false)));

        Assert.Equal("Photo", category.Name);
        Assert.Null(category.UpdatedAt);
    }

    [Fact]
    public async Task Update_without_changes_writes_nothing()
    {
        var category = AddCategory("Photo");

        await UpdateAsync(category, " Photo ", isHidden: false);

        Assert.Null(category.UpdatedAt);
        VerifyAuditCount(CategoryService.UpdateAction, 0);
    }

    [Fact]
    public async Task Category_of_another_fund_cannot_be_updated()
    {
        var foreign = AddCategory("Của quỹ khác", fund: _otherFund);

        await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(foreign, "Đã sửa", isHidden: true));

        Assert.Equal("Của quỹ khác", foreign.Name);
    }

    [Fact]
    public async Task Treasurer_sees_visible_categories_with_transaction_count()
    {
        var used = AddCategory("Photo");
        var empty = AddCategory("Khen thưởng");
        AddCategory("Đã ẩn", hidden: true);
        AddTransaction(used);
        AddTransaction(used, isPublic: false);

        var result = await ListAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Single(item => item.Id == used.Id).TransactionCount);
        Assert.Equal(0, result.Single(item => item.Id == empty.Id).TransactionCount);
    }

    [Fact]
    public async Task Include_hidden_returns_hidden_categories_to_treasurer_and_teacher()
    {
        AddCategory("Photo");
        var hidden = AddCategory("Đã ẩn", hidden: true);

        Assert.True((await ListAsync(includeHidden: true)).Single(item => item.Id == hidden.Id).IsHidden);

        SignIn(_teacher);
        Assert.Equal(2, (await ListAsync(includeHidden: true)).Count);
    }

    [Fact]
    public async Task List_filters_by_type_and_stays_inside_the_fund()
    {
        var income = AddCategory("Đóng quỹ", TransactionType.Income);
        AddCategory("Photo");
        AddCategory("Quỹ khác", TransactionType.Income, fund: _otherFund);

        var result = await ListAsync(TransactionType.Income);

        Assert.Equal(income.Id, Assert.Single(result).Id);
    }

    public static TheoryData<string> PublicViewers => new() { nameof(_parent), nameof(_student) };

    private void SignInPublicViewer(string viewer) => SignIn(viewer == nameof(_parent) ? _parent : _student);

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_viewer_sees_only_visible_categories_with_a_public_transaction(string viewer)
    {
        var visible = AddCategory("Đóng quỹ", TransactionType.Income);
        AddTransaction(visible);
        var hidden = AddCategory("Photo", hidden: true);
        AddTransaction(hidden);
        var privateOnly = AddCategory("Khen thưởng");
        AddTransaction(privateOnly, isPublic: false);
        AddCategory("Chưa dùng");
        SignInPublicViewer(viewer);

        var result = await ListAsync();

        Assert.Equal(visible.Id, Assert.Single(result).Id);
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Include_hidden_never_widens_the_public_view(string viewer)
    {
        var hidden = AddCategory("Photo", hidden: true);
        AddTransaction(hidden);
        SignInPublicViewer(viewer);

        Assert.Empty(await ListAsync(includeHidden: true));
    }

    [Theory]
    [MemberData(nameof(PublicViewers))]
    public async Task Public_json_has_no_internal_fields(string viewer)
    {
        var category = AddCategory("Đóng quỹ", TransactionType.Income);
        AddTransaction(category);
        AddTransaction(category, isPublic: false);
        SignInPublicViewer(viewer);

        var item = Assert.Single(await ListAsync());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(item, JsonSerializerOptions.Web));

        Assert.Equal(
            ["id", "name", "type"],
            json.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Full_json_has_hidden_flag_and_transaction_count()
    {
        AddCategory("Photo");

        var item = Assert.Single(await ListAsync());
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(item, JsonSerializerOptions.Web));

        Assert.False(json.RootElement.GetProperty("isHidden").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("transactionCount").GetInt32());
    }

    [Fact]
    public async Task Public_transaction_of_another_fund_does_not_reveal_a_category()
    {
        var category = AddCategory("Khen thưởng");
        AddTransaction(category, fund: _otherFund);
        SignIn(_parent);

        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task Cancelled_public_transaction_still_counts_for_public_view()
    {
        var category = AddCategory("Photo");
        var transaction = AddTransaction(category);
        typeof(FundTransaction).GetProperty(nameof(FundTransaction.IsCancelled))!.SetValue(transaction, true);
        SignIn(_parent);

        Assert.Equal(category.Id, Assert.Single(await ListAsync()).Id);
    }

    [Fact]
    public async Task Outsider_gets_not_found()
    {
        SignIn(AddUser(UserKind.Parent));

        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync());
    }
}
