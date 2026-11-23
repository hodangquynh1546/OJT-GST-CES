using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>
/// M14-T4 trên PostgreSQL thật: tổng kết cuối năm một lần (kể cả khi hai thủ quỹ bấm cùng lúc), chia cho cả lớp theo tỷ lệ đã đóng,
/// quỹ → Archived chặn mọi ghi mới nhưng vẫn rút được số dư và xác nhận hoàn tiền (BR-37, TC-M14-05).
/// </summary>
[Collection(ApiCollection.Name)]
public class YearEndSettlementApiTests(ApiTestHost host)
{
    private static string Base(Guid fundId) => $"/api/v1/funds/{fundId}";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    private sealed record World(Fund Fund, User Owner, User A, User B, HttpClient OwnerClient, HttpClient AClient, HttpClient BClient, Guid IncomeCategoryId, Guid ExpenseCategoryId);

    /// <summary>A đóng 100.000, B đóng 100.000 (đã xác nhận) vào một khoản thu chung; chi 120.000 → thừa 80.000, mỗi người 40.000.</summary>
    private async Task<World> YearWithSurplusAsync()
    {
        var owner = await host.CreateUserAsync();
        var a = await host.CreateUserAsync(UserOrigin.Provisioned);
        var b = await host.CreateUserAsync(UserOrigin.Provisioned);
        var fund = await host.CreateFundAsync(owner);
        await host.AddMemberAsync(fund, a);
        await host.AddMemberAsync(fund, b);
        var ownerClient = await host.LoginAsync(owner);
        var aClient = await host.LoginAsync(a);
        var bClient = await host.LoginAsync(b);

        var income = await ReadAsync(await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/categories", new { name = "Quỹ lớp", type = "Income" }));
        var expense = await ReadAsync(await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/categories", new { name = "Văn phòng phẩm", type = "Expense" }));
        var created = await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/charges", new
        {
            categoryId = income.GetProperty("id").GetGuid(),
            title = "Quỹ lớp HK1",
            purpose = (string?)null,
            amountPerPerson = 100_000,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        foreach (var client in new[] { aClient, bClient })
        {
            var mine = (await ReadAsync(await client.GetAsync($"{Base(fund.Id)}/me/assignments"))).EnumerateArray().Single();
            var paid = await ReadAsync(await client.PostAsJsonAsync($"{Base(fund.Id)}/me/assignments/{mine.GetProperty("assignmentId").GetGuid()}/payments", new { method = "Cash" }));
            Assert.Equal(HttpStatusCode.OK, (await ownerClient.PostAsync($"{Base(fund.Id)}/payments/{paid.GetProperty("id").GetGuid()}/confirm", null)).StatusCode);
        }

        var spent = await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/ledger-entries", new
        {
            eventId = (Guid?)null,
            categoryId = expense.GetProperty("id").GetGuid(),
            title = "Mua dụng cụ",
            purpose = (string?)null,
            amount = 120_000,
            transactionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd"),
            attachmentIds = Array.Empty<Guid>(),
        });
        Assert.Equal(HttpStatusCode.Created, spent.StatusCode);
        return new World(fund, owner, a, b, ownerClient, aClient, bClient, income.GetProperty("id").GetGuid(), expense.GetProperty("id").GetGuid());
    }

    private static string Url(World w, string rest = "") => $"{Base(w.Fund.Id)}/year-end-settlement{rest}";

    [Fact]
    public async Task Permissions_and_not_found_before_the_year_end()
    {
        var w = await YearWithSurplusAsync();
        var stranger = await host.LoginAsync(await host.CreateUserAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anonymous().GetAsync(Url(w))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Url(w))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.AClient.GetAsync(Url(w, "/preview"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.AClient.PostAsJsonAsync(Url(w), new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.OwnerClient.GetAsync(Url(w))).StatusCode);
    }

    [Fact]
    public async Task Surplus_is_split_across_the_class_credited_and_the_fund_becomes_archived()
    {
        var w = await YearWithSurplusAsync();

        var preview = await ReadAsync(await w.OwnerClient.GetAsync(Url(w, "/preview")));
        Assert.Equal(80_000, preview.GetProperty("surplus").GetInt64());
        Assert.True(preview.GetProperty("canExecute").GetBoolean());
        Assert.Equal(3, preview.GetProperty("memberCount").GetInt32());

        Assert.Equal("settlement.method_required", await CodeOfAsync(await w.OwnerClient.PostAsJsonAsync(Url(w), new { })));
        var done = await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" });
        Assert.Equal(HttpStatusCode.Created, done.StatusCode);
        var lines = (await ReadAsync(done)).GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Equal(40_000, line.GetProperty("amount").GetInt64()));

        var mine = await ReadAsync(await w.AClient.GetAsync(Url(w)));
        Assert.Equal(1, mine.GetProperty("lines").GetArrayLength());
        Assert.Equal(40_000, (await ReadAsync(await w.AClient.GetAsync($"{Base(w.Fund.Id)}/me/balance"))).GetProperty("balance").GetInt64());

        await using var db = host.CreateDbContext();
        Assert.Equal(FundStatus.Archived, (await db.Funds.SingleAsync(item => item.Id == w.Fund.Id)).Status);
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == w.Fund.Id && log.Action == "year_end.settle" && log.Scope == FinancialScope.General));
        Assert.Equal("year_end.already_closed", await CodeOfAsync(await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" })));
    }

    [Fact]
    public async Task An_unsettled_event_blocks_the_year_end()
    {
        var w = await YearWithSurplusAsync();
        Assert.Equal(HttpStatusCode.Created, (await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/events", new { name = "Dã ngoại", eventDate = "2026-12-20" })).StatusCode);

        var blocked = await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Equal("year_end.open_events", await CodeOfAsync(blocked));
        await using var db = host.CreateDbContext();
        Assert.Equal(FundStatus.Active, (await db.Funds.SingleAsync(item => item.Id == w.Fund.Id)).Status);
    }

    [Fact]
    public async Task After_the_year_end_new_writes_are_refused_but_withdrawals_and_refunds_still_work()
    {
        var w = await YearWithSurplusAsync();
        var settled = await ReadAsync(await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "DirectRefund" }));
        var lineId = settled.GetProperty("lines")[0].GetProperty("id").GetGuid();

        Guid memberId;
        await using (var lookup = host.CreateDbContext())
        {
            memberId = (await lookup.FundMembers.SingleAsync(item => item.FundId == w.Fund.Id && item.UserId == w.A.Id)).Id;
        }

        var attempts = new Dictionary<string, HttpResponseMessage>
        {
            ["charge"] = await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/charges", new { categoryId = w.IncomeCategoryId, title = "Mới", amountPerPerson = 10_000, dueDate = "2099-01-01" }),
            ["expense"] = await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/ledger-entries", new { eventId = (Guid?)null, categoryId = w.ExpenseCategoryId, title = "Chi", amount = 1_000, transactionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd"), attachmentIds = Array.Empty<Guid>() }),
            ["event"] = await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/events", new { name = "Mới", eventDate = "2099-01-01" }),
            ["category"] = await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/categories", new { name = "Mới", type = "Income" }),
            ["member"] = await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/members", new { email = "new.student@example.com", fullName = "Học Sinh Mới" }),
            ["treasurer"] = await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/treasurers", new { memberId }),
        };
        foreach (var (name, response) in attempts)
        {
            Assert.True(response.StatusCode is HttpStatusCode.UnprocessableEntity, $"{name}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            Assert.Equal("fund.archived", await CodeOfAsync(response));
        }

        var refunded = await w.OwnerClient.PostAsync(Url(w, $"/lines/{lineId}/refunded"), null);
        Assert.Equal(HttpStatusCode.OK, refunded.StatusCode);
        Assert.Equal("settlement.line_processed", await CodeOfAsync(await w.OwnerClient.PostAsync(Url(w, $"/lines/{lineId}/refunded"), null)));

        // TC-M14-05: học sinh còn số dư gửi yêu cầu rút, thủ quỹ xác nhận đã trả.
        await using (var db = host.CreateDbContext())
        {
            db.PersonalBalances.Add(new PersonalBalance(w.Fund.Id, w.A.Id) { Balance = 30_000 });
            await db.SaveChangesAsync();
        }

        var requested = await w.AClient.PostAsync($"{Base(w.Fund.Id)}/me/withdrawal-requests", null);
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var requestId = (await ReadAsync(await w.OwnerClient.GetAsync($"{Base(w.Fund.Id)}/withdrawal-requests"))).GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await w.OwnerClient.PostAsync($"{Base(w.Fund.Id)}/withdrawal-requests/{requestId}/paid", null)).StatusCode);
    }

    [Fact]
    public async Task Two_treasurers_closing_the_year_at_once_close_it_only_once()
    {
        var w = await YearWithSurplusAsync();
        var second = await host.LoginAsync(w.Owner);

        var results = await Task.WhenAll(
            w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" }),
            second.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" }));

        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.UnprocessableEntity);
        await using var db = host.CreateDbContext();
        Assert.Equal(1, await db.Settlements.CountAsync(item => item.FundId == w.Fund.Id && item.EventId == null));
        Assert.Equal(80_000, await db.PersonalBalances.Where(item => item.FundId == w.Fund.Id).SumAsync(item => item.Balance));
    }
}
