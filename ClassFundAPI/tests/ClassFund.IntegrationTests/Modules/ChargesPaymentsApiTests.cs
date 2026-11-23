using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>M6a-T4 – Khoản thu chung và thanh toán: mã trạng thái, phân quyền, thủ quỹ tích đã thu / hủy thu / nhắc đóng, tự ghi nhận.</summary>
[Collection(ApiCollection.Name)]
public class ChargesPaymentsApiTests(ApiTestHost host)
{
    private static string Base(Guid fundId) => $"/api/v1/funds/{fundId}";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    private static object NewCharge(Guid categoryId, long amount = 50_000) => new
    {
        categoryId,
        title = "Quỹ lớp tháng 12",
        purpose = "Đóng quỹ",
        amountPerPerson = amount,
        dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
    };

    private sealed record Setup(Guid FundId, Guid CategoryId, User Owner, User Treasurer, User Student, User Other, HttpClient OwnerClient, HttpClient TreasurerClient, HttpClient StudentClient, HttpClient OtherClient);

    private async Task<Setup> SetupAsync()
    {
        var owner = await host.CreateUserAsync();
        var treasurer = await host.CreateUserAsync(UserOrigin.Provisioned);
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        var other = await host.CreateUserAsync(UserOrigin.Provisioned);
        var fund = await host.CreateFundAsync(owner);
        await host.AddMemberAsync(fund, treasurer, MemberRole.Treasurer);
        await host.AddMemberAsync(fund, student);
        await host.AddMemberAsync(fund, other);
        var ownerClient = await host.LoginAsync(owner);
        var category = await ownerClient.PostAsJsonAsync($"{Base(fund.Id)}/categories", new { name = "Quỹ lớp", type = "Income" });
        Assert.Equal(HttpStatusCode.Created, category.StatusCode);
        var categoryId = (await ReadAsync(category)).GetProperty("id").GetGuid();
        return new Setup(fund.Id, categoryId, owner, treasurer, student, other,
            ownerClient, await host.LoginAsync(treasurer), await host.LoginAsync(student), await host.LoginAsync(other));
    }

