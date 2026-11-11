using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Auth;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Students;

/// <summary>M3 – học sinh & phụ huynh qua HTTP thật. Test case S-01, S-02, S-05, S-06, S-09, P-01, P-02, P-03, P-05, M-02 (Student, ParentLink); FR-24, FR-25, FR-26, BR-29, BR-30, BR-33, BR-35, NFR-10, NFR-11.</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentsTests(PostgresFixture fixture)
{
    private const string TeacherPassword = "Matkhau123";
    private const string NewPassword = "Matkhau-456";

    private readonly TestData _data = fixture.Data;

    private sealed record Teacher(ApiClient Client, Guid UserId, string Email, Guid FundId);

    private static readonly Regex TemporaryPasswordPattern = new(@"^[A-HJ-NP-Z][a-km-np-z]{3}-[a-km-np-z]{4}-[2-9]{4}-[a-km-np-z]{4}$", RegexOptions.CultureInvariant);

    private static Regex TemporaryPasswordFormat() => TemporaryPasswordPattern;

    private async Task<Teacher> TeacherWithFundAsync()
    {
        var client = fixture.Factory.CreateApiClient();
        var email = TestData.NewEmail();
        var session = await (await client.PostAsync("/auth/register-teacher", new
        {
            fullName = "Cô Hạnh",
            email,
            password = TeacherPassword,
            confirmPassword = TeacherPassword,
        })).ReadSessionAsync(HttpStatusCode.Created);
        var created = await client.PostAsync("/funds", new { name = TestData.Unique("Quỹ lớp 5A ") });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var fundId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return new Teacher(client, session.User.Id, email, fundId);
    }

    private static string Url(Teacher teacher, string rest = "") => $"/funds/{teacher.FundId}/students{rest}";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Mong đợi {(int)expected}, nhận {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<Guid> AddStudentAsync(Teacher teacher, string fullName)
    {
        var created = await JsonAsync(await teacher.Client.PostAsync(Url(teacher), new { fullName }), HttpStatusCode.Created);
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetStudentAsync(Teacher teacher, Guid id) =>
        await JsonAsync(await teacher.Client.GetAsync(Url(teacher, $"/{id}")), HttpStatusCode.OK);

    private static async Task<(string Login, string Password)> IssueAccountAsync(Teacher teacher, Guid id, string? username = null)
    {
        var response = await teacher.Client.PostAsync(Url(teacher, $"/{id}/account"), new { username });
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var credential = await JsonAsync(response, HttpStatusCode.OK);
        return (credential.GetProperty("login").GetString()!, credential.GetProperty("temporaryPassword").GetString()!);
    }

    private static void AssertNoStore(HttpResponseMessage response) =>
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // ---------- Thêm, xem, sửa học sinh (FR-24) ----------

    [Fact]
    public async Task Teacher_adds_lists_views_and_renames_students()
    {
        var teacher = await TeacherWithFundAsync();

        var response = await teacher.Client.PostAsync(Url(teacher), new { fullName = "  Phạm Đức Huy  " });
        var created = await JsonAsync(response, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid();
        Assert.False(created.TryGetProperty("parent", out _));

        var list = await JsonAsync(await teacher.Client.GetAsync(Url(teacher)), HttpStatusCode.OK);
        var item = Assert.Single(list.EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("Phạm Đức Huy", item.GetProperty("fullName").GetString());
        Assert.Equal("None", item.GetProperty("accountStatus").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("username").ValueKind);
        Assert.False(item.GetProperty("isTreasurer").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("parent").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.Client.PutAsync(Url(teacher, $"/{id}"), new { fullName = " Phạm Đức Huy B " })).StatusCode);
        Assert.Equal("Phạm Đức Huy B", (await GetStudentAsync(teacher, id)).GetProperty("fullName").GetString());

        var actions = (await _data.AuditLogsAsync(teacher.UserId)).Select(log => log.Action).ToArray();
        Assert.Contains("Student.Create", actions);
        Assert.Contains("Student.Update", actions);

        await (await teacher.Client.PostAsync(Url(teacher), new { fullName = "  " })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await teacher.Client.PostAsync(Url(teacher), new { fullName = new string('a', 101) })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await teacher.Client.PostAsync(Url(teacher), new { fullName = "Bảo", parentEmail = "khong-phai-email" })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await teacher.Client.GetAsync(Url(teacher, $"/{Guid.NewGuid()}"))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    // ---------- Cấp tài khoản (FR-25, BR-29) ----------

    [Fact]
    public async Task Issuing_account_suggests_username_and_returns_one_time_temporary_password()
    {
        var teacher = await TeacherWithFundAsync();
        var id = await AddStudentAsync(teacher, "Phạm Đức Huy");

        var (login, password) = await IssueAccountAsync(teacher, id);

        Assert.Matches(@"^huy\.pham\.\d{3}$", login);
        Assert.Matches(TemporaryPasswordFormat(), password);
        var detail = await GetStudentAsync(teacher, id);
        Assert.Equal(login, detail.GetProperty("username").GetString());
        Assert.Equal("PendingActivation", detail.GetProperty("accountStatus").GetString());

        var stored = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(user => user.Username == login));
        Assert.Equal(UserKind.Student, stored.Kind);
        Assert.True(stored.MustChangePassword);
        Assert.Null(stored.Email);
        Assert.Equal(stored.Id, await WithDbAsync(db => db.Students.Where(s => s.Id == id).Select(s => s.UserId).SingleAsync()));

        // Mỗi học sinh tối đa một tài khoản.
        await (await teacher.Client.PostAsync(Url(teacher, $"/{id}/account"), new { }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "student.account_exists");

        // Học sinh đăng nhập bằng tên đăng nhập + mật khẩu tạm, phải đổi mật khẩu rồi mới "Đang hoạt động".
        using var student = fixture.Factory.CreateApiClient();
        var session = await student.LoginAsync(login, password);
        Assert.Equal("Student", session.User.Kind);
        Assert.True(session.User.MustChangePassword);
        Assert.Equal(teacher.FundId, Assert.Single(session.User.Memberships).FundId);
        await (await student.PostAsync("/auth/change-password", new { currentPassword = password, newPassword = NewPassword })).ReadSessionAsync(HttpStatusCode.OK);
        Assert.Equal("Active", (await GetStudentAsync(teacher, id)).GetProperty("accountStatus").GetString());
    }

    [Fact]
    public async Task Issuing_account_with_chosen_username_validates_format_and_uniqueness()
    {
        var teacher = await TeacherWithFundAsync();
        var first = await AddStudentAsync(teacher, "Lê Gia Bảo");
        var second = await AddStudentAsync(teacher, "Lê Gia Bảo");
        var username = TestData.Unique("Bao.Le.");

        var (login, _) = await IssueAccountAsync(teacher, first, "  " + username + " ");
        Assert.Equal(username.ToLowerInvariant(), login);

        await (await teacher.Client.PostAsync(Url(teacher, $"/{second}/account"), new { username = login.ToUpperInvariant() }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "user.username_taken");
        foreach (var invalid in new[] { "ab", "bảo.lê", "bao le", new string('a', 61) })
        {
            await (await teacher.Client.PostAsync(Url(teacher, $"/{second}/account"), new { username = invalid }))
                .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "user.username");
        }

        Assert.Equal("None", (await GetStudentAsync(teacher, second)).GetProperty("accountStatus").GetString());
    }

    [Fact]
    public async Task Resetting_password_ends_every_session_and_requires_a_new_change()
    {
        var teacher = await TeacherWithFundAsync();
        var id = await AddStudentAsync(teacher, "Nguyễn Minh Khang");
        await (await teacher.Client.PostAsync(Url(teacher, $"/{id}/reset-password")))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "student.no_account");

        var (login, firstPassword) = await IssueAccountAsync(teacher, id);
        using var student = fixture.Factory.CreateApiClient();
        await student.LoginAsync(login, firstPassword);
        await (await student.PostAsync("/auth/change-password", new { currentPassword = firstPassword, newPassword = NewPassword })).ReadSessionAsync(HttpStatusCode.OK);

        var response = await teacher.Client.PostAsync(Url(teacher, $"/{id}/reset-password"));
        AssertNoStore(response);
        var credential = await JsonAsync(response, HttpStatusCode.OK);
        var resetPassword = credential.GetProperty("temporaryPassword").GetString()!;
        Assert.Equal(login, credential.GetProperty("login").GetString());
        Assert.Matches(TemporaryPasswordFormat(), resetPassword);
        Assert.NotEqual(firstPassword, resetPassword);

        // Phiên cũ hết hiệu lực; mật khẩu học sinh tự đặt không còn dùng được.
        await (await student.GetAsync("/auth/me")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
        using var again = fixture.Factory.CreateApiClient();
        await (await again.PostAsync("/auth/login", new { login, password = NewPassword }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        Assert.True((await again.LoginAsync(login, resetPassword)).User.MustChangePassword);
        Assert.Equal("PendingActivation", (await GetStudentAsync(teacher, id)).GetProperty("accountStatus").GetString());
    }

    // ---------- Liên kết phụ huynh (FR-26, BR-30) ----------

    [Fact]
    public async Task Linking_new_parent_email_creates_parent_account_with_one_time_password()
    {
        var teacher = await TeacherWithFundAsync();
        var id = await AddStudentAsync(teacher, "Phạm Thu Trang");
        var email = TestData.NewEmail();

        var response = await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent"), new { email = " " + email + " ", fullName = "Phạm Thị Hoa" });
        AssertNoStore(response);
        var result = await JsonAsync(response, HttpStatusCode.OK);

        Assert.True(result.GetProperty("accountCreated").GetBoolean());
        Assert.False(result.GetProperty("emailSent").GetBoolean());
        var credential = result.GetProperty("credential");
        Assert.Equal(email, credential.GetProperty("login").GetString());
        var password = credential.GetProperty("temporaryPassword").GetString()!;
        Assert.Matches(TemporaryPasswordFormat(), password);

        var parent = (await GetStudentAsync(teacher, id)).GetProperty("parent");
        Assert.Equal("Phạm Thị Hoa", parent.GetProperty("fullName").GetString());
        Assert.Equal(email, parent.GetProperty("email").GetString());
        Assert.True(parent.GetProperty("pendingActivation").GetBoolean());

        using var parentClient = fixture.Factory.CreateApiClient();
        var session = await parentClient.LoginAsync(email, password);
        Assert.Equal("Parent", session.User.Kind);
        Assert.True(session.User.MustChangePassword);
        Assert.Equal(["Phạm Thu Trang"], Assert.Single(session.User.Memberships).LinkedStudents);

        // v1: mỗi học sinh một phụ huynh đang liên kết.
        await (await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent"), new { email = TestData.NewEmail() }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "parent_link.exists");
    }

    [Fact]
    public async Task Linking_existing_parent_email_only_links_without_new_account()
    {
        var teacherA = await TeacherWithFundAsync();
        var teacherB = await TeacherWithFundAsync();
        var childA = await AddStudentAsync(teacherA, "Lê Gia Bảo");
        var childB = await AddStudentAsync(teacherB, "Lê Thu Hà");
        var email = TestData.NewEmail();
        var first = await JsonAsync(await teacherA.Client.PostAsync(Url(teacherA, $"/{childA}/parent"), new { email }), HttpStatusCode.OK);
        var password = first.GetProperty("credential").GetProperty("temporaryPassword").GetString()!;

        // Cùng email (khác hoa thường) ở quỹ của giáo viên khác → chỉ liên kết, không cấp mật khẩu.
        var second = await JsonAsync(
            await teacherB.Client.PostAsync(Url(teacherB, $"/{childB}/parent"), new { email = email.ToUpperInvariant(), fullName = "Tên khác" }),
            HttpStatusCode.OK);

        Assert.False(second.GetProperty("accountCreated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("credential").ValueKind);
        Assert.False(second.GetProperty("emailSent").GetBoolean());
        Assert.Equal(1, await WithDbAsync(db => db.Users.CountAsync(user => user.NormalizedEmail == email.ToLowerInvariant())));
        Assert.Equal("Phụ huynh Lê Gia Bảo", (await GetStudentAsync(teacherB, childB)).GetProperty("parent").GetProperty("fullName").GetString());

        // Một tài khoản phụ huynh, hai con ở hai quỹ.
        using var parent = fixture.Factory.CreateApiClient();
        var session = await parent.LoginAsync(email, password);
        Assert.Equal(
            new[] { teacherA.FundId, teacherB.FundId }.Order(),
            session.User.Memberships.Select(membership => membership.FundId).Order());
    }

    [Fact]
    public async Task Teacher_or_student_email_cannot_be_linked_as_parent()
    {
        var teacher = await TeacherWithFundAsync();
        var id = await AddStudentAsync(teacher, "Đỗ Hà My");

        await (await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent"), new { email = teacher.Email.ToUpperInvariant() }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "parent.email_in_use");

        // Thêm học sinh kèm email giáo viên: cả thao tác thất bại, không có học sinh nửa vời.
        await (await teacher.Client.PostAsync(Url(teacher), new { fullName = "Vũ Ngọc Ánh", parentEmail = teacher.Email }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "parent.email_in_use");
        Assert.Single((await JsonAsync(await teacher.Client.GetAsync(Url(teacher)), HttpStatusCode.OK)).EnumerateArray());
    }

    [Fact]
    public async Task Adding_student_with_parent_email_links_parent_in_one_step()
    {
        var teacher = await TeacherWithFundAsync();
        var email = TestData.NewEmail();

        var response = await teacher.Client.PostAsync(Url(teacher), new { fullName = "Trần Quốc Việt", parentEmail = email, parentFullName = "Trần Thị Mến" });
        AssertNoStore(response);
        var created = await JsonAsync(response, HttpStatusCode.Created);

        var parent = created.GetProperty("parent");
        Assert.True(parent.GetProperty("accountCreated").GetBoolean());
        Assert.Equal(email, parent.GetProperty("credential").GetProperty("login").GetString());
        var detail = await GetStudentAsync(teacher, created.GetProperty("id").GetGuid());
        Assert.Equal("Trần Thị Mến", detail.GetProperty("parent").GetProperty("fullName").GetString());
    }

    // ---------- Gỡ liên kết (BR-33) ----------

    [Fact]
    public async Task Unlinking_parent_marks_link_removed_keeps_history_and_account()
    {
        var teacher = await TeacherWithFundAsync();
        var id = await AddStudentAsync(teacher, "Phạm Thu Trang");
        var email = TestData.NewEmail();
        var linked = await JsonAsync(await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent"), new { email }), HttpStatusCode.OK);
        var password = linked.GetProperty("credential").GetProperty("temporaryPassword").GetString()!;
        using var parent = fixture.Factory.CreateApiClient();
        await parent.LoginAsync(email, password);
        await (await parent.PostAsync("/auth/change-password", new { currentPassword = password, newPassword = NewPassword })).ReadSessionAsync(HttpStatusCode.OK);
        Assert.Contains(
            (await JsonAsync(await parent.GetAsync("/funds"), HttpStatusCode.OK)).EnumerateArray(),
            fund => fund.GetProperty("id").GetGuid() == teacher.FundId);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent/unlink"))).StatusCode);

        Assert.Equal(JsonValueKind.Null, (await GetStudentAsync(teacher, id)).GetProperty("parent").ValueKind);
        var link = await WithDbAsync(db => db.ParentLinks.AsNoTracking().SingleAsync(item => item.StudentId == id));
        Assert.False(link.IsActive);
        Assert.Equal(teacher.UserId, link.RemovedBy);
        Assert.NotNull(link.RemovedAt);
        Assert.Contains(await _data.AuditLogsAsync(teacher.UserId), log => log.Action == "ParentLink.Remove");

        // Quyền đổi ngay ở request kế tiếp: phụ huynh không còn thuộc quỹ.
        await (await parent.GetAsync($"/funds/{teacher.FundId}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.DoesNotContain(
            (await JsonAsync(await parent.GetAsync("/funds"), HttpStatusCode.OK)).EnumerateArray(),
            fund => fund.GetProperty("id").GetGuid() == teacher.FundId);

        await (await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent/unlink"))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        // Tài khoản phụ huynh vẫn còn: liên kết lại chỉ tạo liên kết mới, không cấp mật khẩu.
        var relinked = await JsonAsync(await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent"), new { email }), HttpStatusCode.OK);
        Assert.False(relinked.GetProperty("accountCreated").GetBoolean());
        Assert.Equal(2, await WithDbAsync(db => db.ParentLinks.CountAsync(item => item.StudentId == id)));
    }

    // ---------- Phân quyền, bảo vệ dữ liệu ----------

    [Fact]
    public async Task Only_homeroom_teacher_manages_students()
    {
        var teacher = await TeacherWithFundAsync();
        var fund = new SeededFund(teacher.FundId, "Quỹ", teacher.UserId);
        var studentId = await AddStudentAsync(teacher, "Lê Gia Bảo");
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var parent = await _data.CreateParentAccountAsync(fund, studentId);

        foreach (var account in new[] { treasurer, parent })
        {
            using var client = fixture.Factory.CreateApiClient();
            await client.LoginAsync(account.Login, account.Password);
            await (await client.PostAsync("/auth/change-password", new { currentPassword = account.Password, newPassword = NewPassword })).ReadSessionAsync(HttpStatusCode.OK);

            await (await client.GetAsync(Url(teacher))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PostAsync(Url(teacher), new { fullName = "Học sinh lạ" })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PostAsync(Url(teacher, $"/{studentId}/account"), new { })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        // Giáo viên khác: quỹ không thuộc mình → 404; học sinh của quỹ khác dưới đường dẫn quỹ mình → 404.
        var other = await TeacherWithFundAsync();
        await (await other.Client.GetAsync(Url(teacher))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await other.Client.PostAsync(Url(other, $"/{studentId}/account"), new { })).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await other.Client.PutAsync(Url(other, $"/{studentId}"), new { fullName = "Đổi trộm" })).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal("Lê Gia Bảo", (await GetStudentAsync(teacher, studentId)).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task List_shows_treasurer_flag_and_parent_of_each_student()
    {
        var teacher = await TeacherWithFundAsync();
        var fund = new SeededFund(teacher.FundId, "Quỹ", teacher.UserId);
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var withParent = await AddStudentAsync(teacher, "Lê Gia Bảo");
        await JsonAsync(await teacher.Client.PostAsync(Url(teacher, $"/{withParent}/parent"), new { email = TestData.NewEmail(), fullName = "Lê Thu Hà" }), HttpStatusCode.OK);

        var list = (await JsonAsync(await teacher.Client.GetAsync(Url(teacher)), HttpStatusCode.OK)).EnumerateArray().ToArray();

        Assert.Equal(["Lê Gia Bảo", "Phạm Thu Trang"], list.Select(item => item.GetProperty("fullName").GetString()));
        var bao = list[0];
        Assert.False(bao.GetProperty("isTreasurer").GetBoolean());
        Assert.Equal("Lê Thu Hà", bao.GetProperty("parent").GetProperty("fullName").GetString());
        var trang = list[1];
        Assert.Equal(treasurer.StudentId, trang.GetProperty("id").GetGuid());
        Assert.True(trang.GetProperty("isTreasurer").GetBoolean());
        Assert.Equal(treasurer.Login, trang.GetProperty("username").GetString());
        Assert.Equal("Active", trang.GetProperty("accountStatus").GetString());
    }

    [Fact]
    public async Task Temporary_passwords_never_reach_audit_log_or_logs()
    {
        var teacher = await TeacherWithFundAsync();
        var id = await AddStudentAsync(teacher, "Nguyễn Minh Khang");
        var (_, issued) = await IssueAccountAsync(teacher, id);
        var reset = (await JsonAsync(await teacher.Client.PostAsync(Url(teacher, $"/{id}/reset-password")), HttpStatusCode.OK))
            .GetProperty("temporaryPassword").GetString()!;
        var parent = (await JsonAsync(await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent"), new { email = TestData.NewEmail() }), HttpStatusCode.OK))
            .GetProperty("credential").GetProperty("temporaryPassword").GetString()!;

        var audit = await _data.AuditLogsAsync(teacher.UserId);
        var auditText = string.Join('\n', audit.Select(log => $"{log.Action} {log.OldValues} {log.NewValues}"));
        Assert.Contains("Student.IssueAccount", auditText);
        Assert.Contains("Student.ResetPassword", auditText);
        Assert.Contains("Parent.CreateAccount", auditText);
        var logText = string.Join('\n', fixture.Factory.Logs.Events);

        foreach (var secret in new[] { issued, reset, parent })
        {
            Assert.DoesNotContain(secret, auditText, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, logText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Students_and_parent_links_cannot_be_hard_deleted()
    {
        var teacher = await TeacherWithFundAsync();
        var id = await AddStudentAsync(teacher, "Phạm Thu Trang");
        await JsonAsync(await teacher.Client.PostAsync(Url(teacher, $"/{id}/parent"), new { email = TestData.NewEmail() }), HttpStatusCode.OK);

        // Trigger TR_NoDelete (AddDatabaseProtections) chặn xóa cứng – kể cả khi gọi thẳng SQL.
        var studentError = await Assert.ThrowsAsync<PostgresException>(() =>
            WithDbAsync(db => db.Students.Where(student => student.Id == id).ExecuteDeleteAsync()));
        var linkError = await Assert.ThrowsAsync<PostgresException>(() =>
            WithDbAsync(db => db.ParentLinks.Where(link => link.StudentId == id).ExecuteDeleteAsync()));

        Assert.Equal("P0001", studentError.SqlState);
        Assert.Equal("P0001", linkError.SqlState);
        Assert.Equal(1, await WithDbAsync(db => db.Students.CountAsync(student => student.Id == id)));
    }

    [Fact]
    public async Task List_reports_all_four_account_statuses()
    {
        var teacher = await TeacherWithFundAsync();
        var fund = new SeededFund(teacher.FundId, "Quỹ", teacher.UserId);
        var none = await _data.CreateStudentAsync(fund, "An");
        var pending = await _data.CreateStudentAccountAsync(fund, "Bình");
        var active = await _data.CreateStudentAccountAsync(fund, "Chi", activated: true);
        var disabled = await _data.CreateStudentAccountAsync(fund, "Dũng", activated: true);
        await _data.DisableUserAsync(disabled.UserId);

        var list = (await JsonAsync(await teacher.Client.GetAsync(Url(teacher)), HttpStatusCode.OK)).EnumerateArray().ToArray();

        Assert.Equal(
            ["None", "PendingActivation", "Active", "Disabled"],
            list.Select(item => item.GetProperty("accountStatus").GetString()));
        Assert.Equal(
            [none.StudentId, pending.StudentId!.Value, active.StudentId!.Value, disabled.StudentId!.Value],
            list.Select(item => item.GetProperty("id").GetGuid()));
    }
}
