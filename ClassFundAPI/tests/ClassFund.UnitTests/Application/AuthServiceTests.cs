using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Auth;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using Moq;

namespace ClassFund.UnitTests.Application;

/// <summary>M1-T3: AuthService (FR-01 → FR-04, BR-27 → BR-29).</summary>
public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeAppDb _db = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    public AuthServiceTests()
    {
        // Hash giả lập: "h:" + mật khẩu; "old:" + mật khẩu là hash tham số cũ cần băm lại.
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(password => "h:" + password);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns<string, string>((hash, password) =>
            hash == "h:" + password ? PasswordCheck.Success
            : hash == "old:" + password ? PasswordCheck.SuccessRehashNeeded
            : PasswordCheck.Failed);
    }

    private AuthService CreateService()
    {
        var request = new Mock<IRequestInfo>();
        return new AuthService(_db, _hasher.Object, new AccessService(_db), _currentUser.Object, new AuditTrail(_db, request.Object, new FixedClock()), new FixedClock());
    }

    private User SeedUser(string email = "a@x.com", string password = "Passw0rd1", UserOrigin origin = UserOrigin.SelfRegistered, bool mustChange = false, bool active = true, string? hash = null)
    {
        var user = new User(email, "Nguyễn An", hash ?? "h:" + password, origin, mustChange, Now) { IsActive = active };
        _db.Seed(user);
        return user;
    }

    private void SignedInAs(User user) => _currentUser.SetupGet(c => c.UserId).Returns(user.Id);

    // ---------- Đăng ký ----------

    [Fact]
    public async Task Register_creates_self_registered_account_with_normalized_email_and_audit()
    {
        var session = await CreateService().RegisterAsync("  Nguyễn An ", "  An@Example.COM ", "Passw0rd1", default);

        var user = Assert.Single(_db.Query<User>());
        Assert.Equal("an@example.com", user.Email);
        Assert.Equal("Nguyễn An", user.FullName);
        Assert.Equal(UserOrigin.SelfRegistered, user.Origin);
        Assert.False(user.MustChangePassword);
        Assert.Equal("h:Passw0rd1", user.PasswordHash);
        Assert.Equal(user.Id, session.Ticket.UserId);
        Assert.Equal(user.SecurityStamp, session.Ticket.SecurityStamp);
        Assert.Contains(Permissions.FundCreate, session.User.AccountPermissions);
        Assert.Empty(session.User.Memberships);

        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(AuthAuditActions.Register, log.Action);
        Assert.Equal(FinancialScope.System, log.Scope);
        Assert.Equal(user.Id, log.ActorUserId);
    }

    [Fact]
    public async Task Register_never_writes_password_or_hash_to_audit()
    {
        await CreateService().RegisterAsync("An", "an@x.com", "Passw0rd1", default);

        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.DoesNotContain("Passw0rd1", log.AfterJson ?? "");
        Assert.DoesNotContain("h:", log.AfterJson ?? "");
    }

    [Fact]
    public async Task Register_with_existing_email_in_other_case_is_conflict()
    {
        SeedUser("an@x.com");
        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RegisterAsync("An", "AN@x.com", "Passw0rd1", default));
        Assert.Equal(AuthErrors.EmailExistsCode, error.Code);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public async Task Register_rejects_password_breaking_policy(string password)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RegisterAsync("An", "an@x.com", password, default));
        Assert.Equal("auth.password_policy", error.Code);
        Assert.Empty(_db.Query<User>());
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    public async Task Register_rejects_invalid_email(string email)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RegisterAsync("An", email, "Passw0rd1", default));
        Assert.Equal(AuthErrors.InvalidEmailCode, error.Code);
    }

    [Fact]
    public async Task Register_rejects_blank_name() =>
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RegisterAsync("   ", "an@x.com", "Passw0rd1", default));

    // ---------- Đăng nhập ----------

    [Fact]
    public async Task Login_succeeds_with_normalized_email_and_returns_memberships()
    {
        var user = SeedUser("an@x.com");
        var fund = new Fund("Lớp 10A1", user.Id, "2026-2027", Now);
        _db.Seed(fund);
        _db.Seed(new FundMember(fund.Id, user.Id, MemberRole.Owner, Now));

        var session = await CreateService().LoginAsync("  AN@x.com ", "Passw0rd1", default);

        Assert.Equal(user.Id, session.User.Id);
        var membership = Assert.Single(session.User.Memberships);
        Assert.Equal(MemberRole.Owner, membership.Role);
        Assert.Contains(Permissions.TreasurerManage, membership.Permissions);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_invalid_credentials()
    {
        SeedUser("an@x.com");
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => CreateService().LoginAsync("an@x.com", "Wrong1234", default));
        Assert.Equal(AuthenticationFailedException.InvalidCredentialsCode, error.Code);
    }

    [Fact]
    public async Task Login_with_unknown_email_gives_same_error_and_still_hashes()
    {
        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => CreateService().LoginAsync("nobody@x.com", "Passw0rd1", default));
        Assert.Equal(AuthenticationFailedException.InvalidCredentialsCode, error.Code);
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), "Passw0rd1"), Times.Once);
    }

    [Fact]
    public async Task Login_to_disabled_account_is_account_disabled_only_after_correct_password()
    {
        SeedUser("an@x.com", active: false);

        var wrong = await Assert.ThrowsAsync<AuthenticationFailedException>(() => CreateService().LoginAsync("an@x.com", "Wrong1234", default));
        Assert.Equal(AuthenticationFailedException.InvalidCredentialsCode, wrong.Code);

        var disabled = await Assert.ThrowsAsync<AuthenticationFailedException>(() => CreateService().LoginAsync("an@x.com", "Passw0rd1", default));
        Assert.Equal(AuthenticationFailedException.AccountDisabledCode, disabled.Code);
    }

    [Fact]
    public async Task Login_rehashes_password_when_hash_parameters_are_old()
    {
        var user = SeedUser("an@x.com", hash: "old:Passw0rd1");
        var session = await CreateService().LoginAsync("an@x.com", "Passw0rd1", default);

        Assert.Equal("h:Passw0rd1", user.PasswordHash);
        Assert.Equal(user.Id, session.User.Id);
    }

    [Fact]
    public async Task Login_with_temporary_password_reports_must_change_password()
    {
        SeedUser("hs@x.com", origin: UserOrigin.Provisioned, mustChange: true);
        var session = await CreateService().LoginAsync("hs@x.com", "Passw0rd1", default);

        Assert.True(session.User.MustChangePassword);
        Assert.Empty(session.User.AccountPermissions);
    }

    // ---------- Phiên hiện tại & hồ sơ ----------

    [Fact]
    public async Task Me_returns_current_user_only()
    {
        SeedUser("other@x.com");
        var me = SeedUser("an@x.com");
        SignedInAs(me);

        var dto = await CreateService().GetMeAsync(default);
        Assert.Equal("an@x.com", dto.Email);
    }

    [Fact]
    public async Task Me_without_session_is_unauthorized() =>
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => CreateService().GetMeAsync(default));

    [Fact]
    public async Task Update_profile_changes_name_and_records_before_and_after()
    {
        var me = SeedUser();
        SignedInAs(me);

        var dto = await CreateService().UpdateProfileAsync("  Trần Bình ", default);

        Assert.Equal("Trần Bình", dto.FullName);
        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(AuthAuditActions.UpdateProfile, log.Action);
        Assert.Contains("Nguyễn An", log.BeforeJson);
        Assert.Contains("Trần Bình", log.AfterJson);
    }

    [Fact]
    public async Task Update_profile_rejects_name_over_100_chars()
    {
        SignedInAs(SeedUser());
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().UpdateProfileAsync(new string('a', 101), default));
    }

    // ---------- Đổi mật khẩu ----------

    [Fact]
    public async Task Change_password_clears_temporary_flag_rotates_stamp_and_audits_without_secrets()
    {
        var me = SeedUser(origin: UserOrigin.Provisioned, mustChange: true, password: "Temp-1234-abcd");
        var oldStamp = me.SecurityStamp;
        SignedInAs(me);

        var session = await CreateService().ChangePasswordAsync("Temp-1234-abcd", "NewPassw0rd", default);

        Assert.False(me.MustChangePassword);
        Assert.Equal("h:NewPassw0rd", me.PasswordHash);
        Assert.NotEqual(oldStamp, me.SecurityStamp);
        Assert.Equal(me.SecurityStamp, session.Ticket.SecurityStamp);
        Assert.False(session.User.MustChangePassword);

        var log = Assert.Single(_db.Query<AuditLog>());
        Assert.Equal(AuthAuditActions.ChangePassword, log.Action);
        Assert.True(log.IsSelfAction);
        var json = (log.BeforeJson ?? "") + (log.AfterJson ?? "");
        Assert.DoesNotContain("NewPassw0rd", json);
        Assert.DoesNotContain("Temp-1234-abcd", json);
        Assert.DoesNotContain(me.SecurityStamp, json);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_is_business_rule_not_401()
    {
        var me = SeedUser();
        SignedInAs(me);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ChangePasswordAsync("Wrong1234", "NewPassw0rd", default));
        Assert.Equal(AuthErrors.CurrentPasswordIncorrectCode, error.Code);
        Assert.Equal("h:Passw0rd1", me.PasswordHash);
        Assert.Empty(_db.Query<AuditLog>());
    }

    [Fact]
    public async Task Change_password_rejects_same_password_and_weak_password()
    {
        var me = SeedUser();
        SignedInAs(me);

        var same = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ChangePasswordAsync("Passw0rd1", "Passw0rd1", default));
        Assert.Equal(AuthErrors.PasswordUnchangedCode, same.Code);

        var weak = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().ChangePasswordAsync("Passw0rd1", "abc", default));
        Assert.Equal("auth.password_policy", weak.Code);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
