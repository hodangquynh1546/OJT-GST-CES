using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>
/// M6b-T4 trên PostgreSQL thật: khoản thu sự kiện chỉ gán người đã nhận tham gia (BR-18), đóng bằng số dư
/// (khoản chung và sự kiện) trừ đúng một lần, không âm khi đóng đồng thời, bị khóa khi đang chờ rút (BR-26).
/// </summary>
[Collection(ApiCollection.Name)]
public class EventChargesAndBalancePaymentsApiTests(ApiTestHost host)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private static string Base(Guid fundId) => $"/api/v1/funds/{fundId}";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    private static object NewCharge(Guid categoryId, string title, long amount) => new
    {
        categoryId,
        title,
        purpose = (string?)null,
        amountPerPerson = amount,
        dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
    };

    private sealed record Setup(Fund Fund, Guid CategoryId, User Owner, User Going, User Staying, HttpClient OwnerClient, HttpClient GoingClient, HttpClient StayingClient);

    private async Task<Setup> SetupAsync()
    {
        var owner = await host.CreateUserAsync();
        var going = await host.CreateUserAsync(UserOrigin.Provisioned);
        var staying = await host.CreateUserAsync(UserOrigin.Provisioned);
        var fund = await host.CreateFundAsync(owner);
        await host.AddMemberAsync(fund, going);
        await host.AddMemberAsync(fund, staying);
        var ownerClient = await host.LoginAsync(owner);
        var category = await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/categories", new { name = "Quỹ lớp", type = "Income" });
        Assert.Equal(HttpStatusCode.Created, category.StatusCode);
        return new Setup(fund, (await ReadAsync(category)).GetProperty("id").GetGuid(), owner, going, staying,
            ownerClient, await host.LoginAsync(going), await host.LoginAsync(staying));
    }

    private async Task SeedBalanceAsync(Fund fund, User user, long amount)
    {
        await using var db = host.CreateDbContext();
        db.PersonalBalances.Add(new PersonalBalance(fund.Id, user.Id) { Balance = amount });
        db.BalanceTransactions.Add(new BalanceTransaction(fund.Id, user.Id, BalanceTransactionType.SettlementCredit, amount, amount, null, fund.CreatedByUserId, Now));
        await db.SaveChangesAsync();
    }

    private async Task<long> BalanceOfAsync(Fund fund, User user)
    {
        await using var db = host.CreateDbContext();
        return (await db.PersonalBalances.SingleOrDefaultAsync(item => item.FundId == fund.Id && item.UserId == user.Id))?.Balance ?? 0;
    }

    private static async Task<Guid> AssignmentIdAsync(HttpClient client, Guid fundId, string title)
    {
        var items = await ReadAsync(await client.GetAsync($"{Base(fundId)}/me/assignments"));
        return items.EnumerateArray().Single(item => item.GetProperty("title").GetString() == title).GetProperty("assignmentId").GetGuid();
    }

    private static Task<HttpResponseMessage> PayFromBalanceAsync(HttpClient client, Guid fundId, Guid assignmentId) =>
        client.PostAsJsonAsync($"{Base(fundId)}/me/assignments/{assignmentId}/payments", new { method = "PersonalBalance" });

    private async Task<Guid> CreateEventWithAnswersAsync(Setup s)
    {
        var created = await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/events", new { name = "Dã ngoại Ba Vì", eventDate = "2026-12-20" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var eventId = (await ReadAsync(created)).GetProperty("id").GetGuid();
        var yes = await s.GoingClient.PutAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/me/response", new { response = "Accepted" });
        var no = await s.StayingClient.PutAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/me/response", new { response = "Declined" });
        Assert.Equal(HttpStatusCode.OK, yes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, no.StatusCode);
        return eventId;
    }

    [Fact]
    public async Task Event_charge_goes_only_to_accepted_people_and_can_be_paid_from_balance()
    {
        var s = await SetupAsync();
        var eventId = await CreateEventWithAnswersAsync(s);
        var eventUrl = $"{Base(s.Fund.Id)}/events/{eventId}";

        Assert.Equal(HttpStatusCode.Forbidden, (await s.GoingClient.PostAsJsonAsync($"{eventUrl}/charges", NewCharge(s.CategoryId, "Tiền xe", 70_000))).StatusCode);
        var created = await s.OwnerClient.PostAsJsonAsync($"{eventUrl}/charges", NewCharge(s.CategoryId, "Tiền xe", 70_000));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var chargeId = (await ReadAsync(created)).GetProperty("id").GetGuid();

        var goingMine = await ReadAsync(await s.GoingClient.GetAsync($"{Base(s.Fund.Id)}/me/assignments"));
        var item = Assert.Single(goingMine.EnumerateArray());
        Assert.Equal(eventId, item.GetProperty("eventId").GetGuid());
        Assert.Equal("Dã ngoại Ba Vì", item.GetProperty("eventName").GetString());
        Assert.Empty((await ReadAsync(await s.StayingClient.GetAsync($"{Base(s.Fund.Id)}/me/assignments"))).EnumerateArray());

        var listed = await s.OwnerClient.GetAsync($"{eventUrl}/charges/{chargeId}/assignments");
        Assert.True(listed.IsSuccessStatusCode, await listed.Content.ReadAsStringAsync());
        var assignments = await ReadAsync(listed);
        Assert.Equal(1, assignments.GetProperty("total").GetInt32());

        var assignmentId = item.GetProperty("assignmentId").GetGuid();
        await SeedBalanceAsync(s.Fund, s.Going, 100_000);
        var paid = await PayFromBalanceAsync(s.GoingClient, s.Fund.Id, assignmentId);
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        var payment = await ReadAsync(paid);
        Assert.Equal("Confirmed", payment.GetProperty("status").GetString());
        Assert.Equal("PersonalBalance", payment.GetProperty("method").GetString());
        Assert.Equal(30_000, await BalanceOfAsync(s.Fund, s.Going));

        await using var db = host.CreateDbContext();
        var entry = await db.LedgerEntries.SingleAsync(item => item.FundId == s.Fund.Id);
        Assert.Equal(eventId, entry.EventId);
        Assert.Equal(70_000, entry.Amount);
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == s.Fund.Id && log.Action == "payment.pay_with_balance" && log.Scope == FinancialScope.Event && log.EventId == eventId));
        var debit = await db.BalanceTransactions.SingleAsync(item => item.FundId == s.Fund.Id && item.Type == BalanceTransactionType.ChargePayment);
        Assert.Equal((-70_000L, 30_000L, payment.GetProperty("id").GetGuid()), (debit.Amount, debit.BalanceAfter, debit.ReferenceId));

        Assert.Equal("payment.not_payable", await CodeOfAsync(await PayFromBalanceAsync(s.GoingClient, s.Fund.Id, assignmentId)));
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerClient.PostAsync($"{eventUrl}/charges/{chargeId}/close", null)).StatusCode);
    }

    [Fact]
    public async Task Balance_payment_needs_enough_money_and_is_locked_while_a_withdrawal_is_pending()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/charges", NewCharge(s.CategoryId, "Quỹ HK1", 50_000))).StatusCode);
        var assignmentId = await AssignmentIdAsync(s.GoingClient, s.Fund.Id, "Quỹ HK1");

        await SeedBalanceAsync(s.Fund, s.Going, 49_999);
        var tooLittle = await PayFromBalanceAsync(s.GoingClient, s.Fund.Id, assignmentId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLittle.StatusCode);
        Assert.Equal("balance.insufficient", await CodeOfAsync(tooLittle));
        Assert.Equal(49_999, await BalanceOfAsync(s.Fund, s.Going));

        Assert.Equal(HttpStatusCode.Created, (await s.GoingClient.PostAsync($"{Base(s.Fund.Id)}/me/withdrawal-requests", null)).StatusCode);
        var locked = await PayFromBalanceAsync(s.GoingClient, s.Fund.Id, assignmentId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, locked.StatusCode);
        Assert.Equal("balance.locked_by_withdrawal", await CodeOfAsync(locked));
        Assert.Equal(49_999, await BalanceOfAsync(s.Fund, s.Going));

        await using var db = host.CreateDbContext();
        Assert.False(await db.Payments.AnyAsync(payment => payment.PayerUserId == s.Going.Id));
        Assert.False(await db.LedgerEntries.AnyAsync(entry => entry.FundId == s.Fund.Id));
    }

    [Fact]
    public async Task Two_balance_payments_at_once_never_overdraw_the_balance()
    {
        var s = await SetupAsync();
        foreach (var title in new[] { "Quỹ HK1", "Quỹ HK2" })
        {
            Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/charges", NewCharge(s.CategoryId, title, 50_000))).StatusCode);
        }

        await SeedBalanceAsync(s.Fund, s.Going, 60_000);
        var first = await AssignmentIdAsync(s.GoingClient, s.Fund.Id, "Quỹ HK1");
        var second = await AssignmentIdAsync(s.GoingClient, s.Fund.Id, "Quỹ HK2");
        var other = await host.LoginAsync(s.Going);

        var results = await Task.WhenAll(
            PayFromBalanceAsync(s.GoingClient, s.Fund.Id, first),
            PayFromBalanceAsync(other, s.Fund.Id, second));

        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        var failed = Assert.Single(results, response => response.StatusCode == HttpStatusCode.UnprocessableEntity);
        Assert.Equal("balance.insufficient", await CodeOfAsync(failed));
        Assert.Equal(10_000, await BalanceOfAsync(s.Fund, s.Going));
        await using var db = host.CreateDbContext();
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.PayerUserId == s.Going.Id));
        Assert.Equal(1, await db.LedgerEntries.CountAsync(entry => entry.FundId == s.Fund.Id));
    }

    [Fact]
    public async Task Short_balance_plus_cash_then_reject_returns_the_balance_and_confirm_writes_the_full_income()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/charges", NewCharge(s.CategoryId, "Quỹ HK1", 50_000))).StatusCode);
        var assignmentId = await AssignmentIdAsync(s.GoingClient, s.Fund.Id, "Quỹ HK1");
        await SeedBalanceAsync(s.Fund, s.Going, 20_000);

        var submitted = await s.GoingClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/me/assignments/{assignmentId}/payments", new { method = "Cash", useBalance = true });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var payment = await ReadAsync(submitted);
        Assert.Equal("Submitted", payment.GetProperty("status").GetString());
        Assert.Equal(20_000, payment.GetProperty("balanceApplied").GetInt64());
        Assert.Equal(0, await BalanceOfAsync(s.Fund, s.Going));
        var paymentId = payment.GetProperty("id").GetGuid();

        var queue = await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.Fund.Id)}/payments"));
        Assert.Equal(20_000, queue.GetProperty("items")[0].GetProperty("balanceApplied").GetInt64());
        Assert.Equal(JsonValueKind.Null, queue.GetProperty("items")[0].GetProperty("eventId").ValueKind);

        Assert.Equal(HttpStatusCode.OK, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/payments/{paymentId}/reject", new { reason = "Chưa nhận được tiền" })).StatusCode);
        Assert.Equal(20_000, await BalanceOfAsync(s.Fund, s.Going));

        var again = await s.GoingClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/me/assignments/{assignmentId}/payments", new { method = "Cash", useBalance = true });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var secondId = (await ReadAsync(again)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerClient.PostAsync($"{Base(s.Fund.Id)}/payments/{secondId}/confirm", null)).StatusCode);

        await using var db = host.CreateDbContext();
        Assert.Equal(50_000, (await db.LedgerEntries.SingleAsync(entry => entry.FundId == s.Fund.Id)).Amount);
        Assert.Equal(0, await BalanceOfAsync(s.Fund, s.Going));
    }

    [Fact]
    public async Task Students_see_expenses_and_only_their_own_income_and_the_detail_names_the_payer()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/charges", NewCharge(s.CategoryId, "Quỹ HK1", 50_000))).StatusCode);
        foreach (var client in new[] { s.GoingClient, s.StayingClient })
        {
            var assignment = await AssignmentIdAsync(client, s.Fund.Id, "Quỹ HK1");
            var paid = await ReadAsync(await client.PostAsJsonAsync($"{Base(s.Fund.Id)}/me/assignments/{assignment}/payments", new { method = "Cash" }));
            Assert.Equal(HttpStatusCode.OK, (await s.OwnerClient.PostAsync($"{Base(s.Fund.Id)}/payments/{paid.GetProperty("id").GetGuid()}/confirm", null)).StatusCode);
        }

        var mine = await ReadAsync(await s.GoingClient.GetAsync($"{Base(s.Fund.Id)}/ledger-entries"));
        var incomeEntry = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal("Income", incomeEntry.GetProperty("type").GetString());
        var detail = await ReadAsync(await s.GoingClient.GetAsync($"{Base(s.Fund.Id)}/ledger-entries/{incomeEntry.GetProperty("id").GetGuid()}"));
        Assert.Equal(s.Going.FullName, detail.GetProperty("payerName").GetString());

        var all = await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.Fund.Id)}/ledger-entries"));
        Assert.Equal(2, all.GetProperty("items").GetArrayLength());
        var otherId = all.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Single(id => id != incomeEntry.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await s.GoingClient.GetAsync($"{Base(s.Fund.Id)}/ledger-entries/{otherId}")).StatusCode);
    }

    [Fact]
    public async Task The_confirmation_queue_names_the_event_of_an_event_payment()
    {
        var s = await SetupAsync();
        var eventId = await CreateEventWithAnswersAsync(s);
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/charges", NewCharge(s.CategoryId, "Tiền xe", 70_000))).StatusCode);
        var assignmentId = await AssignmentIdAsync(s.GoingClient, s.Fund.Id, "Tiền xe");
        var paid = await s.GoingClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/me/assignments/{assignmentId}/payments", new { method = "Cash" });
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);

        var queue = await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.Fund.Id)}/payments"));
        var item = Assert.Single(queue.GetProperty("items").EnumerateArray());
        Assert.Equal(eventId, item.GetProperty("eventId").GetGuid());
        Assert.Equal("Dã ngoại Ba Vì", item.GetProperty("eventName").GetString());
    }

    [Fact]
    public async Task Owner_can_remind_an_unpaid_participant_once_per_hour_and_students_cannot()
    {
        var s = await SetupAsync();
        var eventId = await CreateEventWithAnswersAsync(s);
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/charges", NewCharge(s.CategoryId, "Tiền xe", 70_000))).StatusCode);
        var url = $"{Base(s.Fund.Id)}/events/{eventId}/participants/{s.Going.Id}/remind";

        Assert.Equal(HttpStatusCode.Forbidden, (await s.GoingClient.PostAsync(url, null)).StatusCode);
        var reminded = await s.OwnerClient.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, reminded.StatusCode);
        Assert.Equal(1, (await ReadAsync(reminded)).GetProperty("reminded").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await s.OwnerClient.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OwnerClient.PostAsync($"{Base(s.Fund.Id)}/events/{eventId}/participants/{s.Staying.Id}/remind", null)).StatusCode);

        await using var db = host.CreateDbContext();
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == s.Going.Id && item.Type == NotificationType.EventUnpaidReminder && item.EventId == eventId));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == s.Fund.Id && log.Action == "charge.event_remind" && log.Scope == FinancialScope.Event));
    }
}
