using System.Net.Http.Json;
using System.Text.Json;
using ClassFund.Api.Features.Auth;

namespace ClassFund.IntegrationTests.Infrastructure;

/// <summary>HttpClient giữ cookie và token antiforgery như SPA: lấy token ẩn danh, thay bằng token mới sau đăng nhập.</summary>
public sealed class ApiClient(HttpClient http) : IDisposable
{
    public const string Base = "/api/v1";
    private string? _antiforgeryToken;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<HttpResponseMessage> GetAsync(string path) => http.GetAsync(Base + path);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);

    /// <summary>Gửi request ghi không kèm header X-XSRF-TOKEN.</summary>
    public Task<HttpResponseMessage> PostWithoutAntiforgeryAsync(string path, object body) =>
        http.PostAsJsonAsync(Base + path, body, Json);

    public async Task<SessionResponse> LoginAsync(string login, string password)
    {
        var response = await PostAsync("/auth/login", new { login, password });
        return await response.ReadSessionAsync(System.Net.HttpStatusCode.OK);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, Base + path)
        {
            Content = body is null ? null : JsonContent.Create(body, options: Json),
        };
        request.Headers.Add("X-XSRF-TOKEN", await AntiforgeryTokenAsync());

        var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode && path is "/auth/login" or "/auth/register-teacher" or "/auth/change-password")
        {
            // Đọc body một lần rồi gắn lại để test vẫn đọc được response.
            var json = await response.Content.ReadAsStringAsync();
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, mediaType);
            _antiforgeryToken = JsonSerializer.Deserialize<SessionResponse>(json, Json)!.AntiforgeryToken;
        }

        return response;
    }

    private async Task<string> AntiforgeryTokenAsync()
    {
        if (_antiforgeryToken is null)
        {
            var response = await http.GetFromJsonAsync<JsonElement>(Base + "/auth/antiforgery", Json);
            _antiforgeryToken = response.GetProperty("antiforgeryToken").GetString();
        }

        return _antiforgeryToken!;
    }

    public void Dispose() => http.Dispose();
}

public static class HttpResponseExtensions
{
    public static async Task<SessionResponse> ReadSessionAsync(this HttpResponseMessage response, System.Net.HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(ApiClient.Json);
        Assert.NotNull(session);
        Assert.False(string.IsNullOrEmpty(session.AntiforgeryToken));
        return session;
    }

    public static async Task<MeResponse> ReadMeAsync(this HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeResponse>(ApiClient.Json))!;
    }

    /// <summary>Kiểm tra status và <c>code</c> của ProblemDetails; trả về toàn bộ body JSON.</summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, System.Net.HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Mong đợi {(int)status}, nhận {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }
}
