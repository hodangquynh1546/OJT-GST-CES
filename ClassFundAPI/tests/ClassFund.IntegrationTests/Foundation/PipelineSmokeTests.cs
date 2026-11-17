using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClassFund.IntegrationTests.Foundation;

/// <summary>Kiểm tra pipeline nền (không cần CSDL): Antiforgery, 404 ProblemDetails, correlation id.</summary>
public class PipelineSmokeTests : IClassFixture<PipelineSmokeTests.Factory>
{
    private readonly HttpClient _client;

    public PipelineSmokeTests(Factory factory) => _client = factory.CreateClient();

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:App", "Host=localhost;Database=unused;Username=unused;Password=unused");
            builder.UseSetting("SECRET_KEY", new string('k', 48));
        }
    }

    [Fact]
    public async Task Antiforgery_endpoint_is_public_and_returns_token()
    {
        var response = await _client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("antiforgeryToken", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_problem_json()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
