using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClassFund.IntegrationTests.Infrastructure;

/// <summary>
/// HttpClient giữ cookie và token antiforgery như SPA: lấy token ẩn danh lần đầu; response nào có
/// <c>antiforgeryToken</c> (đăng nhập, đăng ký, đổi mật khẩu…) thì thay token mới.
/// </summary>
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

    /// <summary>
    /// Đăng nhập thẳng bằng cookie phiên cho <paramref name="userId"/> qua endpoint chỉ có trong test
    /// (<see cref="ProbeController"/>) – để test của một module không phụ thuộc màn hình / API đăng nhập.
    /// </summary>
    public async Task SignInAsAsync(Guid userId)
    {
        var response = await PostAsync($"/test-probe/sign-in/{userId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, Base + path)
        {
            Content = body is null ? null : JsonContent.Create(body, options: Json),
        };
        request.Headers.Add("X-XSRF-TOKEN", await AntiforgeryTokenAsync());

        var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            // Đọc body một lần rồi gắn lại để test vẫn đọc được response.
            var json = await response.Content.ReadAsStringAsync();
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            if (json.StartsWith('{')
                && JsonDocument.Parse(json).RootElement.TryGetProperty("antiforgeryToken", out var token)
                && token.ValueKind == JsonValueKind.String)
            {
                _antiforgeryToken = token.GetString();
            }
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
    /// <summary>Kiểm tra status và <c>code</c> của ProblemDetails; trả về toàn bộ body JSON.</summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Mong đợi {(int)status}, nhận {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }
}
