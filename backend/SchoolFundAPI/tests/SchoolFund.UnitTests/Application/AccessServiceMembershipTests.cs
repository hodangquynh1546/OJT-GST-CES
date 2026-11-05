using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Authorization;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;

namespace SchoolFund.UnitTests.Application;

/// <summary><see cref="AccessService.GetMembershipsAsync"/>: danh sách quỹ kèm vai trò cho <c>GET /auth/me</c>.</summary>
public class AccessServiceMembershipTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly AccessService _service;
    private readonly User _teacher;

    public AccessServiceMembershipTests()
    {
        _service = new AccessService(_db);
        _teacher = AddUser(UserKind.Teacher);
    }

    private User AddUser(UserKind kind)
    {
        var user = new User(kind, "Người dùng", "hash", $"{Guid.NewGuid():N}@example.vn", null, false, Now);
        _db.Add(user);
        return user;
    }

    private Fund AddFund(string name, User? owner = null)
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

    private ParentLink Link(Student student, User parent)
    {
        var link = new ParentLink(student.Id, parent.Id, _teacher.Id, Now);
        _db.Add(link);
        return link;
    }

    [Fact]
    public async Task Teacher_gets_every_owned_fund_sorted_by_name()
    {
        var b = AddFund("Quỹ lớp 5B");
        var a = AddFund("Quỹ lớp 5A");
        AddFund("Quỹ của người khác", AddUser(UserKind.Teacher));

        var memberships = await _service.GetMembershipsAsync(_teacher.Id, default);

        Assert.Equal([a.Id, b.Id], memberships.Select(membership => membership.FundId));
        Assert.All(memberships, membership =>
        {
            Assert.Equal(FundRole.Teacher, membership.Role);
            Assert.Equal(RolePermissions.For(FundRole.Teacher), membership.Permissions);
            Assert.Empty(membership.LinkedStudents);
        });
    }

    [Fact]
    public async Task Student_is_treasurer_only_while_assignment_is_active()
    {
        var fund = AddFund("Quỹ lớp 5A");
        var account = AddUser(UserKind.Student);
        var student = AddStudent(fund, "Khang", account);
        var assignment = new TreasurerAssignment(fund.Id, student.Id, _teacher.Id, Now);
        _db.Add(assignment);

        Assert.Equal(FundRole.Treasurer, Assert.Single(await _service.GetMembershipsAsync(account.Id, default)).Role);

        assignment.Revoke(_teacher.Id, Now);

        var membership = Assert.Single(await _service.GetMembershipsAsync(account.Id, default));
        Assert.Equal(FundRole.Student, membership.Role);
        Assert.Equal(RolePermissions.For(FundRole.Student), membership.Permissions);
    }

    [Fact]
    public async Task Parent_gets_funds_of_linked_children_with_their_names()
    {
        var fundA = AddFund("Quỹ lớp 5A");
        var fundB = AddFund("Quỹ lớp 7C", AddUser(UserKind.Teacher));
        var parent = AddUser(UserKind.Parent);
        Link(AddStudent(fundA, "Trang"), parent);
        Link(AddStudent(fundA, "Bảo"), parent);
        Link(AddStudent(fundB, "Huy"), parent);
        Link(AddStudent(fundB, "Đã gỡ"), parent).Remove(_teacher.Id, Now);

        var memberships = await _service.GetMembershipsAsync(parent.Id, default);

        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, membership => Assert.Equal(FundRole.Parent, membership.Role));
        Assert.Equal(["Bảo", "Trang"], memberships[0].LinkedStudents);
        Assert.Equal(["Huy"], memberships[1].LinkedStudents);
    }

    [Fact]
    public async Task User_without_any_role_has_no_memberships()
    {
        AddFund("Quỹ lớp 5A");

        Assert.Empty(await _service.GetMembershipsAsync(AddUser(UserKind.Parent).Id, default));
    }
}
