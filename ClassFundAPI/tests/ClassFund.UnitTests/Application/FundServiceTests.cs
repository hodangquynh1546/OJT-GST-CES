using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Funds;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

public class FundServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly FundService _service;
    private readonly User _teacher;

    public FundServiceTests()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(Now);
        _service = new FundService(
            _db,
            new AccessService(_db),
            _currentUser.Object,
            new AuditTrail(_db, Mock.Of<IRequestInfo>(), clock.Object),
            clock.Object);
        _teacher = AddUser(UserKind.Teacher);
        SignIn(_teacher);
    }

    private User AddUser(UserKind kind)
    {
        var user = new User(kind, "Người dùng", "hash", $"{Guid.NewGuid():N}@example.vn", null, false, Now);
        _db.Add(user);
        return user;
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    private Fund AddFund(string name = "Quỹ lớp 5A", User? owner = null)
    {
        var fund = new Fund((owner ?? _teacher).Id, name, Now);
        _db.Add(fund);
        return fund;
    }

    private Student AddStudent(Fund fund, string name, User? account = null)
    {
        var student = new Student(fund.Id, name, fund.OwnerUserId, Now);
        if (account is not null)
        {
            student.LinkAccount(account.Id, Now);
        }

        _db.Add(student);
        return student;
    }

    private void AddTransaction(Fund fund, TransactionType type, long amount) =>
        _db.Add(new FundTransaction(fund.Id, type, amount, new DateOnly(2026, 11, 5), Guid.NewGuid(), "Khoản", null, true, _teacher.Id, Guid.NewGuid(), Now));

    private IReadOnlyList<AuditLog> Audit(string action) => _db.Query<AuditLog>().Where(log => log.Action == action).ToList();

    // ---------- Tạo quỹ ----------

    [Fact]
    public async Task Create_adds_fund_owned_by_teacher_with_six_default_categories()
    {
        var created = await _service.CreateAsync("  Quỹ lớp 5A  ", default);

        var fund = Assert.Single(_db.Query<Fund>());
        Assert.Equal(created.Id, fund.Id);
        Assert.Equal(_teacher.Id, fund.OwnerUserId);
        Assert.Equal("Quỹ lớp 5A", fund.Name);

        var categories = _db.Query<Category>().ToList();
        Assert.All(categories, category => Assert.Equal(fund.Id, category.FundId));
        Assert.Equal(["Đóng quỹ", "Thu khác"], categories.Where(c => c.Type == TransactionType.Income).Select(c => c.Name));
        Assert.Equal(
            ["Văn phòng phẩm", "Sinh hoạt, liên hoan", "Khen thưởng", "Chi khác"],
            categories.Where(c => c.Type == TransactionType.Expense).Select(c => c.Name));
        Assert.All(categories, category => Assert.False(category.IsHidden));

        var audit = Assert.Single(Audit(AuditActions.FundCreate));
        Assert.Equal(_teacher.Id, audit.ActorUserId);
        Assert.Contains("Quỹ lớp 5A", audit.NewValues);
        Assert.Empty(_db.Query<TreasurerAssignment>());
    }

    [Theory]
    [InlineData(UserKind.Student)]
    [InlineData(UserKind.Parent)]
    public async Task Create_is_forbidden_for_non_teacher_accounts(UserKind kind)
    {
        SignIn(AddUser(kind));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.CreateAsync("Quỹ lạ", default));

        Assert.Empty(_db.Query<Fund>());
        Assert.Empty(_db.Query<Category>());
    }

    // ---------- Đổi tên ----------

    [Fact]
    public async Task Rename_by_homeroom_teacher_updates_name_and_audits()
    {
        var fund = AddFund("Quỹ lớp 5A");

        await _service.RenameAsync(fund.Id, " Quỹ lớp 6A ", default);

        Assert.Equal("Quỹ lớp 6A", fund.Name);
        Assert.Equal(Now, fund.UpdatedAt);
        var audit = Assert.Single(Audit(AuditActions.FundRename));
        Assert.Contains("Quỹ lớp 5A", audit.OldValues);
        Assert.Contains("Quỹ lớp 6A", audit.NewValues);
    }

    [Fact]
    public async Task Rename_to_same_name_does_not_audit()
    {
        var fund = AddFund("Quỹ lớp 5A");

        await _service.RenameAsync(fund.Id, "Quỹ lớp 5A", default);

        Assert.Empty(Audit(AuditActions.FundRename));
        Assert.Null(fund.UpdatedAt);
    }

    [Fact]
    public async Task Rename_by_treasurer_is_forbidden()
    {
        var fund = AddFund();
        var account = AddUser(UserKind.Student);
        var student = AddStudent(fund, "Trang", account);
        _db.Add(new TreasurerAssignment(fund.Id, student.Id, _teacher.Id, Now));
        SignIn(account);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.RenameAsync(fund.Id, "Đổi trộm", default));

        Assert.Equal("Quỹ lớp 5A", fund.Name);
    }

    [Fact]
    public async Task Rename_by_teacher_of_another_fund_is_not_found()
    {
        var fund = AddFund();
        SignIn(AddUser(UserKind.Teacher));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RenameAsync(fund.Id, "Đổi trộm", default));
    }

    // ---------- Danh sách ----------

    [Fact]
    public async Task List_computes_balance_and_treasurer_status_per_fund()
    {
        var withTreasurer = AddFund("Quỹ lớp 5A");
        var account = AddUser(UserKind.Student);
        var trang = AddStudent(withTreasurer, "Trang", account);
        AddStudent(withTreasurer, "Bảo");
        _db.Add(new TreasurerAssignment(withTreasurer.Id, trang.Id, _teacher.Id, Now));
        AddTransaction(withTreasurer, TransactionType.Income, 500_000);
        AddTransaction(withTreasurer, TransactionType.Expense, 120_000);
        var empty = AddFund("Quỹ lớp 5B");

        var funds = await _service.ListMineAsync(default);

        Assert.Equal([withTreasurer.Id, empty.Id], funds.Select(fund => fund.Id));
        Assert.Equal((380_000L, TreasurerStatus.Assigned, 2), (funds[0].CurrentBalance, funds[0].TreasurerStatus, funds[0].StudentCount));
        Assert.Equal(["Trang"], funds[0].TreasurerNames);
        Assert.Equal((0L, TreasurerStatus.Unassigned, 0), (funds[1].CurrentBalance, funds[1].TreasurerStatus, funds[1].StudentCount));
        Assert.All(funds, fund =>
        {
            Assert.Equal(FundRole.Teacher, fund.Role);
            Assert.Null(fund.LinkedStudents);
        });
    }

    [Fact]
    public async Task List_for_parent_has_children_but_no_student_count()
    {
        var fund = AddFund();
        var parent = AddUser(UserKind.Parent);
        _db.Add(new ParentLink(AddStudent(fund, "Bảo").Id, parent.Id, _teacher.Id, Now));
        SignIn(parent);

        var summary = Assert.Single(await _service.ListMineAsync(default));

        Assert.Equal(FundRole.Parent, summary.Role);
        Assert.Equal(["Bảo"], summary.LinkedStudents);
        Assert.Null(summary.StudentCount);
        Assert.Equal(RolePermissions.For(FundRole.Parent).Order(StringComparer.Ordinal), summary.Permissions);
    }

    [Fact]
    public async Task List_is_empty_without_any_role()
    {
        AddFund(owner: AddUser(UserKind.Teacher));

        Assert.Empty(await _service.ListMineAsync(default));
    }
}
