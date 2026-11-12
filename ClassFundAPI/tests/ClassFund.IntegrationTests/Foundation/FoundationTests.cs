using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Foundation;

/// <summary>
/// Hợp đồng nền mọi module dùng chung (docs/api/api-conventions.md): định dạng lỗi, mã trạng thái,
/// xác thực, antiforgery, phân quyền theo quỹ, JSON (enum, ngày, tiền), phân trang.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FoundationTests(PostgresFixture fixture)
{
    private readonly TestData _data = fixture.Data;

    private ApiClient NewClient() => fixture.Factory.CreateApiClient();

    [Fact]
    public async Task Health_is_anonymous_and_checks_database()
    {
        using var client = NewClient();
        var response = await fixture.Factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_returns_problem_404_not_spa()
    {
        using var client = NewClient();
        var problem = await (await client.GetAsync("/khong-ton-tai")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task Correlation_id_from_request_is_echoed()
    {
        using var http = fixture.Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test-probe");
        request.Headers.Add("X-Correlation-Id", "probe-correlation-1");
        var response = await http.SendAsync(request);
        Assert.Equal("probe-correlation-1", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Endpoints_require_sign_in_by_default()
    {
        using var client = NewClient();
        await (await client.GetAsync("/test-probe")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Seeded_teacher_without_fund_signs_in_but_sees_no_fund()
    {
        var teacher = await _data.CreateTeacherAsync();
        var otherFund = await _data.CreateFundAsync();

        var user = await _data.FindUserAsync(teacher.UserId);
        Assert.Equal(ClassFund.Domain.Enums.UserKind.Teacher, user.Kind);
        Assert.Equal(teacher.Login, user.Email);
        Assert.False(user.MustChangePassword);

        using var client = NewClient();
        await client.SignInAsAsync(teacher.UserId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test-probe")).StatusCode);
        await (await client.GetAsync($"/test-probe/funds/{otherFund.Id}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Write_without_antiforgery_header_is_rejected()
    {
        var fund = await _data.CreateFundAsync();
        using var client = NewClient();
        await client.SignInAsAsync(fund.OwnerUserId);
        await (await client.PostWithoutAntiforgeryAsync("/test-probe/echo", new { }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "antiforgery_failed");
    }

    [Fact]
    public async Task Fund_route_returns_404_when_not_member_and_200_when_permitted()
    {
        var fund = await _data.CreateFundAsync();
        var other = await _data.CreateFundAsync();

        using var client = NewClient();
        await client.SignInAsAsync(fund.OwnerUserId);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/test-probe/funds/{fund.Id}")).StatusCode);
        await (await client.GetAsync($"/test-probe/funds/{other.Id}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Temporary_password_blocks_everything_except_allowed_endpoints()
    {
        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund);

        using var client = NewClient();
        await client.SignInAsAsync(student.UserId);
        await (await client.GetAsync("/test-probe")).AssertProblemAsync(HttpStatusCode.Forbidden, "password.change_required");
    }

    [Fact]
    public async Task Seeded_student_states_match_their_account_status()
    {
        var fund = await _data.CreateFundAsync();
        var pending = await _data.CreateStudentAccountAsync(fund, "Chờ kích hoạt");
        var active = await _data.CreateStudentAccountAsync(fund, "Đang hoạt động", activated: true);
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Thủ quỹ", treasurer: true);
        var none = await _data.CreateStudentAsync(fund);

        Assert.Equal(StudentAccountStatus.PendingActivation, StudentAccount.StatusOf(await _data.FindUserAsync(pending.UserId)));
        Assert.Equal(StudentAccountStatus.Active, StudentAccount.StatusOf(await _data.FindUserAsync(active.UserId)));
        Assert.Equal(StudentAccountStatus.Active, StudentAccount.StatusOf(await _data.FindUserAsync(treasurer.UserId)));

        await _data.DisableUserAsync(active.UserId);
        Assert.Equal(StudentAccountStatus.Disabled, StudentAccount.StatusOf(await _data.FindUserAsync(active.UserId)));
        Assert.NotEqual(Guid.Empty, none.StudentId);

        // Học sinh đã kích hoạt (thủ quỹ) không bị chặn đổi mật khẩu tạm.
        using var activeClient = NewClient();
        await activeClient.SignInAsAsync(treasurer.UserId);
        Assert.Equal(HttpStatusCode.OK, (await activeClient.GetAsync("/test-probe")).StatusCode);
    }

    [Fact]
    public async Task Seeded_categories_and_transactions_have_the_requested_state()
    {
        var fund = await _data.CreateFundAsync();
        var defaults = await _data.SeedDefaultCategoriesAsync(fund);
        var hidden = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Photo tài liệu", isHidden: true);
        var income = defaults["Đóng quỹ"];
        var open = await _data.CreateTransactionAsync(fund, income, 200_000);
        var cancelled = await _data.CreateTransactionAsync(fund, hidden, 50_000, isPublic: false, cancelled: true);

        Assert.Equal(6, defaults.Count);
        Assert.Equal(TransactionType.Income, income.Type);
        Assert.True(hidden.IsHidden);
        Assert.Equal(TransactionType.Income, open.Type);
        Assert.True(open.IsPublic);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(7, await db.Categories.CountAsync(category => category.FundId == fund.Id));
        Assert.True((await db.Categories.SingleAsync(category => category.Id == hidden.Id)).IsHidden);
        var stored = await db.FundTransactions.AsNoTracking().Where(transaction => transaction.FundId == fund.Id).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.False(stored.Single(transaction => transaction.Id == open.Id).IsCancelled);
        var cancelledRow = stored.Single(transaction => transaction.Id == cancelled.Id);
        Assert.True(cancelledRow.IsCancelled);
        Assert.False(cancelledRow.IsPublic);
        Assert.Equal(fund.OwnerUserId, cancelledRow.CancelledBy);
    }

    [Fact]
    public async Task Disabled_account_session_is_treated_as_signed_out()
    {
        var fund = await _data.CreateFundAsync();
        using var client = NewClient();
        await client.SignInAsAsync(fund.OwnerUserId);
        await _data.DisableUserAsync(fund.OwnerUserId);

        await (await client.GetAsync("/test-probe")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Theory]
    [InlineData("rule", HttpStatusCode.UnprocessableEntity, "probe.rule")]
    [InlineData("conflict", HttpStatusCode.Conflict, "probe.conflict")]
    [InlineData("not-found", HttpStatusCode.NotFound, "not_found")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "forbidden")]
    [InlineData("crash", HttpStatusCode.InternalServerError, "internal_error")]
    public async Task Exceptions_map_to_status_and_code(string kind, HttpStatusCode status, string code)
    {
        var fund = await _data.CreateFundAsync();
        using var client = NewClient();
        await client.SignInAsAsync(fund.OwnerUserId);

        var response = await client.GetAsync($"/test-probe/errors/{kind}");
        var body = await response.Content.ReadAsStringAsync();
        await response.AssertProblemAsync(status, code);
        Assert.DoesNotContain("secret-detail-should-not-leak", body);
    }

    [Fact]
    public async Task Json_uses_string_enums_iso_dates_and_integer_money()
    {
        var fund = await _data.CreateFundAsync();
        using var client = NewClient();
        await client.SignInAsAsync(fund.OwnerUserId);

        var response = await client.PostAsync("/test-probe/echo", new
        {
            type = "Expense",
            occurredOn = "2026-11-09",
            amount = 1_250_000L,
            at = "2026-11-09T03:15:00Z",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Expense", json.GetProperty("type").GetString());
        Assert.Equal("2026-11-09", json.GetProperty("occurredOn").GetString());
        Assert.Equal(1_250_000L, json.GetProperty("amount").GetInt64());
        Assert.Equal("2026-11-09T03:15:00+00:00", json.GetProperty("at").GetString());

        // Enum dạng số và JSON sai → 400 validation_failed, không lộ lỗi parser.
        var numeric = await client.PostAsync("/test-probe/echo", new { type = 1, occurredOn = "2026-11-09", amount = 1, at = "2026-11-09T00:00:00Z" });
        var problem = await numeric.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("", 1, 20, HttpStatusCode.OK)]
    [InlineData("?page=2&pageSize=100", 2, 100, HttpStatusCode.OK)]
    [InlineData("?page=0", 0, 0, HttpStatusCode.BadRequest)]
    [InlineData("?pageSize=101", 0, 0, HttpStatusCode.BadRequest)]
    public async Task Paging_defaults_and_limits(string query, int page, int pageSize, HttpStatusCode status)
    {
        var fund = await _data.CreateFundAsync();
        using var client = NewClient();
        await client.SignInAsAsync(fund.OwnerUserId);

        var response = await client.GetAsync("/test-probe/page" + query);
        if (status == HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(page, json.GetProperty("page").GetInt32());
            Assert.Equal(pageSize, json.GetProperty("pageSize").GetInt32());
        }
        else
        {
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        }
    }
}
