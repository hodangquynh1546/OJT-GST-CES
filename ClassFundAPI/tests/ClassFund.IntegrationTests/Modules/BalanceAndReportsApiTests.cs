using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>
/// M10-T4 + M13-T3 trên PostgreSQL thật: rút toàn bộ số dư, xử lý đồng thời không làm âm / trừ hai lần,
/// báo cáo quỹ chung không lẫn sự kiện, học sinh chỉ thấy của mình. Số dư ban đầu (M9 chưa có) ghi thẳng vào DB.
/// </summary>
[Collection(ApiCollection.Name)]
public class BalanceAndReportsApiTests(ApiTestHost host)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 11, 15);

    private static string Url(Guid fundId, string rest) => $"/api/v1/funds/{fundId}{rest}";

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    private async Task<(Fund Fund, User Owner, User Student)> SetupAsync()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(fund, student);
        return (fund, owner, student);
    }

    private async Task SeedBalanceAsync(Fund fund, User user, long amount)
    {
        await using var db = host.CreateDbContext();
        db.PersonalBalances.Add(new PersonalBalance(fund.Id, user.Id) { Balance = amount });
        db.BalanceTransactions.Add(new BalanceTransaction(fund.Id, user.Id, BalanceTransactionType.SettlementCredit, amount, amount, null, fund.CreatedByUserId, Now));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Students_only_view_their_balance_and_cannot_withdraw_or_list()
    {
        var (fund, _, student) = await SetupAsync();
        await SeedBalanceAsync(fund, student, 38_000);
        var studentClient = await host.LoginAsync(student);

        var mine = await studentClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/me/balance"));
        Assert.Equal(38_000, mine.GetProperty("balance").GetInt64());
        Assert.Equal(1, mine.GetProperty("history").GetArrayLength());

        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync(Url(fund.Id, "/balances"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync(Url(fund.Id, "/withdrawals"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.PostAsJsonAsync(Url(fund.Id, $"/balances/{student.Id}/withdraw"), new { amount = 10_000 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anonymous().GetAsync(Url(fund.Id, "/balances"))).StatusCode);
    }

    [Fact]
    public async Task Treasurer_lists_balances_and_withdraws_a_valid_amount_with_history_and_audit()
    {
        var (fund, owner, student) = await SetupAsync();
        await SeedBalanceAsync(fund, student, 38_000);
        var ownerClient = await host.LoginAsync(owner);

        var list = await ownerClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/balances"));
        var row = list.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("userId").GetGuid() == student.Id);
        Assert.Equal(38_000, row.GetProperty("balance").GetInt64());
        Assert.Equal(38_000, list.GetProperty("heldTotal").GetInt64());

        var withdraw = await ownerClient.PostAsJsonAsync(Url(fund.Id, $"/balances/{student.Id}/withdraw"), new { amount = 30_000 });
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);
        Assert.Equal(8_000, (await withdraw.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetInt64());

        var history = await ownerClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/withdrawals"));
        var item = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal((student.Id, 30_000L), (item.GetProperty("userId").GetGuid(), item.GetProperty("amount").GetInt64()));

        var mine = await (await host.LoginAsync(student)).GetFromJsonAsync<JsonElement>(Url(fund.Id, "/me/balance"));
        Assert.Equal(8_000, mine.GetProperty("balance").GetInt64());
        Assert.Equal(-30_000, mine.GetProperty("history")[0].GetProperty("amount").GetInt64());

        await using var db = host.CreateDbContext();
        Assert.False(await db.LedgerEntries.AnyAsync(entry => entry.FundId == fund.Id));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == fund.Id && log.Action == "balance.withdraw"));
    }

    [Theory]
    [InlineData(5_000)]
    [InlineData(10_500)]
    [InlineData(0)]
    [InlineData(-10_000)]
    public async Task Withdraw_rejects_a_bad_amount_with_400(long amount)
    {
        var (fund, owner, student) = await SetupAsync();
        await SeedBalanceAsync(fund, student, 50_000);

        var response = await (await host.LoginAsync(owner)).PostAsJsonAsync(Url(fund.Id, $"/balances/{student.Id}/withdraw"), new { amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeOf(response));
    }

    [Fact]
    public async Task Withdraw_more_than_the_balance_is_422_and_an_unknown_member_is_404()
    {
        var (fund, owner, student) = await SetupAsync();
        await SeedBalanceAsync(fund, student, 20_000);
        var ownerClient = await host.LoginAsync(owner);

        var over = await ownerClient.PostAsJsonAsync(Url(fund.Id, $"/balances/{student.Id}/withdraw"), new { amount = 30_000 });
        var unknown = await ownerClient.PostAsJsonAsync(Url(fund.Id, $"/balances/{Guid.NewGuid()}/withdraw"), new { amount = 10_000 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, over.StatusCode);
        Assert.Equal("balance.insufficient", await CodeOf(over));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(20_000, (await ownerClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/balances"))).GetProperty("heldTotal").GetInt64());
    }

    [Fact]
    public async Task Concurrent_withdrawals_never_overdraw_the_balance()
    {
        var (fund, owner, student) = await SetupAsync();
        await SeedBalanceAsync(fund, student, 50_000);
        var a = await host.LoginAsync(owner);
        var b = await host.LoginAsync(owner);

        var results = await Task.WhenAll(
            a.PostAsJsonAsync(Url(fund.Id, $"/balances/{student.Id}/withdraw"), new { amount = 30_000 }),
            b.PostAsJsonAsync(Url(fund.Id, $"/balances/{student.Id}/withdraw"), new { amount = 30_000 }));

        Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.UnprocessableEntity);
        await using var db = host.CreateDbContext();
        Assert.Equal(20_000, (await db.PersonalBalances.SingleAsync(item => item.FundId == fund.Id && item.UserId == student.Id)).Balance);
        Assert.Equal(1, await db.WithdrawalRequests.CountAsync(item => item.FundId == fund.Id));
    }

    [Fact]
    public async Task Reports_keep_general_and_event_figures_apart_and_students_see_only_their_own()
    {
        var (fund, owner, student) = await SetupAsync();
        Guid eventId;
        await using (var db = host.CreateDbContext())
        {
            var category = new Category(fund.Id, "Quà tặng", LedgerType.Expense, isSystem: false);
            var picnic = new Event(fund.Id, "Dã ngoại", null, Day, owner.Id, Now);
            eventId = picnic.Id;
            db.Categories.Add(category);
            db.Events.Add(picnic);
            db.EventParticipants.Add(new EventParticipant(picnic.Id, student.Id) { Response = ParticipationResponse.Accepted });
            var charge = new Charge(fund.Id, null, "Quỹ lớp HK1", null, 200_000, Day, ChargeKind.Regular, owner.Id, Now);
            var paid = new ChargeAssignment(charge.Id, student.Id, 200_000) { Status = AssignmentStatus.Paid };
            db.Charges.Add(charge);
            db.ChargeAssignments.Add(paid);
            var payment = new Payment(paid.Id, student.Id, 200_000, PaymentMethod.BankTransfer, Now) { Status = PaymentStatus.Confirmed, ConfirmationMode = ConfirmationMode.Verified, ConfirmedAt = Now, ConfirmedByUserId = owner.Id };
            db.Payments.Add(payment);
            db.LedgerEntries.AddRange(
                new LedgerEntry(fund.Id, null, LedgerType.Income, LedgerSource.Payment, null, "Thu: Quỹ lớp HK1", null, 200_000, Day, owner.Id, Now) { PaymentId = payment.Id },
                new LedgerEntry(fund.Id, null, LedgerType.Income, LedgerSource.Payment, null, "Thu: tự ghi nhận", null, 100_000, Day, owner.Id, Now) { IsSelfDeclared = true },
                new LedgerEntry(fund.Id, null, LedgerType.Expense, LedgerSource.Manual, category.Id, "Mua hoa", null, 120_000, Day, owner.Id, Now),
                new LedgerEntry(fund.Id, picnic.Id, LedgerType.Income, LedgerSource.Payment, null, "Thu sự kiện", null, 9_000_000, Day, owner.Id, Now));
            await db.SaveChangesAsync();
        }

        var ownerClient = await host.LoginAsync(owner);
        var overview = await ownerClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/reports/overview"));
        var general = overview.GetProperty("general");
        Assert.Equal((180_000L, 300_000L, 120_000L, 100_000L), (general.GetProperty("balance").GetInt64(), general.GetProperty("totalIncome").GetInt64(), general.GetProperty("totalExpense").GetInt64(), general.GetProperty("selfDeclaredIncome").GetInt64()));
        Assert.Equal(3, overview.GetProperty("recentEntries").GetArrayLength());
        Assert.Equal("BankTransfer", overview.GetProperty("recentEntries").EnumerateArray().Single(item => item.GetProperty("title").GetString() == "Thu: Quỹ lớp HK1").GetProperty("paymentMethod").GetString());

        var report = await ownerClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/reports/general?from=2026-11-01&to=2026-11-30"));
        Assert.Equal(120_000, Assert.Single(report.GetProperty("expenseByCategory").EnumerateArray()).GetProperty("amount").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.GetAsync(Url(fund.Id, "/reports/general?from=2026-11-30&to=2026-11-01"))).StatusCode);

        var events = await ownerClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/reports/events"));
        var row = Assert.Single(events.EnumerateArray());
        Assert.Equal((eventId, 9_000_000L), (row.GetProperty("id").GetGuid(), row.GetProperty("collected").GetInt64()));

        var students = await ownerClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/reports/students"));
        Assert.Equal(2, students.GetArrayLength());

        var studentClient = await host.LoginAsync(student);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync(Url(fund.Id, "/reports/students"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await studentClient.GetAsync(Url(fund.Id, "/reports/general"))).StatusCode);
        var me = await studentClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/reports/me"));
        Assert.Equal((student.Id, 200_000L), (me.GetProperty("userId").GetGuid(), me.GetProperty("paidGeneral").GetInt64()));
        var studentOverview = await studentClient.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/reports/overview"));
        Assert.Equal(0, studentOverview.GetProperty("recentEntries").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, studentOverview.GetProperty("todo").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await (await host.LoginAsync(await host.CreateUserAsync())).GetAsync(Url(fund.Id, "/reports/overview"))).StatusCode);
    }
}
