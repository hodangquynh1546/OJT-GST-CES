using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Security;
using ClassFund.IntegrationTests.Foundation;

namespace ClassFund.IntegrationTests.Auth;

/// <summary>
/// M1-T3: API xác thực chạy thật (cookie, antiforgery, rate limit) trên PostgreSQL tạm trong Docker, không dùng Neon.
/// Mỗi test dùng email riêng nên chia sẻ một database.
/// </summary>
public class AuthApiTests(PostgresFixture database) : IClassFixture<PostgresFixture>, IDisposable
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

    private WebApplicationFactory<Program> CreateFactory(int permitLimit = 1000)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:App", database.ConnectionString);
            builder.UseSetting("SECRET_KEY", Pepper);
            builder.UseSetting("RateLimiting:AuthPermitLimit", permitLimit.ToString());
            builder.UseEnvironment("Development");
        });
        _factories.Add(factory);
        return factory;
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>Client có cookie riêng (một "trình duyệt") và đã lấy token antiforgery ẩn danh.</summary>
    private static async Task<HttpClient> BrowserAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        await RefreshAntiforgeryAsync(client);
        return client;
    }

    private static async Task RefreshAntiforgeryAsync(HttpClient client, string? token = null)
    {
        token ??= (await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/antiforgery")).GetProperty("antiforgeryToken").GetString()!;
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    /// <summary>Đăng ký và nhận token antiforgery mới của phiên (như frontend làm).</summary>
    private static async Task<(HttpResponseMessage Response, JsonElement Body)> RegisterAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "Nguyễn An", email, password });
        var body = await ReadAsync(response);
        if (response.IsSuccessStatusCode)
        {
            await RefreshAntiforgeryAsync(client, body.GetProperty("antiforgeryToken").GetString());
        }

        return (response, body);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (response.IsSuccessStatusCode)
        {
            await RefreshAntiforgeryAsync(client, (await ReadAsync(response)).GetProperty("antiforgeryToken").GetString());
        }

        return response;
    }

    private async Task<string> SeedTemporaryAccountAsync()
    {
        var email = NewEmail();
        await using var context = database.CreateContext();
        var hash = new AspNetPasswordHasher(Pepper).Hash("Temp-pass-1234");
        context.Add(new User(email, "Học Sinh", hash, UserOrigin.Provisioned, mustChangePassword: true, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        return email;
    }

    // ---------- Đăng ký → me ----------

    [Fact]
    public async Task Register_signs_in_and_me_returns_the_new_user()
    {
        var client = await BrowserAsync(CreateFactory());
        var email = NewEmail();

        var (response, body) = await RegisterAsync(client, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("antiforgeryToken").GetString()));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("cf_session=") && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var me = await ReadAsync(await client.GetAsync("/api/v1/auth/me"));
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());
        Assert.Contains("Fund.Create", me.GetProperty("accountPermissions").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, me.GetProperty("memberships").GetArrayLength());
        Assert.False(me.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Register_writes_audit_without_any_password_material()
    {
        var client = await BrowserAsync(CreateFactory());
        var email = NewEmail();
        await RegisterAsync(client, email);

        await using var context = database.CreateContext();
        var user = await context.Set<User>().SingleAsync(item => item.Email == email);
        var log = await context.Set<AuditLog>().SingleAsync(item => item.EntityId == user.Id.ToString() && item.Action == "auth.register");
        Assert.Equal(FinancialScope.System, log.Scope);
        Assert.Null(log.FundId);
        Assert.Equal(user.Id, log.ActorUserId);
        Assert.DoesNotContain(Password, log.AfterJson ?? "");
        Assert.DoesNotContain(user.PasswordHash, log.AfterJson ?? "");
        Assert.DoesNotContain(user.SecurityStamp, log.AfterJson ?? "");
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409()
    {
        var client = await BrowserAsync(CreateFactory());
        var email = NewEmail();
        await RegisterAsync(client, email);

        var other = await BrowserAsync(CreateFactory());
        var (response, body) = await RegisterAsync(other, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.email_exists", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_with_invalid_data_returns_400_with_field_errors()
    {
        var client = await BrowserAsync(CreateFactory());

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "", email = "khong-phai-email", password = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("fullName", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    // ---------- Đăng nhập / đăng xuất ----------

    [Fact]
    public async Task Login_with_wrong_password_returns_401_invalid_credentials()
    {
        var email = NewEmail();
        await RegisterAsync(await BrowserAsync(CreateFactory()), email);

        var client = await BrowserAsync(CreateFactory());
        var response = await LoginAsync(client, email, "Wrong1234");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", await CodeOfAsync(response));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_same_error_as_wrong_password()
    {
        var client = await BrowserAsync(CreateFactory());
        var response = await LoginAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", await CodeOfAsync(response));
    }

    [Fact]
    public async Task Login_then_logout_ends_the_session()
    {
        var email = NewEmail();
        await RegisterAsync(await BrowserAsync(CreateFactory()), email);

        var client = await BrowserAsync(CreateFactory());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Disabled_account_cannot_login_and_existing_session_is_rejected()
    {
        var email = NewEmail();
        var client = await BrowserAsync(CreateFactory());
        await RegisterAsync(client, email);

        await using (var context = database.CreateContext())
        {
            var user = await context.Set<User>().SingleAsync(item => item.Email == email);
            user.IsActive = false;
            await context.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        var other = await BrowserAsync(CreateFactory());
        var response = await LoginAsync(other, email);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.account_disabled", await CodeOfAsync(response));
    }

    [Fact]
    public async Task Write_request_without_antiforgery_header_is_rejected()
    {
        var client = CreateFactory().CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery_failed", await CodeOfAsync(response));
    }

    // ---------- Mật khẩu tạm (FR-03, BR-17) ----------

    [Fact]
    public async Task Temporary_password_blocks_other_endpoints_until_changed()
    {
        var email = await SeedTemporaryAccountAsync();
        var client = await BrowserAsync(CreateFactory());

        var login = await LoginAsync(client, email, "Temp-pass-1234");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await ReadAsync(login)).GetProperty("user").GetProperty("mustChangePassword").GetBoolean());

        // Được phép: xem phiên.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        // Bị chặn: endpoint khác.
        var blocked = await client.PutAsJsonAsync("/api/v1/auth/me", new { fullName = "Tên Mới" });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password.change_required", await CodeOfAsync(blocked));

        // Đổi mật khẩu xong thì dùng được.
        var change = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Temp-pass-1234", newPassword = "NewPassw0rd" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var body = await ReadAsync(change);
        Assert.False(body.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        await RefreshAntiforgeryAsync(client, body.GetProperty("antiforgeryToken").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/auth/me", new { fullName = "Tên Mới" })).StatusCode);
    }

    // ---------- Đổi mật khẩu (BR-29) ----------

    [Fact]
    public async Task Changing_password_invalidates_other_sessions_but_keeps_the_current_one()
    {
        var email = NewEmail();
        await RegisterAsync(await BrowserAsync(CreateFactory()), email);

        var current = await BrowserAsync(CreateFactory());
        var elsewhere = await BrowserAsync(CreateFactory());
        await LoginAsync(current, email);
        await LoginAsync(elsewhere, email);
        Assert.Equal(HttpStatusCode.OK, (await elsewhere.GetAsync("/api/v1/auth/me")).StatusCode);

        var change = await current.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "NewPassw0rd" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        await RefreshAntiforgeryAsync(current, (await ReadAsync(change)).GetProperty("antiforgeryToken").GetString());

        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/v1/auth/me")).StatusCode);

        var oldPassword = await LoginAsync(await BrowserAsync(CreateFactory()), email, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var newPassword = await LoginAsync(await BrowserAsync(CreateFactory()), email, "NewPassw0rd");
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_is_422_and_keeps_session()
    {
        var client = await BrowserAsync(CreateFactory());
        await RegisterAsync(client, NewEmail());

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Wrong1234", newPassword = "NewPassw0rd" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("auth.current_password_incorrect", await CodeOfAsync(response));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Change_password_validates_the_new_password()
    {
        var client = await BrowserAsync(CreateFactory());
        await RegisterAsync(client, NewEmail());

        var weak = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True((await ReadAsync(weak)).GetProperty("errors").TryGetProperty("newPassword", out _));
    }

    [Fact]
    public async Task Change_password_audit_has_no_password_material()
    {
        var email = NewEmail();
        var client = await BrowserAsync(CreateFactory());
        await RegisterAsync(client, email);
        await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "NewPassw0rd" });

        await using var context = database.CreateContext();
        var user = await context.Set<User>().SingleAsync(item => item.Email == email);
        var log = await context.Set<AuditLog>().SingleAsync(item => item.EntityId == user.Id.ToString() && item.Action == "auth.change_password");
        var json = (log.BeforeJson ?? "") + (log.AfterJson ?? "");
        Assert.DoesNotContain("NewPassw0rd", json);
        Assert.DoesNotContain(Password, json);
        Assert.DoesNotContain(user.PasswordHash, json);
        Assert.True(log.IsSelfAction);
    }

    // ---------- Hồ sơ ----------

    [Fact]
    public async Task Update_profile_changes_the_name_in_me()
    {
        var client = await BrowserAsync(CreateFactory());
        await RegisterAsync(client, NewEmail());

        var update = await client.PutAsJsonAsync("/api/v1/auth/me", new { fullName = "Trần Bình" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Trần Bình", (await ReadAsync(await client.GetAsync("/api/v1/auth/me"))).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Anonymous_user_gets_401_on_protected_auth_endpoints()
    {
        var client = await BrowserAsync(CreateFactory());

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/auth/me", new { fullName = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "NewPassw0rd" })).StatusCode);
    }

    // ---------- Rate limit ----------

    [Fact]
    public async Task Login_is_rate_limited_per_ip_with_429()
    {
        var client = await BrowserAsync(CreateFactory(permitLimit: 3));
        var email = NewEmail();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email, "Wrong1234")).StatusCode);
        }

        var limited = await LoginAsync(client, email, "Wrong1234");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("auth.rate_limited", await CodeOfAsync(limited));
    }
}
