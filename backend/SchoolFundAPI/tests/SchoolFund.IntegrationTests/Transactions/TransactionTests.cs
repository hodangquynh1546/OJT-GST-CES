using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolFund.Infrastructure.Persistence;
using SchoolFund.IntegrationTests.Infrastructure;

namespace SchoolFund.IntegrationTests.Transactions;

/// <summary>Sổ thu chi qua HTTP thật (đăng nhập, cookie, antiforgery, PostgreSQL) – FR-02 → FR-07, BR-12, BR-14, D-18, NFR-08, NFR-09.</summary>
[Collection(ApiCollection.Name)]
public sealed class TransactionTests(PostgresFixture fixture)
{
    private const string NewPassword = "Matkhau-456";
    private const string Day = "2026-11-01";
    private const string SecretNote = "Hoa và phong bì";

    private readonly TestData _data = fixture.Data;

    private sealed record World(
        SeededFund Fund,
        ApiClient Teacher,
        ApiClient Treasurer,
        ApiClient SecondTreasurer,
        ApiClient Student,
        ApiClient Parent,
        Guid IncomeId,
        Guid ExpenseId);

    private static string Url(SeededFund fund, string rest = "") => $"/funds/{fund.Id}/transactions{rest}";

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
        var session = await (await teacher.PostAsync("/auth/register-teacher", new
        {
            fullName = "Cô Hạnh",
            email = TestData.NewEmail(),
            password,
            confirmPassword = password,
        })).ReadSessionAsync(HttpStatusCode.Created);

        var fund = await _data.CreateFundAsync(session.User.Id);
        var treasurer = await SignInAsync(await _data.CreateStudentAccountAsync(fund, "Minh Khang", treasurer: true));
        var second = await SignInAsync(await _data.CreateStudentAccountAsync(fund, "Thu Hà", treasurer: true));
        var studentAccount = await _data.CreateStudentAccountAsync(fund);
        var student = await SignInAsync(studentAccount);
        var parent = await SignInAsync(await _data.CreateParentAccountAsync(fund, studentAccount.StudentId!.Value));

