using System.Net;
using ClassFund.Domain.Enums;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Auth;

/// <summary>M1 – Auth & Tài khoản qua HTTP thật (cookie, antiforgery, PostgreSQL): FR-20, FR-13, FR-21, BR-26, BR-28, NFR-10.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthTests(PostgresFixture fixture)
{
    private const string TeacherPassword = TestData.TeacherPassword;
    private static readonly string[] PublicPermissions = ["Fund.View", "Ledger.ViewPublic"];

    private readonly TestData _data = fixture.Data;

    private ApiClient NewClient() => fixture.Factory.CreateApiClient();

    private static object RegisterBody(string email, string password = TeacherPassword, string? confirm = null) =>
        new { fullName = "  Cô Hạnh  ", email, password, confirmPassword = confirm ?? password };

    // ---------- Đăng ký giáo viên ----------

    [Fact]
    public async Task Teacher_registers_and_is_signed_in_as_teacher()
    {
        using var client = NewClient();
        var email = TestData.NewEmail();

        // Trường "role" / "kind" gửi kèm bị bỏ qua: tài khoản luôn là giáo viên (D-24).
        var response = await client.PostAsync("/auth/register-teacher", new
        {
            fullName = "  Cô Hạnh  ",
            email,
            password = TeacherPassword,
            confirmPassword = TeacherPassword,
            role = "Parent",
            kind = "Student",
        });

        var session = await response.ReadSessionAsync(HttpStatusCode.Created);
        Assert.Equal("Teacher", session.User.Kind);
        Assert.Equal("Cô Hạnh", session.User.FullName);
        Assert.Equal(email, session.User.Email);
        Assert.False(session.User.MustChangePassword);
        Assert.Equal(["Fund.Create"], session.User.AccountPermissions);
        Assert.Empty(session.User.Memberships);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        var me = await (await client.GetAsync("/auth/me")).ReadMeAsync();
        Assert.Equal(session.User.Id, me.Id);

        var stored = await _data.FindUserAsync(me.Id);
        Assert.Equal(UserKind.Teacher, stored.Kind);
        Assert.Equal(email.ToLowerInvariant(), stored.NormalizedEmail);
        Assert.NotNull(stored.LastLoginAt);
        Assert.Contains(await _data.AuditLogsAsync(me.Id), log => log.Action == "Account.RegisterTeacher");
    }

    [Fact]
    public async Task Register_rejects_mismatched_confirmation_weak_password_and_taken_email()
    {
        using var client = NewClient();
        var email = TestData.NewEmail();

        var mismatch = await client.PostAsync("/auth/register-teacher", RegisterBody(email, confirm: "Khac12345"));
        var problem = await mismatch.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("confirmPassword", out _));

        foreach (var weak in new[] { "abc12", "chicochu", "12345678", new string('a', 127) + "1x" })
        {
            var response = await client.PostAsync("/auth/register-teacher", RegisterBody(email, weak));
            await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "auth.password_policy");
        }

        await (await client.PostAsync("/auth/register-teacher", RegisterBody(email))).ReadSessionAsync(HttpStatusCode.Created);

        using var other = NewClient();
        var duplicate = await other.PostAsync("/auth/register-teacher", RegisterBody("  " + email.ToUpperInvariant() + " "));
        await duplicate.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "user.email_taken");
    }

    // ---------- Đăng nhập một ô ----------

    [Fact]
    public async Task Teacher_signs_in_with_email_case_insensitively_without_role_parameter()
    {
        var email = TestData.NewEmail();
        using (var registrar = NewClient())
        {
            await (await registrar.PostAsync("/auth/register-teacher", RegisterBody(email))).ReadSessionAsync(HttpStatusCode.Created);
        }

        using var client = NewClient();
        var response = await client.PostAsync("/auth/login", new { login = " " + email.ToUpperInvariant(), password = TeacherPassword, role = "Parent" });

        var session = await response.ReadSessionAsync(HttpStatusCode.OK);
        Assert.Equal("Teacher", session.User.Kind);
        Assert.Equal(["Fund.Create"], session.User.AccountPermissions);
        Assert.Equal(session.User.Id, (await (await client.GetAsync("/auth/me")).ReadMeAsync()).Id);

        // Trường "email" của client cũ vẫn được nhận.
        using var legacy = NewClient();
        await (await legacy.PostAsync("/auth/login", new { email, password = TeacherPassword })).ReadSessionAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Student_signs_in_with_username_and_must_change_temporary_password()
    {
        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund);
        using var client = NewClient();

        var session = await client.LoginAsync(student.Login.ToUpperInvariant(), student.Password);

        Assert.Equal("Student", session.User.Kind);
        Assert.Equal(student.Login, session.User.Username);
        Assert.Null(session.User.Email);
        Assert.True(session.User.MustChangePassword);
        Assert.Empty(session.User.AccountPermissions);
        var membership = Assert.Single(session.User.Memberships);
        Assert.Equal(fund.Id, membership.FundId);
        Assert.Equal("Student", membership.Role);
        Assert.Equal(PublicPermissions, membership.Permissions);
    }

    [Fact]
    public async Task Login_failures_return_401_with_specific_codes()
    {
        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund);
        using var client = NewClient();

        await (await client.PostAsync("/auth/login", new { login = student.Login, password = "SaiMatKhau1" }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await (await client.PostAsync("/auth/login", new { login = "khong.ton.tai@example.vn", password = "Matkhau123" }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await (await client.PostAsync("/auth/login", new { login = "", password = "" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        await _data.DisableUserAsync(student.UserId);

        // Chỉ báo "bị khóa" khi mật khẩu đúng (BR-26).
        await (await client.PostAsync("/auth/login", new { login = student.Login, password = student.Password }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.account_disabled");
        await (await client.PostAsync("/auth/login", new { login = student.Login, password = "SaiMatKhau1" }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        var failures = (await _data.AuditLogsAsync(student.UserId)).Where(log => log.Action == "Auth.SignInFailed").ToList();
        Assert.Equal(3, failures.Count);
        Assert.All(failures, log => Assert.Null(log.ActorUserId));
    }

    [Fact]
    public async Task Write_requests_require_antiforgery_token()
    {
        using var client = NewClient();
        var response = await client.PostWithoutAntiforgeryAsync("/auth/login", new { login = "a@example.vn", password = "Matkhau123" });
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "antiforgery_failed");
    }

    // ---------- Mật khẩu tạm & đổi mật khẩu ----------

    [Fact]
    public async Task Temporary_password_blocks_other_endpoints_until_changed()
    {
        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund);
        using var client = NewClient();
        await client.LoginAsync(student.Login, student.Password);

        // Mọi chức năng khác bị chặn: endpoint chỉ cần đăng nhập và endpoint theo quỹ có [RequirePermission].
        await (await client.GetAsync("/test-probe")).AssertProblemAsync(HttpStatusCode.Forbidden, "password.change_required");
        await (await client.GetAsync($"/test-probe/funds/{fund.Id}")).AssertProblemAsync(HttpStatusCode.Forbidden, "password.change_required");

        // Quỹ không thuộc mình vẫn là 404 trước (roles-and-permissions.md mục 3).
        await (await client.GetAsync($"/test-probe/funds/{Guid.NewGuid()}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        // Được phép: GET / PUT me, change-password, logout.
        Assert.True((await (await client.GetAsync("/auth/me")).ReadMeAsync()).MustChangePassword);
        Assert.Equal("Khang Nguyễn", (await (await client.PutAsync("/auth/me", new { fullName = "Khang Nguyễn" })).ReadMeAsync()).FullName);

        await (await client.PostAsync("/auth/change-password", new { currentPassword = "SaiMatKhau1", newPassword = "MatKhauMoi1" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "auth.wrong_password");
        await (await client.PostAsync("/auth/change-password", new { currentPassword = student.Password, newPassword = "yeu" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "auth.password_policy");
        await (await client.PostAsync("/auth/change-password", new { currentPassword = student.Password, newPassword = student.Password }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "auth.password_reused");

        var changed = await (await client.PostAsync("/auth/change-password", new { currentPassword = student.Password, newPassword = "MatKhauMoi1" }))
            .ReadSessionAsync(HttpStatusCode.OK);
        Assert.False(changed.User.MustChangePassword);

        // Sau khi đổi: dùng được các chức năng khác với cùng phiên (cookie mới, token mới).
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test-probe")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/test-probe/funds/{fund.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync("/auth/me", new { fullName = "Nguyễn Minh Khang" })).StatusCode);

        using var other = NewClient();
        await (await other.PostAsync("/auth/login", new { login = student.Login, password = student.Password }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        Assert.False((await other.LoginAsync(student.Login, "MatKhauMoi1")).User.MustChangePassword);
        Assert.Contains(await _data.AuditLogsAsync(student.UserId), log => log.Action == "Auth.PasswordChanged");
    }

    [Fact]
    public async Task Changing_password_ends_every_other_session()
    {
        var email = TestData.NewEmail();
        using var deviceOne = NewClient();
        await (await deviceOne.PostAsync("/auth/register-teacher", RegisterBody(email))).ReadSessionAsync(HttpStatusCode.Created);
        using var deviceTwo = NewClient();
        await deviceTwo.LoginAsync(email, TeacherPassword);

        await (await deviceOne.PostAsync("/auth/change-password", new { currentPassword = TeacherPassword, newPassword = "MatKhauMoi2" }))
            .ReadSessionAsync(HttpStatusCode.OK);

        await (await deviceTwo.GetAsync("/auth/me")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(HttpStatusCode.OK, (await deviceOne.GetAsync("/auth/me")).StatusCode);
    }

    // ---------- Tài khoản & phiên ----------

    [Fact]
    public async Task Logout_clears_session()
    {
        using var client = NewClient();
        await (await client.PostAsync("/auth/register-teacher", RegisterBody(TestData.NewEmail()))).ReadSessionAsync(HttpStatusCode.Created);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/auth/logout")).StatusCode);

        await (await client.GetAsync("/auth/me")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Update_me_trims_full_name_and_validates()
    {
        using var client = NewClient();
        var session = await (await client.PostAsync("/auth/register-teacher", RegisterBody(TestData.NewEmail()))).ReadSessionAsync(HttpStatusCode.Created);

        var me = await (await client.PutAsync("/auth/me", new { fullName = "  Trần Thị Hạnh  " })).ReadMeAsync();
        Assert.Equal("Trần Thị Hạnh", me.FullName);

        await (await client.PutAsync("/auth/me", new { fullName = "   " })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await client.PutAsync("/auth/me", new { fullName = new string('a', 201) })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        var audit = Assert.Single(await _data.AuditLogsAsync(session.User.Id), log => log.Action == "Account.UpdateProfile");
        Assert.Contains("Trần Thị Hạnh", audit.NewValues);
    }

    [Fact]
    public async Task Me_requires_sign_in()
    {
        using var client = NewClient();
        await (await client.GetAsync("/auth/me")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Me_lists_memberships_with_roles_derived_from_data()
    {
        // Giáo viên đăng ký rồi có quỹ → GVCN.
        var email = TestData.NewEmail();
        using var teacherClient = NewClient();
        var teacher = await (await teacherClient.PostAsync("/auth/register-teacher", RegisterBody(email))).ReadSessionAsync(HttpStatusCode.Created);
        var fund = await _data.CreateFundAsync(teacher.User.Id, TestData.Unique("Quỹ lớp 5A "));

        var teacherMembership = Assert.Single((await (await teacherClient.GetAsync("/auth/me")).ReadMeAsync()).Memberships);
        Assert.Equal(fund.Id, teacherMembership.FundId);
        Assert.Equal(fund.Name, teacherMembership.FundName);
        Assert.Equal("Teacher", teacherMembership.Role);
        Assert.Equal(["Fund.Manage", "Fund.View", "Ledger.ViewFull", "Member.View", "Student.Manage", "Treasurer.Assign"], teacherMembership.Permissions);

        // Học sinh được phân công → Thủ quỹ.
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        using var treasurerClient = NewClient();
        var treasurerMembership = Assert.Single((await treasurerClient.LoginAsync(treasurer.Login, treasurer.Password)).User.Memberships);
        Assert.Equal("Treasurer", treasurerMembership.Role);
        Assert.Equal(["Category.Manage", "Fund.View", "Ledger.ViewFull", "Member.View", "Transaction.Manage"], treasurerMembership.Permissions);

        // Phụ huynh có hai con trong quỹ → Phụ huynh, kèm tên con (kể cả khi con là thủ quỹ).
        var student = await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo");
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value, treasurer.StudentId!.Value);
        using var parentClient = NewClient();
        var parentSession = await parentClient.LoginAsync(parent.Login, parent.Password);
        Assert.Equal("Parent", parentSession.User.Kind);
        var parentMembership = Assert.Single(parentSession.User.Memberships);
        Assert.Equal("Parent", parentMembership.Role);
        Assert.Equal(PublicPermissions, parentMembership.Permissions);
        Assert.Equal(["Lê Gia Bảo", "Phạm Thu Trang"], parentMembership.LinkedStudents);
    }

    // ---------- Không lộ bí mật (NFR-10) ----------

    [Fact]
    public async Task Passwords_hashes_and_cookies_never_reach_logs_or_audit_log()
    {
        const string secretPassword = "BiMat-Dang-Ky-7391";
        const string newSecretPassword = "BiMat-Moi-Doi-8264";
        var email = TestData.NewEmail();
        using var client = NewClient();

        var response = await client.PostAsync("/auth/register-teacher", RegisterBody(email, secretPassword));
        var session = await response.ReadSessionAsync(HttpStatusCode.Created);
        var cookies = response.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0].Split('=', 2)[1]).ToList();
        await (await client.PostAsync("/auth/login", new { login = email, password = "Sai-" + secretPassword })).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await client.LoginAsync(email, secretPassword);
        await (await client.PostAsync("/auth/change-password", new { currentPassword = secretPassword, newPassword = newSecretPassword })).ReadSessionAsync(HttpStatusCode.OK);

        var hash = (await _data.FindUserAsync(session.User.Id)).PasswordHash;
        string[] secrets = [secretPassword, newSecretPassword, hash, session.AntiforgeryToken, ApiFactory.TestPepper, .. cookies];

        var audit = await _data.AuditLogsAsync(session.User.Id);
        Assert.NotEmpty(audit);
        var auditText = string.Join('\n', audit.Select(log => $"{log.Action} {log.OldValues} {log.NewValues}"));
        var logText = string.Join('\n', fixture.Factory.Logs.Events);
        Assert.NotEmpty(fixture.Factory.Logs.Events);

        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, auditText, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, logText, StringComparison.Ordinal);
        }
    }

    // ---------- Pepper (SECRET_KEY) ----------

    [Fact]
    public async Task Password_created_before_pepper_still_signs_in_and_is_rehashed_with_pepper()
    {
        var teacher = await _data.CreateTeacherWithLegacyHashAsync("Matkhau123");
        var legacyHash = (await _data.FindUserAsync(teacher.UserId)).PasswordHash;
        using var client = NewClient();

        await client.LoginAsync(teacher.Login, teacher.Password);

        var upgradedHash = (await _data.FindUserAsync(teacher.UserId)).PasswordHash;
        Assert.NotEqual(legacyHash, upgradedHash);
        Assert.Equal(
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,
            new Microsoft.AspNetCore.Identity.PasswordHasher<Domain.Entities.User>().VerifyHashedPassword(null!, upgradedHash, teacher.Password));

        // Lần sau vẫn đăng nhập bình thường bằng hash mới (có pepper).
        using var again = NewClient();
        await again.LoginAsync(teacher.Login, teacher.Password);
    }

    // ---------- Rate limit ----------

    [Fact]
    public async Task Login_is_rate_limited_per_client()
    {
        await using var limited = new ApiFactory(fixture.ConnectionString, authPermitLimit: 2);
        using var client = limited.CreateApiClient();
        var body = new { login = "khong.ton.tai@example.vn", password = "Matkhau123" };

        await (await client.PostAsync("/auth/login", body)).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await (await client.PostAsync("/auth/login", body)).AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await (await client.PostAsync("/auth/login", body)).AssertProblemAsync(HttpStatusCode.TooManyRequests, "auth.rate_limited");
    }
}