    private async Task<Guid> CreateChargeAsync(Setup s)
    {
        var response = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", NewCharge(s.CategoryId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AssignmentIdAsync(HttpClient client, Guid fundId)
    {
        var mine = (await ReadAsync(await client.GetAsync($"{Base(fundId)}/me/assignments"))).EnumerateArray().First();
        return mine.GetProperty("assignmentId").GetGuid();
    }

    private static Task<HttpResponseMessage> CollectAsync(HttpClient client, Guid fundId, Guid assignmentId, object? body = null) =>
        client.PostAsJsonAsync($"{Base(fundId)}/assignments/{assignmentId}/collect", body ?? new { method = "Cash" });

    // ---------- 401 / 403 / 404 / 400 ----------

    [Fact]
    public async Task Anonymous_gets_401_and_outsiders_404()
    {
        var s = await SetupAsync();
        var anonymous = host.Anonymous();
        var stranger = await host.LoginAsync(await host.CreateUserAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Base(s.FundId)}/charges")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Base(s.FundId)}/me/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base(s.FundId)}/charges")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base(s.FundId)}/me/assignments")).StatusCode);
    }

    [Fact]
    public async Task Students_cannot_create_close_cancel_list_assignments_or_collect()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);

        var responses = new[]
        {
            await s.OtherClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", NewCharge(s.CategoryId)),
            await s.OtherClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/close", null),
            await s.OtherClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/cancel", null),
            await s.OtherClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments"),
            await s.OtherClient.PostAsJsonAsync($"{Base(s.FundId)}/assignments/{assignmentId}/collect", new { method = "Cash" }),
            await s.OtherClient.PostAsJsonAsync($"{Base(s.FundId)}/assignments/{assignmentId}/uncollect", new { reason = "x" }),
            await s.OtherClient.PostAsync($"{Base(s.FundId)}/assignments/{assignmentId}/remind", null),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [Fact]
    public async Task The_old_student_submit_and_approval_endpoints_are_gone()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);

        var submit = await s.StudentClient.PostAsJsonAsync($"{Base(s.FundId)}/me/assignments/{assignmentId}/payments", new { method = "Cash" });
        var queue = await s.TreasurerClient.GetAsync($"{Base(s.FundId)}/payments");

        Assert.Contains(submit.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Contains(queue.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task Create_validates_the_body_with_400()
    {
        var s = await SetupAsync();

        var response = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", new { title = "", amountPerPerson = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeOfAsync(response));
    }

    [Fact]
    public async Task Create_needs_a_usable_income_category_and_the_collected_payment_carries_it()
    {
        var s = await SetupAsync();
        var missing = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", new { title = "A", amountPerPerson = 1000, dueDate = "2099-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var unknown = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", NewCharge(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal("category.unusable", await CodeOfAsync(unknown));

        await CreateChargeAsync(s);
        var collect = await CollectAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.StudentClient, s.FundId));
        var paymentId = (await ReadAsync(collect)).GetProperty("id").GetGuid();

        await using var db = host.CreateDbContext();
        Assert.Equal(s.CategoryId, (await db.LedgerEntries.SingleAsync(item => item.PaymentId == paymentId)).CategoryId);
        var list = (await ReadAsync(await s.StudentClient.GetAsync($"{Base(s.FundId)}/charges"))).GetProperty("items")[0];
        Assert.Equal("Quỹ lớp", list.GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task Create_with_a_past_due_date_is_422_and_a_general_charge_may_have_no_due_date()
    {
        var s = await SetupAsync();

        var past = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", new { title = "A", amountPerPerson = 1000, dueDate = "2020-01-01", categoryId = s.CategoryId });
        var none = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", new { title = "Không hạn", amountPerPerson = 1000, categoryId = s.CategoryId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, past.StatusCode);
        Assert.Equal("charge.due_date_in_past", await CodeOfAsync(past));
        Assert.Equal(HttpStatusCode.Created, none.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadAsync(none)).GetProperty("dueDate").ValueKind);
    }

    // ---------- Khoản thu ----------

    [Fact]
    public async Task Create_gives_every_active_member_an_obligation_and_a_notification_and_students_see_only_their_own()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);

        await using var db = host.CreateDbContext();
        Assert.Equal(4, await db.ChargeAssignments.CountAsync(item => item.ChargeId == chargeId));
        Assert.Equal(4, await db.Notifications.CountAsync(item => item.FundId == s.FundId && item.Type == NotificationType.GeneralChargeCreated));
        Assert.True(await db.AuditLogs.AnyAsync(item => item.Action == "charge.create" && item.FundId == s.FundId));

        var asStudent = (await ReadAsync(await s.StudentClient.GetAsync($"{Base(s.FundId)}/charges"))).GetProperty("items").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, asStudent.GetProperty("progress").ValueKind);
        Assert.Equal("Unpaid", asStudent.GetProperty("mine").GetProperty("status").GetString());
        var asTreasurer = (await ReadAsync(await s.TreasurerClient.GetAsync($"{Base(s.FundId)}/charges"))).GetProperty("items").EnumerateArray().Single();
        Assert.Equal(4, asTreasurer.GetProperty("progress").GetProperty("assignedCount").GetInt32());
    }

    [Fact]
    public async Task Assignments_list_shows_who_paid_how_and_filters_by_status()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        await CollectAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.StudentClient, s.FundId), new { method = "BankTransfer", transferCode = "FT1" });
        await CollectAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.TreasurerClient, s.FundId));

        var all = (await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments"))).GetProperty("items").EnumerateArray().ToArray();
        var paid = (await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments?status=Paid"))).GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(4, all.Length);
        Assert.Equal(2, paid.Length);
        var row = all.Single(item => item.GetProperty("userId").GetGuid() == s.Student.Id);
        Assert.Equal("BankTransfer", row.GetProperty("method").GetString());
        Assert.Equal("Student", row.GetProperty("role").GetString());
        Assert.False(row.GetProperty("isSelfDeclared").GetBoolean());
        var self = all.Single(item => item.GetProperty("userId").GetGuid() == s.Treasurer.Id);
        Assert.True(self.GetProperty("isSelfDeclared").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await s.OwnerClient.GetAsync($"{Base(s.FundId)}/charges/{Guid.NewGuid()}/assignments")).StatusCode);
    }

    [Fact]
    public async Task A_member_added_after_the_charge_still_owes_it()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        var email = $"late-{Guid.NewGuid():N}@example.com";

        var add = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/members", new { email, fullName = "Vào Muộn" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var rows = (await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments?search=Vào Muộn"))).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Unpaid", Assert.Single(rows).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Close_then_close_again_is_409_and_a_closed_charge_cannot_be_collected()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);

        Assert.Equal(HttpStatusCode.OK, (await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/close", null)).StatusCode);
        var again = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/close", null);
        var collect = await CollectAsync(s.TreasurerClient, s.FundId, assignmentId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, collect.StatusCode);
        Assert.Equal("payment.charge_not_open", await CodeOfAsync(collect));
    }

    [Fact]
    public async Task Cancel_is_blocked_after_a_payment_with_422()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        await CollectAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.StudentClient, s.FundId));

        var response = await s.OwnerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/cancel", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("charge.has_payments", await CodeOfAsync(response));
    }

    // ---------- Thu tiền (thủ quỹ tích) ----------

    [Fact]
    public async Task The_treasurer_collects_once_and_one_income_entry_is_written()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);

        var collect = await CollectAsync(s.TreasurerClient, s.FundId, assignmentId, new { method = "BankTransfer", transferCode = "FT9" });
        Assert.Equal(HttpStatusCode.Created, collect.StatusCode);
        var body = await ReadAsync(collect);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("isSelfDeclared").GetBoolean());

        var second = await CollectAsync(s.OwnerClient, s.FundId, assignmentId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("payment.not_payable", await CodeOfAsync(second));

        var mine = (await ReadAsync(await s.StudentClient.GetAsync($"{Base(s.FundId)}/me/assignments"))).EnumerateArray().Single();
        Assert.Equal("Paid", mine.GetProperty("status").GetString());

        await using var db = host.CreateDbContext();
        var entry = await db.LedgerEntries.SingleAsync(item => item.PaymentId == body.GetProperty("id").GetGuid());
        Assert.Equal((LedgerType.Income, LedgerSource.Payment, false, 50_000L), (entry.Type, entry.Source, entry.IsSelfDeclared, entry.Amount));
        Assert.True(await db.AuditLogs.AnyAsync(item => item.Action == "payment.collect" && item.ActorUserId == s.Treasurer.Id));
    }

    [Fact]
    public async Task Personal_balance_is_not_a_collect_method_and_the_body_is_validated()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);

        var balance = await CollectAsync(s.TreasurerClient, s.FundId, assignmentId, new { method = "PersonalBalance" });
        var empty = await CollectAsync(s.TreasurerClient, s.FundId, assignmentId, new { });

        Assert.Equal(HttpStatusCode.BadRequest, balance.StatusCode);
        Assert.Equal("validation_failed", await CodeOfAsync(balance));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Uncollect_needs_a_reason_reverses_the_ledger_and_allows_collecting_again()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);
        var notCollected = await s.TreasurerClient.PostAsJsonAsync($"{Base(s.FundId)}/assignments/{assignmentId}/uncollect", new { reason = "Tích nhầm" });
        Assert.Equal(HttpStatusCode.Conflict, notCollected.StatusCode);
        Assert.Equal("payment.not_collected", await CodeOfAsync(notCollected));
        await CollectAsync(s.TreasurerClient, s.FundId, assignmentId);

        var blank = await s.TreasurerClient.PostAsJsonAsync($"{Base(s.FundId)}/assignments/{assignmentId}/uncollect", new { reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var undo = await s.TreasurerClient.PostAsJsonAsync($"{Base(s.FundId)}/assignments/{assignmentId}/uncollect", new { reason = "Tích nhầm người" });
        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);

        var mine = (await ReadAsync(await s.StudentClient.GetAsync($"{Base(s.FundId)}/me/assignments"))).EnumerateArray().Single();
        Assert.Equal("Unpaid", mine.GetProperty("status").GetString());
        await using (var db = host.CreateDbContext())
        {
            var entries = await db.LedgerEntries.Where(item => item.FundId == s.FundId).ToListAsync();
            Assert.Equal(0, entries.Where(item => item.Type == LedgerType.Income).Sum(item => item.Amount) - entries.Where(item => item.Type == LedgerType.Expense).Sum(item => item.Amount));
            Assert.True(await db.AuditLogs.AnyAsync(item => item.Action == "payment.uncollect" && item.FundId == s.FundId));
        }

        Assert.Equal(HttpStatusCode.Created, (await CollectAsync(s.TreasurerClient, s.FundId, assignmentId)).StatusCode);
    }

    [Fact]
    public async Task A_treasurer_collecting_for_themselves_is_self_declared_in_the_ledger_and_audit()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);

        var response = await CollectAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.TreasurerClient, s.FundId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.True(body.GetProperty("isSelfDeclared").GetBoolean());
        await using var db = host.CreateDbContext();
        Assert.True((await db.LedgerEntries.SingleAsync(item => item.PaymentId == body.GetProperty("id").GetGuid())).IsSelfDeclared);
        Assert.True(await db.AuditLogs.AnyAsync(item => item.Action == "payment.collect" && item.IsSelfAction && item.ActorUserId == s.Treasurer.Id));
    }

    [Fact]
    public async Task Someone_elses_obligation_is_404_for_a_student_view_and_unknown_assignment_is_404_for_the_treasurer()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);

        var unknown = await CollectAsync(s.TreasurerClient, s.FundId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var mine = (await ReadAsync(await s.StudentClient.GetAsync($"{Base(s.FundId)}/me/assignments"))).EnumerateArray().ToArray();
        Assert.Single(mine);
    }

    // ---------- Nhắc đóng ----------

    [Fact]
    public async Task Remind_one_person_logs_it_notifies_and_is_throttled()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);

        var remind = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/assignments/{assignmentId}/remind", null);
        Assert.Equal(HttpStatusCode.OK, remind.StatusCode);
        Assert.Equal(1, (await ReadAsync(remind)).GetProperty("reminded").GetInt32());
        var again = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/assignments/{assignmentId}/remind", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("charge.remind_too_soon", await CodeOfAsync(again));

        await using var db = host.CreateDbContext();
        Assert.Equal(1, await db.PaymentReminders.CountAsync(item => item.FundId == s.FundId && item.UserId == s.Student.Id && item.Kind == ReminderKind.Manual));
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == s.Student.Id && item.Type == NotificationType.ChargeDueSoon));
        Assert.True(await db.AuditLogs.AnyAsync(item => item.Action == "payment.remind" && item.FundId == s.FundId));
    }

    [Fact]
    public async Task Remind_someone_who_already_paid_is_422()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var assignmentId = await AssignmentIdAsync(s.StudentClient, s.FundId);
        await CollectAsync(s.TreasurerClient, s.FundId, assignmentId);

        var response = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/assignments/{assignmentId}/remind", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Remind_the_whole_charge_reaches_everyone_unpaid()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        await CollectAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.StudentClient, s.FundId));

        var remind = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/remind", null);

        Assert.Equal(HttpStatusCode.OK, remind.StatusCode);
        Assert.Equal(3, (await ReadAsync(remind)).GetProperty("reminded").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await s.StudentClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/remind", null)).StatusCode);
    }
}
