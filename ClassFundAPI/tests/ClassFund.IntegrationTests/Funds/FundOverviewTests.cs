using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Application.Common.Security;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Funds;

/// <summary>
/// M7a – Tổng quan quỹ <c>GET /funds/{fundId}</c> qua HTTP thật (module-plan.md mục M7; FR-08, BR-02, BR-15, BR-23, NFR-08;
/// test case S-07, P-07). Số tổng giống nhau ở mọi vai trò; danh sách gần đây theo phạm vi của người xem.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FundOverviewTests(PostgresFixture fixture)
{
    private static readonly string[] OverviewProperties =
    [
        "role", "permissions", "currentBalance", "totalIncome", "totalExpense", "currentMonth",
        "treasurerStatus", "managers", "recentTransactions",
    ];

    private readonly TestData _data = fixture.Data;

    private static DateOnly VietnamToday => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    [Fact]
    public async Task Teacher_sees_totals_current_month_managers_and_recent_list_with_cancelled_excluded()
    {
        var teacher = await _data.CreateTeacherAsync("Cô Hạnh");
        var fund = await _data.CreateFundAsync(teacher.UserId);
        var categories = await _data.SeedDefaultCategoriesAsync(fund);
        var today = VietnamToday;
        await _data.CreateTransactionAsync(fund, categories["Đóng quỹ"], 1_000_000, occurredOn: today.AddMonths(-2), description: "Quý trước");
        await _data.CreateTransactionAsync(fund, categories["Đóng quỹ"], 500_000, occurredOn: today, description: "Đóng quỹ tháng này");
        await _data.CreateTransactionAsync(fund, categories["Văn phòng phẩm"], 200_000, isPublic: false, occurredOn: today, description: "Mua bút");
        await _data.CreateTransactionAsync(fund, categories["Chi khác"], 9_000_000, cancelled: true, occurredOn: today, description: "Đã hủy");
        await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        await _data.CreateStudentAccountAsync(fund, "Võ Gia Huy");
        using var client = await SignInAsync(teacher.UserId);

        var overview = await GetAsync(client, fund.Id);

        Assert.Equal(OverviewProperties.Order(), overview.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal("Teacher", overview.GetProperty("role").GetString());
        Assert.Equal(1_300_000, overview.GetProperty("currentBalance").GetInt64());
        Assert.Equal(1_500_000, overview.GetProperty("totalIncome").GetInt64());
        Assert.Equal(200_000, overview.GetProperty("totalExpense").GetInt64());
        var month = overview.GetProperty("currentMonth");
        Assert.Equal((500_000, 200_000, 300_000), (month.GetProperty("income").GetInt64(), month.GetProperty("expense").GetInt64(), month.GetProperty("net").GetInt64()));
        Assert.Equal("Assigned", overview.GetProperty("treasurerStatus").GetString());
        Assert.Equal(
            [("Cô Hạnh", "Teacher"), ("Phạm Thu Trang", "Treasurer")],
            overview.GetProperty("managers").EnumerateArray().Select(item => (item.GetProperty("fullName").GetString()!, item.GetProperty("role").GetString()!)));
        Assert.All(overview.GetProperty("managers").EnumerateArray(), item => Assert.Equal(["fullName", "role"], item.EnumerateObject().Select(property => property.Name)));
        var recent = overview.GetProperty("recentTransactions").EnumerateArray().ToArray();
        Assert.Equal(3, recent.Length);
        Assert.DoesNotContain(recent, item => item.GetProperty("description").GetString() == "Đã hủy");
        Assert.Contains(recent, item => item.TryGetProperty("isPublic", out var isPublic) && !isPublic.GetBoolean());
    }

    [Fact]
    public async Task Fund_without_transactions_or_treasurer_has_zeroes_and_unassigned_status()
    {
        var teacher = await _data.CreateTeacherAsync();
        var fund = await _data.CreateFundAsync(teacher.UserId);
        using var client = await SignInAsync(teacher.UserId);

        var overview = await GetAsync(client, fund.Id);

        Assert.Equal(0, overview.GetProperty("currentBalance").GetInt64());
        Assert.Equal(0, overview.GetProperty("currentMonth").GetProperty("net").GetInt64());
        Assert.Equal("Unassigned", overview.GetProperty("treasurerStatus").GetString());
        Assert.Single(overview.GetProperty("managers").EnumerateArray());
        Assert.Empty(overview.GetProperty("recentTransactions").EnumerateArray());
    }

    [Fact]
    public async Task Balance_can_be_negative_and_recent_list_is_limited_to_five_newest()
    {
        var teacher = await _data.CreateTeacherAsync();
        var fund = await _data.CreateFundAsync(teacher.UserId);
        var categories = await _data.SeedDefaultCategoriesAsync(fund);
        var today = VietnamToday;
        for (var day = 0; day < 7; day++)
        {
            await _data.CreateTransactionAsync(fund, categories["Chi khác"], 100_000, occurredOn: today.AddDays(-day), description: $"Chi {day}");
        }

        using var client = await SignInAsync(teacher.UserId);
        var overview = await GetAsync(client, fund.Id);

        Assert.Equal(-700_000, overview.GetProperty("currentBalance").GetInt64());
        Assert.Equal(
            ["Chi 0", "Chi 1", "Chi 2", "Chi 3", "Chi 4"],
            overview.GetProperty("recentTransactions").EnumerateArray().Select(item => item.GetProperty("description").GetString()!));
    }

    [Fact]
    public async Task Treasurer_student_and_parent_see_the_same_totals_but_public_viewers_only_public_recent_transactions()
    {
        var fund = await _data.CreateFundAsync();
        var categories = await _data.SeedDefaultCategoriesAsync(fund);
        await _data.CreateTransactionAsync(fund, categories["Đóng quỹ"], 800_000, description: "Công khai", occurredOn: VietnamToday);
        await _data.CreateTransactionAsync(fund, categories["Chi khác"], 300_000, isPublic: false, description: "Nội bộ", occurredOn: VietnamToday);
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var student = await _data.CreateStudentAccountAsync(fund, "Võ Gia Huy");
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);
        await ActivateAsync(treasurer.UserId);
        await ActivateAsync(student.UserId);
        await ActivateAsync(parent.UserId);

        var seen = new Dictionary<string, JsonElement>();
        foreach (var (role, userId) in new[] { ("Treasurer", treasurer.UserId), ("Student", student.UserId), ("Parent", parent.UserId) })
        {
            using var client = await SignInAsync(userId);
            seen[role] = await GetAsync(client, fund.Id);
            Assert.Equal(role, seen[role].GetProperty("role").GetString());
            Assert.Equal(500_000, seen[role].GetProperty("currentBalance").GetInt64());
            Assert.Equal(800_000, seen[role].GetProperty("totalIncome").GetInt64());
            Assert.Equal(300_000, seen[role].GetProperty("totalExpense").GetInt64());
            Assert.Equal(500_000, seen[role].GetProperty("currentMonth").GetProperty("net").GetInt64());
        }

        Assert.Equal(2, seen["Treasurer"].GetProperty("recentTransactions").GetArrayLength());
        foreach (var role in new[] { "Student", "Parent" })
        {
            var item = Assert.Single(seen[role].GetProperty("recentTransactions").EnumerateArray());
            Assert.Equal("Công khai", item.GetProperty("description").GetString());
            Assert.False(item.TryGetProperty("isPublic", out _));
            Assert.False(item.TryGetProperty("note", out _));
            Assert.False(item.TryGetProperty("createdByName", out _));
            Assert.DoesNotContain("Nội bộ", seen[role].GetRawText());
        }
    }

    [Fact]
    public async Task Requires_sign_in_hides_other_funds_and_blocks_temporary_password()
    {
        using var anonymous = fixture.Factory.CreateApiClient();
        var teacherA = await _data.CreateTeacherAsync("Cô Hạnh");
        var teacherB = await _data.CreateTeacherAsync("Thầy Nam");
        var fundA = await _data.CreateFundAsync(teacherA.UserId);
        await (await anonymous.GetAsync($"/funds/{fundA.Id}")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");

        using var other = await SignInAsync(teacherB.UserId);
        await (await other.GetAsync($"/funds/{fundA.Id}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await other.GetAsync($"/funds/{Guid.NewGuid()}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await other.GetAsync("/funds/khong-phai-guid")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        var student = await _data.CreateStudentAccountAsync(fundA);
        using var temporary = await SignInAsync(student.UserId);
        await (await temporary.GetAsync($"/funds/{fundA.Id}")).AssertProblemAsync(HttpStatusCode.Forbidden, "password.change_required");
    }

    private async Task<JsonElement> GetAsync(ApiClient client, Guid fundId)
    {
        var response = await client.GetAsync($"/funds/{fundId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<ApiClient> SignInAsync(Guid userId)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.SignInAsAsync(userId);
        return client;
    }

    /// <summary>Tài khoản đã đổi mật khẩu tạm.</summary>
    private async Task ActivateAsync(Guid userId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId);
        user.ChangePassword(hasher.Hash("Matkhau-456"), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }
}
