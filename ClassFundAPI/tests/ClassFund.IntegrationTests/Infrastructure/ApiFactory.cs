using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using ClassFund.Api.Hosting;

namespace ClassFund.IntegrationTests.Infrastructure;

/// <summary>API thật (pipeline, cookie, antiforgery, EF Core) chạy trên PostgreSQL của <see cref="PostgresFixture"/>.</summary>
public sealed class ApiFactory(string connectionString, int authPermitLimit = 10_000, string passwordPepper = ApiFactory.TestPepper)
    : WebApplicationFactory<Program>
{
    /// <summary>Pepper chỉ dùng trong test.</summary>
    public const string TestPepper = "integration-test-pepper-0123456789abcdef";

    public CapturedLogSink Logs { get; } = new();

    public ApiClient CreateApiClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            // Cookie phiên có cờ Secure ngoài môi trường Development.
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        }));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:App", connectionString);
        builder.UseSetting(ServiceRegistration.PasswordPepperSetting, passwordPepper);
        builder.UseSetting(RateLimitPolicies.AuthPermitLimitSetting, authPermitLimit.ToString());
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogEventSink>(Logs);
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        });
    }
}
