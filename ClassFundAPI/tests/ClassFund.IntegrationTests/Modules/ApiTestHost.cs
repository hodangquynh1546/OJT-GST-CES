using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Api.Authorization;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Foundation;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>
/// Api thật trên PostgreSQL Testcontainers + lưu file Local (thư mục tạm). M1 (đăng nhập) chưa có nên test đăng nhập bằng
/// endpoint chỉ có trong test <c>/__test/login/{userId}</c>: phát đúng cookie phiên như AuthController sẽ làm.
/// </summary>
public sealed class ApiTestHost : IAsyncLifetime
{
    public const string TestLoginPath = "/__test/login/";
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres = new();
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "classfund-it-" + Guid.NewGuid().ToString("N"));
    private Factory? _factory;

    public string StorageRoot => _storageRoot;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _factory = new Factory(_postgres.ConnectionString, _storageRoot);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    public AppDbContext CreateDbContext() => _postgres.CreateContext();

    public HttpClient Anonymous() => _factory!.CreateClient();

    /// <summary>Client đã đăng nhập bằng <paramref name="user"/>, có sẵn header antiforgery.</summary>
    public async Task<HttpClient> LoginAsync(User user)
    {
        var client = _factory!.CreateClient();
        (await client.PostAsync(TestLoginPath + user.Id, null)).EnsureSuccessStatusCode();
        var token = await client.GetFromJsonAsync<AntiforgeryBody>("/api/v1/auth/antiforgery");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token!.AntiforgeryToken);
        return client;
    }

    public async Task<User> CreateUserAsync(UserOrigin origin = UserOrigin.SelfRegistered, bool mustChangePassword = false)
    {
        var user = new User($"{Guid.NewGuid():N}@it.local", "Người " + Guid.NewGuid().ToString("N")[..6], "x", origin, mustChangePassword, Now);
        await using var db = CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Quỹ có Owner + một học sinh mỗi lần gọi <see cref="AddMemberAsync"/>.</summary>
    public async Task<Fund> CreateFundAsync(User owner)
    {
        var fund = new Fund("Quỹ IT " + Guid.NewGuid().ToString("N")[..6], owner.Id, "2026-2027", Now);
        await using var db = CreateDbContext();
        db.Funds.Add(fund);
        db.FundMembers.Add(new FundMember(fund.Id, owner.Id, MemberRole.Owner, Now));
        await db.SaveChangesAsync();
        return fund;
    }

    public async Task AddMemberAsync(Fund fund, User user, MemberRole role = MemberRole.Student)
    {
        await using var db = CreateDbContext();
        db.FundMembers.Add(new FundMember(fund.Id, user.Id, role, Now));
        await db.SaveChangesAsync();
    }

    public static MultipartFormDataContent Multipart(byte[] bytes, string fileName, string contentType, string? kind = null)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        if (kind is not null)
        {
            content.Add(new StringContent(kind), "kind");
        }

        return content;
    }

    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[256]];

    public static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8, .. new byte[256]];

    private sealed record AntiforgeryBody(string AntiforgeryToken);

    private sealed class Factory(string connectionString, string storageRoot) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:App", connectionString);
            builder.UseSetting("SECRET_KEY", new string('k', 48));
            builder.UseSetting("Storage:Provider", "Local");
            builder.UseSetting("Storage:LocalRoot", storageRoot);
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, TestLoginStartupFilter>());
        }
    }

    /// <summary>Chỉ có trong test: đăng nhập không mật khẩu bằng mã người dùng.</summary>
    private sealed class TestLoginStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path.StartsWithSegments(TestLoginPath.TrimEnd('/'), out var rest)
                    && Guid.TryParse(rest.Value?.Trim('/'), out var userId))
                {
                    var db = context.RequestServices.GetRequiredService<AppDbContext>();
                    var stamp = await db.Users.Where(user => user.Id == userId).Select(user => user.SecurityStamp).SingleAsync();
                    await SessionCookie.SignInAsync(context, new SessionTicket(userId, stamp));
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
