using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Members;

/// <summary>M5 – Thành viên quỹ, chỉ xem (docs/implementation/module-plan.md mục M5; FR-12, BR-23; test case M-01).</summary>
[Collection(ApiCollection.Name)]
public sealed class MembersTests(PostgresFixture fixture)
{
    private readonly TestData _data = fixture.Data;

    [Fact]
    public async Task Teacher_lists_teacher_treasurer_and_students_with_only_name_and_role()
    {
        var teacher = await _data.CreateTeacherAsync("Cô Hạnh");
        var fund = await _data.CreateFundAsync(teacher.UserId);
        await _data.CreateStudentAccountAsync(fund, "Võ Gia Huy", activated: true);
        await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        await _data.CreateStudentAsync(fund, "Đặng An"); // chưa có tài khoản
        var khang = await _data.CreateStudentAccountAsync(fund, "Nguyễn Minh Khang");
        await _data.CreateParentAccountAsync(fund, khang.StudentId!.Value);
        using var client = await SignInAsync(teacher.UserId);

        var members = await ListAsync(client, fund.Id);

        Assert.All(members.EnumerateArray(), item => Assert.Equal(["fullName", "role"], item.EnumerateObject().Select(property => property.Name)));
        Assert.Equal(
            [("Cô Hạnh", "Teacher"), ("Phạm Thu Trang", "Treasurer"), ("Đặng An", "Student"), ("Nguyễn Minh Khang", "Student"), ("Võ Gia Huy", "Student")],
            members.EnumerateArray().Select(item => (item.GetProperty("fullName").GetString()!, item.GetProperty("role").GetString()!)));
    }

    [Fact]
    public async Task Revoked_treasurer_is_listed_as_student_and_treasurer_appears_once()
    {
        var teacher = await _data.CreateTeacherAsync("Cô Hạnh");
        var fund = await _data.CreateFundAsync(teacher.UserId);
        var current = await _data.CreateStudentAccountAsync(fund, "Thủ quỹ hiện tại", treasurer: true);
        await _data.CreateStudentAccountAsync(fund, "Thủ quỹ cũ", treasurer: true);
        using var client = await SignInAsync(teacher.UserId);
        var former = (await (await client.GetAsync($"/funds/{fund.Id}/treasurers")).Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().Single(item => item.GetProperty("studentName").GetString() == "Thủ quỹ cũ").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/funds/{fund.Id}/treasurers/{former}/revoke")).StatusCode);

        var members = (await ListAsync(client, fund.Id)).EnumerateArray()
            .Select(item => (item.GetProperty("fullName").GetString()!, item.GetProperty("role").GetString()!)).ToArray();

        Assert.Equal([("Cô Hạnh", "Teacher"), ("Thủ quỹ hiện tại", "Treasurer"), ("Thủ quỹ cũ", "Student")], members);
        Assert.NotNull(current);
    }

    [Fact]
    public async Task Treasurer_can_view_the_list()
    {
        var fund = await _data.CreateFundAsync();
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        using var client = await SignInAsync(treasurer.UserId);

        Assert.Equal(2, (await ListAsync(client, fund.Id)).GetArrayLength());
    }

    [Fact]
    public async Task Student_and_parent_cannot_view_the_list()
    {
        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund, activated: true);
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);
        await ActivateAsync(parent.UserId);

        foreach (var userId in new[] { student.UserId, parent.UserId })
        {
            using var client = await SignInAsync(userId);
            await (await client.GetAsync($"/funds/{fund.Id}/members")).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }
    }

    [Fact]
    public async Task Outsider_gets_not_found_and_anonymous_gets_unauthorized()
    {
        var fund = await _data.CreateFundAsync();
        var outsider = await _data.CreateTeacherAsync("Cô khác");
        using var client = await SignInAsync(outsider.UserId);

        await (await client.GetAsync($"/funds/{fund.Id}/members")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        using var anonymous = fixture.Factory.CreateApiClient();
        await (await anonymous.GetAsync($"/funds/{fund.Id}/members")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    private async Task<ApiClient> SignInAsync(Guid userId)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.SignInAsAsync(userId);
        return client;
    }

    private static async Task<JsonElement> ListAsync(ApiClient client, Guid fundId)
    {
        var response = await client.GetAsync($"/funds/{fundId}/members");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task ActivateAsync(Guid userId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<ClassFund.Application.Common.Security.IPasswordHasher>();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId);
        user.ChangePassword(hasher.Hash("Matkhau-456"), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }
}
