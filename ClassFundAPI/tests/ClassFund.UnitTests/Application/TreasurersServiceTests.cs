using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Treasurers;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

/// <summary>Quy tắc của module Phân công thủ quỹ (M4): FR-27, BR-31, BR-32, BR-33, BR-37.</summary>
public class TreasurersServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly User _teacher;
    private readonly Fund _fund;

    public TreasurersServiceTests()
    {
        _teacher = AddUser(UserKind.Teacher, "Cô Hạnh", mustChangePassword: false);
        _currentUser.SetupGet(current => current.UserId).Returns(_teacher.Id);
        _fund = new Fund(_teacher.Id, "Quỹ lớp 5A", Now.AddDays(-30));
        _db.Add(_fund);
    }

    private TreasurersService Service()
    {
        var clock = new FixedClock(Now);
        return new TreasurersService(_db, new AccessService(_db), _currentUser.Object, new AuditTrail(_db, new Mock<IRequestInfo>().Object, clock), clock);
    }

    private User AddUser(UserKind kind, string fullName, bool mustChangePassword)
    {
        var student = kind == UserKind.Student;
        var user = new User(
            kind,
            fullName,
            "hash",
            student ? null : $"{Guid.NewGuid():N}@example.vn",
            student ? $"hs.{Guid.NewGuid():N}"[..12] : null,
            mustChangePassword,
            Now);
        _db.Add(user);
        return user;
    }

    /// <summary>Học sinh của quỹ với trạng thái tài khoản <paramref name="account"/> (None = chưa cấp tài khoản).</summary>
    private Student AddStudent(string fullName, StudentAccountStatus account, Fund? fund = null)
    {
        var student = new Student((fund ?? _fund).Id, fullName, _teacher.Id, Now.AddDays(-10));
        if (account != StudentAccountStatus.None)
        {
            var user = AddUser(UserKind.Student, fullName, mustChangePassword: account == StudentAccountStatus.PendingActivation);
            if (account == StudentAccountStatus.Disabled)
            {
                user.Disable(Now);
            }

            student.LinkAccount(user.Id, Now.AddDays(-9));
        }

        _db.Add(student);
        return student;
    }

    private TreasurerAssignment AddAssignment(Student student, DateTimeOffset at)
    {
        var assignment = new TreasurerAssignment(student.FundId, student.Id, _teacher.Id, at);
        _db.Add(assignment);
        return assignment;
    }

    [Fact]
    public async Task Assign_active_student_adds_assignment_and_audit()
    {
        var khang = AddStudent("Nguyễn Minh Khang", StudentAccountStatus.Active);

        var created = await Service().AssignAsync(_fund.Id, khang.Id, CancellationToken.None);

        var assignment = Assert.Single(_db.Query<TreasurerAssignment>());
        Assert.Equal(created.Id, assignment.Id);
        Assert.Equal((_fund.Id, khang.Id, _teacher.Id, Now), (assignment.FundId, assignment.StudentId, assignment.AssignedBy, assignment.AssignedAt));
        Assert.Null(assignment.RevokedAt);
        var audit = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(TreasurersAuditActions.Assign, audit.Action);
        Assert.Equal(_teacher.Id, audit.ActorUserId);
        Assert.Equal(assignment.Id.ToString(), audit.EntityId);
        Assert.Contains("Nguyễn Minh Khang", audit.NewValues);
    }

    [Theory]
    [InlineData(StudentAccountStatus.None, TreasurersErrors.NoAccountCode)]
    [InlineData(StudentAccountStatus.PendingActivation, TreasurersErrors.AccountNotActivatedCode)]
    [InlineData(StudentAccountStatus.Disabled, TreasurersErrors.AccountDisabledCode)]
    public async Task Assign_rejects_student_whose_account_is_not_active(StudentAccountStatus status, string code)
    {
        var student = AddStudent("Phạm Gia Huy", status);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().AssignAsync(_fund.Id, student.Id, CancellationToken.None));

        Assert.Equal(code, error.Code);
        Assert.Empty(_db.Query<TreasurerAssignment>());
        Assert.Empty(_db.Query<AuditLog>());
    }

    [Fact]
    public async Task Assign_rejects_current_treasurer_but_allows_previously_revoked_one()
    {
        var current = AddStudent("Phạm Thu Trang", StudentAccountStatus.Active);
        AddAssignment(current, Now.AddDays(-2));
        var former = AddStudent("Lê Gia Bảo", StudentAccountStatus.Active);
        AddAssignment(former, Now.AddDays(-5)).Revoke(_teacher.Id, Now.AddDays(-4));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().AssignAsync(_fund.Id, current.Id, CancellationToken.None));
        Assert.Equal(TreasurersErrors.AlreadyAssignedCode, error.Code);

        await Service().AssignAsync(_fund.Id, former.Id, CancellationToken.None);
        Assert.Equal(2, _db.Query<TreasurerAssignment>().Count(assignment => assignment.RevokedAt == null));
    }

    [Fact]
    public async Task Assign_student_of_another_fund_is_not_found()
    {
        var otherFund = new Fund(_teacher.Id, "Quỹ lớp 5B", Now);
        _db.Add(otherFund);
        var outsider = AddStudent("Học sinh 5B", StudentAccountStatus.Active, otherFund);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().AssignAsync(_fund.Id, outsider.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Unique_index_violation_on_save_is_reported_as_already_assigned()
    {
        var student = AddStudent("Nguyễn Minh Khang", StudentAccountStatus.Active);
        _db.SaveFailure = new UniqueConstraintException("UQ_TreasurerAssignment_Student_Active", new InvalidOperationException());

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().AssignAsync(_fund.Id, student.Id, CancellationToken.None));

        Assert.Equal(TreasurersErrors.AlreadyAssignedCode, error.Code);
    }

    [Fact]
    public async Task Revoke_last_treasurer_is_rejected()
    {
        var assignment = AddAssignment(AddStudent("Nguyễn Minh Khang", StudentAccountStatus.Active), Now.AddDays(-1));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().RevokeAsync(_fund.Id, assignment.Id, CancellationToken.None));

        Assert.Equal(TreasurersErrors.LastCode, error.Code);
        Assert.Null(assignment.RevokedAt);
        Assert.Empty(_db.Query<AuditLog>());
    }

    [Fact]
    public async Task Last_treasurer_rule_counts_assignments_even_when_other_account_is_disabled()
    {
        var first = AddAssignment(AddStudent("Thủ quỹ 1", StudentAccountStatus.Active), Now.AddDays(-2));
        var second = AddStudent("Thủ quỹ 2", StudentAccountStatus.Active);
        AddAssignment(second, Now.AddDays(-1));
        _db.Query<User>().Single(user => user.Id == second.UserId).Disable(Now);

        await Service().RevokeAsync(_fund.Id, first.Id, CancellationToken.None);

        Assert.Equal(Now, first.RevokedAt);
    }

    [Fact]
    public async Task Revoke_marks_assignment_and_records_audit()
    {
        var old = AddAssignment(AddStudent("Lê Gia Bảo", StudentAccountStatus.Active), Now.AddDays(-5));
        AddAssignment(AddStudent("Phạm Thu Trang", StudentAccountStatus.Active), Now.AddDays(-1));

        await Service().RevokeAsync(_fund.Id, old.Id, CancellationToken.None);

        Assert.Equal(Now, old.RevokedAt);
        Assert.Equal(_teacher.Id, old.RevokedBy);
        Assert.Equal(2, _db.Query<TreasurerAssignment>().Count());
        var audit = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(TreasurersAuditActions.Revoke, audit.Action);
        Assert.Equal(old.Id.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Revoke_already_revoked_is_conflict_and_unknown_is_not_found()
    {
        var revoked = AddAssignment(AddStudent("Lê Gia Bảo", StudentAccountStatus.Active), Now.AddDays(-5));
        revoked.Revoke(_teacher.Id, Now.AddDays(-4));
        AddAssignment(AddStudent("Thủ quỹ 2", StudentAccountStatus.Active), Now.AddDays(-2));
        AddAssignment(AddStudent("Thủ quỹ 3", StudentAccountStatus.Active), Now.AddDays(-1));

        var error = await Assert.ThrowsAsync<ConflictException>(() => Service().RevokeAsync(_fund.Id, revoked.Id, CancellationToken.None));
        Assert.Equal(TreasurersErrors.AlreadyRevokedCode, error.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => Service().RevokeAsync(_fund.Id, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task List_puts_active_first_newest_first_with_actor_names()
    {
        var bao = AddStudent("Lê Gia Bảo", StudentAccountStatus.Active);
        var khang = AddStudent("Nguyễn Minh Khang", StudentAccountStatus.Active);
        var trang = AddStudent("Phạm Thu Trang", StudentAccountStatus.Active);
        var revoked = AddAssignment(bao, Now.AddDays(-10));
        revoked.Revoke(_teacher.Id, Now.AddDays(-9));
        var older = AddAssignment(khang, Now.AddDays(-8));
        var newer = AddAssignment(trang, Now.AddDays(-1));

        var list = await Service().ListAsync(_fund.Id, CancellationToken.None);

        Assert.Equal([newer.Id, older.Id, revoked.Id], list.Select(item => item.Id));
        Assert.Equal(["Phạm Thu Trang", "Nguyễn Minh Khang", "Lê Gia Bảo"], list.Select(item => item.StudentName));
        Assert.All(list, item => Assert.Equal("Cô Hạnh", item.AssignedByName));
        Assert.Equal([true, true, false], list.Select(item => item.IsActive));
        Assert.Equal([null, null, "Cô Hạnh"], list.Select(item => item.RevokedByName));
        Assert.Equal(Now.AddDays(-9), list[2].RevokedAt);
    }

    [Fact]
    public async Task Student_member_cannot_list_assign_or_revoke()
    {
        var treasurer = AddStudent("Thủ quỹ", StudentAccountStatus.Active);
        var assignment = AddAssignment(treasurer, Now.AddDays(-1));
        _currentUser.SetupGet(current => current.UserId).Returns(treasurer.UserId);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().ListAsync(_fund.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().AssignAsync(_fund.Id, treasurer.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service().RevokeAsync(_fund.Id, assignment.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Teacher_of_another_fund_gets_not_found()
    {
        var other = AddUser(UserKind.Teacher, "Thầy B", mustChangePassword: false);
        _currentUser.SetupGet(current => current.UserId).Returns(other.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().ListAsync(_fund.Id, CancellationToken.None));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
