using Moq;
using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Auth;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(Now);
        _service = new AuthService(
            _db,
            new FakePasswordHasher(),
            _currentUser.Object,
            new AccessService(_db),
            new AuditTrail(_db, Mock.Of<IRequestInfo>(), clock.Object),
            clock.Object);
    }

    private User AddUser(UserKind kind, string password, string? email = null, string? username = null, bool mustChange = false, string hashPrefix = "hash:")
    {
        var user = new User(kind, "Người dùng", hashPrefix + password, email, username, mustChange, Now);
        _db.Add(user);
        return user;
    }

    private void SignIn(User user) => _currentUser.SetupGet(current => current.UserId).Returns(user.Id);

    private IReadOnlyList<AuditLog> Audit(string action) => _db.Query<AuditLog>().Where(log => log.Action == action).ToList();

    // ---------- Đăng ký ----------

    [Fact]
    public async Task Register_teacher_creates_teacher_session_and_audits_without_password()
    {
        var result = await _service.RegisterTeacherAsync(new RegisterTeacherCommand(" Cô Hạnh ", " Co.Hanh@Example.vn ", "Matkhau123"), default);

        var user = Assert.Single(_db.Query<User>());
        Assert.Equal(UserKind.Teacher, user.Kind);
        Assert.Equal("Cô Hạnh", user.FullName);
        Assert.Equal("co.hanh@example.vn", user.NormalizedEmail);
        Assert.Equal("hash:Matkhau123", user.PasswordHash);
        Assert.Equal(Now, user.LastLoginAt);
        Assert.Equal(new SessionTicket(user.Id, user.SecurityStamp), result.Session);
        Assert.Equal(["Fund.Create"], result.Me.AccountPermissions);

        var audit = Assert.Single(Audit(AuditActions.AccountRegisterTeacher));
        Assert.Equal(user.Id, audit.ActorUserId);
        Assert.DoesNotContain("Matkhau123", audit.NewValues);
    }

    [Fact]
    public async Task Register_rejects_password_outside_policy()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RegisterTeacherAsync(new RegisterTeacherCommand("Cô Hạnh", "a@example.vn", "chicochu"), default));

        Assert.Equal("auth.password_policy", error.Code);
        Assert.Empty(_db.Query<User>());
    }

    [Fact]
    public async Task Register_rejects_taken_email_case_insensitively()
    {
        AddUser(UserKind.Parent, "Matkhau123", email: "ph@example.vn");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RegisterTeacherAsync(new RegisterTeacherCommand("Cô Hạnh", "PH@Example.VN", "Matkhau123"), default));

        Assert.Equal("user.email_taken", error.Code);
    }

    // ---------- Đăng nhập ----------

    [Fact]
    public async Task Login_accepts_email_or_username_and_derives_kind()
    {
        var teacher = AddUser(UserKind.Teacher, "Matkhau123", email: "co.hanh@example.vn");
        var student = AddUser(UserKind.Student, "Tam-1234", username: "khang.nguyen", mustChange: true);

        var byEmail = await _service.LoginAsync(" CO.HANH@example.vn ", "Matkhau123", default);
        var byUsername = await _service.LoginAsync("Khang.Nguyen", "Tam-1234", default);

        Assert.Equal((teacher.Id, UserKind.Teacher, false), (byEmail.Me.Id, byEmail.Me.Kind, byEmail.Me.MustChangePassword));
        Assert.Equal((student.Id, UserKind.Student, true), (byUsername.Me.Id, byUsername.Me.Kind, byUsername.Me.MustChangePassword));
        Assert.Equal(2, Audit(AuditActions.AuthSignIn).Count);
    }

    [Theory]
    [InlineData("co.hanh@example.vn", "SaiMatKhau1")]
    [InlineData("khong.co@example.vn", "Matkhau123")]
    public async Task Login_with_wrong_credentials_fails_with_generic_code(string login, string password)
    {
        AddUser(UserKind.Teacher, "Matkhau123", email: "co.hanh@example.vn");

        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.LoginAsync(login, password, default));

        Assert.Equal("auth.invalid_credentials", error.Code);
        var audit = Assert.Single(Audit(AuditActions.AuthSignInFailed));
        Assert.Null(audit.ActorUserId);
        Assert.DoesNotContain(password, audit.NewValues);
    }

    [Fact]
    public async Task Login_reports_disabled_account_only_with_correct_password()
    {
        var user = AddUser(UserKind.Teacher, "Matkhau123", email: "co.hanh@example.vn");
        user.Disable(Now);

        var right = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.LoginAsync("co.hanh@example.vn", "Matkhau123", default));
        var wrong = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.LoginAsync("co.hanh@example.vn", "SaiMatKhau1", default));

        Assert.Equal("auth.account_disabled", right.Code);
        Assert.Equal("auth.invalid_credentials", wrong.Code);
    }

    [Fact]
    public async Task Login_upgrades_outdated_hash()
    {
        var user = AddUser(UserKind.Teacher, "Matkhau123", email: "co.hanh@example.vn", hashPrefix: "legacy:");

        await _service.LoginAsync("co.hanh@example.vn", "Matkhau123", default);

        Assert.Equal("hash:Matkhau123", user.PasswordHash);
    }

    // ---------- Tài khoản & đổi mật khẩu ----------

    [Fact]
    public async Task Change_password_clears_temporary_flag_and_issues_new_session()
    {
        var user = AddUser(UserKind.Student, "Tam-1234", username: "khang.nguyen", mustChange: true);
        var oldStamp = user.SecurityStamp;
        SignIn(user);

        var result = await _service.ChangePasswordAsync(new ChangePasswordCommand("Tam-1234", "MatKhauMoi1"), default);

        Assert.False(user.MustChangePassword);
        Assert.Equal("hash:MatKhauMoi1", user.PasswordHash);
        Assert.NotEqual(oldStamp, result.Session.SecurityStamp);
        Assert.Equal(user.SecurityStamp, result.Session.SecurityStamp);
        Assert.False(result.Me.MustChangePassword);
        Assert.Single(Audit(AuditActions.AuthPasswordChanged));
    }

    [Theory]
    [InlineData("SaiMatKhau1", "MatKhauMoi1", "auth.wrong_password")]
    [InlineData("Tam-1234", "yeu", "auth.password_policy")]
    [InlineData("Tam-1234", "Tam-1234", "auth.password_reused")]
    public async Task Change_password_rejects_invalid_requests(string current, string next, string code)
    {
        var user = AddUser(UserKind.Student, "Tam-1234", username: "khang.nguyen", mustChange: true);
        SignIn(user);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ChangePasswordAsync(new ChangePasswordCommand(current, next), default));

        Assert.Equal(code, error.Code);
        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public async Task Update_me_renames_current_user_and_audits_change()
    {
        var user = AddUser(UserKind.Teacher, "Matkhau123", email: "co.hanh@example.vn");
        SignIn(user);

        var me = await _service.UpdateMeAsync("  Trần Thị Hạnh ", default);

        Assert.Equal("Trần Thị Hạnh", me.FullName);
        var audit = Assert.Single(Audit(AuditActions.AccountUpdateProfile));
        Assert.Contains("Người dùng", audit.OldValues);
    }

    [Fact]
    public async Task Me_requires_signed_in_user()
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.GetMeAsync(default));

        Assert.Equal("unauthorized", error.Code);
    }
}
