using Moq;
using SchoolFund.Application.Common.Auditing;
using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Security;
using SchoolFund.Application.Features.Treasurers;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;
using SchoolFund.Domain.Exceptions;

namespace SchoolFund.UnitTests.Application;

public class TreasurerServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TreasurerService _service;
    private readonly User _teacher;
    private readonly Fund _fund;

    public TreasurerServiceTests()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(Now);
        _service = new TreasurerService(
            _db,
            new AccessService(_db),
            _currentUser.Object,
            new AuditTrail(_db, Mock.Of<IRequestInfo>(), clock.Object),
            clock.Object);
        _teacher = new User(UserKind.Teacher, "Cô Hạnh", "hash", "co.hanh@example.vn", null, false, Now);
        _db.Add(_teacher);
        _fund = new Fund(_teacher.Id, "Quỹ lớp 5A", Now);
        _db.Add(_fund);
        SignIn(_teacher);
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    /// <summary>Học sinh có tài khoản đã kích hoạt (không còn mật khẩu tạm) trừ khi chỉ định khác.</summary>
    private Student AddStudent(string name, bool account = true, bool mustChangePassword = false, bool disabled = false)
    {
        var student = new Student(_fund.Id, name, _teacher.Id, Now);
        if (account)
        {
            var user = new User(UserKind.Student, name, "hash", null, $"hs.{Guid.NewGuid():N}"[..12], mustChangePassword, Now);
            if (disabled)
            {
                user.Disable(Now);
            }

            _db.Add(user);
            student.LinkAccount(user.Id, Now);
        }

        _db.Add(student);
        return student;
    }

    private TreasurerAssignment AddAssignment(Student student, bool revoked = false)
    {
        var assignment = new TreasurerAssignment(_fund.Id, student.Id, _teacher.Id, Now);
        if (revoked)
        {
            assignment.Revoke(_teacher.Id, Now.AddMinutes(1));
        }

        _db.Add(assignment);
        return assignment;
    }

    private IReadOnlyList<AuditLog> Audit(string action) => _db.Query<AuditLog>().Where(log => log.Action == action).ToList();

    // ---------- Phân công ----------

    [Fact]
    public async Task Assign_creates_active_assignment_recorded_with_current_teacher()
    {
        var student = AddStudent("Phạm Thu Trang");

        var created = await _service.AssignAsync(_fund.Id, student.Id, default);

        var assignment = Assert.Single(_db.Query<TreasurerAssignment>());
        Assert.Equal(created.Id, assignment.Id);
        Assert.Equal((_fund.Id, student.Id, _teacher.Id, Now), (assignment.FundId, assignment.StudentId, assignment.AssignedBy, assignment.AssignedAt));
        Assert.Null(assignment.RevokedAt);
        var audit = Assert.Single(Audit(AuditActions.TreasurerAssign));
        Assert.Equal(_teacher.Id, audit.ActorUserId);
        Assert.Equal(assignment.Id.ToString(), audit.EntityId);
    }

    [Theory]
    [InlineData("no_account", TreasurerService.NoAccountCode)]
    [InlineData("pending", TreasurerService.AccountNotActivatedCode)]
    [InlineData("disabled", TreasurerService.AccountDisabledCode)]
    public async Task Assign_rejects_student_without_an_activated_active_account(string kind, string code)
    {
        var student = kind switch
        {
            "no_account" => AddStudent("An", account: false),
            "pending" => AddStudent("An", mustChangePassword: true),
            _ => AddStudent("An", disabled: true),
        };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AssignAsync(_fund.Id, student.Id, default));

        Assert.Equal(code, error.Code);
        Assert.Empty(_db.Query<TreasurerAssignment>());
        Assert.Empty(Audit(AuditActions.TreasurerAssign));
    }

    [Fact]
    public async Task Assign_rejects_student_who_is_already_an_active_treasurer_but_allows_a_revoked_one()
    {
        var current = AddStudent("Đang là thủ quỹ");
        AddAssignment(current);
        var former = AddStudent("Từng là thủ quỹ");
        AddAssignment(former, revoked: true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AssignAsync(_fund.Id, current.Id, default));
        await _service.AssignAsync(_fund.Id, former.Id, default);

        Assert.Equal(TreasurerService.AlreadyAssignedCode, error.Code);
        Assert.Equal(2, _db.Query<TreasurerAssignment>().Count(assignment => assignment.RevokedAt == null));
    }

    [Fact]
    public async Task Assign_student_of_another_fund_is_not_found()
    {
        var otherFund = new Fund(_teacher.Id, "Quỹ khác", Now);
        _db.Add(otherFund);
        var outsider = new Student(otherFund.Id, "Học sinh quỹ khác", _teacher.Id, Now);
        _db.Add(outsider);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AssignAsync(_fund.Id, outsider.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.AssignAsync(_fund.Id, Guid.NewGuid(), default));
    }

    // ---------- Thu hồi ----------

    [Fact]
    public async Task Revoke_marks_assignment_with_current_teacher_when_another_treasurer_remains()
    {
        var first = AddAssignment(AddStudent("An"));
        var second = AddAssignment(AddStudent("Bình"));

        await _service.RevokeAsync(_fund.Id, first.Id, default);

        Assert.Equal((Now, _teacher.Id), (first.RevokedAt, first.RevokedBy));
        Assert.Null(second.RevokedAt);
        var audit = Assert.Single(Audit(AuditActions.TreasurerRevoke));
        Assert.Equal(first.Id.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Revoke_last_active_treasurer_is_rejected_and_changes_nothing()
    {
        var only = AddAssignment(AddStudent("An"));
        AddAssignment(AddStudent("Đã thu hồi"), revoked: true);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.RevokeAsync(_fund.Id, only.Id, default));

        Assert.Equal(TreasurerService.LastTreasurerCode, error.Code);
        Assert.Null(only.RevokedAt);
        Assert.Empty(Audit(AuditActions.TreasurerRevoke));
    }

    [Fact]
    public async Task Revoke_already_revoked_assignment_is_a_conflict_even_when_it_would_also_be_the_last()
    {
        var revoked = AddAssignment(AddStudent("An"), revoked: true);
        AddAssignment(AddStudent("Bình"));

        var error = await Assert.ThrowsAsync<ConflictException>(() => _service.RevokeAsync(_fund.Id, revoked.Id, default));

        Assert.Equal(TreasurerService.AlreadyRevokedCode, error.Code);
    }

    [Fact]
    public async Task Revoke_assignment_of_another_fund_or_unknown_is_not_found()
    {
        var otherFund = new Fund(_teacher.Id, "Quỹ khác", Now);
        _db.Add(otherFund);
        var otherStudent = new Student(otherFund.Id, "Học sinh quỹ khác", _teacher.Id, Now);
        _db.Add(otherStudent);
        var foreign = new TreasurerAssignment(otherFund.Id, otherStudent.Id, _teacher.Id, Now);
        _db.Add(foreign);
        AddAssignment(AddStudent("An"));
        AddAssignment(AddStudent("Bình"));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RevokeAsync(_fund.Id, foreign.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RevokeAsync(_fund.Id, Guid.NewGuid(), default));
        Assert.Null(foreign.RevokedAt);
    }

    // ---------- Danh sách ----------

    [Fact]
    public async Task List_returns_active_first_then_history_with_names()
    {
        var revoked = AddAssignment(AddStudent("Cường"), revoked: true);
        var active = AddAssignment(AddStudent("An"));

        var list = await _service.ListAsync(_fund.Id, default);

        Assert.Equal([active.Id, revoked.Id], list.Select(item => item.Id));
        Assert.Equal((true, "An", "Cô Hạnh", null), (list[0].IsActive, list[0].StudentName, list[0].AssignedByName, list[0].RevokedByName));
        Assert.Equal((false, "Cường", "Cô Hạnh", "Cô Hạnh"), (list[1].IsActive, list[1].StudentName, list[1].AssignedByName, list[1].RevokedByName));
        Assert.Equal(Now.AddMinutes(1), list[1].RevokedAt);
    }

    [Fact]
    public async Task List_is_empty_for_a_fund_without_assignments()
    {
        Assert.Empty(await _service.ListAsync(_fund.Id, default));
    }

    // ---------- Phân quyền ----------

    [Fact]
    public async Task Treasurer_and_outsider_cannot_manage_assignments()
    {
        var treasurerStudent = AddStudent("Thủ quỹ");
        AddAssignment(treasurerStudent);
        var assignmentToRevoke = AddAssignment(AddStudent("Người thứ hai"));
        var plain = AddStudent("Học sinh");

        SignIn(_db.Query<User>().Single(user => user.Id == treasurerStudent.UserId));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.ListAsync(_fund.Id, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.AssignAsync(_fund.Id, plain.Id, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.RevokeAsync(_fund.Id, assignmentToRevoke.Id, default));

        var other = new User(UserKind.Teacher, "Giáo viên khác", "hash", "khac@example.vn", null, false, Now);
        _db.Add(other);
        SignIn(other);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(_fund.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RevokeAsync(_fund.Id, assignmentToRevoke.Id, default));
        Assert.Null(assignmentToRevoke.RevokedAt);
    }
}
