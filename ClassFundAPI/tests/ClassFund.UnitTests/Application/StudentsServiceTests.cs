using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Students;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

public class StudentsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly StudentsService _service;
    private readonly User _teacher;
    private readonly Fund _fund;

    public StudentsServiceTests()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(Now);
        _service = new StudentsService(
            _db,
            new AccessService(_db),
            _currentUser.Object,
            new FakePasswordHasher(),
            new AuditTrail(_db, Mock.Of<IRequestInfo>(), clock.Object),
            clock.Object);
        _teacher = AddUser(UserKind.Teacher, "co.hanh@example.vn");
        _fund = new Fund(_teacher.Id, "Quỹ lớp 5A", Now);
        _db.Add(_fund);
        SignIn(_teacher);
    }

    private User AddUser(UserKind kind, string? email = null, string? username = null)
    {
        var user = new User(kind, "Người dùng", "hash", email, username, false, Now);
        _db.Add(user);
        return user;
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    private Student AddStudent(string name = "Phạm Đức Huy")
    {
        var student = new Student(_fund.Id, name, _teacher.Id, Now);
        _db.Add(student);
        return student;
    }

    private IReadOnlyList<AuditLog> Audit(string action) => _db.Query<AuditLog>().Where(log => log.Action == action).ToList();

    [Fact]
    public async Task Issue_account_creates_student_user_with_temporary_password()
    {
        var student = AddStudent();

        var credential = await _service.IssueAccountAsync(_fund.Id, student.Id, null, default);

        var account = Assert.Single(_db.Query<User>(), user => user.Kind == UserKind.Student);
        Assert.Equal(account.Id, student.UserId);
        Assert.Equal(credential.Login, account.Username);
        Assert.StartsWith("huy.pham.", credential.Login);
        Assert.True(account.MustChangePassword);
        Assert.Equal("hash:" + credential.TemporaryPassword, account.PasswordHash);
        Assert.DoesNotContain(credential.TemporaryPassword, Assert.Single(Audit(StudentsAuditActions.IssueAccount)).NewValues);
    }

    [Fact]
    public async Task Issue_account_twice_is_rejected()
    {
        var student = AddStudent();
        await _service.IssueAccountAsync(_fund.Id, student.Id, "huy.pham", default);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.IssueAccountAsync(_fund.Id, student.Id, null, default));

        Assert.Equal(StudentsService.AccountExistsCode, error.Code);
    }

    [Fact]
    public async Task Issue_account_rejects_taken_or_invalid_username()
    {
        AddUser(UserKind.Student, username: "huy.pham");
        var student = AddStudent();

        var taken = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.IssueAccountAsync(_fund.Id, student.Id, "Huy.Pham", default));
        var invalid = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.IssueAccountAsync(_fund.Id, student.Id, "huy pham", default));

        Assert.Equal(StudentsService.UsernameTakenCode, taken.Code);
        Assert.Equal(StudentsService.InvalidUsernameCode, invalid.Code);
        Assert.Null(student.UserId);
    }

    [Fact]
    public async Task Reset_password_requires_account_and_rotates_session()
    {
        var student = AddStudent();
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ResetPasswordAsync(_fund.Id, student.Id, default));
        Assert.Equal(StudentsService.NoAccountCode, missing.Code);

        await _service.IssueAccountAsync(_fund.Id, student.Id, "huy.pham", default);
        var account = _db.Query<User>().Single(user => user.Id == student.UserId);
        account.ChangePassword("hash:Matkhau-moi1", Now);
        var stamp = account.SecurityStamp;

        var credential = await _service.ResetPasswordAsync(_fund.Id, student.Id, default);

        Assert.Equal("huy.pham", credential.Login);
        Assert.True(account.MustChangePassword);
        Assert.NotEqual(stamp, account.SecurityStamp);
        Assert.Equal("hash:" + credential.TemporaryPassword, account.PasswordHash);
    }

    [Fact]
    public async Task Link_new_email_creates_parent_account_and_link()
    {
        var student = AddStudent();

        var result = await _service.LinkParentAsync(_fund.Id, student.Id, " PH.Moi@Example.vn ", null, default);

        Assert.True(result.AccountCreated);
        Assert.False(result.EmailSent);
        var parent = Assert.Single(_db.Query<User>(), user => user.Kind == UserKind.Parent);
        Assert.Equal("ph.moi@example.vn", parent.NormalizedEmail);
        Assert.Equal("Phụ huynh Phạm Đức Huy", parent.FullName);
        Assert.True(parent.MustChangePassword);
        Assert.Equal(parent.Email, result.Credential!.Login);
        var link = Assert.Single(_db.Query<ParentLink>());
        Assert.Equal((student.Id, parent.Id, true), (link.StudentId, link.ParentUserId, link.IsActive));
    }

    [Fact]
    public async Task Link_existing_parent_email_only_adds_link()
    {
        var parent = AddUser(UserKind.Parent, "ph@example.vn");
        var student = AddStudent();

        var result = await _service.LinkParentAsync(_fund.Id, student.Id, "PH@example.vn", "Tên mới", default);

        Assert.False(result.AccountCreated);
        Assert.Null(result.Credential);
        Assert.Equal(parent.Id, Assert.Single(_db.Query<ParentLink>()).ParentUserId);
        Assert.Equal("Người dùng", parent.FullName);
        Assert.Empty(Audit(StudentsAuditActions.ParentCreateAccount));
    }

    [Theory]
    [InlineData(UserKind.Teacher)]
    [InlineData(UserKind.Student)]
    public async Task Link_rejects_email_of_teacher_or_student(UserKind kind)
    {
        AddUser(kind, "khac@example.vn");
        var student = AddStudent();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LinkParentAsync(_fund.Id, student.Id, "khac@example.vn", null, default));

        Assert.Equal(StudentsService.ParentEmailInUseCode, error.Code);
        Assert.Empty(_db.Query<ParentLink>());
    }

    [Fact]
    public async Task Second_active_link_for_same_student_is_rejected()
    {
        var student = AddStudent();
        await _service.LinkParentAsync(_fund.Id, student.Id, "a@example.vn", null, default);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.LinkParentAsync(_fund.Id, student.Id, "b@example.vn", null, default));

        Assert.Equal(StudentsService.ParentLinkExistsCode, error.Code);
    }

    [Fact]
    public async Task Unlink_marks_link_removed_by_current_teacher()
    {
        var student = AddStudent();
        await _service.LinkParentAsync(_fund.Id, student.Id, "a@example.vn", null, default);

        await _service.UnlinkParentAsync(_fund.Id, student.Id, default);

        var link = Assert.Single(_db.Query<ParentLink>());
        Assert.Equal((false, _teacher.Id, Now), (link.IsActive, link.RemovedBy, link.RemovedAt));
        Assert.Single(Audit(StudentsAuditActions.ParentLinkRemove));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UnlinkParentAsync(_fund.Id, student.Id, default));
    }

    [Fact]
    public async Task List_reports_account_status_treasurer_and_parent()
    {
        var none = AddStudent("An");
        var pending = AddStudent("Bình");
        var account = new User(UserKind.Student, "Bình", "hash", null, "binh.nguyen", true, Now);
        _db.Add(account);
        pending.LinkAccount(account.Id, Now);
        _db.Add(new TreasurerAssignment(_fund.Id, pending.Id, _teacher.Id, Now));
        await _service.LinkParentAsync(_fund.Id, none.Id, "ph.an@example.vn", "Mẹ An", default);

        var list = await _service.ListAsync(_fund.Id, default);

        Assert.Equal(["An", "Bình"], list.Select(item => item.FullName));
        Assert.Equal((StudentAccountStatus.None, false, "Mẹ An"), (list[0].AccountStatus, list[0].IsTreasurer, list[0].Parent!.FullName));
        Assert.Equal((StudentAccountStatus.PendingActivation, true, "binh.nguyen"), (list[1].AccountStatus, list[1].IsTreasurer, list[1].Username));
        Assert.Null(list[1].Parent);
    }

    [Fact]
    public async Task Non_homeroom_member_is_forbidden_and_outsider_gets_not_found()
    {
        var account = AddUser(UserKind.Student);
        var student = AddStudent();
        student.LinkAccount(account.Id, Now);
        _db.Add(new TreasurerAssignment(_fund.Id, student.Id, _teacher.Id, Now));

        SignIn(account);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.ListAsync(_fund.Id, default));

        SignIn(AddUser(UserKind.Teacher));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(_fund.Id, default));
    }
}
