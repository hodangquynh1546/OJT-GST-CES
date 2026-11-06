using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolFund.Infrastructure.Persistence;
using SchoolFund.IntegrationTests.Infrastructure;

namespace SchoolFund.IntegrationTests.Treasurers;

/// <summary>Phân công / thu hồi thủ quỹ qua HTTP thật – FR-27, BR-31, BR-32, BR-33, BR-36, P-15, NFR-09.</summary>
[Collection(ApiCollection.Name)]
public sealed class TreasurerTests(PostgresFixture fixture)
{
    private const string TeacherPassword = "Matkhau123";
    private const string NewPassword = "Matkhau-456";

    private readonly TestData _data = fixture.Data;

    private sealed record Teacher(ApiClient Client, Guid UserId, Guid FundId);

    /// <summary>Học sinh đã được GVCN thêm và cấp tài khoản; <see cref="Client"/> chỉ có khi đã đổi mật khẩu tạm (kích hoạt).</summary>
    private sealed record Pupil(Guid StudentId, string Login, string TemporaryPassword, ApiClient? Client);

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd");

    private async Task<Teacher> TeacherWithFundAsync()
    {
        var client = fixture.Factory.CreateApiClient();
        var session = await (await client.PostAsync("/auth/register-teacher", new
        {
            fullName = "Cô Hạnh",
            email = TestData.NewEmail(),
            password = TeacherPassword,
            confirmPassword = TeacherPassword,
        })).ReadSessionAsync(HttpStatusCode.Created);
        return new Teacher(client, session.User.Id, await CreateFundAsync(client));
    }

    private static async Task<Guid> CreateFundAsync(ApiClient client)
    {
        var created = await JsonAsync(await client.PostAsync("/funds", new { name = TestData.Unique("Quỹ lớp 5A ") }), HttpStatusCode.Created);
        return created.GetProperty("id").GetGuid();
    }

