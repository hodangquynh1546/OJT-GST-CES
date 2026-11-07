using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Funds;

/// <summary>Quỹ của tôi, tạo quỹ, đổi tên quỹ qua HTTP thật – FR-22, FR-23, FR-11, BR-02, BR-23, BR-36.</summary>
[Collection(ApiCollection.Name)]
public sealed class FundTests(PostgresFixture fixture)
{
    private const string TeacherPassword = "Matkhau123";
    private const string NewPassword = "Matkhau-456";

    private static readonly string[] DefaultCategories =
    [
        "Expense: Chi khác",
        "Expense: Khen thưởng",
        "Expense: Sinh hoạt, liên hoan",
        "Expense: Văn phòng phẩm",
        "Income: Đóng quỹ",
        "Income: Thu khác",
    ];

    private readonly TestData _data = fixture.Data;

    private sealed record Teacher(ApiClient Client, Guid UserId);

    private sealed record World(SeededFund Fund, Teacher Teacher, ApiClient Treasurer, ApiClient Student, ApiClient Parent);

    private async Task<Teacher> RegisterTeacherAsync()
    {
        var client = fixture.Factory.CreateApiClient();
        var session = await (await client.PostAsync("/auth/register-teacher", new
        {
            fullName = "Cô Hạnh",
            email = TestData.NewEmail(),
            password = TeacherPassword,
            confirmPassword = TeacherPassword,
        })).ReadSessionAsync(HttpStatusCode.Created);
        return new Teacher(client, session.User.Id);
    }

    private async Task<ApiClient> SignInAsync(SeededAccount account, bool changePassword = true)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.LoginAsync(account.Login, account.Password);
        if (changePassword)
        {
            await (await client.PostAsync("/auth/change-password", new { currentPassword = account.Password, newPassword = NewPassword }))
                .ReadSessionAsync(HttpStatusCode.OK);
        }

