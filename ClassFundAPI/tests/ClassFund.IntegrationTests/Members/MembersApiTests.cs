using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.IntegrationTests.Foundation;

namespace ClassFund.IntegrationTests.Members;

/// <summary>M3-T3: API thành viên chạy thật (cookie, antiforgery, phân quyền) trên PostgreSQL tạm trong Docker, không dùng Neon.</summary>
public class MembersApiTests(PostgresFixture database) : IClassFixture<PostgresFixture>, IDisposable
{
    private const string Pepper = "integration-test-pepper-0123456789-abcdef";
    private const string Password = "Passw0rd1";

    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
    }

    private WebApplicationFactory<Program> CreateFactory(int authLimit = 1000)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:App", database.ConnectionString);
            builder.UseSetting("SECRET_KEY", Pepper);
            builder.UseSetting("RateLimiting:AuthPermitLimit", authLimit.ToString());
            builder.UseEnvironment("Development");
        });
        _factories.Add(factory);
        return factory;
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    private static async Task SetTokenAsync(HttpClient client, string? token = null)
    {
        token ??= (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/antiforgery")).GetProperty("antiforgeryToken").GetString()!;
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    /// <summary>Một "trình duyệt": client có cookie riêng và token antiforgery.</summary>
    private async Task<HttpClient> BrowserAsync(WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? CreateFactory()).CreateClient();
        await SetTokenAsync(client);
        return client;
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (response.IsSuccessStatusCode)
        {
            await SetTokenAsync(client, (await ReadAsync(response)).GetProperty("antiforgeryToken").GetString());
        }

        return response;
    }

    /// <summary>Thủ quỹ chính đã đăng nhập kèm một quỹ (M2 chưa có nên tạo quỹ trực tiếp trong DB).</summary>
    private async Task<(HttpClient Client, Guid FundId, Guid UserId, string Email)> OwnerWithFundAsync(WebApplicationFactory<Program>? factory = null)
    {
        var client = await BrowserAsync(factory);
        var email = NewEmail();
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "Thủ Quỹ Chính", email, password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var body = await ReadAsync(register);
        await SetTokenAsync(client, body.GetProperty("antiforgeryToken").GetString());
        var userId = body.GetProperty("user").GetProperty("id").GetGuid();

        await using var context = database.CreateContext();
        var fund = new Fund("Lớp 10A1", userId, "2026-2027", DateTimeOffset.UtcNow);
        context.Add(fund);
        context.Add(new FundMember(fund.Id, userId, MemberRole.Owner, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        return (client, fund.Id, userId, email);
    }

    private static string Url(Guid fundId, string path = "") => $"/api/v1/funds/{fundId}/members{path}";

    private async Task<(Guid MemberId, Guid UserId, string Email, string TemporaryPassword)> AddStudentAsync(HttpClient owner, Guid fundId, string? name = null)
    {
        var email = NewEmail();
        var response = await owner.PostAsJsonAsync(Url(fundId), new { email, fullName = name ?? "Học Sinh" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        var member = body.GetProperty("member");
        return (member.GetProperty("id").GetGuid(), member.GetProperty("userId").GetGuid(), email, body.GetProperty("temporaryPassword").GetString()!);
    }

    private async Task SetRoleAsync(Guid memberId, MemberRole role)
    {
        await using var context = database.CreateContext();
        var member = await context.Set<FundMember>().SingleAsync(item => item.Id == memberId);
        member.Role = role;
        await context.SaveChangesAsync();
    }

    // ---------- Thêm một email ----------

    [Fact]
    public async Task Owner_adds_a_new_email_and_the_student_can_sign_in_with_the_temporary_password_but_must_change_it()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId, "Trần An");

        var list = await ReadAsync(await owner.GetAsync(Url(fundId)));
        Assert.Equal(2, list.GetProperty("total").GetInt32()); // owner + học sinh
        var row = list.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("email").GetString() == student.Email);
        Assert.Equal("Student", row.GetProperty("role").GetString());
        Assert.Equal("Active", row.GetProperty("status").GetString());
        Assert.True(row.GetProperty("mustChangePassword").GetBoolean());

        var studentClient = await BrowserAsync();
        var login = await LoginAsync(studentClient, student.Email, student.TemporaryPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = (await ReadAsync(login)).GetProperty("user");
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(fundId, me.GetProperty("memberships")[0].GetProperty("fundId").GetGuid());

        // Còn mật khẩu tạm: bị chặn trước cả việc kiểm tra quyền.
        var blocked = await studentClient.GetAsync(Url(fundId));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password.change_required", await CodeOfAsync(blocked));
    }

    [Fact]
    public async Task Add_audit_is_written_for_the_fund_without_any_password_material()
    {
        var (owner, fundId, ownerId, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);

        await using var context = database.CreateContext();
        var log = await context.Set<AuditLog>().SingleAsync(item => item.EntityId == student.MemberId.ToString() && item.Action == "member.add");
        Assert.Equal(fundId, log.FundId);
        Assert.Equal(ownerId, log.ActorUserId);
        Assert.Equal(FinancialScope.General, log.Scope);
        var user = await context.Set<User>().SingleAsync(item => item.Id == student.UserId);
        var json = (log.BeforeJson ?? "") + (log.AfterJson ?? "");
        Assert.DoesNotContain(student.TemporaryPassword, json);
        Assert.DoesNotContain(user.PasswordHash, json);
        Assert.DoesNotContain(user.SecurityStamp, json);
    }

    [Fact]
    public async Task Add_existing_account_gives_no_temporary_password_and_does_not_touch_the_account()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var other = await OwnerWithFundAsync(); // tài khoản tự đăng ký ở quỹ khác

        var response = await owner.PostAsJsonAsync(Url(fundId), new { email = other.Email.ToUpperInvariant(), fullName = "Tên bị bỏ qua" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("ExistingAdded", body.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("temporaryPassword").ValueKind);
        Assert.Equal("Thủ Quỹ Chính", body.GetProperty("member").GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Add_same_email_twice_is_409_member_already_exists()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);

        var again = await owner.PostAsJsonAsync(Url(fundId), new { email = student.Email.ToUpperInvariant(), fullName = "Học Sinh" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("member.already_exists", await CodeOfAsync(again));
    }

    [Fact]
    public async Task Add_with_invalid_data_is_400_with_field_errors()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();

        var response = await owner.PostAsJsonAsync(Url(fundId), new { email = "khong-phai-email", fullName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("email", out _));
        Assert.True(body.GetProperty("errors").TryGetProperty("fullName", out _));
    }

    [Fact]
    public async Task Add_to_an_archived_fund_is_422_fund_archived()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        await using (var context = database.CreateContext())
        {
            (await context.Set<Fund>().SingleAsync(item => item.Id == fundId)).Status = FundStatus.Archived;
            await context.SaveChangesAsync();
        }

        var response = await owner.PostAsJsonAsync(Url(fundId), new { email = NewEmail(), fullName = "A" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fund.archived", await CodeOfAsync(response));
    }

    // ---------- Thêm nhiều email (OQ-20) ----------

    [Fact]
    public async Task Bulk_add_reports_each_row_and_every_new_account_can_sign_in()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var existing = await AddStudentAsync(owner, fundId);
        var fresh = new[] { NewEmail(), NewEmail() };

        var response = await owner.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails = new[] { fresh[0], existing.Email, "khong-hop-le", fresh[1], fresh[0].ToUpperInvariant() } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await ReadAsync(response)).GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(4, results.Length); // email trùng trong danh sách bị bỏ
        Assert.Equal(new[] { "Created", "AlreadyExists", "Invalid", "Created" }, results.Select(item => item.GetProperty("outcome").GetString()!).ToArray());
        Assert.Equal(JsonValueKind.Null, results[1].GetProperty("temporaryPassword").ValueKind);
        Assert.Equal(JsonValueKind.Null, results[2].GetProperty("temporaryPassword").ValueKind);

        foreach (var index in new[] { 0, 3 })
        {
            var student = await BrowserAsync();
            var login = await LoginAsync(student, results[index].GetProperty("email").GetString()!, results[index].GetProperty("temporaryPassword").GetString()!);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
    }

    [Fact]
    public async Task Bulk_add_writes_one_audit_row_per_added_member_without_passwords()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var emails = new[] { NewEmail(), NewEmail(), NewEmail() };

        var response = await owner.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails });
        var passwords = (await ReadAsync(response)).GetProperty("results").EnumerateArray().Select(item => item.GetProperty("temporaryPassword").GetString()!).ToArray();

        await using var context = database.CreateContext();
        var logs = await context.Set<AuditLog>().Where(item => item.FundId == fundId && item.Action == "member.add").ToListAsync();
        Assert.Equal(3, logs.Count);
        Assert.All(logs, log => Assert.All(passwords, password => Assert.DoesNotContain(password, (log.BeforeJson ?? "") + (log.AfterJson ?? ""))));
    }

    [Fact]
    public async Task Bulk_add_rejects_empty_and_more_than_50_with_400()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();

        var empty = await owner.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var tooMany = await owner.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails = Enumerable.Range(1, 51).Select(_ => NewEmail()).ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal("validation_failed", await CodeOfAsync(tooMany));
    }

    [Fact]
    public async Task Bulk_add_is_rate_limited_per_user()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails = new[] { "khong-hop-le" } })).StatusCode);
        }

        var limited = await owner.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails = new[] { "khong-hop-le" } });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    // ---------- Danh sách ----------

    [Fact]
    public async Task List_supports_search_role_status_and_paging()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var an = await AddStudentAsync(owner, fundId, "Nguyễn Văn An");
        await AddStudentAsync(owner, fundId, "Trần Thị Bình");
        var gone = await AddStudentAsync(owner, fundId, "Lê Đã Nghỉ");
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync(Url(fundId, $"/{gone.MemberId}/remove"), null)).StatusCode);

        Assert.Equal(3, (await ReadAsync(await owner.GetAsync(Url(fundId)))).GetProperty("total").GetInt32());
        var removed = await ReadAsync(await owner.GetAsync(Url(fundId) + "?status=Removed"));
        Assert.Equal("Lê Đã Nghỉ", removed.GetProperty("items")[0].GetProperty("fullName").GetString());
        var byName = await ReadAsync(await owner.GetAsync(Url(fundId) + "?search=bình"));
        Assert.Equal(1, byName.GetProperty("total").GetInt32());
        var byEmail = await ReadAsync(await owner.GetAsync(Url(fundId) + "?search=" + an.Email[..12]));
        Assert.Equal(an.UserId, byEmail.GetProperty("items")[0].GetProperty("userId").GetGuid());
        var owners = await ReadAsync(await owner.GetAsync(Url(fundId) + "?role=Owner"));
        Assert.Equal(1, owners.GetProperty("total").GetInt32());
        var page = await ReadAsync(await owner.GetAsync(Url(fundId) + "?page=2&pageSize=2"));
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(3, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_rejects_bad_paging_with_400()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(Url(fundId) + "?pageSize=1000")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(Url(fundId) + "?page=0")).StatusCode);
    }

    // ---------- Quyền: 401 / 403 / 404 ----------

    [Fact]
    public async Task Anonymous_user_gets_401_on_every_member_endpoint()
    {
        var (_, fundId, _, _) = await OwnerWithFundAsync();
        var anonymous = await BrowserAsync();
        var memberId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(fundId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Url(fundId), new { email = NewEmail(), fullName = "A" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails = new[] { NewEmail() } })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(Url(fundId, $"/{memberId}"), new { fullName = "A" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Url(fundId, $"/{memberId}/reset-password"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(fundId, $"/{memberId}/removal-preview"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Url(fundId, $"/{memberId}/remove"), null)).StatusCode);
    }

    [Fact]
    public async Task Student_without_member_manage_gets_403_on_every_member_endpoint()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);
        var studentClient = await BrowserAsync();
        await LoginAsync(studentClient, student.Email, student.TemporaryPassword);
        var change = await studentClient.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = student.TemporaryPassword, newPassword = "MatKhauMoi1" });
        await SetTokenAsync(studentClient, (await ReadAsync(change)).GetProperty("antiforgeryToken").GetString());

        var responses = new[]
        {
            await studentClient.GetAsync(Url(fundId)),
            await studentClient.PostAsJsonAsync(Url(fundId), new { email = NewEmail(), fullName = "A" }),
            await studentClient.PostAsJsonAsync(Url(fundId, "/bulk"), new { emails = new[] { NewEmail() } }),
            await studentClient.PutAsJsonAsync(Url(fundId, $"/{student.MemberId}"), new { fullName = "A" }),
            await studentClient.PostAsync(Url(fundId, $"/{student.MemberId}/reset-password"), null),
            await studentClient.PostAsync(Url(fundId, $"/{student.MemberId}/remove"), null),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        Assert.Equal("forbidden", await CodeOfAsync(responses[0]));
    }

    [Fact]
    public async Task Person_outside_the_fund_gets_404_not_403()
    {
        var (_, fundId, _, _) = await OwnerWithFundAsync();
        var (stranger, _, _, _) = await OwnerWithFundAsync(); // thủ quỹ chính của quỹ KHÁC

        var response = await stranger.GetAsync(Url(fundId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", await CodeOfAsync(response));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(Url(fundId), new { email = NewEmail(), fullName = "A" })).StatusCode);
    }

    [Fact]
    public async Task Member_of_another_fund_or_unknown_member_is_404()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var (otherOwner, otherFundId, _, _) = await OwnerWithFundAsync();
        var foreign = await AddStudentAsync(otherOwner, otherFundId);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(Url(fundId, $"/{foreign.MemberId}"), new { fullName = "A" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync(Url(fundId, $"/{foreign.MemberId}/reset-password"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync(Url(fundId, $"/{foreign.MemberId}/remove"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(Url(fundId, $"/{Guid.NewGuid()}/removal-preview"))).StatusCode);
    }

    [Fact]
    public async Task Treasurer_cannot_touch_the_owner_or_another_treasurer_but_can_manage_students_AZ02()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var treasurer = await AddStudentAsync(owner, fundId, "Thủ Quỹ");
        var otherTreasurer = await AddStudentAsync(owner, fundId, "Thủ Quỹ 2");
        var student = await AddStudentAsync(owner, fundId);
        await SetRoleAsync(treasurer.MemberId, MemberRole.Treasurer);
        await SetRoleAsync(otherTreasurer.MemberId, MemberRole.Treasurer);

        var treasurerClient = await BrowserAsync();
        await LoginAsync(treasurerClient, treasurer.Email, treasurer.TemporaryPassword);
        var change = await treasurerClient.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = treasurer.TemporaryPassword, newPassword = "MatKhauMoi1" });
        await SetTokenAsync(treasurerClient, (await ReadAsync(change)).GetProperty("antiforgeryToken").GetString());

        var ownerRow = (await ReadAsync(await treasurerClient.GetAsync(Url(fundId) + "?role=Owner"))).GetProperty("items")[0].GetProperty("id").GetGuid();
        foreach (var protectedId in new[] { ownerRow, otherTreasurer.MemberId })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await treasurerClient.PutAsJsonAsync(Url(fundId, $"/{protectedId}"), new { fullName = "X" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await treasurerClient.PostAsync(Url(fundId, $"/{protectedId}/reset-password"), null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await treasurerClient.PostAsync(Url(fundId, $"/{protectedId}/remove"), null)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await treasurerClient.PostAsync(Url(fundId, $"/{student.MemberId}/reset-password"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await treasurerClient.PostAsJsonAsync(Url(fundId), new { email = NewEmail(), fullName = "Mới" })).StatusCode);

        // Chủ quỹ thì được thao tác trên thủ quỹ.
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(Url(fundId, $"/{otherTreasurer.MemberId}"), new { fullName = "Thủ Quỹ Đổi Tên" })).StatusCode);
    }

    // ---------- Sửa họ tên / cấp lại mật khẩu ----------

    [Fact]
    public async Task Update_name_works_and_is_visible_in_the_list()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId, "Tên cũ");

        var response = await owner.PutAsJsonAsync(Url(fundId, $"/{student.MemberId}"), new { fullName = "  Tên mới " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Tên mới", (await ReadAsync(response)).GetProperty("fullName").GetString());
        var list = await ReadAsync(await owner.GetAsync(Url(fundId) + "?search=" + Uri.EscapeDataString("tên mới")));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Cannot_rename_or_reset_a_self_registered_account_AZ07()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var (_, _, _, selfEmail) = await OwnerWithFundAsync();
        var added = await ReadAsync(await owner.PostAsJsonAsync(Url(fundId), new { email = selfEmail, fullName = "A" }));
        var memberId = added.GetProperty("member").GetProperty("id").GetGuid();

        var rename = await owner.PutAsJsonAsync(Url(fundId, $"/{memberId}"), new { fullName = "Chiếm quyền" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rename.StatusCode);
        Assert.Equal("member.account_not_managed", await CodeOfAsync(rename));
        var reset = await owner.PostAsync(Url(fundId, $"/{memberId}/reset-password"), null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reset.StatusCode);
        Assert.Equal("member.account_not_managed", await CodeOfAsync(reset));
    }

    [Fact]
    public async Task Reset_password_gives_a_new_temporary_password_ends_old_sessions_and_audits_without_secrets()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);
        var studentClient = await BrowserAsync();
        await LoginAsync(studentClient, student.Email, student.TemporaryPassword);
        Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync("/api/v1/auth/me")).StatusCode);

        var reset = await owner.PostAsync(Url(fundId, $"/{student.MemberId}/reset-password"), null);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var newPassword = (await ReadAsync(reset)).GetProperty("temporaryPassword").GetString()!;
        Assert.NotEqual(student.TemporaryPassword, newPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await studentClient.GetAsync("/api/v1/auth/me")).StatusCode); // BR-29

        var oldLogin = await LoginAsync(await BrowserAsync(), student.Email, student.TemporaryPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newClient = await BrowserAsync();
        var newLogin = await LoginAsync(newClient, student.Email, newPassword);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.True((await ReadAsync(newLogin)).GetProperty("user").GetProperty("mustChangePassword").GetBoolean());

        await using var context = database.CreateContext();
        var log = await context.Set<AuditLog>().SingleAsync(item => item.EntityId == student.MemberId.ToString() && item.Action == "member.reset_password");
        Assert.DoesNotContain(newPassword, (log.BeforeJson ?? "") + (log.AfterJson ?? ""));
    }

    // ---------- Ngừng thành viên ----------

    [Fact]
    public async Task Remove_ends_access_keeps_the_account_and_can_be_reactivated()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);
        var studentClient = await BrowserAsync();
        await LoginAsync(studentClient, student.Email, student.TemporaryPassword);
        var change = await studentClient.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = student.TemporaryPassword, newPassword = "MatKhauMoi1" });
        await SetTokenAsync(studentClient, (await ReadAsync(change)).GetProperty("antiforgeryToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync(Url(fundId))).StatusCode); // còn là thành viên

        var removed = await owner.PostAsync(Url(fundId, $"/{student.MemberId}/remove"), null);

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(0, (await ReadAsync(removed)).GetProperty("cancelledAssignments").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await studentClient.GetAsync(Url(fundId))).StatusCode); // BR-30: mất quyền truy cập quỹ
        Assert.Equal(HttpStatusCode.OK, (await studentClient.GetAsync("/api/v1/auth/me")).StatusCode);  // tài khoản vẫn còn
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync(Url(fundId, $"/{student.MemberId}/remove"), null)).StatusCode);

        var again = await owner.PostAsJsonAsync(Url(fundId), new { email = student.Email, fullName = "Học Sinh" });
        Assert.Equal("Reactivated", (await ReadAsync(again)).GetProperty("outcome").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync(Url(fundId))).StatusCode); // vào lại quỹ
    }

    [Fact]
    public async Task Remove_cancels_unpaid_assignments_of_open_charges_and_keeps_paid_ones_OQ15()
    {
        var (owner, fundId, ownerId, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);
        Guid unpaidId, paidId;
        await using (var context = database.CreateContext())
        {
            var charge = new Charge(fundId, null, "Quỹ lớp", null, 100_000, new DateOnly(2026, 12, 1), ChargeKind.Regular, ownerId, DateTimeOffset.UtcNow);
            var unpaid = new ChargeAssignment(charge.Id, student.UserId, 100_000);
            var paidCharge = new Charge(fundId, null, "Quỹ khác", null, 50_000, new DateOnly(2026, 12, 1), ChargeKind.Regular, ownerId, DateTimeOffset.UtcNow);
            var paid = new ChargeAssignment(paidCharge.Id, student.UserId, 50_000) { Status = AssignmentStatus.Paid };
            context.AddRange(charge, paidCharge, unpaid, paid);
            await context.SaveChangesAsync();
            unpaidId = unpaid.Id;
            paidId = paid.Id;
        }

        var preview = await ReadAsync(await owner.GetAsync(Url(fundId, $"/{student.MemberId}/removal-preview")));
        Assert.Equal(1, preview.GetProperty("unpaidCount").GetInt32());
        Assert.True(preview.GetProperty("canRemove").GetBoolean());

        var removed = await ReadAsync(await owner.PostAsync(Url(fundId, $"/{student.MemberId}/remove"), null));

        Assert.Equal(1, removed.GetProperty("cancelledAssignments").GetInt32());
        await using var check = database.CreateContext();
        Assert.Equal(AssignmentStatus.Cancelled, (await check.Set<ChargeAssignment>().SingleAsync(item => item.Id == unpaidId)).Status);
        Assert.Equal(AssignmentStatus.Paid, (await check.Set<ChargeAssignment>().SingleAsync(item => item.Id == paidId)).Status);
    }

    [Fact]
    public async Task Remove_is_422_while_the_member_has_a_personal_balance()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);
        await using (var context = database.CreateContext())
        {
            context.Add(new PersonalBalance(fundId, student.UserId) { Balance = 150_000 });
            await context.SaveChangesAsync();
        }

        var preview = await ReadAsync(await owner.GetAsync(Url(fundId, $"/{student.MemberId}/removal-preview")));
        Assert.False(preview.GetProperty("canRemove").GetBoolean());
        Assert.Equal(150_000, preview.GetProperty("balance").GetInt64());

        var response = await owner.PostAsync(Url(fundId, $"/{student.MemberId}/remove"), null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("member.has_balance", await CodeOfAsync(response));
        var list = await ReadAsync(await owner.GetAsync(Url(fundId)));
        Assert.Equal(2, list.GetProperty("total").GetInt32()); // vẫn hoạt động
    }

    [Fact]
    public async Task Owner_cannot_be_removed()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var ownerRow = (await ReadAsync(await owner.GetAsync(Url(fundId) + "?role=Owner"))).GetProperty("items")[0].GetProperty("id").GetGuid();

        var response = await owner.PostAsync(Url(fundId, $"/{ownerRow}/remove"), null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("member.cannot_remove_owner", await CodeOfAsync(response));
    }

    [Fact]
    public async Task Update_with_blank_name_is_400()
    {
        var (owner, fundId, _, _) = await OwnerWithFundAsync();
        var student = await AddStudentAsync(owner, fundId);

        var response = await owner.PutAsJsonAsync(Url(fundId, $"/{student.MemberId}"), new { fullName = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