        var income = await CreateCategoryAsync(treasurer, fund, "Income", "Đóng quỹ");
        var expense = await CreateCategoryAsync(treasurer, fund, "Expense", "Khen thưởng");
        return new World(fund, teacher, treasurer, second, student, parent, income, expense);
    }

    private static async Task<Guid> CreateCategoryAsync(ApiClient client, SeededFund fund, string type, string name)
    {
        var response = await client.PostAsync($"/funds/{fund.Id}/categories", new { type, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object Body(
        Guid categoryId,
        string type = "Income",
        long amount = 100_000,
        string description = "Thu tháng 11",
        string? note = null,
        bool? isPublic = null,
        Guid? key = null) =>
        new { type, amount, occurredOn = Day, categoryId, description, note, isPublic, idempotencyKey = key ?? Guid.NewGuid() };

    private static object Edit(Guid categoryId, long amount = 100_000, string description = "Thu tháng 11", string? note = null, bool isPublic = true) =>
        new { amount, occurredOn = Day, categoryId, description, note, isPublic };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Mong đợi {(int)status}, nhận {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<JsonElement> CreateAsync(World world, object body) =>
        await JsonAsync(await world.Treasurer.PostAsync(Url(world.Fund), body), HttpStatusCode.Created);

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();

    private async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<HashSet<Guid>> IdsAsync(ApiClient client, SeededFund fund, string query = "")
    {
        var page = await JsonAsync(await client.GetAsync(Url(fund, query)), HttpStatusCode.OK);
        return page.GetProperty("items").EnumerateArray().Select(Id).ToHashSet();
    }

    [Fact]
    public async Task Create_saves_public_by_default_with_audit_and_replays_the_same_key()
    {
        var world = await SeedAsync();
        var key = Guid.NewGuid();

        var created = await CreateAsync(world, Body(world.IncomeId, key: key));

        Assert.False(created.GetProperty("replayed").GetBoolean());
        Assert.True(created.GetProperty("isPublic").GetBoolean());
        Assert.Equal("Minh Khang", created.GetProperty("createdByName").GetString());

        var replay = await JsonAsync(
            await world.Treasurer.PostAsync(Url(world.Fund), Body(world.IncomeId, amount: 999, key: key)),
            HttpStatusCode.OK);

        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(Id(created), Id(replay));
        Assert.Equal(100_000, replay.GetProperty("amount").GetInt64());
        Assert.Equal(1, await ReadAsync(db => db.FundTransactions.CountAsync(item => item.FundId == world.Fund.Id)));
        var entityId = Id(created).ToString();
        Assert.Equal(1, await ReadAsync(db => db.AuditLogs.CountAsync(log => log.EntityId == entityId && log.Action == "Income.Create")));
    }

    [Fact]
    public async Task Five_concurrent_saves_with_one_key_keep_one_row()
    {
        var world = await SeedAsync();
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            world.Treasurer.PostAsync(Url(world.Fund), Body(world.IncomeId, key: key))));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(4, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, await ReadAsync(db => db.FundTransactions.CountAsync(item => item.FundId == world.Fund.Id)));
    }

    [Fact]
    public async Task Expense_over_balance_is_saved_with_warning_and_private_flag_false_is_kept()
    {
        var world = await SeedAsync();

        var created = await CreateAsync(world, Body(world.ExpenseId, "Expense", isPublic: false));

        Assert.True(created.GetProperty("isNegativeBalance").GetBoolean());
        Assert.False(created.GetProperty("isPublic").GetBoolean());
    }

    [Fact]
    public async Task Rules_are_enforced_at_the_api()
    {
        var world = await SeedAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1)).ToString("yyyy-MM-dd");

        await (await world.Treasurer.PostAsync(Url(world.Fund), new { type = "Income", amount = 1, occurredOn = tomorrow, categoryId = world.IncomeId, description = "A", idempotencyKey = Guid.NewGuid() }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.future_date");
        await (await world.Treasurer.PostAsync(Url(world.Fund), Body(world.ExpenseId)))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.category_invalid");
        await (await world.Treasurer.PostAsync(Url(world.Fund), Body(world.IncomeId, amount: 0)))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await world.Treasurer.PostAsync(Url(world.Fund), new { type = "Income", amount = 1, occurredOn = Day, categoryId = world.IncomeId, description = "A" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        await world.Treasurer.PutAsync($"/funds/{world.Fund.Id}/categories/{world.IncomeId}", new { name = "Đóng quỹ", isHidden = true });
        await (await world.Treasurer.PostAsync(Url(world.Fund), Body(world.IncomeId)))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.category_invalid");
    }

    [Fact]
    public async Task Update_records_the_second_treasurer_and_never_changes_type()
    {
        var world = await SeedAsync();
        var created = await CreateAsync(world, Body(world.IncomeId));
        var url = Url(world.Fund, $"/{Id(created)}");

        var updated = await JsonAsync(
            await world.SecondTreasurer.PutAsync(url, new
            {
                type = "Expense",
                amount = 250_000,
                occurredOn = Day,
                categoryId = world.IncomeId,
                description = "Đã sửa",
                note = "Ghi chú",
                isPublic = false,
            }),
            HttpStatusCode.OK);

        Assert.Equal("Income", updated.GetProperty("type").GetString());
        Assert.Equal(250_000, updated.GetProperty("amount").GetInt64());
        Assert.Equal("Thu Hà", updated.GetProperty("updatedByName").GetString());

        await (await world.Treasurer.PutAsync(url, Edit(world.ExpenseId)))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.category_invalid");
    }

    [Theory]
    [InlineData("""{ "reason": "" }""")]
    [InlineData("""{ "reason": "    " }""")]
    [InlineData("""{ }""")]
    public async Task Cancel_requires_a_reason(string json)
    {
        var world = await SeedAsync();
        var created = await CreateAsync(world, Body(world.IncomeId));

        var response = await world.Treasurer.PostAsync(Url(world.Fund, $"/{Id(created)}/cancel"), JsonDocument.Parse(json).RootElement);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var id = Id(created);
        Assert.False(await ReadAsync(db => db.FundTransactions.Where(item => item.Id == id).Select(item => item.IsCancelled).SingleAsync()));
    }

    [Fact]
    public async Task Cancel_stores_who_and_why_and_blocks_further_changes()
    {
        var world = await SeedAsync();
        var created = await CreateAsync(world, Body(world.IncomeId));
        var url = Url(world.Fund, $"/{Id(created)}");

        var cancelled = await JsonAsync(
            await world.SecondTreasurer.PostAsync($"{url}/cancel", new { reason = "Nhập nhầm" }),
            HttpStatusCode.OK);

        Assert.True(cancelled.GetProperty("isCancelled").GetBoolean());
        Assert.Equal("Nhập nhầm", cancelled.GetProperty("cancelReason").GetString());
        Assert.Equal("Thu Hà", cancelled.GetProperty("cancelledByName").GetString());
        Assert.False(cancelled.GetProperty("isNegativeBalance").GetBoolean());

        await (await world.Treasurer.PostAsync($"{url}/cancel", new { reason = "Lần hai" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.cancelled");
        await (await world.Treasurer.PutAsync(url, Edit(world.IncomeId, amount: 5)))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.cancelled");
    }

    [Fact]
    public async Task Two_treasurers_cancelling_at_once_leave_one_cancel()
    {
        var world = await SeedAsync();
        var created = await CreateAsync(world, Body(world.IncomeId));
        var url = Url(world.Fund, $"/{Id(created)}/cancel");

        var responses = await Task.WhenAll(
            world.Treasurer.PostAsync(url, new { reason = "Người thứ nhất" }),
            world.SecondTreasurer.PostAsync(url, new { reason = "Người thứ hai" }));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.UnprocessableEntity));
        var entityId = Id(created).ToString();
        Assert.Equal(1, await ReadAsync(db => db.AuditLogs.CountAsync(log => log.EntityId == entityId && log.Action == "Income.Cancel")));
    }

    [Fact]
    public async Task Edit_and_cancel_at_once_never_change_a_cancelled_transaction()
    {
        var world = await SeedAsync();
        var created = await CreateAsync(world, Body(world.IncomeId));
        var url = Url(world.Fund, $"/{Id(created)}");

        var responses = await Task.WhenAll(
            world.Treasurer.PutAsync(url, Edit(world.IncomeId, amount: 777)),
            world.SecondTreasurer.PostAsync($"{url}/cancel", new { reason = "Nhập nhầm" }));

        Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
        var id = Id(created);
        var amount = await ReadAsync(db => db.FundTransactions.Where(item => item.Id == id).Select(item => item.Amount).SingleAsync());
        Assert.Equal(responses[0].StatusCode == HttpStatusCode.OK ? 777 : 100_000, amount);
    }

    [Fact]
    public async Task Filters_search_and_paging_run_on_real_sql()
    {
        var world = await SeedAsync();
        var first = await CreateAsync(world, Body(world.IncomeId, description: "Mua PHONG BÌ"));
        var second = await CreateAsync(world, Body(world.IncomeId, description: "Khác", note: "phong bì 100%"));
        var expense = await CreateAsync(world, Body(world.ExpenseId, "Expense"));
        await world.Treasurer.PostAsync(Url(world.Fund, $"/{Id(expense)}/cancel"), new { reason = "Nhập nhầm" });

        Assert.Equal(new HashSet<Guid> { Id(first), Id(second) }, await IdsAsync(world.Treasurer, world.Fund));
        Assert.Equal(new HashSet<Guid> { Id(expense) }, await IdsAsync(world.Treasurer, world.Fund, "?status=Cancelled"));
        Assert.Equal(3, (await IdsAsync(world.Treasurer, world.Fund, "?status=All")).Count);
        Assert.Equal(new HashSet<Guid> { Id(expense) }, await IdsAsync(world.Treasurer, world.Fund, $"?status=All&type=Expense&categoryId={world.ExpenseId}"));
        Assert.Equal(new HashSet<Guid> { Id(first), Id(second) }, await IdsAsync(world.Treasurer, world.Fund, "?search=phong%20b%C3%AC"));
        Assert.Equal(new HashSet<Guid> { Id(second) }, await IdsAsync(world.Treasurer, world.Fund, "?search=100%25"));
        Assert.Empty(await IdsAsync(world.Treasurer, world.Fund, $"?to=2026-10-31"));

        var page = await JsonAsync(await world.Teacher.GetAsync(Url(world.Fund, "?status=All&page=2&pageSize=2")), HttpStatusCode.OK);
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());

        await (await world.Treasurer.GetAsync(Url(world.Fund, "?pageSize=101"))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await world.Treasurer.GetAsync(Url(world.Fund, "?status=Other"))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Only_treasurer_writes_and_outsiders_get_not_found()
    {
        var world = await SeedAsync();
        var created = await CreateAsync(world, Body(world.IncomeId));
        var url = Url(world.Fund, $"/{Id(created)}");

        foreach (var client in new[] { world.Teacher, world.Student, world.Parent })
        {
            await (await client.PostAsync(Url(world.Fund), Body(world.IncomeId))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PutAsync(url, Edit(world.IncomeId))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PostAsync($"{url}/cancel", new { reason = "A" })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        var otherFund = await _data.CreateFundAsync();
        await (await world.Treasurer.GetAsync(Url(otherFund))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        using var anonymous = fixture.Factory.CreateApiClient();
        await (await anonymous.GetAsync(Url(world.Fund))).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Public_viewers_never_receive_notes_or_private_transactions()
    {
        var world = await SeedAsync();
        var shown = await CreateAsync(world, Body(world.IncomeId, description: "Công khai", note: SecretNote));
        var hidden = await CreateAsync(world, Body(world.ExpenseId, "Expense", description: "Khoản riêng tư", note: SecretNote, isPublic: false));
        var cancelled = await CreateAsync(world, Body(world.IncomeId, description: "Đã hủy công khai"));
        await world.Treasurer.PostAsync(Url(world.Fund, $"/{Id(cancelled)}/cancel"), new { reason = "Lý do nội bộ" });
        var hiddenCancelled = await CreateAsync(world, Body(world.IncomeId, description: "Hủy riêng tư", isPublic: false));
        await world.Treasurer.PostAsync(Url(world.Fund, $"/{Id(hiddenCancelled)}/cancel"), new { reason = "Lý do nội bộ" });

        foreach (var viewer in new[] { world.Parent, world.Student })
        {
            var raw = await (await viewer.GetAsync(Url(world.Fund, "?status=All&pageSize=1"))).Content.ReadAsStringAsync();
            var page = JsonDocument.Parse(raw).RootElement;
            Assert.Equal(2, page.GetProperty("total").GetInt32());

            var all = await (await viewer.GetAsync(Url(world.Fund, "?status=All"))).Content.ReadAsStringAsync();
            foreach (var secret in new[] { SecretNote, "Khoản riêng tư", "Hủy riêng tư", "Lý do nội bộ", "Minh Khang", "note", "isPublic", "cancelReason", "createdByName", "updatedByName", "cancelledByName", "createdAt", "updatedAt" })
            {
                Assert.DoesNotContain(secret, all);
            }

            Assert.Equal(new HashSet<Guid> { Id(shown), Id(cancelled) }, await IdsAsync(viewer, world.Fund, "?status=All"));
            Assert.Empty(await IdsAsync(viewer, world.Fund, "?status=All&search=phong"));
            Assert.Empty(await IdsAsync(viewer, world.Fund, $"?status=All&categoryId={world.ExpenseId}"));

            var cancelledRow = await JsonAsync(await viewer.GetAsync(Url(world.Fund, $"/{Id(cancelled)}")), HttpStatusCode.OK);
            Assert.True(cancelledRow.GetProperty("isCancelled").GetBoolean());
            Assert.True(cancelledRow.TryGetProperty("cancelledOn", out _));
            Assert.False(cancelledRow.TryGetProperty("cancelReason", out _));
            Assert.False(cancelledRow.TryGetProperty("cancelledAt", out _));

            var privateDetail = await (await viewer.GetAsync(Url(world.Fund, $"/{Id(hidden)}"))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
            var missingDetail = await (await viewer.GetAsync(Url(world.Fund, $"/{Guid.NewGuid()}"))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
            Assert.Equal(missingDetail.GetProperty("title").GetString(), privateDetail.GetProperty("title").GetString());

            await (await viewer.GetAsync(Url(world.Fund, $"/{Id(shown)}/history"))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }
    }

    [Fact]
    public async Task History_shows_each_change_to_treasurer_and_teacher()
    {
        var world = await SeedAsync();
        var created = await CreateAsync(world, Body(world.IncomeId));
        var url = Url(world.Fund, $"/{Id(created)}");
        await world.SecondTreasurer.PutAsync(url, Edit(world.IncomeId, amount: 200_000));
        await world.Treasurer.PostAsync($"{url}/cancel", new { reason = "Nhập nhầm" });

        var history = await JsonAsync(await world.Teacher.GetAsync($"{url}/history"), HttpStatusCode.OK);

        var rows = history.EnumerateArray().ToArray();
        Assert.Equal(["Income.Create", "Income.Update", "Income.Cancel"], rows.Select(row => row.GetProperty("action").GetString()));
        Assert.Equal(["Minh Khang", "Thu Hà", "Minh Khang"], rows.Select(row => row.GetProperty("actorName").GetString()));
        Assert.Equal(100_000, rows[1].GetProperty("oldValues").GetProperty("amount").GetInt64());
        Assert.Equal(200_000, rows[1].GetProperty("newValues").GetProperty("amount").GetInt64());
    }
}
