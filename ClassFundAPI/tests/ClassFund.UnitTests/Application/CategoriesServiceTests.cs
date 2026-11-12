using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Categories;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

/// <summary>Quy tắc của module Danh mục (M5): BR-08, BR-25, X-01, BR-37.</summary>
public class CategoriesServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly User _teacher;
    private readonly Fund _fund;
    private readonly Student _treasurerStudent;
    private readonly Student _student;

    public CategoriesServiceTests()
    {
        _teacher = AddUser(UserKind.Teacher, "Cô Hạnh");
        _fund = new Fund(_teacher.Id, "Quỹ lớp 5A", Now.AddDays(-30));
        _db.Add(_fund);
        _treasurerStudent = AddStudent("Phạm Thu Trang");
        _db.Add(new TreasurerAssignment(_fund.Id, _treasurerStudent.Id, _teacher.Id, Now.AddDays(-5)));
        _student = AddStudent("Nguyễn Minh Khang");
        SignInAs(_treasurerStudent);
    }

    private CategoriesService Service()
    {
        var clock = new FixedClock(Now);
        return new CategoriesService(_db, new AccessService(_db), _currentUser.Object, new AuditTrail(_db, new Mock<IRequestInfo>().Object, clock), clock);
    }

    private User AddUser(UserKind kind, string fullName)
    {
        var student = kind == UserKind.Student;
        var user = new User(kind, fullName, "hash", student ? null : $"{Guid.NewGuid():N}@example.vn", student ? $"hs.{Guid.NewGuid():N}"[..12] : null, false, Now);
        _db.Add(user);
        return user;
    }

    private Student AddStudent(string fullName)
    {
        var user = AddUser(UserKind.Student, fullName);
        var student = new Student(_fund.Id, fullName, _teacher.Id, Now.AddDays(-10));
        student.LinkAccount(user.Id, Now.AddDays(-9));
        _db.Add(student);
        return student;
    }

    private void SignInAs(Student student) => _currentUser.SetupGet(current => current.UserId).Returns(student.UserId);

    private Category AddCategory(TransactionType type, string name, bool hidden = false)
    {
        var category = new Category(_fund.Id, type, name, Now.AddDays(-20));
        if (hidden)
        {
            category.Update(name, true, Now.AddDays(-19));
        }

        _db.Add(category);
        return category;
    }

    private void AddTransaction(Category category, bool isPublic = true, bool cancelled = false)
    {
        var transaction = new FundTransaction(_fund.Id, category.Type, 100_000, new DateOnly(2026, 11, 1), category.Id, "Khoản", null, isPublic, _teacher.Id, Guid.NewGuid(), Now);
        if (cancelled)
        {
            transaction.Cancel("Ghi nhầm", _teacher.Id, Now);
        }

        _db.Add(transaction);
    }

    [Fact]
    public async Task Create_trims_name_and_records_audit()
    {
        var created = await Service().CreateAsync(_fund.Id, TransactionType.Income, "  Tiền quỹ hè ", CancellationToken.None);

        var category = Assert.Single(_db.Query<Category>());
        Assert.Equal((created.Id, "Tiền quỹ hè", false), (category.Id, category.Name, category.IsHidden));
        var audit = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(CategoriesAuditActions.Create, audit.Action);
        Assert.Equal(_treasurerStudent.UserId, audit.ActorUserId);
        Assert.Equal(category.Id.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Create_rejects_same_name_in_same_type_ignoring_case_but_not_in_other_type()
    {
        AddCategory(TransactionType.Expense, "Văn phòng phẩm");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().CreateAsync(_fund.Id, TransactionType.Expense, "VĂN PHÒNG PHẨM", CancellationToken.None));
        Assert.Equal(CategoriesErrors.NameTakenCode, error.Code);
        Assert.Empty(_db.Query<AuditLog>());

        await Service().CreateAsync(_fund.Id, TransactionType.Income, "Văn phòng phẩm", CancellationToken.None);
        Assert.Equal(2, _db.Query<Category>().Count());
    }

    [Fact]
    public async Task Create_unique_index_violation_is_reported_as_name_taken()
    {
        _db.SaveFailure = new UniqueConstraintException("UQ_Category_Fund_Type_Name", new InvalidOperationException());

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().CreateAsync(_fund.Id, TransactionType.Expense, "Quà", CancellationToken.None));

        Assert.Equal(CategoriesErrors.NameTakenCode, error.Code);
    }

    [Fact]
    public async Task Create_and_update_require_category_manage()
    {
        var category = AddCategory(TransactionType.Expense, "Quà");
        SignInAs(_student);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().CreateAsync(_fund.Id, TransactionType.Expense, "Mới", CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().UpdateAsync(_fund.Id, category.Id, "Đổi", true, CancellationToken.None));

        _currentUser.SetupGet(current => current.UserId).Returns(_teacher.Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().CreateAsync(_fund.Id, TransactionType.Expense, "Mới", CancellationToken.None));
        Assert.Equal("Quà", Assert.Single(_db.Query<Category>()).Name);
    }

    [Fact]
    public async Task Update_renames_hides_and_records_old_and_new_values()
    {
        var category = AddCategory(TransactionType.Expense, "Photo");

        await Service().UpdateAsync(_fund.Id, category.Id, " Photo tài liệu ", true, CancellationToken.None);

        Assert.Equal(("Photo tài liệu", true, Now), (category.Name, category.IsHidden, category.UpdatedAt));
        var audit = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(CategoriesAuditActions.Update, audit.Action);
        Assert.Contains("Photo", audit.OldValues);
        Assert.Contains("Photo tài liệu", audit.NewValues);
    }

    [Fact]
    public async Task Update_without_change_does_nothing()
    {
        var category = AddCategory(TransactionType.Expense, "Quà");

        await Service().UpdateAsync(_fund.Id, category.Id, "Quà ", false, CancellationToken.None);

        Assert.Null(category.UpdatedAt);
        Assert.Empty(_db.Query<AuditLog>());
    }

    [Fact]
    public async Task Update_rename_to_name_of_another_category_in_same_type_is_rejected()
    {
        AddCategory(TransactionType.Expense, "Đã ẩn", hidden: true);
        var category = AddCategory(TransactionType.Expense, "Quà");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().UpdateAsync(_fund.Id, category.Id, "đã ẩn", false, CancellationToken.None));

        Assert.Equal(CategoriesErrors.NameTakenCode, error.Code);
        Assert.Equal("Quà", category.Name);
    }

    [Fact]
    public async Task Update_category_of_another_fund_is_not_found()
    {
        var otherFund = new Fund(_teacher.Id, "Quỹ lớp 5B", Now);
        var foreign = new Category(otherFund.Id, TransactionType.Expense, "Quà", Now);
        _db.Add(otherFund);
        _db.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().UpdateAsync(_fund.Id, foreign.Id, "Đổi", true, CancellationToken.None));
    }

    [Fact]
    public async Task Full_view_counts_cancelled_transactions_and_hides_hidden_unless_requested()
    {
        var used = AddCategory(TransactionType.Income, "Đóng quỹ");
        AddTransaction(used);
        AddTransaction(used, isPublic: false);
        AddTransaction(used, cancelled: true);
        var hidden = AddCategory(TransactionType.Expense, "Đã ẩn", hidden: true);
        AddTransaction(hidden);

        var visible = await Service().ListAsync(_fund.Id, null, includeHidden: false, CancellationToken.None);
        var all = await Service().ListAsync(_fund.Id, null, includeHidden: true, CancellationToken.None);

        var item = Assert.Single(visible);
        Assert.Equal((3, false), (item.TransactionCount, item.IsHidden));
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all.Single(category => category.Name == "Đã ẩn").TransactionCount);
    }

    [Fact]
    public async Task Public_view_applies_x01_and_omits_internal_fields()
    {
        AddTransaction(AddCategory(TransactionType.Income, "Có khoản công khai"));
        AddTransaction(AddCategory(TransactionType.Expense, "Chỉ khoản riêng"), isPublic: false);
        AddTransaction(AddCategory(TransactionType.Expense, "Chỉ khoản công khai đã hủy"), cancelled: true);
        AddTransaction(AddCategory(TransactionType.Expense, "Đã ẩn", hidden: true));
        AddCategory(TransactionType.Expense, "Chưa dùng");
        SignInAs(_student);

        var items = await Service().ListAsync(_fund.Id, null, includeHidden: true, CancellationToken.None);

        Assert.Equal(new[] { "Chỉ khoản công khai đã hủy", "Có khoản công khai" }.Order(), items.Select(item => item.Name).Order());
        Assert.All(items, item => Assert.Equal((null, null), (item.IsHidden, item.TransactionCount)));
    }

    [Fact]
    public async Task List_for_user_outside_the_fund_is_not_found()
    {
        var outsider = AddUser(UserKind.Teacher, "Thầy B");
        _currentUser.SetupGet(current => current.UserId).Returns(outsider.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().ListAsync(_fund.Id, null, false, CancellationToken.None));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
