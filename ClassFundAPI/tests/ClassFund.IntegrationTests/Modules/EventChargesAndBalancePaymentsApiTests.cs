using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>
/// Luồng sự kiện theo góp ý mentor trên PostgreSQL thật: khoản thu chỉ gán người đã nhận tham gia (BR-18), thủ quỹ tích "đã thu"
/// (Tiền mặt / Chuyển khoản), đóng danh sách loại người chưa đóng đủ và hoàn tiền đã đóng vào số dư cá nhân (BR-19, BR-23).
/// </summary>
[Collection(ApiCollection.Name)]
public class EventChargesAndBalancePaymentsApiTests(ApiTestHost host)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

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

    private static Task<HttpResponseMessage> CollectAsync(HttpClient client, Guid fundId, Guid assignmentId, string method = "Cash") =>
        client.PostAsJsonAsync($"{Base(fundId)}/assignments/{assignmentId}/collect", new { method });

    /// <summary>Id nghĩa vụ của một người với khoản thu (thủ quỹ xem danh sách của khoản thu sự kiện).</summary>
    private static async Task<Guid> AssignmentOfAsync(HttpClient manager, Guid fundId, Guid eventId, Guid chargeId, Guid userId)
    {
        var page = await ReadAsync(await manager.GetAsync($"{Base(fundId)}/events/{eventId}/charges/{chargeId}/assignments"));
        return page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("userId").GetGuid() == userId).GetProperty("assignmentId").GetGuid();
    }

    private async Task<Guid> CreateEventWithAnswersAsync(Setup s)
    {
        var created = await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/events", new { name = "Dã ngoại Ba Vì", eventDate = Today.AddDays(20).ToString("yyyy-MM-dd"), listDeadline = Today.ToString("yyyy-MM-dd") });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var eventId = (await ReadAsync(created)).GetProperty("id").GetGuid();
        var yes = await s.GoingClient.PutAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/me/response", new { response = "Accepted" });
        var no = await s.StayingClient.PutAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/me/response", new { response = "Declined" });
        Assert.Equal(HttpStatusCode.OK, yes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, no.StatusCode);
        return eventId;
    }

    [Fact]
    public async Task Event_charge_goes_only_to_accepted_people_and_the_treasurer_collects_it_into_the_event_ledger()
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

        var assignmentId = item.GetProperty("assignmentId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await CollectAsync(s.GoingClient, s.Fund.Id, assignmentId)).StatusCode);
        var balanceMethod = await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/assignments/{assignmentId}/collect", new { method = "PersonalBalance" });
        Assert.Equal(HttpStatusCode.BadRequest, balanceMethod.StatusCode);

        var collected = await CollectAsync(s.OwnerClient, s.Fund.Id, assignmentId, "BankTransfer");
        Assert.Equal(HttpStatusCode.Created, collected.StatusCode);
        Assert.Equal("BankTransfer", (await ReadAsync(collected)).GetProperty("method").GetString());
        Assert.Equal("payment.not_payable", await CodeOfAsync(await CollectAsync(s.OwnerClient, s.Fund.Id, assignmentId)));

        await using var db = host.CreateDbContext();
        var entry = await db.LedgerEntries.SingleAsync(entry => entry.FundId == s.Fund.Id);
        Assert.Equal((eventId, 70_000L), (entry.EventId, entry.Amount));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == s.Fund.Id && log.Action == "payment.collect" && log.Scope == FinancialScope.Event && log.EventId == eventId));
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerClient.PostAsync($"{eventUrl}/charges/{chargeId}/close", null)).StatusCode);
    }

    [Fact]
    public async Task Close_list_excludes_the_person_who_paid_only_one_of_two_charges_and_refunds_to_their_balance()
    {
        var s = await SetupAsync();
        // Cả hai cùng tham gia; Going đóng đủ 2 khoản, Staying chỉ đóng 1 khoản.
        var created = await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/events", new { name = "Dã ngoại Ba Vì", eventDate = Today.AddDays(20).ToString("yyyy-MM-dd"), listDeadline = Today.ToString("yyyy-MM-dd") });
        var eventId = (await ReadAsync(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await s.GoingClient.PutAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/me/response", new { response = "Accepted" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.StayingClient.PutAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/me/response", new { response = "Accepted" })).StatusCode);
        var chargeIds = new List<Guid>();
        foreach (var (title, amount) in new[] { ("Tiền xe", 70_000L), ("Tiền ăn", 100_000L) })
        {
            var response = await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/charges", NewCharge(s.CategoryId, title, amount));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            chargeIds.Add((await ReadAsync(response)).GetProperty("id").GetGuid());
        }

        foreach (var chargeId in chargeIds)
        {
            await CollectAsync(s.OwnerClient, s.Fund.Id, await AssignmentOfAsync(s.OwnerClient, s.Fund.Id, eventId, chargeId, s.Going.Id));
        }

        await CollectAsync(s.OwnerClient, s.Fund.Id, await AssignmentOfAsync(s.OwnerClient, s.Fund.Id, eventId, chargeIds[0], s.Staying.Id), "BankTransfer");

        var url = $"{Base(s.Fund.Id)}/events/{eventId}";
        var preview = await ReadAsync(await s.OwnerClient.GetAsync($"{url}/close-list-preview"));
        var excludedPreview = Assert.Single(preview.GetProperty("excluded").EnumerateArray());
        Assert.Equal((s.Staying.Id, 70_000L), (excludedPreview.GetProperty("userId").GetGuid(), excludedPreview.GetProperty("refund").GetInt64()));
        Assert.Equal(HttpStatusCode.Forbidden, (await s.GoingClient.PostAsJsonAsync($"{url}/close-list", new { })).StatusCode);

        var closed = await s.OwnerClient.PostAsJsonAsync($"{url}/close-list", new { });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(1, (await ReadAsync(closed)).GetProperty("remainingCount").GetInt32());

        // Tiền Staying đã đóng được hoàn vào số dư cá nhân.
        Assert.Equal(70_000, await BalanceOfAsync(s.Fund, s.Staying));
        Assert.Equal(0, await BalanceOfAsync(s.Fund, s.Going));
        var mine = await ReadAsync(await s.StayingClient.GetAsync($"{Base(s.Fund.Id)}/me/balance"));
        var history = mine.GetProperty("history").EnumerateArray().First();
        Assert.Equal((70_000L, "ListCloseRefund"), (history.GetProperty("amount").GetInt64(), history.GetProperty("type").GetString()));

        // Lịch sử trên trang sự kiện: ai, lý do, số tiền hoàn.
        var detail = await ReadAsync(await s.OwnerClient.GetAsync(url));
        var exclusion = Assert.Single(detail.GetProperty("exclusions").EnumerateArray());
        Assert.Equal((s.Staying.Id, 70_000L), (exclusion.GetProperty("userId").GetGuid(), exclusion.GetProperty("refundedAmount").GetInt64()));
        Assert.False(string.IsNullOrWhiteSpace(exclusion.GetProperty("reason").GetString()));
        Assert.Equal(1, detail.GetProperty("participantCount").GetInt32());

        await using var db = host.CreateDbContext();
        // Sổ sự kiện: thu 70k + 170k, hoàn 70k cho Staying → còn đúng phần của Going (170k).
        var income = await db.LedgerEntries.Where(entry => entry.EventId == eventId && entry.Type == LedgerType.Income).SumAsync(entry => entry.Amount);
        var expense = await db.LedgerEntries.Where(entry => entry.EventId == eventId && entry.Type == LedgerType.Expense).SumAsync(entry => entry.Amount);
        Assert.Equal(170_000, income - expense);
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == s.Fund.Id && log.Action == "event.close_list" && log.EventId == eventId));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == s.Fund.Id && log.Action == "event.exclude" && log.EventId == eventId));
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == s.Staying.Id && item.Type == NotificationType.EventParticipantExcluded));
        Assert.Equal(AssignmentStatus.Excluded, await db.ChargeAssignments.Where(item => item.UserId == s.Staying.Id && item.ChargeId == chargeIds[1]).Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task Remind_an_unpaid_participant_is_logged_throttled_and_students_cannot()
    {
        var s = await SetupAsync();
        var eventId = await CreateEventWithAnswersAsync(s);
        var charge = await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/events/{eventId}/charges", NewCharge(s.CategoryId, "Tiền xe", 70_000));
        Assert.Equal(HttpStatusCode.Created, charge.StatusCode);
        var url = $"{Base(s.Fund.Id)}/events/{eventId}/participants/{s.Going.Id}/remind";

        Assert.Equal(HttpStatusCode.Forbidden, (await s.GoingClient.PostAsync(url, null)).StatusCode);
        var reminded = await s.OwnerClient.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, reminded.StatusCode);
        Assert.Equal(1, (await ReadAsync(reminded)).GetProperty("reminded").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await s.OwnerClient.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OwnerClient.PostAsync($"{Base(s.Fund.Id)}/events/{eventId}/participants/{s.Staying.Id}/remind", null)).StatusCode);

        await using var db = host.CreateDbContext();
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == s.Going.Id && item.Type == NotificationType.EventUnpaidReminder && item.EventId == eventId));
        Assert.True(await db.PaymentReminders.AnyAsync(item => item.FundId == s.Fund.Id && item.EventId == eventId && item.UserId == s.Going.Id && item.Kind == ReminderKind.Manual));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == s.Fund.Id && log.Action == "charge.event_remind" && log.Scope == FinancialScope.Event));
    }

    [Fact]
    public async Task Students_see_expenses_and_only_their_own_income_and_the_detail_names_the_payer()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync($"{Base(s.Fund.Id)}/charges", NewCharge(s.CategoryId, "Quỹ HK1", 50_000))).StatusCode);
        foreach (var client in new[] { s.GoingClient, s.StayingClient })
        {
            var assignment = await AssignmentIdAsync(client, s.Fund.Id, "Quỹ HK1");
            Assert.Equal(HttpStatusCode.Created, (await CollectAsync(s.OwnerClient, s.Fund.Id, assignment)).StatusCode);
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
}
