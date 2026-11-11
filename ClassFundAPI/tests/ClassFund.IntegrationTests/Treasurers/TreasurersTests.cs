using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ClassFund.Domain.Entities;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Treasurers;

/// <summary>
/// M4 – Phân công / thu hồi thủ quỹ qua HTTP thật (docs/implementation/module-plan.md mục M4; FR-27, BR-31, BR-32,
/// BR-33, BR-37, NFR-09; test case TR-01, TR-03 → TR-07, T-05, M-02).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TreasurersTests(PostgresFixture fixture)
{
    private readonly TestData _data = fixture.Data;

    // ---------- GET / POST /funds/{id}/treasurers ----------

    [Fact]
    public async Task Teacher_assigns_activated_student_and_sees_it_in_list_with_audit()
    {
        var (teacher, fund, client) = await TeacherFundAsync("Cô Hạnh");
        var khang = await _data.CreateStudentAccountAsync(fund, "Nguyễn Minh Khang", activated: true);

        Assert.Empty((await ListAsync(client, fund.Id)).EnumerateArray());

        var assignmentId = await AssignAsync(client, fund.Id, khang.StudentId!.Value);

        var item = Assert.Single((await ListAsync(client, fund.Id)).EnumerateArray());
        Assert.Equal(
            ["id", "studentId", "studentName", "isActive", "assignedAt", "assignedByName", "revokedAt", "revokedByName"],
            item.EnumerateObject().Select(property => property.Name));
        Assert.Equal(assignmentId, item.GetProperty("id").GetGuid());
        Assert.Equal(khang.StudentId, item.GetProperty("studentId").GetGuid());
        Assert.Equal("Nguyễn Minh Khang", item.GetProperty("studentName").GetString());
        Assert.True(item.GetProperty("isActive").GetBoolean());
        Assert.True(item.GetProperty("assignedAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Equal("Cô Hạnh", item.GetProperty("assignedByName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("revokedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("revokedByName").ValueKind);

        var audit = Assert.Single(await AuditAsync(assignmentId));
        Assert.Equal("Treasurer.Assign", audit.Action);
        Assert.Equal(teacher.UserId, audit.ActorUserId);
        Assert.Contains("Nguyễn Minh Khang", audit.NewValues);

        // Quỹ chuyển sang "Đã có thủ quỹ" (BR-36) và học sinh có quyền thủ quỹ ngay (đọc quyền từ DB mỗi request).
        using var khangClient = await SignInAsync(khang.UserId);
        var membership = Assert.Single((await GetJsonAsync(khangClient, "/funds")).EnumerateArray());
        Assert.Equal("Treasurer", membership.GetProperty("role").GetString());
        Assert.Contains("Transaction.Manage", Strings(membership.GetProperty("permissions")));
    }

    [Fact]
    public async Task Assign_rejects_students_without_activated_account_already_assigned_or_outside_fund()
    {
        var (_, fund, client) = await TeacherFundAsync();
        var pending = await _data.CreateStudentAccountAsync(fund, "Phạm Gia Huy");
        var none = await _data.CreateStudentAsync(fund, "Lê Gia Bảo");
        var disabled = await _data.CreateStudentAccountAsync(fund, "Trần Quốc Việt", activated: true);
        await _data.DisableUserAsync(disabled.UserId);
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var otherFund = await _data.CreateFundAsync();
        var outsider = await _data.CreateStudentAccountAsync(otherFund, "Học sinh quỹ khác", activated: true);

        await (await PostAssignAsync(client, fund.Id, pending.StudentId)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.account_not_activated");
        await (await PostAssignAsync(client, fund.Id, none.StudentId)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.no_account");
        await (await PostAssignAsync(client, fund.Id, disabled.StudentId)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.account_disabled");
        await (await PostAssignAsync(client, fund.Id, treasurer.StudentId)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.already_assigned");
        await (await PostAssignAsync(client, fund.Id, outsider.StudentId)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await PostAssignAsync(client, fund.Id, Guid.NewGuid())).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        Assert.Single((await ListAsync(client, fund.Id)).EnumerateArray());
        Assert.Equal(0, await ActiveCountAsync(otherFund.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Assign_without_student_is_validation_error(string? studentId)
    {
        var (_, fund, client) = await TeacherFundAsync();

        var response = await client.PostAsync($"/funds/{fund.Id}/treasurers", new { studentId = studentId is null ? (Guid?)null : Guid.Parse(studentId) });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(["Chọn học sinh."], Strings(problem.GetProperty("errors").GetProperty("studentId")));
    }

    // ---------- POST /funds/{id}/treasurers/{aid}/revoke ----------

    [Fact]
    public async Task Revoking_the_only_treasurer_is_rejected()
    {
        var (_, fund, client) = await TeacherFundAsync();
        await _data.CreateStudentAccountAsync(fund, treasurer: true);
        var assignmentId = (await ListAsync(client, fund.Id))[0].GetProperty("id").GetGuid();

        var problem = await (await RevokeAsync(client, fund.Id, assignmentId)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.last");

        Assert.Contains("phân công người mới trước", problem.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True((await ListAsync(client, fund.Id))[0].GetProperty("isActive").GetBoolean());
        Assert.Empty(await AuditAsync(assignmentId));
    }

    [Fact]
    public async Task Changing_treasurer_assign_new_then_revoke_old_moves_permissions_and_keeps_history()
    {
        var (teacher, fund, client) = await TeacherFundAsync("Thầy Hùng");
        var oldTreasurer = await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo", treasurer: true);
        var newTreasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", activated: true);
        using var oldClient = await SignInAsync(oldTreasurer.UserId);
        Assert.Equal("Treasurer", (await MembershipAsync(oldClient)).GetProperty("role").GetString());
        var oldAssignmentId = (await ListAsync(client, fund.Id))[0].GetProperty("id").GetGuid();

        var newAssignmentId = await AssignAsync(client, fund.Id, newTreasurer.StudentId!.Value);
        var revoke = await RevokeAsync(client, fund.Id, oldAssignmentId);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // Người cũ mất quyền thủ quỹ ngay ở request kế tiếp (cùng phiên); người mới có quyền ghi.
        var oldMembership = await MembershipAsync(oldClient);
        Assert.Equal("Student", oldMembership.GetProperty("role").GetString());
        Assert.DoesNotContain("Transaction.Manage", Strings(oldMembership.GetProperty("permissions")));
        using var newClient = await SignInAsync(newTreasurer.UserId);
        Assert.Contains("Transaction.Manage", Strings((await MembershipAsync(newClient)).GetProperty("permissions")));

        // Đang hiệu lực trước, rồi lịch sử; thu hồi chỉ đánh dấu (BR-33).
        var list = (await ListAsync(client, fund.Id)).EnumerateArray().ToArray();
        Assert.Equal([newAssignmentId, oldAssignmentId], list.Select(item => item.GetProperty("id").GetGuid()));
        var revoked = list[1];
        Assert.False(revoked.GetProperty("isActive").GetBoolean());
        Assert.Equal("Lê Gia Bảo", revoked.GetProperty("studentName").GetString());
        Assert.Equal("Thầy Hùng", revoked.GetProperty("revokedByName").GetString());
        Assert.True(revoked.GetProperty("revokedAt").GetDateTimeOffset() >= revoked.GetProperty("assignedAt").GetDateTimeOffset());

        var audit = Assert.Single(await AuditAsync(oldAssignmentId));
        Assert.Equal("Treasurer.Revoke", audit.Action);
        Assert.Equal(teacher.UserId, audit.ActorUserId);

        // Quỹ vẫn "Đã có thủ quỹ" với tên người mới.
        var summary = Assert.Single((await GetJsonAsync(client, "/funds")).EnumerateArray());
        Assert.Equal(["Phạm Thu Trang"], Strings(summary.GetProperty("treasurerNames")));
    }

    [Fact]
    public async Task Revoking_an_already_revoked_assignment_is_conflict()
    {
        var (_, fund, client) = await TeacherFundAsync();
        await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 1", treasurer: true);
        await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 2", treasurer: true);
        var assignmentId = (await ListAsync(client, fund.Id))[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await RevokeAsync(client, fund.Id, assignmentId)).StatusCode);

        await (await RevokeAsync(client, fund.Id, assignmentId)).AssertProblemAsync(HttpStatusCode.Conflict, "treasurer.already_revoked");

        Assert.Single(await AuditAsync(assignmentId));
    }

    [Fact]
    public async Task Revoked_student_can_be_assigned_again_as_a_new_assignment()
    {
        var (_, fund, client) = await TeacherFundAsync();
        var bao = await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo", treasurer: true);
        await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var baoAssignment = (await ListAsync(client, fund.Id)).EnumerateArray()
            .Single(item => item.GetProperty("studentId").GetGuid() == bao.StudentId).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await RevokeAsync(client, fund.Id, baoAssignment)).StatusCode);

        var again = await AssignAsync(client, fund.Id, bao.StudentId!.Value);

        Assert.NotEqual(baoAssignment, again);
        Assert.Equal(3, (await ListAsync(client, fund.Id)).GetArrayLength());
    }

    [Fact]
    public async Task Concurrent_revokes_of_two_treasurers_leave_exactly_one()
    {
        // NFR-09 / BR-32: khóa dòng Fund (SELECT … FOR UPDATE) → lệnh sau đếm lại sau khi lệnh trước commit.
        for (var round = 0; round < 5; round++)
        {
            var (teacher, fund, first) = await TeacherFundAsync();
            using var second = await SignInAsync(teacher.UserId);
            await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 1", treasurer: true);
            await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 2", treasurer: true);
            var ids = (await ListAsync(first, fund.Id)).EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();

            var responses = await Task.WhenAll(RevokeAsync(first, fund.Id, ids[0]), RevokeAsync(second, fund.Id, ids[1]));

            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            await Assert.Single(responses, response => response.StatusCode != HttpStatusCode.NoContent)
                .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.last");
            Assert.Single((await ListAsync(first, fund.Id)).EnumerateArray(), item => item.GetProperty("isActive").GetBoolean());
            first.Dispose();
        }
    }

    [Fact]
    public async Task Assignment_of_another_fund_is_not_found()
    {
        var (teacher, fund, client) = await TeacherFundAsync();
        var otherFund = await _data.CreateFundAsync(teacher.UserId);
        await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 1", treasurer: true);
        await _data.CreateStudentAccountAsync(otherFund, "Thủ quỹ A", treasurer: true);
        await _data.CreateStudentAccountAsync(otherFund, "Thủ quỹ B", treasurer: true);
        var otherAssignment = (await ListAsync(client, otherFund.Id))[0].GetProperty("id").GetGuid();

        await (await RevokeAsync(client, fund.Id, otherAssignment)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await RevokeAsync(client, fund.Id, Guid.NewGuid())).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        Assert.All((await ListAsync(client, otherFund.Id)).EnumerateArray(), item => Assert.True(item.GetProperty("isActive").GetBoolean()));
    }

    // ---------- Phạm vi & quyền ----------

    [Fact]
    public async Task Teacher_cannot_see_assign_or_revoke_in_another_teachers_fund()
    {
        var (_, fund, _) = await TeacherFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund, activated: true);
        await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 1", treasurer: true);
        await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 2", treasurer: true);
        var assignmentId = await FirstAssignmentIdAsync(fund.Id);
        var teacherB = await _data.CreateTeacherAsync("Thầy B");
        using var client = await SignInAsync(teacherB.UserId);

        await (await client.GetAsync($"/funds/{fund.Id}/treasurers")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await PostAssignAsync(client, fund.Id, student.StudentId)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await RevokeAsync(client, fund.Id, assignmentId)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/funds/khong-phai-guid/treasurers")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/funds/{fund.Id}/treasurers/khong-phai-guid/revoke")).StatusCode);
        Assert.Empty(await AuditAsync(assignmentId));
    }

    [Fact]
    public async Task Treasurer_student_and_parent_cannot_view_assign_or_revoke()
    {
        var fund = await _data.CreateFundAsync();
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 1", treasurer: true);
        await _data.CreateStudentAccountAsync(fund, "Thủ quỹ 2", treasurer: true);
        var student = await _data.CreateStudentAccountAsync(fund, "Học sinh", activated: true);
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);
        await ActivateAsync(parent.UserId);
        var assignmentId = await FirstAssignmentIdAsync(fund.Id);

        foreach (var userId in new[] { treasurer.UserId, student.UserId, parent.UserId })
        {
            using var client = await SignInAsync(userId);
            await (await client.GetAsync($"/funds/{fund.Id}/treasurers")).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await PostAssignAsync(client, fund.Id, student.StudentId)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await RevokeAsync(client, fund.Id, assignmentId)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        Assert.Equal(2, await ActiveCountAsync(fund.Id));
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        var fund = await _data.CreateFundAsync();
        using var client = fixture.Factory.CreateApiClient();

        await (await client.GetAsync($"/funds/{fund.Id}/treasurers")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    // ---------- Bảo vệ ở database (M-02, KI-23) ----------

    [Fact]
    public async Task Treasurer_assignment_cannot_be_hard_deleted()
    {
        var fund = await _data.CreateFundAsync();
        await _data.CreateStudentAccountAsync(fund, treasurer: true);
        var assignmentId = await FirstAssignmentIdAsync(fund.Id);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""DELETE FROM "TreasurerAssignment" WHERE "Id" = @id""", connection);
        command.Parameters.AddWithValue("id", assignmentId);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("P0001", error.SqlState);
        Assert.Equal(1, await ActiveCountAsync(fund.Id));
    }

    // ---------- Helper ----------

    private async Task<(SeededAccount Teacher, SeededFund Fund, ApiClient Client)> TeacherFundAsync(string fullName = "Cô Hạnh")
    {
        var teacher = await _data.CreateTeacherAsync(fullName);
        var fund = await _data.CreateFundAsync(teacher.UserId);
        return (teacher, fund, await SignInAsync(teacher.UserId));
    }

    private async Task<ApiClient> SignInAsync(Guid userId)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.SignInAsAsync(userId);
        return client;
    }

    private static Task<HttpResponseMessage> PostAssignAsync(ApiClient client, Guid fundId, Guid? studentId) =>
        client.PostAsync($"/funds/{fundId}/treasurers", new { studentId });

    private static async Task<Guid> AssignAsync(ApiClient client, Guid fundId, Guid studentId)
    {
        var response = await PostAssignAsync(client, fundId, studentId);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Mong đợi 201, nhận {(int)response.StatusCode}: {body}");
        var created = JsonDocument.Parse(body).RootElement;
        Assert.Single(created.EnumerateObject());
        return created.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> RevokeAsync(ApiClient client, Guid fundId, Guid assignmentId) =>
        client.PostAsync($"/funds/{fundId}/treasurers/{assignmentId}/revoke");

    private static async Task<JsonElement> ListAsync(ApiClient client, Guid fundId, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await client.GetAsync($"/funds/{fundId}/treasurers");
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> GetJsonAsync(ApiClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> MembershipAsync(ApiClient client) =>
        Assert.Single((await GetJsonAsync(client, "/funds")).EnumerateArray());

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private async Task<Guid> FirstAssignmentIdAsync(Guid fundId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<TreasurerAssignment>().Where(item => item.FundId == fundId).OrderBy(item => item.Id).Select(item => item.Id).FirstAsync();
    }

    private async Task<int> ActiveCountAsync(Guid fundId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<TreasurerAssignment>().CountAsync(item => item.FundId == fundId && item.RevokedAt == null);
    }

    private async Task<List<AuditLog>> AuditAsync(Guid assignmentId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = assignmentId.ToString();
        return await db.AuditLogs.AsNoTracking().Where(log => log.EntityId == id).OrderBy(log => log.Id).ToListAsync();
    }

    /// <summary>Phụ huynh đã đổi mật khẩu tạm (seed của <see cref="TestData"/> tạo phụ huynh còn mật khẩu tạm).</summary>
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
