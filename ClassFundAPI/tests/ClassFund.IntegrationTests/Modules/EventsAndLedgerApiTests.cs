using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>
/// M7-T3 + M8-T3 trên PostgreSQL thật: quyền, khóa theo phạm vi khi ghi chi song song (BR-38), kết thúc sự kiện (BR-19),
/// sự kiện đã kết thúc không nhận chi mới (BR-20), tổng quỹ chung không gồm giao dịch sự kiện (BR-01).
/// Khoản thu / thanh toán (M6) chưa có API nên được ghi thẳng vào DB.
/// </summary>
[Collection(ApiCollection.Name)]
public class EventsAndLedgerApiTests(ApiTestHost host)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static string Events(Guid fundId, string rest = "") => $"/api/v1/funds/{fundId}/events{rest}";

    private static string Ledger(Guid fundId, string rest = "") => $"/api/v1/funds/{fundId}/ledger-entries{rest}";

    private sealed record Setup(Fund Fund, User Owner, User Student, Guid CategoryId);

    private async Task<Setup> CreateFundAsync()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(fund, student);
        var category = new Category(fund.Id, "Quà tặng", LedgerType.Expense, isSystem: false);
        await using var db = host.CreateDbContext();
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return new Setup(fund, owner, student, category.Id);
    }

    /// <summary>Thu đã xác nhận (M6 sẽ sinh dòng này khi thủ quỹ xác nhận thanh toán).</summary>
    private async Task SeedIncomeAsync(Setup setup, long amount, Guid? eventId = null)
    {
        await using var db = host.CreateDbContext();
        db.LedgerEntries.Add(new LedgerEntry(setup.Fund.Id, eventId, LedgerType.Income, LedgerSource.Payment, null, "Thu: đã xác nhận", null, amount, Today, setup.Owner.Id, Now));
        await db.SaveChangesAsync();
    }

    private static object Expense(Setup setup, long amount, Guid? eventId = null, Guid[]? files = null) => new
    {
        eventId,
        categoryId = setup.CategoryId,
        title = "Mua hoa 20/11",
        purpose = "Tặng cô chủ nhiệm",
        amount,
        transactionDate = Today.ToString("yyyy-MM-dd"),
        attachmentIds = files ?? [],
    };

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    [Fact]
    public async Task Permissions_students_read_only_outsiders_404_anonymous_401()
    {
        var setup = await CreateFundAsync();
        var owner = await host.LoginAsync(setup.Owner);
        var student = await host.LoginAsync(setup.Student);
        var outsider = await host.LoginAsync(await host.CreateUserAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync(Events(setup.Fund.Id), new { name = "X", eventDate = "2026-11-20" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 1000))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync(Ledger(setup.Fund.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync(Events(setup.Fund.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(Ledger(setup.Fund.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(Events(setup.Fund.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anonymous().GetAsync(Ledger(setup.Fund.Id))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), new { title = "", amount = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(Events(setup.Fund.Id), new { name = "Không ngày" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(Ledger(setup.Fund.Id, "?from=2026-11-17&to=2026-11-01"))).StatusCode);
    }

    [Fact]
    public async Task Expense_rules_on_postgres_balance_evidence_scope_and_reversal()
    {
        var setup = await CreateFundAsync();
        var owner = await host.LoginAsync(setup.Owner);
        await SeedIncomeAsync(setup, 1_000_000);

        var over = await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 1_000_001));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, over.StatusCode);
        Assert.Equal("ledger.exceeds_balance", await CodeOf(over));

        var noProof = await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 650_000));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noProof.StatusCode);
        Assert.Equal("evidence.required", await CodeOf(noProof));

        var upload = await owner.PostAsync($"/api/v1/funds/{setup.Fund.Id}/attachments", ApiTestHost.Multipart(ApiTestHost.Pdf, "hoa-don.pdf", "application/pdf", "Receipt"));
        var fileId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var created = await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 650_000, files: [fileId]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detail = await created.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = detail.GetProperty("entry").GetProperty("id").GetGuid();
        Assert.Equal(1, detail.GetProperty("entry").GetProperty("attachmentCount").GetInt32());
        Assert.True(detail.GetProperty("canReverse").GetBoolean());

        var list = await owner.GetFromJsonAsync<JsonElement>(Ledger(setup.Fund.Id, "?type=Expense"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(350_000, list.GetProperty("summary").GetProperty("balance").GetInt64());

        // Học sinh thấy chứng từ chi công khai (AZ-05) nhưng không thấy dòng thu từ thanh toán (OQ-14).
        var student = await host.LoginAsync(setup.Student);
        var studentList = await student.GetFromJsonAsync<JsonElement>(Ledger(setup.Fund.Id));
        Assert.Equal(1, studentList.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/api/v1/funds/{setup.Fund.Id}/attachments/{fileId}")).StatusCode);

        var reversed = await owner.PostAsJsonAsync(Ledger(setup.Fund.Id, $"/{entryId}/reverse"), new { reason = "Ghi nhầm số tiền" });
        Assert.Equal(HttpStatusCode.Created, reversed.StatusCode);
        var again = await owner.PostAsJsonAsync(Ledger(setup.Fund.Id, $"/{entryId}/reverse"), new { reason = "Lần hai" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(Ledger(setup.Fund.Id, $"/{entryId}/reverse"), new { reason = " " })).StatusCode);

        var original = await owner.GetFromJsonAsync<JsonElement>(Ledger(setup.Fund.Id, $"/{entryId}"));
        Assert.Equal("Ghi nhầm số tiền", original.GetProperty("reversedBy").GetProperty("reason").GetString());
        var totals = (await owner.GetFromJsonAsync<JsonElement>(Ledger(setup.Fund.Id))).GetProperty("summary");
        Assert.Equal((1_000_000L, 0L, 1_000_000L), (totals.GetProperty("balance").GetInt64(), totals.GetProperty("totalExpense").GetInt64(), totals.GetProperty("totalIncome").GetInt64()));

        // Sổ chỉ ghi thêm: trigger chặn UPDATE dòng gốc.
        await using var db = host.CreateDbContext();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlAsync($"UPDATE \"LedgerEntry\" SET \"Amount\" = 1 WHERE \"Id\" = {entryId}"));
    }

    [Fact]
    public async Task Concurrent_expenses_cannot_overspend_the_scope_BR38()
    {
        var setup = await CreateFundAsync();
        await SeedIncomeAsync(setup, 500_000);
        var first = await host.LoginAsync(setup.Owner);
        var second = await host.LoginAsync(setup.Owner);

        var results = await Task.WhenAll(
            first.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 300_000)),
            second.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 300_000)));

        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.UnprocessableEntity);
        await using var db = host.CreateDbContext();
        Assert.Equal(1, await db.LedgerEntries.CountAsync(entry => entry.FundId == setup.Fund.Id && entry.Type == LedgerType.Expense));
    }

    private static object NewEvent(string name, string? description = null, int deadlineInDays = 0) => new
    {
        name,
        description,
        eventDate = Today.AddDays(10).ToString("yyyy-MM-dd"),
        listDeadline = Today.AddDays(deadlineInDays).ToString("yyyy-MM-dd"),
    };

    [Fact]
    public async Task Event_lifecycle_invite_respond_close_list_spend_and_closed_scope_BR19_BR20()
    {
        var setup = await CreateFundAsync();
        var owner = await host.LoginAsync(setup.Owner);
        var lazy = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(setup.Fund, lazy);

        var created = await owner.PostAsJsonAsync(Events(setup.Fund.Id), NewEvent("Dã ngoại Ba Vì", "Xe 45 chỗ"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = createdBody.GetProperty("id").GetGuid();
        Assert.Equal(Today.ToString("yyyy-MM-dd"), createdBody.GetProperty("listDeadline").GetString());

        // Hạn đóng danh sách bắt buộc, không được ở quá khứ.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(Events(setup.Fund.Id), new { name = "Thiếu hạn", eventDate = Today.AddDays(5).ToString("yyyy-MM-dd") })).StatusCode);
        Assert.Equal("event.list_deadline_in_past", await CodeOf(await owner.PostAsJsonAsync(Events(setup.Fund.Id), NewEvent("Quá khứ", null, -1))));

        // Thành viên thêm sau qua API M3 được mời vào sự kiện đang mở (TD-10).
        var added = await owner.PostAsJsonAsync($"/api/v1/funds/{setup.Fund.Id}/members", new { email = $"late-{Guid.NewGuid():N}@it.local", fullName = "Vào Sau" });
        var lateUserId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("member").GetProperty("userId").GetGuid();

        var student = await host.LoginAsync(setup.Student);
        Assert.Equal(HttpStatusCode.OK, (await student.PutAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/me/response"), new { response = "Accepted" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await host.LoginAsync(lazy)).PutAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/me/response"), new { response = "Accepted" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PutAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/me/response"), new { response = "Pending" })).StatusCode);

        // Ghi khoản thu sự kiện (350.000đ) + thanh toán đã thu của học sinh thẳng vào DB.
        await using (var db = host.CreateDbContext())
        {
            Assert.True(await db.EventParticipants.AnyAsync(item => item.EventId == eventId && item.UserId == lateUserId));
            var charge = new Charge(setup.Fund.Id, eventId, "Phí dã ngoại", null, 350_000, Today, ChargeKind.Regular, setup.Owner.Id, Now);
            var paid = new ChargeAssignment(charge.Id, setup.Student.Id, 350_000) { Status = AssignmentStatus.Paid };
            var unpaid = new ChargeAssignment(charge.Id, lazy.Id, 350_000);
            db.Charges.Add(charge);
            db.ChargeAssignments.AddRange(paid, unpaid);
            var payment = new Payment(paid.Id, setup.Student.Id, 350_000, PaymentMethod.Cash, Now)
            {
                Status = PaymentStatus.Confirmed,
                ConfirmedByUserId = setup.Owner.Id,
                ConfirmedAt = Now,
                ConfirmationMode = ConfirmationMode.Verified,
            };
            db.Payments.Add(payment);
            db.LedgerEntries.Add(new LedgerEntry(setup.Fund.Id, eventId, LedgerType.Income, LedgerSource.Payment, null, "Thu: Phí dã ngoại", null, 350_000, Today, setup.Owner.Id, Now) { PaymentId = payment.Id });
            await db.SaveChangesAsync();
        }

        Assert.Equal("event.has_money", await CodeOf(await owner.PostAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/cancel"), new { reason = "Mưa" })));
        // Chưa đóng danh sách thì chưa được ghi chi.
        Assert.Equal("event.list_not_closed", await CodeOf(await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 100_000, eventId))));
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/close-list"), new { })).StatusCode);

        var participants = await owner.GetFromJsonAsync<JsonElement>(Events(setup.Fund.Id, $"/{eventId}/participants?paymentStatus=Unpaid"));
        Assert.Equal(lazy.Id, Assert.Single(participants.GetProperty("items").EnumerateArray()).GetProperty("userId").GetGuid());
        Assert.Equal(2, participants.GetProperty("total").GetInt32());

        var preview = await owner.GetFromJsonAsync<JsonElement>(Events(setup.Fund.Id, $"/{eventId}/close-list-preview"));
        Assert.Equal(lazy.Id, Assert.Single(preview.GetProperty("excluded").EnumerateArray()).GetProperty("userId").GetGuid());
        Assert.False(preview.GetProperty("beforeDeadline").GetBoolean());

        var closed = await owner.PostAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/close-list"), new { });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(1, (await closed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("remainingCount").GetInt32());

        // Sau khi đóng danh sách mới được chi, và không được chi vượt tiền đã thu.
        Assert.Equal("ledger.exceeds_balance", await CodeOf(await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 350_001, eventId))));
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 200_000, eventId))).StatusCode);
        // Quỹ chung không gồm tiền sự kiện (BR-01): chi quỹ chung 1đ vẫn vượt số dư 0.
        Assert.Equal("ledger.exceeds_balance", await CodeOf(await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 1))));

        Assert.Equal("event.invalid_transition", await CodeOf(await owner.PostAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/close-list"), new { })));
        Assert.Equal("event.not_open", await CodeOf(await student.PutAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/me/response"), new { response = "Declined" })));

        var detail = await owner.GetFromJsonAsync<JsonElement>(Events(setup.Fund.Id, $"/{eventId}"));
        Assert.Equal("Completed", detail.GetProperty("status").GetString());
        Assert.Equal((1, 1), (detail.GetProperty("participantCount").GetInt32(), detail.GetProperty("excludedCount").GetInt32()));
        Assert.Equal((350_000L, 200_000L, 150_000L), (detail.GetProperty("totalIncome").GetInt64(), detail.GetProperty("totalExpense").GetInt64(), detail.GetProperty("balance").GetInt64()));
        var exclusion = Assert.Single(detail.GetProperty("exclusions").EnumerateArray());
        Assert.Equal((lazy.Id, 0L), (exclusion.GetProperty("userId").GetGuid(), exclusion.GetProperty("refundedAmount").GetInt64()));

        await using (var db = host.CreateDbContext())
        {
            Assert.Equal(AssignmentStatus.Excluded, await db.ChargeAssignments.Where(item => item.UserId == lazy.Id).Select(item => item.Status).SingleAsync());
            Assert.True(await db.Notifications.AnyAsync(item => item.UserId == lazy.Id && item.Type == NotificationType.EventParticipantExcluded));
            Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == setup.Fund.Id && log.Action == "event.close_list" && log.EventId == eventId));
        }

        // Học sinh không được mời (thêm sau khi sự kiện đã đóng danh sách) → 404.
        var stranger = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(setup.Fund, stranger);
        Assert.Equal(HttpStatusCode.NotFound, (await (await host.LoginAsync(stranger)).GetAsync(Events(setup.Fund.Id, $"/{eventId}"))).StatusCode);
    }

    [Fact]
    public async Task Closing_the_list_before_the_deadline_needs_confirm_early()
    {
        var setup = await CreateFundAsync();
        var owner = await host.LoginAsync(setup.Owner);
        var eventId = (await (await owner.PostAsJsonAsync(Events(setup.Fund.Id), NewEvent("Cắm trại", null, 5))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var preview = await owner.GetFromJsonAsync<JsonElement>(Events(setup.Fund.Id, $"/{eventId}/close-list-preview"));
        Assert.True(preview.GetProperty("beforeDeadline").GetBoolean());
        var refused = await owner.PostAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/close-list"), new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("event.list_deadline_not_reached", await CodeOf(refused));

        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/close-list"), new { confirmEarly = true })).StatusCode);
    }

    [Fact]
    public async Task Write_apis_record_audit_with_scope_TD14_checklist()
    {
        var setup = await CreateFundAsync();
        var owner = await host.LoginAsync(setup.Owner);
        await SeedIncomeAsync(setup, 500_000);

        var created = await owner.PostAsJsonAsync(Events(setup.Fund.Id), NewEvent("Văn nghệ"));
        var eventId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await owner.PutAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}"), NewEvent("Văn nghệ 20/11"));
        await owner.PutAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/me/response"), new { response = "Accepted" });
        await owner.PostAsJsonAsync(Events(setup.Fund.Id, $"/{eventId}/close-list"), new { });

        var other = await owner.PostAsJsonAsync(Events(setup.Fund.Id), NewEvent("Cắm trại", null, 5));
        var otherId = (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync(Events(setup.Fund.Id, $"/{otherId}/cancel"), new { reason = "Thời tiết" });

        var expense = await owner.PostAsJsonAsync(Ledger(setup.Fund.Id), Expense(setup, 100_000));
        var entryId = (await expense.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("entry").GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync(Ledger(setup.Fund.Id, $"/{entryId}/reverse"), new { reason = "Sai" });

        await using var db = host.CreateDbContext();
        var logs = await db.AuditLogs.Where(log => log.FundId == setup.Fund.Id).ToListAsync();
        string[] eventActions = ["event.create", "event.update", "event.respond", "event.close_list", "event.cancel"];
        Assert.All(eventActions, action => Assert.Contains(logs, log => log.Action == action && log.Scope == FinancialScope.Event && log.EventId != null));
        Assert.Contains(logs, log => log.Action == "ledger.create" && log.Scope == FinancialScope.General && log.EventId == null);
        Assert.Contains(logs, log => log.Action == "ledger.reverse" && log.Note == "Sai");
        Assert.Contains(logs, log => log.Action == "event.cancel" && log.Note == "Thời tiết");
    }
}
