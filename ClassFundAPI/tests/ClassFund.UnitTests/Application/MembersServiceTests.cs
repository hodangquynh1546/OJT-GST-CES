using Moq;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Members;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.UnitTests.Application;

/// <summary>Danh sách thành viên (M5): FR-12, BR-23.</summary>
public class MembersServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly User _teacher;
    private readonly Fund _fund;

    public MembersServiceTests()
    {
        _teacher = new User(UserKind.Teacher, "Cô Hạnh", "hash", "hanh@example.vn", null, false, Now);
        _db.Add(_teacher);
        _fund = new Fund(_teacher.Id, "Quỹ lớp 5A", Now.AddDays(-30));
        _db.Add(_fund);
        _currentUser.SetupGet(current => current.UserId).Returns(_teacher.Id);
    }

    private MembersService Service() => new(_db, new AccessService(_db), _currentUser.Object);

    private Student AddStudent(string fullName, bool withAccount = true, bool treasurer = false, bool revoked = false)
    {
        var student = new Student(_fund.Id, fullName, _teacher.Id, Now.AddDays(-10));
        if (withAccount)
        {
            var user = new User(UserKind.Student, fullName, "hash", null, $"hs.{Guid.NewGuid():N}"[..12], false, Now);
            _db.Add(user);
            student.LinkAccount(user.Id, Now.AddDays(-9));
        }

        _db.Add(student);
        if (treasurer)
        {
            var assignment = new TreasurerAssignment(_fund.Id, student.Id, _teacher.Id, Now.AddDays(-5));
            if (revoked)
            {
                assignment.Revoke(_teacher.Id, Now.AddDays(-1));
            }

            _db.Add(assignment);
        }

        return student;
    }

    [Fact]
    public async Task Lists_teacher_then_treasurers_then_students_each_by_name_including_students_without_account()
    {
        AddStudent("Võ Gia Huy");
        AddStudent("Đặng An", withAccount: false);
        AddStudent("Phạm Thu Trang", treasurer: true);
        AddStudent("Lê Gia Bảo", treasurer: true, revoked: true);

        var members = await Service().ListAsync(_fund.Id, CancellationToken.None);

        Assert.Equal(
            [("Cô Hạnh", FundRole.Teacher), ("Phạm Thu Trang", FundRole.Treasurer)],
            members.Take(2).Select(member => (member.FullName, member.Role)));
        Assert.Equal(new[] { "Đặng An", "Lê Gia Bảo", "Võ Gia Huy" }.Order(StringComparer.CurrentCulture), members.Skip(2).Select(member => member.FullName));
        Assert.All(members.Skip(2), member => Assert.Equal(FundRole.Student, member.Role));
    }

    [Fact]
    public async Task Student_cannot_view_the_list()
    {
        var student = AddStudent("Nguyễn Minh Khang");
        _currentUser.SetupGet(current => current.UserId).Returns(student.UserId);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().ListAsync(_fund.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Outsider_is_not_found()
    {
        var outsider = new User(UserKind.Teacher, "Thầy B", "hash", "b@example.vn", null, false, Now);
        _db.Add(outsider);
        _currentUser.SetupGet(current => current.UserId).Returns(outsider.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().ListAsync(_fund.Id, CancellationToken.None));
    }
}
