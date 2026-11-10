using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Funds;

/// <summary>
/// M2 – Quỹ của tôi / Quỹ của con, tạo quỹ, đổi tên quỹ qua HTTP thật (docs/implementation/module-plan.md mục M2;
/// FR-22, FR-23, FR-11, BR-02, BR-23, BR-36, BR-37; test case T-03, T-05, TR-03, P-04, S-08, P-08).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FundsTests(PostgresFixture fixture)
{
    private static readonly string[] DefaultCategories =
    [
        "Expense: Chi khác",
        "Expense: Khen thưởng",
        "Expense: Sinh hoạt, liên hoan",
        "Expense: Văn phòng phẩm",
        "Income: Đóng quỹ",
        "Income: Thu khác",
    ];

    private static readonly string[] TeacherPermissions =
        ["Fund.Manage", "Fund.View", "Ledger.ViewFull", "Member.View", "Student.Manage", "Treasurer.Assign"];

    private readonly TestData _data = fixture.Data;

    // ---------- POST /funds ----------

    [Fact]
    public async Task Teacher_creates_two_funds_with_default_categories_unassigned_and_no_students()
    {
        var teacher = await _data.CreateTeacherAsync();
        using var client = await SignInAsync(teacher.UserId);

        var first = await CreateFundAsync(client, "  Quỹ lớp 5A  ");
        var second = await CreateFundAsync(client, "Quỹ lớp 5B");

        var (fund, categories, audit) = await LoadFundAsync(first);
        Assert.Equal(teacher.UserId, fund.OwnerUserId);
        Assert.Equal("Quỹ lớp 5A", fund.Name);
        Assert.Equal(DefaultCategories.Order(StringComparer.Ordinal), categories);
        var created = Assert.Single(audit, log => log.Action == "Fund.Create");
        Assert.Equal(teacher.UserId, created.ActorUserId);
        Assert.Contains("Quỹ lớp 5A", created.NewValues);
        Assert.Equal(DefaultCategories.Order(StringComparer.Ordinal), (await LoadFundAsync(second)).Categories);

        var list = await ListAsync(client);
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(["Quỹ lớp 5A", "Quỹ lớp 5B"], list.EnumerateArray().Select(item => item.GetProperty("name").GetString()));

        var summary = Single(list, first);
        Assert.Equal("Teacher", summary.GetProperty("role").GetString());
        Assert.Equal(TeacherPermissions, Strings(summary.GetProperty("permissions")));
        Assert.Equal(0, summary.GetProperty("currentBalance").GetInt64());
        Assert.Equal("Unassigned", summary.GetProperty("treasurerStatus").GetString());
        Assert.Empty(summary.GetProperty("treasurerNames").EnumerateArray());
        Assert.Equal(0, summary.GetProperty("studentCount").GetInt32());
        Assert.False(summary.TryGetProperty("linkedStudents", out _));
        Assert.True(summary.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Teacher_without_fund_sees_empty_list()
    {
        var teacher = await _data.CreateTeacherAsync();
        await _data.CreateFundAsync();
        using var client = await SignInAsync(teacher.UserId);

        Assert.Empty((await ListAsync(client)).EnumerateArray());
    }

    [Theory]
    [InlineData(null, "Nhập tên quỹ.")]
    [InlineData("", "Nhập tên quỹ.")]
    [InlineData("   ", "Nhập tên quỹ.")]
    [InlineData(101, "Tên quỹ tối đa 100 ký tự.")]
    public async Task Create_rejects_invalid_name(object? name, string message)
    {
        var teacher = await _data.CreateTeacherAsync();
        using var client = await SignInAsync(teacher.UserId);

        var response = await client.PostAsync("/funds", new { name = NameValue(name) });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal([message], Strings(problem.GetProperty("errors").GetProperty("name")));
        Assert.Empty((await ListAsync(client)).EnumerateArray());
    }

    [Fact]
    public async Task Name_of_exactly_100_characters_after_trim_is_accepted()
    {
        var teacher = await _data.CreateTeacherAsync();
        using var client = await SignInAsync(teacher.UserId);
        var name = new string('A', 100);

        var fundId = await CreateFundAsync(client, $"  {name}  ");

        Assert.Equal(name, (await LoadFundAsync(fundId)).Fund.Name);
    }

    // ---------- GET /funds ----------

    [Fact]
    public async Task Teacher_sees_balance_student_count_and_treasurer_names()
    {
        var teacher = await _data.CreateTeacherAsync();
        var fund = await _data.CreateFundAsync(teacher.UserId);
        await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo");
        await AddStudentWithoutAccountAsync(fund, "Đỗ Minh Anh");
        await AddTransactionAsync(fund, TransactionType.Income, 1_500_000);
        await AddTransactionAsync(fund, TransactionType.Expense, 250_000);
        await AddTransactionAsync(fund, TransactionType.Expense, 900_000, cancelled: true);
        using var client = await SignInAsync(teacher.UserId);

        var summary = Single(await ListAsync(client), fund.Id);

        Assert.Equal(fund.Name, summary.GetProperty("name").GetString());
        Assert.Equal(1_250_000, summary.GetProperty("currentBalance").GetInt64());
        Assert.Equal(3, summary.GetProperty("studentCount").GetInt32());
        Assert.Equal("Assigned", summary.GetProperty("treasurerStatus").GetString());
        Assert.Equal(["Phạm Thu Trang"], Strings(summary.GetProperty("treasurerNames")));
    }

    [Fact]
    public async Task Parent_sees_child_funds_without_student_count()
    {
        var teacher = await _data.CreateTeacherAsync();
        var fundA = await _data.CreateFundAsync(teacher.UserId, TestData.Unique("Quỹ lớp 5A "));
        var fundB = await _data.CreateFundAsync(name: TestData.Unique("Quỹ lớp 3C "));
        var treasurer = await _data.CreateStudentAccountAsync(fundA, "Phạm Thu Trang", treasurer: true);
        var childA = await AddStudentWithoutAccountAsync(fundA, "Phạm Gia Huy");
        var childB = await AddStudentWithoutAccountAsync(fundB, "Phạm Gia Hân");
        var parentA = await _data.CreateParentAccountAsync(fundA, childA);
        await LinkParentAsync(fundB, childB, parentA.UserId);
        await AddTransactionAsync(fundA, TransactionType.Income, 300_000);
        using var client = await SignInAsync(parentA.UserId, activate: true);

        var list = await ListAsync(client);

        Assert.Equal(2, list.GetArrayLength());
        var a = Single(list, fundA.Id);
        Assert.Equal("Parent", a.GetProperty("role").GetString());
        Assert.Equal(["Fund.View", "Ledger.ViewPublic"], Strings(a.GetProperty("permissions")));
        Assert.Equal(["Phạm Gia Huy"], Strings(a.GetProperty("linkedStudents")));
        Assert.Equal(["Phạm Thu Trang"], Strings(a.GetProperty("treasurerNames")));
        Assert.Equal(300_000, a.GetProperty("currentBalance").GetInt64());
        Assert.False(a.TryGetProperty("studentCount", out _));
        Assert.Equal(["Phạm Gia Hân"], Strings(Single(list, fundB.Id).GetProperty("linkedStudents")));
        Assert.NotNull(treasurer.StudentId);
    }

    [Fact]
    public async Task Treasurer_and_student_see_fund_without_student_count_or_children()
    {
        var fund = await _data.CreateFundAsync();
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var student = await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo");

        using var treasurerClient = await SignInAsync(treasurer.UserId, activate: true);
        using var studentClient = await SignInAsync(student.UserId, activate: true);

        var asTreasurer = Assert.Single((await ListAsync(treasurerClient)).EnumerateArray());
        Assert.Equal("Treasurer", asTreasurer.GetProperty("role").GetString());
        Assert.Equal("Assigned", asTreasurer.GetProperty("treasurerStatus").GetString());
        Assert.False(asTreasurer.TryGetProperty("studentCount", out _));
        Assert.False(asTreasurer.TryGetProperty("linkedStudents", out _));

        var asStudent = Assert.Single((await ListAsync(studentClient)).EnumerateArray());
        Assert.Equal("Student", asStudent.GetProperty("role").GetString());
        Assert.Equal(["Phạm Thu Trang"], Strings(asStudent.GetProperty("treasurerNames")));
        Assert.False(asStudent.TryGetProperty("studentCount", out _));
    }

    [Fact]
    public async Task List_requires_sign_in_and_no_temporary_password()
    {
        using var anonymous = fixture.Factory.CreateApiClient();
        await (await anonymous.GetAsync("/funds")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");

        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund);
        using var client = await SignInAsync(student.UserId);
        await (await client.GetAsync("/funds")).AssertProblemAsync(HttpStatusCode.Forbidden, "password.change_required");
    }

    // ---------- PUT /funds/{fundId} ----------

    [Fact]
    public async Task Teacher_renames_fund_and_audit_keeps_old_and_new_name()
    {
        var teacher = await _data.CreateTeacherAsync();
        using var client = await SignInAsync(teacher.UserId);
        var fundId = await CreateFundAsync(client, "Quỹ lớp 5A");

        var response = await client.PutAsync($"/funds/{fundId}", new { name = "  Quỹ lớp 5A – 2026  " });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var (fund, _, audit) = await LoadFundAsync(fundId);
        Assert.Equal("Quỹ lớp 5A – 2026", fund.Name);
        Assert.NotNull(fund.UpdatedAt);
        var renamed = Assert.Single(audit, log => log.Action == "Fund.Rename");
        Assert.Equal(teacher.UserId, renamed.ActorUserId);
        Assert.Contains("Quỹ lớp 5A", renamed.OldValues);
        Assert.Contains("Quỹ lớp 5A – 2026", renamed.NewValues);
        Assert.Equal("Quỹ lớp 5A – 2026", Single(await ListAsync(client), fundId).GetProperty("name").GetString());

        // Tên không đổi: 204, không ghi thêm AuditLog.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/funds/{fundId}", new { name = "Quỹ lớp 5A – 2026" })).StatusCode);
        Assert.Single((await LoadFundAsync(fundId)).Audit, log => log.Action == "Fund.Rename");
    }

    [Theory]
    [InlineData("", "Nhập tên quỹ.")]
    [InlineData(101, "Tên quỹ tối đa 100 ký tự.")]
    public async Task Rename_rejects_invalid_name(object? name, string message)
    {
        var teacher = await _data.CreateTeacherAsync();
        var fund = await _data.CreateFundAsync(teacher.UserId);
        using var client = await SignInAsync(teacher.UserId);

        var response = await client.PutAsync($"/funds/{fund.Id}", new { name = NameValue(name) });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal([message], Strings(problem.GetProperty("errors").GetProperty("name")));
        Assert.Equal(fund.Name, (await LoadFundAsync(fund.Id)).Fund.Name);
    }

    [Fact]
    public async Task Teacher_cannot_see_or_rename_another_teachers_fund()
    {
        var teacherA = await _data.CreateTeacherAsync("Cô Hạnh");
        var teacherB = await _data.CreateTeacherAsync("Thầy Nam");
        var fundA = await _data.CreateFundAsync(teacherA.UserId);
        using var client = await SignInAsync(teacherB.UserId);

        Assert.Empty((await ListAsync(client)).EnumerateArray());
        await (await client.PutAsync($"/funds/{fundA.Id}", new { name = "Đổi trộm" }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await client.PutAsync($"/funds/{Guid.NewGuid()}", new { name = "Không có" }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await client.PutAsync("/funds/khong-phai-guid", new { name = "Sai định dạng" }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal(fundA.Name, (await LoadFundAsync(fundA.Id)).Fund.Name);
    }

    [Fact]
    public async Task Treasurer_student_and_parent_cannot_create_or_rename_fund()
    {
        var fund = await _data.CreateFundAsync();
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        var student = await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo");
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);

        foreach (var account in new[] { treasurer, student, parent })
        {
            using var client = await SignInAsync(account.UserId, activate: true);
            await (await client.PostAsync("/funds", new { name = "Quỹ mới" }))
                .AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PutAsync($"/funds/{fund.Id}", new { name = "Đổi tên" }))
                .AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        Assert.Equal(fund.Name, (await LoadFundAsync(fund.Id)).Fund.Name);
    }

    // ---------- helpers ----------

    private async Task<ApiClient> SignInAsync(Guid userId, bool activate = false)
    {
        if (activate)
        {
            await ActivateAsync(userId);
        }

        var client = fixture.Factory.CreateApiClient();
        await client.SignInAsAsync(userId);
        return client;
    }

    private static async Task<Guid> CreateFundAsync(ApiClient client, string name)
    {
        var response = await client.PostAsync("/funds", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(body.EnumerateObject());
        return body.GetProperty("id").GetGuid();
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

    /// <summary><c>int</c> → chuỗi dài đúng số ký tự đó; còn lại giữ nguyên (null, chuỗi).</summary>
    private static string? NameValue(object? name) => name is int length ? new string('A', length) : (string?)name;

    /// <summary>Tài khoản đã đổi mật khẩu tạm (M1 chưa có API đổi mật khẩu).</summary>
    private async Task ActivateAsync(Guid userId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId);
        user.ChangePassword(hasher.Hash("Matkhau-456"), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    /// <summary>Học sinh chưa cấp tài khoản (M3 chưa có API).</summary>
    private async Task<Guid> AddStudentWithoutAccountAsync(SeededFund fund, string fullName)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var student = new Student(fund.Id, fullName, fund.OwnerUserId, DateTimeOffset.UtcNow);
        db.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }

    /// <summary>Liên kết thêm một con cho phụ huynh đã có (M3 chưa có API).</summary>
    private async Task LinkParentAsync(SeededFund fund, Guid studentId, Guid parentUserId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(new ParentLink(studentId, parentUserId, fund.OwnerUserId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    /// <summary>Ghi giao dịch thẳng vào DB (M6 chưa có API).</summary>
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
}
