using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolFund.Domain.Rules;
using SchoolFund.IntegrationTests.Infrastructure;

namespace SchoolFund.IntegrationTests.Reports;

/// <summary>Tổng quan, báo cáo, thành viên qua HTTP thật và PostgreSQL – FR-01, FR-08, FR-09, FR-12, BR-02, BR-10, BR-15, P1, NFR-08.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReportTests(PostgresFixture fixture)
{
    private const string NewPassword = "Matkhau-456";
    private const string SecretDescription = "Quà cho cô";
    private const string SecretNote = "Hoa và phong bì";
    private const string HiddenCategory = "Khen thưởng";

    private readonly TestData _data = fixture.Data;

    private sealed record World(
        SeededFund Fund,
        ApiClient Teacher,
        ApiClient Treasurer,
        ApiClient Student,
        ApiClient Parent,
        Guid Income,
        Guid Expense,
        Guid HiddenExpense,
        string TeacherEmail);

    private async Task<ApiClient> SignInAsync(SeededAccount account)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.LoginAsync(account.Login, account.Password);
        var changed = await client.PostAsync("/auth/change-password", new { currentPassword = account.Password, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        return client;
    }

    private async Task<World> SeedAsync()
    {
        var teacher = fixture.Factory.CreateApiClient();
        var password = "Matkhau123";
        var email = TestData.NewEmail();
        var session = await (await teacher.PostAsync("/auth/register-teacher", new
        {
            fullName = "Cô Hạnh",
            email,
            password,
            confirmPassword = password,
        })).ReadSessionAsync(HttpStatusCode.Created);

        var fund = await _data.CreateFundAsync(session.User.Id);
        var treasurer = await SignInAsync(await _data.CreateStudentAccountAsync(fund, "Minh Khang", treasurer: true));
        var studentAccount = await _data.CreateStudentAccountAsync(fund, "Bảo Anh");
        var student = await SignInAsync(studentAccount);
        var parent = await SignInAsync(await _data.CreateParentAccountAsync(fund, studentAccount.StudentId!.Value));

        return new World(
            fund,
            teacher,
            treasurer,
            student,
            parent,
            await CreateCategoryAsync(treasurer, fund, "Income", "Đóng quỹ"),
            await CreateCategoryAsync(treasurer, fund, "Expense", "Văn phòng phẩm"),
            await CreateCategoryAsync(treasurer, fund, "Expense", HiddenCategory),
            email);
    }

    private static async Task<Guid> CreateCategoryAsync(ApiClient client, SeededFund fund, string type, string name)
    {
        var response = await client.PostAsync($"/funds/{fund.Id}/categories", new { type, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Mong đợi {(int)status}, nhận {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<Guid> AddAsync(
        World world,
        string type,
        Guid categoryId,
        long amount,
        string day,
        bool isPublic = true,
        string description = "Nội dung",
        string? note = null)
    {
        var created = await JsonAsync(
            await world.Treasurer.PostAsync($"/funds/{world.Fund.Id}/transactions", new
            {
                type,
                amount,
                occurredOn = day,
                categoryId,
                description,
                note,
                isPublic,
                idempotencyKey = Guid.NewGuid(),
            }),
            HttpStatusCode.Created);
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> MonthlyAsync(ApiClient client, World world, int year, int month) =>
        await JsonAsync(await client.GetAsync($"/funds/{world.Fund.Id}/reports/monthly?year={year}&month={month}"));

    private static long Long(JsonElement element, string name) => element.GetProperty(name).GetInt64();

    private static long SumLines(JsonElement report, string type) => report.GetProperty("byCategory")
        .EnumerateArray()
        .Where(line => line.GetProperty("type").GetString() == type)
        .Sum(line => Long(line, "amount"));

    [Fact]
    public async Task Next_opening_balance_equals_previous_closing_even_after_editing_and_cancelling()
    {
        var world = await SeedAsync();
        var oldIncome = await AddAsync(world, "Income", world.Income, 300_000, "2026-08-10");
        var edited = await AddAsync(world, "Income", world.Income, 100_000, "2026-09-10");
        await AddAsync(world, "Expense", world.Expense, 40_000, "2026-10-10");

        var before = await MonthlyAsync(world.Teacher, world, 2026, 10);
        Assert.Equal(400_000, Long(before, "openingBalance"));
        Assert.Equal(360_000, Long(before, "closingBalance"));
        Assert.Equal(-40_000, Long(before, "netChange"));

        var cancelled = await world.Treasurer.PostAsync(
            $"/funds/{world.Fund.Id}/transactions/{oldIncome}/cancel",
            new { reason = "Nhập nhầm" });
        await JsonAsync(cancelled);
        var updated = await world.Treasurer.PutAsync($"/funds/{world.Fund.Id}/transactions/{edited}", new
        {
            amount = 150_000,
            occurredOn = "2026-09-10",
            categoryId = world.Income,
            description = "Nội dung",
            isPublic = true,
        });
        await JsonAsync(updated);

        var august = await MonthlyAsync(world.Teacher, world, 2026, 8);
        var september = await MonthlyAsync(world.Teacher, world, 2026, 9);
        var october = await MonthlyAsync(world.Teacher, world, 2026, 10);
        var overview = await JsonAsync(await world.Teacher.GetAsync($"/funds/{world.Fund.Id}"));

        Assert.Equal(0, Long(august, "totalIncome"));
        Assert.Equal(Long(august, "closingBalance"), Long(september, "openingBalance"));
        Assert.Equal(Long(september, "closingBalance"), Long(october, "openingBalance"));
        Assert.Equal(150_000, Long(october, "openingBalance"));
        Assert.Equal(110_000, Long(october, "closingBalance"));
        Assert.Equal(110_000, Long(overview, "currentBalance"));
    }

    [Fact]
    public async Task Parent_and_student_get_other_lines_that_add_up_without_leaking_details()
    {
        var world = await SeedAsync();
        await AddAsync(world, "Income", world.Income, 100_000, "2026-10-05");
        await AddAsync(world, "Income", world.Income, 20_000, "2026-10-06", isPublic: false);
        await AddAsync(world, "Expense", world.Expense, 15_000, "2026-10-08");
        await AddAsync(world, "Expense", world.HiddenExpense, 5_000, "2026-10-09", isPublic: false, SecretDescription, SecretNote);
        await AddAsync(world, "Expense", world.HiddenExpense, 7_000, "2026-10-10", isPublic: false);

        var full = await MonthlyAsync(world.Treasurer, world, 2026, 10);
        Assert.Equal(3, full.GetProperty("byCategory").GetArrayLength());
        Assert.Equal(5, full.GetProperty("transactions").GetArrayLength());
        Assert.Contains(HiddenCategory, full.GetRawText());

        foreach (var viewer in new[] { world.Parent, world.Student })
        {
            var response = await viewer.GetAsync($"/funds/{world.Fund.Id}/reports/monthly?year=2026&month=10");
            var raw = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var report = JsonDocument.Parse(raw).RootElement;

            var others = report.GetProperty("byCategory")
                .EnumerateArray()
                .Where(line => line.GetProperty("categoryName").GetString() == "Khoản khác")
                .ToArray();
            Assert.Equal(2, others.Length);
            Assert.All(others, line =>
            {
                Assert.Equal(JsonValueKind.Null, line.GetProperty("categoryId").ValueKind);
                Assert.Equal(JsonValueKind.Null, line.GetProperty("count").ValueKind);
            });
            Assert.Equal(20_000, Long(others.Single(line => line.GetProperty("type").GetString() == "Income"), "amount"));
            Assert.Equal(12_000, Long(others.Single(line => line.GetProperty("type").GetString() == "Expense"), "amount"));

            Assert.Equal(Long(full, "totalIncome"), Long(report, "totalIncome"));
            Assert.Equal(Long(full, "totalExpense"), Long(report, "totalExpense"));
            Assert.Equal(Long(full, "closingBalance"), Long(report, "closingBalance"));
            Assert.Equal(Long(report, "totalIncome"), SumLines(report, "Income"));
            Assert.Equal(Long(report, "totalExpense"), SumLines(report, "Expense"));

            Assert.Equal(2, report.GetProperty("transactions").GetArrayLength());
            Assert.DoesNotContain(HiddenCategory, raw);
            Assert.DoesNotContain(SecretDescription, raw);
            Assert.DoesNotContain(SecretNote, raw);
            Assert.DoesNotContain(world.HiddenExpense.ToString(), raw);
            Assert.DoesNotContain("createdByName", raw);
            Assert.DoesNotContain("\"note\"", raw);
        }
    }

    [Fact]
    public async Task Overview_returns_totals_current_month_managers_and_scoped_recent_transactions()
    {
        var world = await SeedAsync();
        var today = VietnamTime.DateOf(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        await AddAsync(world, "Income", world.Income, 500_000, "2026-01-15");
        await AddAsync(world, "Expense", world.Expense, 30_000, today);
        await AddAsync(world, "Income", world.Income, 80_000, today, isPublic: false, SecretDescription, SecretNote);

        var full = await JsonAsync(await world.Teacher.GetAsync($"/funds/{world.Fund.Id}"));
        Assert.Equal("Teacher", full.GetProperty("role").GetString());
        Assert.Equal("Assigned", full.GetProperty("treasurerStatus").GetString());
        Assert.Equal(550_000, Long(full, "currentBalance"));
        Assert.Equal(580_000, Long(full, "totalIncome"));
        Assert.Equal(30_000, Long(full, "totalExpense"));
        var month = full.GetProperty("currentMonth");
        Assert.Equal(80_000, Long(month, "income"));
        Assert.Equal(30_000, Long(month, "expense"));
        Assert.Equal(50_000, Long(month, "net"));
        Assert.Equal(3, full.GetProperty("recentTransactions").GetArrayLength());

        foreach (var viewer in new[] { world.Parent, world.Student })
        {
            var response = await viewer.GetAsync($"/funds/{world.Fund.Id}");
            var raw = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var overview = JsonDocument.Parse(raw).RootElement;

            Assert.Equal(Long(full, "currentBalance"), Long(overview, "currentBalance"));
            Assert.Equal(Long(month, "net"), Long(overview.GetProperty("currentMonth"), "net"));
            Assert.Equal(2, overview.GetProperty("recentTransactions").GetArrayLength());

            var managers = overview.GetProperty("managers").EnumerateArray().ToArray();
            Assert.Equal(["Cô Hạnh", "Minh Khang"], managers.Select(item => item.GetProperty("fullName").GetString()));
            Assert.Equal(["Teacher", "Treasurer"], managers.Select(item => item.GetProperty("role").GetString()));
            Assert.All(managers, item => Assert.Equal(
                ["fullName", "role"],
                item.EnumerateObject().Select(property => property.Name)));

            Assert.DoesNotContain(SecretDescription, raw);
            Assert.DoesNotContain(SecretNote, raw);
            Assert.DoesNotContain(world.TeacherEmail, raw);
        }
    }

    [Fact]
    public async Task Yearly_report_chains_twelve_months_and_contains_only_totals()
    {
        var world = await SeedAsync();
        await AddAsync(world, "Income", world.Income, 500_000, "2025-12-31");
        await AddAsync(world, "Income", world.Income, 200_000, "2026-03-15");
        await AddAsync(world, "Expense", world.HiddenExpense, 80_000, "2026-03-31", isPublic: false, SecretDescription, SecretNote);

        var response = await world.Parent.GetAsync($"/funds/{world.Fund.Id}/reports/yearly?year=2026");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var months = JsonDocument.Parse(raw).RootElement.GetProperty("months").EnumerateArray().ToArray();
        Assert.Equal(12, months.Length);
        Assert.Equal(500_000, Long(months[0], "openingBalance"));
        Assert.Equal(200_000, Long(months[2], "totalIncome"));
        Assert.Equal(80_000, Long(months[2], "totalExpense"));
        Assert.Equal(620_000, Long(months[11], "closingBalance"));
        for (var index = 1; index < months.Length; index++)
        {
            Assert.Equal(Long(months[index - 1], "closingBalance"), Long(months[index], "openingBalance"));
        }

        Assert.All(months, item => Assert.Equal(
            ["month", "openingBalance", "totalIncome", "totalExpense", "netChange", "closingBalance"],
            item.EnumerateObject().Select(property => property.Name)));
        Assert.DoesNotContain(HiddenCategory, raw);
        Assert.DoesNotContain(SecretDescription, raw);
    }

    [Fact]
    public async Task Members_returns_only_names_and_roles_to_teacher_and_treasurer()
    {
        var world = await SeedAsync();
        var path = $"/funds/{world.Fund.Id}/members";

        foreach (var viewer in new[] { world.Teacher, world.Treasurer })
        {
            var response = await viewer.GetAsync(path);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var members = JsonDocument.Parse(raw).RootElement.EnumerateArray().ToArray();
            Assert.Equal(["Cô Hạnh", "Minh Khang", "Bảo Anh"], members.Select(item => item.GetProperty("fullName").GetString()));
            Assert.Equal(["Teacher", "Treasurer", "Student"], members.Select(item => item.GetProperty("role").GetString()));
            Assert.All(members, item => Assert.Equal(
                ["fullName", "role"],
                item.EnumerateObject().Select(property => property.Name)));
            Assert.DoesNotContain(world.TeacherEmail, raw);
            Assert.DoesNotContain("hs.", raw);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Student.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Parent.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/members")]
    [InlineData("/reports/monthly?year=2026&month=10")]
    [InlineData("/reports/yearly?year=2026")]
    public async Task Outsider_gets_404_and_anonymous_gets_401(string rest)
    {
        var world = await SeedAsync();
        var otherFund = await _data.CreateFundAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await world.Teacher.GetAsync($"/funds/{otherFund.Id}{rest}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Factory.CreateApiClient().GetAsync($"/funds/{world.Fund.Id}{rest}")).StatusCode);
    }

    [Theory]
    [InlineData("/reports/monthly")]
    [InlineData("/reports/monthly?year=2026")]
    [InlineData("/reports/monthly?year=2026&month=13")]
    [InlineData("/reports/monthly?year=abc&month=1")]
    [InlineData("/reports/yearly")]
    [InlineData("/reports/yearly?year=0")]
    public async Task Invalid_period_returns_400(string rest)
    {
        var world = await SeedAsync();

        var problem = await JsonAsync(await world.Teacher.GetAsync($"/funds/{world.Fund.Id}{rest}"), HttpStatusCode.BadRequest);

        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(9999, 12)]
    public async Task Extreme_periods_do_not_fail(int year, int month)
    {
        var world = await SeedAsync();

        var report = await MonthlyAsync(world.Teacher, world, year, month);
        var yearly = await JsonAsync(await world.Teacher.GetAsync($"/funds/{world.Fund.Id}/reports/yearly?year={year}"));

        Assert.Equal(0, Long(report, "closingBalance"));
        Assert.Equal(12, yearly.GetProperty("months").GetArrayLength());
    }
}
