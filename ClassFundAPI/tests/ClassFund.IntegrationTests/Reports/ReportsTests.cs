using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Application.Common.Security;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Reports;

/// <summary>
/// M7b – Báo cáo tháng / năm qua HTTP thật và PostgreSQL (module-plan.md mục M7; FR-08, FR-09, BR-02, BR-10, BR-15, P1, NFR-08;
/// test case P-07, R-01, LEAK-09, LEAK-12). Quỹ tạo bằng <c>POST /funds</c> (6 danh mục mặc định); khoản thu chi ghi qua API M6.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportsTests(PostgresFixture fixture)
{
    private const string SecretDescription = "Quà tặng cô giáo 20/11";
    private const string SecretNote = "Hoa và phong bì";
    private const string HiddenCategory = "Khen thưởng";

    private readonly TestData _data = fixture.Data;

    private sealed record World(
        Guid FundId,
        SeededFund Fund,
        ApiClient Teacher,
        ApiClient Treasurer,
        ApiClient Student,
        ApiClient Parent,
        Dictionary<string, Guid> Categories);

    // ---------- GET /reports/monthly ----------

    [Fact]
    public async Task Monthly_report_has_opening_totals_closing_by_category_and_active_transactions()
    {
        var world = await WorldAsync();
        await AddAsync(world, "Income", 2_450_000, "Đóng quỹ", "Đóng quỹ tháng 10", "2026-10-02");
        await AddAsync(world, "Income", 3_200_000, "Đóng quỹ", "Đóng quỹ tháng 11", "2026-11-02");
        await AddAsync(world, "Expense", 180_000, "Văn phòng phẩm", "Mua giấy A4", "2026-11-05");
        await AddAsync(world, "Expense", 1_250_000, "Sinh hoạt, liên hoan", "Liên hoan 20/11", "2026-11-08");
        var cancelled = await AddAsync(world, "Expense", 90_000, "Chi khác", "Nhập nhầm", "2026-11-09");
        await CancelAsync(world, cancelled);

        var report = await MonthlyAsync(world.Teacher, world, 2026, 11);

        Assert.Equal(2026, report.GetProperty("year").GetInt32());
        Assert.Equal(11, report.GetProperty("month").GetInt32());
        Assert.Equal(2_450_000, Long(report, "openingBalance"));
        Assert.Equal(3_200_000, Long(report, "totalIncome"));
        Assert.Equal(1_430_000, Long(report, "totalExpense"));
        Assert.Equal(1_770_000, Long(report, "netChange"));
        Assert.Equal(4_220_000, Long(report, "closingBalance"));

        // Thu trước chi; trong mỗi loại số tiền lớn trước. Khoản đã hủy không tính.
        var lines = report.GetProperty("byCategory").EnumerateArray().ToArray();
        Assert.Equal(["Đóng quỹ", "Sinh hoạt, liên hoan", "Văn phòng phẩm"], lines.Select(line => line.GetProperty("categoryName").GetString()));
        Assert.Equal(["Income", "Expense", "Expense"], lines.Select(line => line.GetProperty("type").GetString()));
        Assert.All(lines, line => Assert.Equal(1, line.GetProperty("count").GetInt32()));
        Assert.Equal(world.Categories["Đóng quỹ"], lines[0].GetProperty("categoryId").GetGuid());

        // Danh sách: chỉ khoản đang hiệu lực của tháng, mới nhất trước, bản đầy đủ có trường nội bộ.
        var transactions = report.GetProperty("transactions").EnumerateArray().ToArray();
        Assert.Equal(["Liên hoan 20/11", "Mua giấy A4", "Đóng quỹ tháng 11"], transactions.Select(item => item.GetProperty("description").GetString()));
        Assert.All(transactions, item => Assert.True(item.TryGetProperty("createdByName", out _)));
    }

    [Fact]
    public async Task Next_opening_balance_equals_previous_closing_even_after_editing_and_cancelling_an_old_transaction()
    {
        var world = await WorldAsync();
        var august = await AddAsync(world, "Income", 300_000, "Đóng quỹ", "Tháng 8", "2026-08-10");
        var september = await AddAsync(world, "Income", 100_000, "Đóng quỹ", "Tháng 9", "2026-09-10");
        await AddAsync(world, "Expense", 40_000, "Văn phòng phẩm", "Tháng 10", "2026-10-10");

        var before = await MonthlyAsync(world.Teacher, world, 2026, 10);
        Assert.Equal(400_000, Long(before, "openingBalance"));
        Assert.Equal(-40_000, Long(before, "netChange"));
        Assert.Equal(360_000, Long(before, "closingBalance"));

        await CancelAsync(world, august);
        await ReadAsync(
            await world.Treasurer.PutAsync($"/funds/{world.FundId}/transactions/{september}", new
            {
                amount = 150_000,
                occurredOn = "2026-09-10",
                categoryId = world.Categories["Đóng quỹ"],
                description = "Tháng 9",
                isPublic = true,
            }),
            HttpStatusCode.OK);

        var months = new List<JsonElement>();
        for (var month = 8; month <= 11; month++)
        {
            months.Add(await MonthlyAsync(world.Teacher, world, 2026, month));
        }

        Assert.Equal(0, Long(months[0], "totalIncome"));
        for (var index = 1; index < months.Count; index++)
        {
            Assert.Equal(Long(months[index - 1], "closingBalance"), Long(months[index], "openingBalance"));
        }

        Assert.Equal(150_000, Long(months[2], "openingBalance"));
        Assert.Equal(110_000, Long(months[2], "closingBalance"));

        var yearly = await YearlyAsync(world.Teacher, world, 2026);
        var october = yearly.GetProperty("months")[9];
        Assert.Equal(Long(months[2], "openingBalance"), Long(october, "openingBalance"));
        Assert.Equal(Long(months[2], "closingBalance"), Long(october, "closingBalance"));
    }

    [Fact]
    public async Task Student_and_parent_get_other_lines_that_add_up_without_leaking_details()
    {
        var world = await WorldAsync();
        await AddAsync(world, "Income", 3_200_000, "Đóng quỹ", "Đóng quỹ tháng 11", "2026-11-02");
        await AddAsync(world, "Income", 300_000, "Thu khác", "Ủng hộ riêng", "2026-11-03", isPublic: false);
        await AddAsync(world, "Expense", 180_000, "Văn phòng phẩm", "Mua giấy A4", "2026-11-05");
        await AddAsync(world, "Expense", 500_000, HiddenCategory, SecretDescription, "2026-11-10", isPublic: false, note: SecretNote);
        await AddAsync(world, "Expense", 70_000, HiddenCategory, "Thiệp", "2026-11-11", isPublic: false);
        // Danh mục có cả khoản công khai và không công khai: phần không công khai vào "Khoản khác" (TD-108).
        await AddAsync(world, "Expense", 20_000, "Văn phòng phẩm", "Bút riêng", "2026-11-12", isPublic: false);

        var full = await MonthlyAsync(world.Treasurer, world, 2026, 11);
        Assert.Equal(4, full.GetProperty("byCategory").GetArrayLength());
        Assert.Equal(6, full.GetProperty("transactions").GetArrayLength());
        Assert.Contains(HiddenCategory, full.GetRawText());
        Assert.Equal(full.GetRawText(), (await MonthlyAsync(world.Teacher, world, 2026, 11)).GetRawText());

        foreach (var viewer in new[] { world.Student, world.Parent })
        {
            var response = await viewer.GetAsync($"/funds/{world.FundId}/reports/monthly?year=2026&month=11");
            var raw = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var report = JsonDocument.Parse(raw).RootElement;

            foreach (var name in new[] { "openingBalance", "totalIncome", "totalExpense", "netChange", "closingBalance" })
            {
                Assert.Equal(Long(full, name), Long(report, name));
            }

            var lines = report.GetProperty("byCategory").EnumerateArray().ToArray();
            Assert.Equal(
                ["Đóng quỹ", "Khoản khác", "Văn phòng phẩm", "Khoản khác"],
                lines.Select(line => line.GetProperty("categoryName").GetString()));
            var others = lines.Where(line => line.GetProperty("categoryName").GetString() == "Khoản khác").ToArray();
            Assert.All(others, line =>
            {
                Assert.Equal(JsonValueKind.Null, line.GetProperty("categoryId").ValueKind);
                Assert.Equal(JsonValueKind.Null, line.GetProperty("count").ValueKind);
            });
            Assert.Equal(300_000, Long(others[0], "amount"));
            Assert.Equal(590_000, Long(others[1], "amount"));
            Assert.Equal(1, lines[2].GetProperty("count").GetInt32());
            Assert.Equal(180_000, Long(lines[2], "amount"));

            // Σ các dòng = số tổng (BR-15).
            Assert.Equal(Long(report, "totalIncome"), SumLines(report, "Income"));
            Assert.Equal(Long(report, "totalExpense"), SumLines(report, "Expense"));

            var transactions = report.GetProperty("transactions").EnumerateArray().ToArray();
            Assert.Equal(["Mua giấy A4", "Đóng quỹ tháng 11"], transactions.Select(item => item.GetProperty("description").GetString()));
            Assert.DoesNotContain(HiddenCategory, raw);
            Assert.DoesNotContain(SecretDescription, raw);
            Assert.DoesNotContain(SecretNote, raw);
            Assert.DoesNotContain("Ủng hộ riêng", raw);
            Assert.DoesNotContain(world.Categories[HiddenCategory].ToString(), raw);
            Assert.DoesNotContain(world.Categories["Thu khác"].ToString(), raw);
            Assert.DoesNotContain("createdByName", raw);
            Assert.DoesNotContain("\"note\"", raw);
            Assert.DoesNotContain("\"isPublic\"", raw);
        }
    }

    [Fact]
    public async Task Month_without_transactions_returns_zero_totals_and_carried_balance()
    {
        var world = await WorldAsync();
        await AddAsync(world, "Income", 650_000, "Đóng quỹ", "Đầu năm", "2026-09-05");

        var report = await MonthlyAsync(world.Student, world, 2026, 11);
        Assert.Equal(650_000, Long(report, "openingBalance"));
        Assert.Equal(0, Long(report, "totalIncome"));
        Assert.Equal(0, Long(report, "totalExpense"));
        Assert.Equal(0, Long(report, "netChange"));
        Assert.Equal(650_000, Long(report, "closingBalance"));
        Assert.Equal(0, report.GetProperty("byCategory").GetArrayLength());
        Assert.Equal(0, report.GetProperty("transactions").GetArrayLength());
    }

    // ---------- GET /reports/yearly ----------

    [Fact]
    public async Task Yearly_report_always_has_twelve_chained_months_with_totals_only_and_same_for_every_role()
    {
        var world = await WorldAsync();
        await AddAsync(world, "Income", 1_000_000, "Đóng quỹ", "Năm trước", "2025-12-20");
        await AddAsync(world, "Income", 4_800_000, "Đóng quỹ", "Tháng 9", "2026-09-05");
        await AddAsync(world, "Expense", 2_100_000, "Sinh hoạt, liên hoan", "Tháng 9", "2026-09-20", isPublic: false);
        await AddAsync(world, "Expense", 4_750_000, "Sinh hoạt, liên hoan", "Tháng 10", "2026-10-20");
        await AddAsync(world, "Income", 4_500_000, "Đóng quỹ", "Tháng 10", "2026-10-02");
        var cancelled = await AddAsync(world, "Income", 999_000, "Thu khác", "Hủy", "2026-10-03");
        await CancelAsync(world, cancelled);

        var yearly = await YearlyAsync(world.Teacher, world, 2026);
        Assert.Equal(2026, yearly.GetProperty("year").GetInt32());
        Assert.Equal(["year", "months"], yearly.EnumerateObject().Select(property => property.Name));
        var months = yearly.GetProperty("months").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, 12), months.Select(month => month.GetProperty("month").GetInt32()));
        Assert.All(months, month => Assert.Equal(
            ["month", "openingBalance", "totalIncome", "totalExpense", "netChange", "closingBalance"],
            month.EnumerateObject().Select(property => property.Name)));

        Assert.Equal(1_000_000, Long(months[0], "openingBalance"));
        Assert.Equal(1_000_000, Long(months[7], "closingBalance"));
        Assert.Equal(4_800_000, Long(months[8], "totalIncome"));
        Assert.Equal(2_100_000, Long(months[8], "totalExpense"));
        Assert.Equal(2_700_000, Long(months[8], "netChange"));
        Assert.Equal(3_700_000, Long(months[8], "closingBalance"));
        Assert.Equal(4_500_000, Long(months[9], "totalIncome"));
        Assert.Equal(-250_000, Long(months[9], "netChange"));
        Assert.Equal(3_450_000, Long(months[11], "closingBalance"));
        for (var index = 1; index < months.Length; index++)
        {
            Assert.Equal(Long(months[index - 1], "closingBalance"), Long(months[index], "openingBalance"));
        }

        foreach (var viewer in new[] { world.Treasurer, world.Student, world.Parent })
        {
            Assert.Equal(yearly.GetRawText(), (await YearlyAsync(viewer, world, 2026)).GetRawText());
        }

        // Năm chưa có giao dịch vẫn đủ 12 tháng.
        var empty = await YearlyAsync(world.Parent, world, 2024);
        Assert.Equal(12, empty.GetProperty("months").GetArrayLength());
        Assert.All(empty.GetProperty("months").EnumerateArray(), month => Assert.Equal(0, Long(month, "closingBalance")));
    }

    // ---------- Lỗi ----------

    [Fact]
    public async Task Missing_or_out_of_range_parameters_return_validation_errors()
    {
        var world = await WorldAsync();
        var url = $"/funds/{world.FundId}/reports";

        var missing = await (await world.Teacher.GetAsync($"{url}/monthly")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(missing.GetProperty("errors").TryGetProperty("year", out _));
        Assert.True(missing.GetProperty("errors").TryGetProperty("month", out _));

        var outOfRange = await (await world.Teacher.GetAsync($"{url}/monthly?year=0&month=13"))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(outOfRange.GetProperty("errors").TryGetProperty("year", out _));
        Assert.True(outOfRange.GetProperty("errors").TryGetProperty("month", out _));

        await (await world.Teacher.GetAsync($"{url}/yearly")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await world.Teacher.GetAsync($"{url}/yearly?year=10000")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(12, (await YearlyAsync(world.Teacher, world, 9999)).GetProperty("months").GetArrayLength());
        Assert.Equal(12, (await MonthlyAsync(world.Teacher, world, 9999, 12)).GetProperty("month").GetInt32());
    }

    [Fact]
    public async Task Member_of_another_fund_gets_404_and_anonymous_gets_401()
    {
        var world = await WorldAsync();
        var other = await WorldAsync();
        await AddAsync(world, "Income", 100_000, "Đóng quỹ", "Quỹ A", "2026-11-02");

        foreach (var path in new[] { "monthly?year=2026&month=11", "yearly?year=2026" })
        {
            await (await other.Treasurer.GetAsync($"/funds/{world.FundId}/reports/{path}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
            await (await other.Parent.GetAsync($"/funds/{world.FundId}/reports/{path}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
            using var anonymous = fixture.Factory.CreateApiClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/funds/{world.FundId}/reports/{path}")).StatusCode);
        }
    }

    // ---------- Helper ----------

    /// <summary>GVCN tạo quỹ bằng API (6 danh mục mặc định) + thủ quỹ, học sinh chưa phân công, phụ huynh (đều đã kích hoạt).</summary>
    private async Task<World> WorldAsync()
    {
        var teacher = await _data.CreateTeacherAsync("Cô Hạnh");
        var teacherClient = await SignInAsync(teacher.UserId);
        var created = await ReadAsync(await teacherClient.PostAsync("/funds", new { name = TestData.Unique("Quỹ lớp ") }), HttpStatusCode.Created);
        var fundId = created.GetProperty("id").GetGuid();
        var fund = new SeededFund(fundId, "Quỹ", teacher.UserId);
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Nguyễn Minh Khang", treasurer: true);
        var student = await _data.CreateStudentAccountAsync(fund, "Lê Gia Bảo", activated: true);
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);
        await ActivateAsync(parent.UserId);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var categories = await db.Categories.AsNoTracking().Where(category => category.FundId == fundId)
            .ToDictionaryAsync(category => category.Name, category => category.Id);
        return new World(
            fundId,
            fund,
            teacherClient,
            await SignInAsync(treasurer.UserId),
            await SignInAsync(student.UserId),
            await SignInAsync(parent.UserId),
            categories);
    }

    private async Task<ApiClient> SignInAsync(Guid userId)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.SignInAsAsync(userId);
        return client;
    }

    private static async Task<Guid> AddAsync(
        World world,
        string type,
        long amount,
        string category,
        string description,
        string day,
        bool isPublic = true,
        string? note = null)
    {
        var created = await ReadAsync(
            await world.Treasurer.PostAsync($"/funds/{world.FundId}/transactions", new
            {
                type,
                amount,
                occurredOn = day,
                categoryId = world.Categories[category],
                description,
                note,
                isPublic,
                idempotencyKey = Guid.NewGuid(),
            }),
            HttpStatusCode.Created);
        return created.GetProperty("id").GetGuid();
    }

    private static async Task CancelAsync(World world, Guid id) =>
        await ReadAsync(
            await world.Treasurer.PostAsync($"/funds/{world.FundId}/transactions/{id}/cancel", new { reason = "Nhập nhầm" }),
            HttpStatusCode.OK);

    private static async Task<JsonElement> MonthlyAsync(ApiClient client, World world, int year, int month) =>
        await ReadAsync(await client.GetAsync($"/funds/{world.FundId}/reports/monthly?year={year}&month={month}"), HttpStatusCode.OK);

    private static async Task<JsonElement> YearlyAsync(ApiClient client, World world, int year) =>
        await ReadAsync(await client.GetAsync($"/funds/{world.FundId}/reports/yearly?year={year}"), HttpStatusCode.OK);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Mong đợi {(int)expected}, nhận {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static long Long(JsonElement element, string name) => element.GetProperty(name).GetInt64();

    private static long SumLines(JsonElement report, string type) => report.GetProperty("byCategory")
        .EnumerateArray()
        .Where(line => line.GetProperty("type").GetString() == type)
        .Sum(line => Long(line, "amount"));

    /// <summary>Phụ huynh đã đổi mật khẩu tạm.</summary>
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
