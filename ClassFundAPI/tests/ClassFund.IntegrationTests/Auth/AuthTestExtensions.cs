using System.Net;
using System.Text.Json;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Auth;

/// <summary>Response đổi phiên của M1 đọc trong test (enum dạng chuỗi như JSON thật).</summary>
public sealed record TestSession(string AntiforgeryToken, TestMe User);

public sealed record TestMe(
    Guid Id,
    string Kind,
    string FullName,
    string? Email,
    string? Username,
    bool MustChangePassword,
    string[] AccountPermissions,
    TestMembership[] Memberships);

public sealed record TestMembership(Guid FundId, string FundName, string Role, string[] Permissions, string[] LinkedStudents);

/// <summary>Helper đăng nhập thật qua <c>POST /auth/login</c> – thuộc M1, module khác dùng <c>SignInAsAsync</c>.</summary>
public static class AuthTestExtensions
{
    public static async Task<TestSession> LoginAsync(this ApiClient client, string login, string password) =>
        await (await client.PostAsync("/auth/login", new { login, password })).ReadSessionAsync(HttpStatusCode.OK);

    public static async Task<TestSession> ReadSessionAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Mong đợi {(int)expected}, nhận {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<TestSession>(body, ApiClient.Json)!;
    }

    public static async Task<TestMe> ReadMeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Mong đợi 200, nhận {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<TestMe>(body, ApiClient.Json)!;
    }
}