    private static string Url(Teacher teacher, string rest = "") => $"/funds/{teacher.FundId}/treasurers{rest}";

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Mong đợi {(int)expected}, nhận {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private async Task<Pupil> AddPupilAsync(Teacher teacher, string fullName, bool account = true, bool activate = true)
    {
        var added = await JsonAsync(
            await teacher.Client.PostAsync($"/funds/{teacher.FundId}/students", new { fullName }),
            HttpStatusCode.Created);
        var studentId = added.GetProperty("id").GetGuid();
        if (!account)
        {
            return new Pupil(studentId, string.Empty, string.Empty, null);
        }

        var credential = await JsonAsync(
            await teacher.Client.PostAsync($"/funds/{teacher.FundId}/students/{studentId}/account", new { }),
            HttpStatusCode.OK);
        var login = credential.GetProperty("login").GetString()!;
        var temporary = credential.GetProperty("temporaryPassword").GetString()!;
        if (!activate)
        {
            return new Pupil(studentId, login, temporary, null);
        }

        var client = fixture.Factory.CreateApiClient();
        await client.LoginAsync(login, temporary);
        await (await client.PostAsync("/auth/change-password", new { currentPassword = temporary, newPassword = NewPassword }))
            .ReadSessionAsync(HttpStatusCode.OK);
        return new Pupil(studentId, login, temporary, client);
    }

    private static async Task<Guid> AssignAsync(Teacher teacher, Pupil pupil)
    {
        var created = await JsonAsync(await teacher.Client.PostAsync(Url(teacher), new { studentId = pupil.StudentId }), HttpStatusCode.Created);
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> ListAsync(Teacher teacher) =>
        (await JsonAsync(await teacher.Client.GetAsync(Url(teacher)), HttpStatusCode.OK)).EnumerateArray().ToArray();

    private static Task<HttpResponseMessage> RevokeAsync(Teacher teacher, Guid assignmentId) =>
        teacher.Client.PostAsync(Url(teacher, $"/{assignmentId}/revoke"));

    private static async Task<Guid> IncomeCategoryAsync(Teacher teacher)
    {
        var categories = await JsonAsync(await teacher.Client.GetAsync($"/funds/{teacher.FundId}/categories?type=Income"), HttpStatusCode.OK);
        return categories.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> AddTransactionAsync(ApiClient client, Guid fundId, Guid categoryId) =>
        client.PostAsync($"/funds/{fundId}/transactions", new
        {
            type = "Income",
            amount = 100_000,
            occurredOn = Today,
            categoryId,
            description = "Đóng quỹ tháng 11",
            note = (string?)null,
            isPublic = true,
            idempotencyKey = Guid.NewGuid(),
        });

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> ActiveCountAsync(Guid fundId) =>
        WithDbAsync(db => db.TreasurerAssignments.CountAsync(assignment => assignment.FundId == fundId && assignment.RevokedAt == null));

    /// <summary>Thủ quỹ seed thẳng vào DB (nhanh hơn luồng đầy đủ) và Id phân công của họ.</summary>
    private async Task<List<Guid>> SeedTreasurersAsync(Teacher teacher, int count)
    {
        var fund = new SeededFund(teacher.FundId, "Quỹ", teacher.UserId);
        var assignmentIds = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var seeded = await _data.CreateStudentAccountAsync(fund, $"Thủ quỹ {i + 1}", treasurer: true);
            assignmentIds.Add(await WithDbAsync(db => db.TreasurerAssignments
                .Where(assignment => assignment.StudentId == seeded.StudentId!.Value)
                .Select(assignment => assignment.Id)
                .SingleAsync()));
        }

        return assignmentIds;
    }

    // ---------- Phân công (BR-31) ----------

    [Fact]
    public async Task Teacher_assigns_activated_student_who_then_records_transactions()
    {
        var teacher = await TeacherWithFundAsync();
        var pupil = await AddPupilAsync(teacher, "Phạm Thu Trang");
        var categoryId = await IncomeCategoryAsync(teacher);

        // Quỹ mới: chưa có thủ quỹ (BR-36) nên học sinh chưa ghi được thu chi.
        Assert.Empty(await ListAsync(teacher));
        await (await AddTransactionAsync(pupil.Client!, teacher.FundId, categoryId)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        var before = (await JsonAsync(await teacher.Client.GetAsync("/funds"), HttpStatusCode.OK)).EnumerateArray()
            .Single(fund => fund.GetProperty("id").GetGuid() == teacher.FundId);
        Assert.Equal("Unassigned", before.GetProperty("treasurerStatus").GetString());

        var assignmentId = await AssignAsync(teacher, pupil);

        var item = Assert.Single(await ListAsync(teacher));
        Assert.Equal(assignmentId, item.GetProperty("id").GetGuid());
        Assert.Equal(pupil.StudentId, item.GetProperty("studentId").GetGuid());
        Assert.Equal("Phạm Thu Trang", item.GetProperty("studentName").GetString());
        Assert.True(item.GetProperty("isActive").GetBoolean());
        Assert.Equal("Cô Hạnh", item.GetProperty("assignedByName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("revokedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("revokedByName").ValueKind);

        // Vai trò đổi ngay ở request kế tiếp (suy ra từ dữ liệu): học sinh ghi được thu chi.
        var me = await (await pupil.Client!.GetAsync("/auth/me")).ReadMeAsync();
        Assert.Equal("Treasurer", Assert.Single(me.Memberships).Role);
        Assert.Equal(HttpStatusCode.Created, (await AddTransactionAsync(pupil.Client!, teacher.FundId, categoryId)).StatusCode);

        var after = (await JsonAsync(await teacher.Client.GetAsync("/funds"), HttpStatusCode.OK)).EnumerateArray()
            .Single(fund => fund.GetProperty("id").GetGuid() == teacher.FundId);
        Assert.Equal("Assigned", after.GetProperty("treasurerStatus").GetString());
        Assert.Equal(["Phạm Thu Trang"], after.GetProperty("treasurerNames").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(100_000, after.GetProperty("currentBalance").GetInt64());

        var students = await JsonAsync(await teacher.Client.GetAsync($"/funds/{teacher.FundId}/students"), HttpStatusCode.OK);
        Assert.True(Assert.Single(students.EnumerateArray()).GetProperty("isTreasurer").GetBoolean());

        var audit = Assert.Single(await _data.AuditLogsAsync(teacher.UserId), log => log.Action == "Treasurer.Assign");
        Assert.Equal(teacher.UserId, audit.ActorUserId);
        Assert.Equal(assignmentId.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Assign_rejects_students_who_are_not_eligible()
    {
        var teacher = await TeacherWithFundAsync();
        var noAccount = await AddPupilAsync(teacher, "Chưa tài khoản", account: false);
        var pending = await AddPupilAsync(teacher, "Chưa kích hoạt", activate: false);
        var disabled = await AddPupilAsync(teacher, "Bị khóa");
        var reset = await AddPupilAsync(teacher, "Được cấp lại mật khẩu");
        var assigned = await AddPupilAsync(teacher, "Đã là thủ quỹ");
        await AssignAsync(teacher, assigned);

        await _data.DisableUserAsync(await WithDbAsync(db => db.Users.Where(user => user.Username == disabled.Login).Select(user => user.Id).SingleAsync()));
        await JsonAsync(await teacher.Client.PostAsync($"/funds/{teacher.FundId}/students/{reset.StudentId}/reset-password"), HttpStatusCode.OK);

        async Task Expect(Guid studentId, HttpStatusCode status, string code) =>
            await (await teacher.Client.PostAsync(Url(teacher), new { studentId })).AssertProblemAsync(status, code);

        await Expect(noAccount.StudentId, HttpStatusCode.UnprocessableEntity, "treasurer.no_account");
        await Expect(pending.StudentId, HttpStatusCode.UnprocessableEntity, "treasurer.account_not_activated");
        await Expect(disabled.StudentId, HttpStatusCode.UnprocessableEntity, "treasurer.account_disabled");
        await Expect(reset.StudentId, HttpStatusCode.UnprocessableEntity, "treasurer.account_not_activated");
        await Expect(assigned.StudentId, HttpStatusCode.UnprocessableEntity, "treasurer.already_assigned");
        await Expect(Guid.NewGuid(), HttpStatusCode.NotFound, "not_found");

        // Học sinh của quỹ khác (kể cả quỹ khác của cùng giáo viên) → 404.
        var otherFund = new Teacher(teacher.Client, teacher.UserId, await CreateFundAsync(teacher.Client));
        var elsewhere = await AddPupilAsync(otherFund, "Quỹ khác");
        await Expect(elsewhere.StudentId, HttpStatusCode.NotFound, "not_found");

        await (await teacher.Client.PostAsync(Url(teacher), new { })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await teacher.Client.PostAsync(Url(teacher), new { studentId = Guid.Empty })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        // Chỉ lần phân công hợp lệ duy nhất được lưu.
        Assert.Single(await ListAsync(teacher));
        Assert.Equal(1, await ActiveCountAsync(teacher.FundId));
        Assert.Empty(await ListAsync(otherFund));
    }

    // ---------- Thu hồi (BR-32, BR-33) ----------

    [Fact]
    public async Task Revoking_the_only_treasurer_is_blocked()
    {
        var teacher = await TeacherWithFundAsync();
        var pupil = await AddPupilAsync(teacher, "Phạm Thu Trang");
        var assignmentId = await AssignAsync(teacher, pupil);
        var categoryId = await IncomeCategoryAsync(teacher);

        var response = await RevokeAsync(teacher, assignmentId);

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.last");
        Assert.Contains("phân công người mới trước", problem.GetProperty("title").GetString());
        Assert.True(Assert.Single(await ListAsync(teacher)).GetProperty("isActive").GetBoolean());
        Assert.Equal(1, await ActiveCountAsync(teacher.FundId));
        Assert.Equal(HttpStatusCode.Created, (await AddTransactionAsync(pupil.Client!, teacher.FundId, categoryId)).StatusCode);
        Assert.DoesNotContain(await _data.AuditLogsAsync(teacher.UserId), log => log.Action == "Treasurer.Revoke");
    }

    [Fact]
    public async Task Changing_treasurer_assigns_the_new_one_first_then_revokes_the_old_one()
    {
        var teacher = await TeacherWithFundAsync();
        var trang = await AddPupilAsync(teacher, "Phạm Thu Trang");
        var bao = await AddPupilAsync(teacher, "Lê Gia Bảo");
        var categoryId = await IncomeCategoryAsync(teacher);
        var trangAssignment = await AssignAsync(teacher, trang);
        var baoAssignment = await AssignAsync(teacher, bao);
        Assert.Equal(2, (await ListAsync(teacher)).Count(item => item.GetProperty("isActive").GetBoolean()));

        Assert.Equal(HttpStatusCode.NoContent, (await RevokeAsync(teacher, trangAssignment)).StatusCode);

        // Có hiệu lực ngay ở request kế tiếp: người cũ không ghi được, người mới ghi được.
        await (await AddTransactionAsync(trang.Client!, teacher.FundId, categoryId)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(HttpStatusCode.Created, (await AddTransactionAsync(bao.Client!, teacher.FundId, categoryId)).StatusCode);
        Assert.Equal("Student", Assert.Single((await (await trang.Client!.GetAsync("/auth/me")).ReadMeAsync()).Memberships).Role);

        // Lịch sử: đang hiệu lực trước, người đã thu hồi sau kèm người / thời điểm thu hồi.
        var list = await ListAsync(teacher);
        Assert.Equal([baoAssignment, trangAssignment], list.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal([true, false], list.Select(item => item.GetProperty("isActive").GetBoolean()));
        Assert.Equal("Cô Hạnh", list[1].GetProperty("revokedByName").GetString());
        Assert.NotEqual(JsonValueKind.Null, list[1].GetProperty("revokedAt").ValueKind);
        var revoked = await WithDbAsync(db => db.TreasurerAssignments.AsNoTracking().SingleAsync(item => item.Id == trangAssignment));
        Assert.Equal(teacher.UserId, revoked.RevokedBy);

        var fund = (await JsonAsync(await teacher.Client.GetAsync("/funds"), HttpStatusCode.OK)).EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == teacher.FundId);
        Assert.Equal(["Lê Gia Bảo"], fund.GetProperty("treasurerNames").EnumerateArray().Select(name => name.GetString()));

        // Thu hồi lại → 409; người cuối cùng → 422.
        await (await RevokeAsync(teacher, trangAssignment)).AssertProblemAsync(HttpStatusCode.Conflict, "treasurer.already_revoked");
        await (await RevokeAsync(teacher, baoAssignment)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.last");
        Assert.Single(await _data.AuditLogsAsync(teacher.UserId), log => log.Action == "Treasurer.Revoke");

        // Học sinh đã bị thu hồi có thể được phân công lại: lịch sử giữ nguyên, thêm một dòng mới.
        await AssignAsync(teacher, trang);
        Assert.Equal(3, (await ListAsync(teacher)).Length);
        Assert.Equal(2, await ActiveCountAsync(teacher.FundId));
        Assert.Equal(HttpStatusCode.Created, (await AddTransactionAsync(trang.Client!, teacher.FundId, categoryId)).StatusCode);
    }

    [Fact]
    public async Task Assignment_of_another_fund_cannot_be_revoked_through_this_fund()
    {
        var teacher = await TeacherWithFundAsync();
        var pupil = await AddPupilAsync(teacher, "Phạm Thu Trang");
        var second = await AddPupilAsync(teacher, "Lê Gia Bảo");
        var assignmentId = await AssignAsync(teacher, pupil);
        await AssignAsync(teacher, second);
        var otherFund = new Teacher(teacher.Client, teacher.UserId, await CreateFundAsync(teacher.Client));

        await (await RevokeAsync(otherFund, assignmentId)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await RevokeAsync(teacher, Guid.NewGuid())).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        Assert.Equal(2, await ActiveCountAsync(teacher.FundId));
    }

    // ---------- Đồng thời (BR-32, NFR-09) ----------

    /// <summary>Phát đồng loạt các request: chờ cổng rồi gửi cùng lúc trên nhiều luồng.</summary>
    private static async Task<HttpResponseMessage[]> FireTogetherAsync(IEnumerable<Func<Task<HttpResponseMessage>>> requests)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = requests.Select(async send =>
        {
            await gate.Task;
            return await send();
        }).ToArray();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Concurrent_revokes_never_leave_a_fund_without_a_treasurer(int treasurerCount)
    {
        var teacher = await TeacherWithFundAsync();

        // Nhiều quỹ liên tiếp để tăng xác suất các request thật sự chen nhau.
        for (var round = 0; round < 6; round++)
        {
            var fundId = round == 0 ? teacher.FundId : await CreateFundAsync(teacher.Client);
            var target = new Teacher(teacher.Client, teacher.UserId, fundId);
            var assignmentIds = await SeedTreasurersAsync(target, treasurerCount);

            // Mỗi lệnh thu hồi một thủ quỹ khác nhau: nếu không có khóa, tất cả cùng thấy "còn ≥ 2" và cùng lọt.
            var responses = await FireTogetherAsync(assignmentIds.Select<Guid, Func<Task<HttpResponseMessage>>>(id => () => RevokeAsync(target, id)));

            Assert.Equal(treasurerCount - 1, responses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
            var rejected = responses.Where(response => response.StatusCode != HttpStatusCode.NoContent).ToArray();
            var last = Assert.Single(rejected);
            await last.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.last");
            Assert.Equal(1, await ActiveCountAsync(fundId));
            Assert.Single(await ListAsync(target), item => item.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task Concurrent_revokes_of_the_same_assignment_succeed_only_once()
    {
        var teacher = await TeacherWithFundAsync();
        var assignmentIds = await SeedTreasurersAsync(teacher, 3);

        var responses = await FireTogetherAsync(Enumerable.Range(0, 5).Select<int, Func<Task<HttpResponseMessage>>>(_ => () => RevokeAsync(teacher, assignmentIds[0])));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
        foreach (var response in responses.Where(response => response.StatusCode != HttpStatusCode.NoContent))
        {
            await response.AssertProblemAsync(HttpStatusCode.Conflict, "treasurer.already_revoked");
        }

        Assert.Equal(2, await ActiveCountAsync(teacher.FundId));
        Assert.Single(await _data.AuditLogsAsync(teacher.UserId), log => log.Action == "Treasurer.Revoke");
    }

    [Fact]
    public async Task Concurrent_assignments_of_the_same_student_create_only_one_assignment()
    {
        var teacher = await TeacherWithFundAsync();
        var pupil = await AddPupilAsync(teacher, "Phạm Thu Trang");

        var responses = await FireTogetherAsync(Enumerable.Range(0, 6).Select<int, Func<Task<HttpResponseMessage>>>(
            _ => () => teacher.Client.PostAsync(Url(teacher), new { studentId = pupil.StudentId })));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        foreach (var response in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
        {
            await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "treasurer.already_assigned");
        }

        Assert.Equal(1, await WithDbAsync(db => db.TreasurerAssignments.CountAsync(item => item.StudentId == pupil.StudentId)));
        Assert.Single(await _data.AuditLogsAsync(teacher.UserId), log => log.Action == "Treasurer.Assign");
    }

    // ---------- Phân quyền ----------

    [Fact]
    public async Task Only_homeroom_teacher_manages_treasurers()
    {
        var teacher = await TeacherWithFundAsync();
        var treasurer = await AddPupilAsync(teacher, "Phạm Thu Trang");
        var plainStudent = await AddPupilAsync(teacher, "Lê Gia Bảo");
        var assignmentId = await AssignAsync(teacher, treasurer);

        var parentLink = await JsonAsync(
            await teacher.Client.PostAsync($"/funds/{teacher.FundId}/students/{plainStudent.StudentId}/parent", new { email = TestData.NewEmail() }),
            HttpStatusCode.OK);
        var parentLogin = parentLink.GetProperty("credential").GetProperty("login").GetString()!;
        var parentTemporary = parentLink.GetProperty("credential").GetProperty("temporaryPassword").GetString()!;
        using var parent = fixture.Factory.CreateApiClient();
        await parent.LoginAsync(parentLogin, parentTemporary);
        await (await parent.PostAsync("/auth/change-password", new { currentPassword = parentTemporary, newPassword = NewPassword })).ReadSessionAsync(HttpStatusCode.OK);

        foreach (var client in new[] { treasurer.Client!, plainStudent.Client!, parent })
        {
            await (await client.GetAsync(Url(teacher))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PostAsync(Url(teacher), new { studentId = plainStudent.StudentId })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PostAsync(Url(teacher, $"/{assignmentId}/revoke"))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        // Giáo viên khác: quỹ không thuộc mình → 404 (không lộ quỹ tồn tại); Id sai định dạng cũng 404.
        var other = await TeacherWithFundAsync();
        await (await other.Client.GetAsync(Url(teacher))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await other.Client.PostAsync(Url(teacher), new { studentId = plainStudent.StudentId })).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await other.Client.PostAsync(Url(teacher, $"/{assignmentId}/revoke"))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await other.Client.GetAsync("/funds/khong-phai-guid/treasurers")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        using var anonymous = fixture.Factory.CreateApiClient();
        await (await anonymous.GetAsync(Url(teacher))).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");

        Assert.Equal(1, await ActiveCountAsync(teacher.FundId));
    }
}
