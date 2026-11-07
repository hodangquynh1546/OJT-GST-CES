using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

/// <summary>Thứ tự suy ra vai trò theo docs/business-analysis/roles-and-permissions.md mục 1.</summary>
public class AccessServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly AccessService _service;
    private readonly User _teacher;
    private readonly Fund _fund;
    private readonly Fund _otherFund;

    public AccessServiceTests()
    {
        _service = new AccessService(_db);
        _teacher = AddUser(UserKind.Teacher);
        _fund = AddFund(_teacher);
        _otherFund = AddFund(AddUser(UserKind.Teacher));
    }

    private User AddUser(UserKind kind)
    {
        var user = new User(kind, "Người dùng", "hash", $"{Guid.NewGuid():N}@example.vn", null, false, Now);
        _db.Add(user);
        return user;
    }

    private Fund AddFund(User owner)
    {
        var fund = new Fund(owner.Id, "Quỹ lớp", Now);
        _db.Add(fund);
        return fund;
    }

    private Student AddStudent(Fund fund, User? account = null)
    {
        var student = new Student(fund.Id, "Học sinh", fund.OwnerUserId, Now);
        if (account is not null)
        {
            student.LinkAccount(account.Id, Now);
        }

        _db.Add(student);
        return student;
    }

    private TreasurerAssignment Assign(Student student)
    {
        var assignment = new TreasurerAssignment(student.FundId, student.Id, _teacher.Id, Now);
        _db.Add(assignment);
        return assignment;
    }

    private ParentLink LinkParent(Student student, User parent)
    {
        var link = new ParentLink(student.Id, parent.Id, _teacher.Id, Now);
        _db.Add(link);
        return link;
    }

    private Task<FundAccess> AccessAsync(User user, Fund fund) =>
        _service.GetFundAccessAsync(user.Id, fund.Id, CancellationToken.None);

    [Fact]
    public async Task Fund_owner_is_the_teacher()
    {
        var access = await AccessAsync(_teacher, _fund);

        Assert.Equal(FundRole.Teacher, access.Role);
        Assert.Equal(_fund.Id, access.FundId);
        Assert.Equal(RolePermissions.For(FundRole.Teacher), access.Permissions);
    }

    [Fact]
    public async Task Owner_outranks_every_other_relation()
    {
        var student = AddStudent(_fund, _teacher);
        Assign(student);
        LinkParent(student, _teacher);

        var access = await AccessAsync(_teacher, _fund);

        Assert.Equal(FundRole.Teacher, access.Role);
    }

    [Fact]
    public async Task Student_with_an_active_assignment_is_the_treasurer()
    {
        var account = AddUser(UserKind.Student);
        Assign(AddStudent(_fund, account));

        var access = await AccessAsync(account, _fund);

        Assert.Equal(FundRole.Treasurer, access.Role);
        Assert.Equal(RolePermissions.For(FundRole.Treasurer), access.Permissions);
    }

    [Fact]
    public async Task Student_without_an_assignment_is_a_student()
    {
        var account = AddUser(UserKind.Student);
        AddStudent(_fund, account);

        var access = await AccessAsync(account, _fund);

        Assert.Equal(FundRole.Student, access.Role);
        Assert.Equal(RolePermissions.For(FundRole.Student), access.Permissions);
    }

    [Fact]
    public async Task Revoked_treasurer_falls_back_to_student()
    {
        var account = AddUser(UserKind.Student);
        Assign(AddStudent(_fund, account)).Revoke(_teacher.Id, Now);

        var access = await AccessAsync(account, _fund);

        Assert.Equal(FundRole.Student, access.Role);
    }

    [Fact]
    public async Task Reassigned_treasurer_with_old_revoked_history_is_the_treasurer()
    {
        var account = AddUser(UserKind.Student);
        var student = AddStudent(_fund, account);
        Assign(student).Revoke(_teacher.Id, Now);
        Assign(student);

        var access = await AccessAsync(account, _fund);

        Assert.Equal(FundRole.Treasurer, access.Role);
    }

    [Fact]
    public async Task Another_student_being_treasurer_does_not_promote_this_student()
    {
        var account = AddUser(UserKind.Student);
        AddStudent(_fund, account);
        Assign(AddStudent(_fund, AddUser(UserKind.Student)));

        var access = await AccessAsync(account, _fund);

        Assert.Equal(FundRole.Student, access.Role);
    }

    [Fact]
    public async Task Linked_parent_is_a_parent()
    {
        var parent = AddUser(UserKind.Parent);
        LinkParent(AddStudent(_fund), parent);

        var access = await AccessAsync(parent, _fund);

        Assert.Equal(FundRole.Parent, access.Role);
        Assert.Equal(RolePermissions.For(FundRole.Parent), access.Permissions);
    }

    [Fact]
    public async Task Parent_of_a_treasurer_stays_a_parent()
    {
        var parent = AddUser(UserKind.Parent);
        var child = AddStudent(_fund, AddUser(UserKind.Student));
        Assign(child);
        LinkParent(child, parent);

        var access = await AccessAsync(parent, _fund);

        Assert.Equal(FundRole.Parent, access.Role);
        Assert.DoesNotContain(Permissions.TransactionManage, access.Permissions);
    }

    [Fact]
    public async Task Parent_with_children_in_two_funds_is_a_parent_in_both()
    {
        var parent = AddUser(UserKind.Parent);
        LinkParent(AddStudent(_fund), parent);
        LinkParent(AddStudent(_otherFund), parent);

        Assert.Equal(FundRole.Parent, (await AccessAsync(parent, _fund)).Role);
        Assert.Equal(FundRole.Parent, (await AccessAsync(parent, _otherFund)).Role);
    }

    [Fact]
    public async Task Removed_parent_link_gives_not_found()
    {
        var parent = AddUser(UserKind.Parent);
        LinkParent(AddStudent(_fund), parent).Remove(_teacher.Id, Now);

        await Assert.ThrowsAsync<NotFoundException>(() => AccessAsync(parent, _fund));
    }

    [Fact]
    public async Task Parent_of_a_child_in_another_fund_gives_not_found()
    {
        var parent = AddUser(UserKind.Parent);
        LinkParent(AddStudent(_otherFund), parent);

        await Assert.ThrowsAsync<NotFoundException>(() => AccessAsync(parent, _fund));
    }

    [Fact]
    public async Task Student_of_another_fund_gives_not_found()
    {
        var account = AddUser(UserKind.Student);
        Assign(AddStudent(_otherFund, account));

        await Assert.ThrowsAsync<NotFoundException>(() => AccessAsync(account, _fund));
    }

    [Fact]
    public async Task Teacher_of_another_fund_gives_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => AccessAsync(_teacher, _otherFund));
    }

    [Fact]
    public async Task Unknown_fund_gives_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetFundAccessAsync(_teacher.Id, Guid.CreateVersion7(), CancellationToken.None));
    }

    [Fact]
    public async Task Only_teachers_have_the_fund_create_permission()
    {
        var teacher = await _service.GetAccountPermissionsAsync(_teacher.Id, CancellationToken.None);
        var student = await _service.GetAccountPermissionsAsync(AddUser(UserKind.Student).Id, CancellationToken.None);
        var parent = await _service.GetAccountPermissionsAsync(AddUser(UserKind.Parent).Id, CancellationToken.None);
        var unknown = await _service.GetAccountPermissionsAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Contains(Permissions.FundCreate, teacher);
        Assert.Empty(student);
        Assert.Empty(parent);
        Assert.Empty(unknown);
    }
}
