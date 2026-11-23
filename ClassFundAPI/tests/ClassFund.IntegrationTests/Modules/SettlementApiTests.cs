using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>
/// M9-T4 trên PostgreSQL thật: quyết toán sự kiện chia đều tiền thừa theo đầu người trong danh sách chốt (làm tròn xuống bội 1.000đ),
/// cộng số dư / hoàn trực tiếp, chỉ quyết toán một lần kể cả khi hai thủ quỹ bấm cùng lúc, không chạm quỹ chung.
/// </summary>
[Collection(ApiCollection.Name)]
public class SettlementApiTests(ApiTestHost host)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static string Base(Guid fundId) => $"/api/v1/funds/{fundId}";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    private sealed record World(Fund Fund, User Owner, User A, User B, HttpClient OwnerClient, HttpClient AClient, HttpClient BClient, Guid EventId, Guid ChargeId);

    /// <summary>Sự kiện còn mở: A và B đã tham gia; khoản thu 100.000 đ/người; owner đã tạo, chưa ai nộp.</summary>
    private async Task<World> OpenEventAsync()
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

        var category = await ReadAsync(await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/categories", new { name = "Quỹ lớp", type = "Income" }));
        var created = await ReadAsync(await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/events", new { name = "Dã ngoại Ba Vì", eventDate = Today.AddDays(20).ToString("yyyy-MM-dd"), listDeadline = Today.ToString("yyyy-MM-dd") }));
        var eventId = created.GetProperty("id").GetGuid();
        foreach (var client in new[] { aClient, bClient })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Base(fund.Id)}/events/{eventId}/me/response", new { response = "Accepted" })).StatusCode);
        }

        var charge = await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/events/{eventId}/charges", new
        {
            categoryId = category.GetProperty("id").GetGuid(),
            title = "Tiền xe",
            purpose = (string?)null,
            amountPerPerson = 100_000,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Created, charge.StatusCode);
        return new World(fund, owner, a, b, ownerClient, aClient, bClient, eventId, (await ReadAsync(charge)).GetProperty("id").GetGuid());
    }

    /// <summary>Thủ quỹ tích "đã thu" tiền mặt cho người dùng của <paramref name="client"/>.</summary>
    private static async Task<Guid> CollectCashAsync(HttpClient manager, HttpClient client, Guid fundId)
    {
        var mine = (await ReadAsync(await client.GetAsync($"{Base(fundId)}/me/assignments"))).EnumerateArray().Single();
        var paid = await manager.PostAsJsonAsync($"{Base(fundId)}/assignments/{mine.GetProperty("assignmentId").GetGuid()}/collect", new { method = "Cash" });
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        return (await ReadAsync(paid)).GetProperty("id").GetGuid();
    }

    private async Task SeedExpenseAsync(World w, long amount)
    {
        await using var db = host.CreateDbContext();
        db.LedgerEntries.Add(new LedgerEntry(w.Fund.Id, w.EventId, LedgerType.Expense, LedgerSource.Manual, null, "Thuê xe", null, amount,
            DateOnly.FromDateTime(DateTime.UtcNow), w.Owner.Id, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    /// <summary>A đóng 100.000, B đóng 100.000 (đã xác nhận), chi <paramref name="spent"/>, rồi đóng danh sách.</summary>
    private async Task<World> CompletedEventAsync(long spent)
    {
        var w = await OpenEventAsync();
        foreach (var client in new[] { w.AClient, w.BClient })
        {
            await CollectCashAsync(w.OwnerClient, client, w.Fund.Id);
        }

        Assert.Equal(HttpStatusCode.OK, (await w.OwnerClient.PostAsJsonAsync($"{Base(w.Fund.Id)}/events/{w.EventId}/close-list", new { })).StatusCode);
        await SeedExpenseAsync(w, spent);
        return w;
    }

    private static string Url(World w, string rest = "") => $"{Base(w.Fund.Id)}/events/{w.EventId}/settlement{rest}";

    [Fact]
    public async Task Cannot_settle_an_open_event_and_students_are_forbidden()
    {
        var w = await OpenEventAsync();

        var early = await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, early.StatusCode);
        Assert.Equal("settlement.event_not_completed", await CodeOfAsync(early));
        Assert.Equal(HttpStatusCode.Forbidden, (await w.AClient.GetAsync(Url(w, "/preview"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.AClient.PostAsJsonAsync(Url(w), new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anonymous().GetAsync(Url(w))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.OwnerClient.GetAsync(Url(w))).StatusCode);
    }

    [Fact]
    public async Task Surplus_is_split_equally_credited_to_balance_and_the_event_is_settled()
    {
        var w = await CompletedEventAsync(spent: 120_000); // thu 200.000, thừa 80.000, mỗi người 40.000

        var preview = await ReadAsync(await w.OwnerClient.GetAsync(Url(w, "/preview")));
        Assert.Equal(80_000, preview.GetProperty("surplus").GetInt64());
        Assert.True(preview.GetProperty("canExecute").GetBoolean());

        var missing = await w.OwnerClient.PostAsJsonAsync(Url(w), new { });
        Assert.Equal("settlement.method_required", await CodeOfAsync(missing));

        var done = await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" });
        Assert.Equal(HttpStatusCode.Created, done.StatusCode);
        var result = await ReadAsync(done);
        Assert.Equal(2, result.GetProperty("lines").GetArrayLength());
        Assert.All(result.GetProperty("lines").EnumerateArray(), line => Assert.Equal(40_000, line.GetProperty("amount").GetInt64()));

        var mine = await ReadAsync(await w.AClient.GetAsync(Url(w)));
        Assert.Equal(1, mine.GetProperty("lines").GetArrayLength());
        var balance = await ReadAsync(await w.AClient.GetAsync($"{Base(w.Fund.Id)}/me/balance"));
        Assert.Equal(40_000, balance.GetProperty("balance").GetInt64());

        await using var db = host.CreateDbContext();
        Assert.Equal(EventStatus.Settled, (await db.Events.SingleAsync(item => item.Id == w.EventId)).Status);
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == w.Fund.Id && log.Action == "settlement.execute" && log.Scope == FinancialScope.Event));
        Assert.False(await db.LedgerEntries.AnyAsync(entry => entry.FundId == w.Fund.Id && entry.EventId == null));
        Assert.Equal("settlement.already_settled", await CodeOfAsync(await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" })));
    }

    [Fact]
    public async Task Direct_refund_is_confirmed_line_by_line_and_writes_a_refund_expense()
    {
        var w = await CompletedEventAsync(spent: 100_000); // thừa 100.000, mỗi người 50.000

        var result = await ReadAsync(await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "DirectRefund" }));
        var lineId = result.GetProperty("lines")[0].GetProperty("id").GetGuid();
        Assert.All(result.GetProperty("lines").EnumerateArray(), line => Assert.Equal("PendingRefund", line.GetProperty("status").GetString()));

        Assert.Equal(HttpStatusCode.Forbidden, (await w.AClient.PostAsync(Url(w, $"/lines/{lineId}/refunded"), null)).StatusCode);
        var refunded = await w.OwnerClient.PostAsync(Url(w, $"/lines/{lineId}/refunded"), null);
        Assert.Equal(HttpStatusCode.OK, refunded.StatusCode);
        Assert.Equal("Refunded", (await ReadAsync(refunded)).GetProperty("status").GetString());
        Assert.Equal("settlement.line_processed", await CodeOfAsync(await w.OwnerClient.PostAsync(Url(w, $"/lines/{lineId}/refunded"), null)));

        await using var db = host.CreateDbContext();
        var refund = await db.LedgerEntries.SingleAsync(entry => entry.FundId == w.Fund.Id && entry.Source == LedgerSource.Refund);
        Assert.Equal((LedgerType.Expense, 50_000L, (Guid?)w.EventId), (refund.Type, refund.Amount, refund.EventId));
        Assert.False(await db.PersonalBalances.AnyAsync(item => item.FundId == w.Fund.Id));
    }

    [Fact]
    public async Task Equal_split_rounds_down_to_thousands_and_leaves_the_remainder_unallocated()
    {
        var w = await CompletedEventAsync(spent: 99_500); // thu 200.000, thừa 100.500, mỗi người 50.250 → 50.000, dư 500đ không chia

        var done = await w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" });

        Assert.Equal(HttpStatusCode.Created, done.StatusCode);
        var lines = (await ReadAsync(done)).GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Equal(50_000, line.GetProperty("amount").GetInt64()));
    }

    [Fact]
    public async Task Two_treasurers_settling_at_once_settle_only_once_and_credit_only_once()
    {
        var w = await CompletedEventAsync(spent: 120_000);
        var second = await host.LoginAsync(w.Owner);

        var results = await Task.WhenAll(
            w.OwnerClient.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" }),
            second.PostAsJsonAsync(Url(w), new { surplusMethod = "PersonalBalance" }));

        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var db = host.CreateDbContext();
        Assert.Equal(1, await db.Settlements.CountAsync(item => item.EventId == w.EventId));
        Assert.Equal(80_000, await db.PersonalBalances.Where(item => item.FundId == w.Fund.Id).SumAsync(item => item.Balance));
    }
}