        return client;
    }

    private async Task<World> SeedAsync()
    {
        var teacher = await RegisterTeacherAsync();
        var fund = await _data.CreateFundAsync(teacher.UserId, TestData.Unique("Quỹ lớp 5A "));
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var student = await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo");
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);
        return new World(fund, teacher, await SignInAsync(treasurer), await SignInAsync(student), await SignInAsync(parent));
    }

    private static async Task<Guid> CreateFundAsync(ApiClient client, string name)
    {
        var response = await client.PostAsync("/funds", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ListAsync(ApiClient client)
    {
        var response = await client.GetAsync("/funds");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonElement Single(JsonElement list, Guid fundId) =>
        Assert.Single(list.EnumerateArray(), item => item.GetProperty("id").GetGuid() == fundId);

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    /// <summary>Ghi giao dịch thẳng vào DB (feature Thu chi chưa có API).</summary>
    private async Task AddTransactionAsync(SeededFund fund, TransactionType type, long amount, bool cancelled = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var category = new Category(fund.Id, type, TestData.Unique("Danh mục "), now);
        var transaction = new FundTransaction(
            fund.Id, type, amount, new DateOnly(2026, 11, 5), category.Id, "Giao dịch", null, true, fund.OwnerUserId, Guid.NewGuid(), now);
        db.AddRange(category, transaction);
        await db.SaveChangesAsync();

        if (cancelled)
        {
            await db.FundTransactions
                .Where(item => item.Id == transaction.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(item => item.IsCancelled, true)
                    .SetProperty(item => item.CancelledAt, now)
                    .SetProperty(item => item.CancelReason, "Nhập nhầm")
                    .SetProperty(item => item.CancelledBy, fund.OwnerUserId));
        }
    }

    private async Task<(Fund Fund, string[] Categories, List<AuditLog> Audit)> LoadFundAsync(Guid fundId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fund = await db.Funds.AsNoTracking().SingleAsync(item => item.Id == fundId);
        var categories = await db.Categories.AsNoTracking()
            .Where(category => category.FundId == fundId && !category.IsHidden)
            .Select(category => category.Type.ToString() + ": " + category.Name)
            .ToListAsync();
        var id = fundId.ToString();
        var audit = await db.AuditLogs.AsNoTracking().Where(log => log.EntityId == id).OrderBy(log => log.Id).ToListAsync();
        return (fund, categories.Order(StringComparer.Ordinal).ToArray(), audit);
    }

    // ---------- POST /funds ----------

    [Fact]
    public async Task Teacher_creates_fund_with_six_default_categories_and_no_treasurer()
    {
        var teacher = await RegisterTeacherAsync();

        var fundId = await CreateFundAsync(teacher.Client, "  Quỹ lớp 5A  ");

        var (fund, categories, audit) = await LoadFundAsync(fundId);
        Assert.Equal(teacher.UserId, fund.OwnerUserId);
        Assert.Equal("Quỹ lớp 5A", fund.Name);
        Assert.Equal(DefaultCategories.Order(StringComparer.Ordinal), categories);
        var created = Assert.Single(audit, log => log.Action == "Fund.Create");
        Assert.Equal(teacher.UserId, created.ActorUserId);

        // Người tạo là GVCN ngay ở request kế tiếp; quỹ mới chưa có thủ quỹ, chưa có học sinh, số dư 0.
        var summary = Single(await ListAsync(teacher.Client), fundId);
        Assert.Equal("Teacher", summary.GetProperty("role").GetString());
        Assert.Equal("Unassigned", summary.GetProperty("treasurerStatus").GetString());
        Assert.Empty(summary.GetProperty("treasurerNames").EnumerateArray());
        Assert.Equal(0, summary.GetProperty("studentCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("currentBalance").GetInt64());

        // Danh mục mặc định đọc được qua API Danh mục của quỹ.
        var listed = await (await teacher.Client.GetAsync($"/funds/{fundId}/categories")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, listed.GetArrayLength());
    }

    [Fact]
    public async Task Create_fund_validates_name_length_after_trimming()
    {
        var teacher = await RegisterTeacherAsync();

        await (await teacher.Client.PostAsync("/funds", new { name = "   " })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await teacher.Client.PostAsync("/funds", new { })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var tooLong = await (await teacher.Client.PostAsync("/funds", new { name = new string('a', 101) }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(tooLong.GetProperty("errors").TryGetProperty("name", out _));

        await CreateFundAsync(teacher.Client, " " + new string('a', 100) + " ");
    }

    [Fact]
    public async Task Only_teachers_can_create_funds()
    {
        var world = await SeedAsync();

        foreach (var client in new[] { world.Treasurer, world.Student, world.Parent })
        {
            await (await client.PostAsync("/funds", new { name = "Quỹ lạ" })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }
    }

    // ---------- PUT /funds/{fundId} ----------

    [Fact]
    public async Task Homeroom_teacher_renames_fund_and_change_is_audited()
    {
        var teacher = await RegisterTeacherAsync();
        var fundId = await CreateFundAsync(teacher.Client, "Quỹ lớp 5A");

        var response = await teacher.Client.PutAsync($"/funds/{fundId}", new { name = "  Quỹ lớp 6A  " });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Quỹ lớp 6A", Single(await ListAsync(teacher.Client), fundId).GetProperty("name").GetString());
        var (fund, _, audit) = await LoadFundAsync(fundId);
        Assert.NotNull(fund.UpdatedAt);
        var rename = Assert.Single(audit, log => log.Action == "Fund.Rename");
        Assert.Contains("Quỹ lớp 5A", rename.OldValues);
        Assert.Contains("Quỹ lớp 6A", rename.NewValues);

        // Đổi sang đúng tên hiện tại: không ghi nhật ký thừa.
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.Client.PutAsync($"/funds/{fundId}", new { name = "Quỹ lớp 6A" })).StatusCode);
        Assert.Single((await LoadFundAsync(fundId)).Audit, log => log.Action == "Fund.Rename");

        await (await teacher.Client.PutAsync($"/funds/{fundId}", new { name = "" })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Non_homeroom_accounts_cannot_rename_fund()
    {
        var world = await SeedAsync();
        var url = $"/funds/{world.Fund.Id}";

        // Thuộc quỹ nhưng không phải GVCN → 403.
        foreach (var client in new[] { world.Treasurer, world.Student, world.Parent })
        {
            await (await client.PutAsync(url, new { name = "Đổi trộm" })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        // Giáo viên khác không thuộc quỹ → 404 (không lộ quỹ tồn tại).
        var otherTeacher = await RegisterTeacherAsync();
        await (await otherTeacher.Client.PutAsync(url, new { name = "Đổi trộm" })).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await world.Teacher.Client.PutAsync($"/funds/{Guid.NewGuid()}", new { name = "Không có" }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        var (fund, _, audit) = await LoadFundAsync(world.Fund.Id);
        Assert.Equal(world.Fund.Name, fund.Name);
        Assert.DoesNotContain(audit, log => log.Action == "Fund.Rename");
    }

    // ---------- GET /funds ----------

    [Fact]
    public async Task List_returns_balance_treasurers_and_role_specific_fields()
    {
        var world = await SeedAsync();
        await AddTransactionAsync(world.Fund, TransactionType.Income, 500_000);
        await AddTransactionAsync(world.Fund, TransactionType.Expense, 120_000);
        await AddTransactionAsync(world.Fund, TransactionType.Expense, 50_000, cancelled: true);

        var teacher = Single(await ListAsync(world.Teacher.Client), world.Fund.Id);
        var treasurer = Single(await ListAsync(world.Treasurer), world.Fund.Id);
        var student = Single(await ListAsync(world.Student), world.Fund.Id);
        var parent = Single(await ListAsync(world.Parent), world.Fund.Id);

        // Giống nhau ở mọi vai trò: số dư (khoản hủy không tính), trạng thái và họ tên thủ quỹ.
        foreach (var item in new[] { teacher, treasurer, student, parent })
        {
            Assert.Equal(world.Fund.Name, item.GetProperty("name").GetString());
            Assert.Equal(380_000, item.GetProperty("currentBalance").GetInt64());
            Assert.Equal("Assigned", item.GetProperty("treasurerStatus").GetString());
            Assert.Equal(["Phạm Thu Trang"], Strings(item.GetProperty("treasurerNames")));
            Assert.True(item.TryGetProperty("createdAt", out _));
        }

        Assert.Equal("Teacher", teacher.GetProperty("role").GetString());
        Assert.Equal(2, teacher.GetProperty("studentCount").GetInt32());
        Assert.False(teacher.TryGetProperty("linkedStudents", out _));

        Assert.Equal("Treasurer", treasurer.GetProperty("role").GetString());
        Assert.Contains("Transaction.Manage", Strings(treasurer.GetProperty("permissions")));

        Assert.Equal("Student", student.GetProperty("role").GetString());
        Assert.Equal(["Fund.View", "Ledger.ViewPublic"], Strings(student.GetProperty("permissions")));

        Assert.Equal("Parent", parent.GetProperty("role").GetString());
        Assert.Equal(["Lê Gia Bảo"], Strings(parent.GetProperty("linkedStudents")));

        // Số học sinh chỉ GVCN thấy; tên con chỉ phụ huynh thấy (không có trong JSON của vai trò khác).
        foreach (var item in new[] { treasurer, student, parent })
        {
            Assert.False(item.TryGetProperty("studentCount", out _));
        }

        foreach (var item in new[] { treasurer, student })
        {
            Assert.False(item.TryGetProperty("linkedStudents", out _));
        }
    }

    [Fact]
    public async Task List_contains_only_funds_of_current_account()
    {
        var teacher = await RegisterTeacherAsync();
        var otherTeacher = await RegisterTeacherAsync();
        var b = await CreateFundAsync(teacher.Client, "Quỹ lớp 5B");
        var a = await CreateFundAsync(teacher.Client, "Quỹ lớp 5A");
        await CreateFundAsync(otherTeacher.Client, "Quỹ của giáo viên khác");

        var ids = (await ListAsync(teacher.Client)).EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();

        Assert.Equal([a, b], ids);
        Assert.Empty((await ListAsync((await RegisterTeacherAsync()).Client)).EnumerateArray());
    }

    [Fact]
    public async Task List_requires_sign_in_and_changed_password()
    {
        using var anonymous = fixture.Factory.CreateApiClient();
        await (await anonymous.GetAsync("/funds")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");

        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund);
        using var client = await SignInAsync(student, changePassword: false);
        await (await client.GetAsync("/funds")).AssertProblemAsync(HttpStatusCode.Forbidden, "password.change_required");
    }
}
