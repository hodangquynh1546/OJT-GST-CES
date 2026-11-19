using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>M6a-T4 – Khoản thu chung và thanh toán: mã trạng thái, phân quyền, vòng đời Submitted → Confirmed/Rejected, tự ghi nhận.</summary>
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

    private static Task<HttpResponseMessage> PayAsync(HttpClient client, Guid fundId, Guid assignmentId, object body) =>
        client.PostAsJsonAsync($"{Base(fundId)}/me/assignments/{assignmentId}/payments", body);

    // ---------- 401 / 403 / 404 / 400 ----------

    [Fact]
    public async Task Anonymous_gets_401_and_outsiders_404()
    {
        var s = await SetupAsync();
        var anonymous = host.Anonymous();
        var stranger = await host.LoginAsync(await host.CreateUserAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Base(s.FundId)}/charges")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Base(s.FundId)}/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base(s.FundId)}/charges")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base(s.FundId)}/me/assignments")).StatusCode);
    }

    [Fact]
    public async Task Students_cannot_create_close_cancel_list_assignments_or_handle_payments()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        var paymentId = await SubmitAsync(s.StudentClient, s.FundId);

        var responses = new[]
        {
            await s.OtherClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", NewCharge(s.CategoryId)),
            await s.OtherClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/close", null),
            await s.OtherClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/cancel", null),
            await s.OtherClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments"),
            await s.OtherClient.GetAsync($"{Base(s.FundId)}/payments"),
            await s.OtherClient.PostAsync($"{Base(s.FundId)}/payments/{paymentId}/confirm", null),
            await s.OtherClient.PostAsJsonAsync($"{Base(s.FundId)}/payments/{paymentId}/reject", new { reason = "x" }),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    private async Task<Guid> SubmitAsync(HttpClient client, Guid fundId)
    {
        var assignmentId = await AssignmentIdAsync(client, fundId);
        var response = await PayAsync(client, fundId, assignmentId, new { method = "Cash" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
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
    public async Task Create_needs_a_usable_income_category_and_the_confirmed_payment_carries_it()
    {
        var s = await SetupAsync();
        var missing = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", new { title = "A", amountPerPerson = 1000, dueDate = "2099-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var unknown = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", NewCharge(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal("category.unusable", await CodeOfAsync(unknown));

        await CreateChargeAsync(s);
        var paymentId = await SubmitAsync(s.StudentClient, s.FundId);
        await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/payments/{paymentId}/confirm", null);

        await using var db = host.CreateDbContext();
        Assert.Equal(s.CategoryId, (await db.LedgerEntries.SingleAsync(item => item.PaymentId == paymentId)).CategoryId);
        var list = (await ReadAsync(await s.StudentClient.GetAsync($"{Base(s.FundId)}/charges"))).GetProperty("items")[0];
        Assert.Equal("Quỹ lớp", list.GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task Create_with_a_past_due_date_is_422()
    {
        var s = await SetupAsync();

        var response = await s.OwnerClient.PostAsJsonAsync($"{Base(s.FundId)}/charges", new { title = "A", amountPerPerson = 1000, dueDate = "2020-01-01", categoryId = s.CategoryId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("charge.due_date_in_past", await CodeOfAsync(response));
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
        var paymentId = await SubmitAsync(s.StudentClient, s.FundId);
        await PayAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.TreasurerClient, s.FundId), new { method = "Cash" });

        var all = (await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments"))).GetProperty("items").EnumerateArray().ToArray();
        var awaiting = (await ReadAsync(await s.OwnerClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments?status=AwaitingConfirmation"))).GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(4, all.Length);
        var row = Assert.Single(awaiting);
        Assert.Equal(paymentId, row.GetProperty("paymentId").GetGuid());
        Assert.Equal("Cash", row.GetProperty("method").GetString());
        Assert.Equal("Student", row.GetProperty("role").GetString());
        var self = all.Single(item => item.GetProperty("userId").GetGuid() == s.Treasurer.Id);
        Assert.True(self.GetProperty("isSelfDeclared").GetBoolean());
        Assert.Equal("Paid", self.GetProperty("status").GetString());
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
    public async Task The_treasurer_records_cash_for_a_student_and_can_remind_the_rest()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        var rows = (await ReadAsync(await s.TreasurerClient.GetAsync($"{Base(s.FundId)}/charges/{chargeId}/assignments"))).GetProperty("items").EnumerateArray().ToArray();
        var studentRow = rows.Single(item => item.GetProperty("userId").GetGuid() == s.Student.Id);
        var assignmentId = studentRow.GetProperty("assignmentId").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await s.StudentClient.PostAsync($"{Base(s.FundId)}/assignments/{assignmentId}/record-cash", null)).StatusCode);
        var record = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/assignments/{assignmentId}/record-cash", null);
        Assert.Equal(HttpStatusCode.Created, record.StatusCode);
        Assert.False((await ReadAsync(record)).GetProperty("isSelfDeclared").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/assignments/{assignmentId}/record-cash", null)).StatusCode);

        var remind = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/remind", null);
        Assert.Equal(HttpStatusCode.OK, remind.StatusCode);
        Assert.Equal(3, (await ReadAsync(remind)).GetProperty("reminded").GetInt32()); // 4 người - 1 đã đóng
        var again = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/remind", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("charge.remind_too_soon", await CodeOfAsync(again));
        Assert.Equal(HttpStatusCode.Forbidden, (await s.StudentClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/remind", null)).StatusCode);

        await using var db = host.CreateDbContext();
        Assert.True(await db.LedgerEntries.AnyAsync(item => item.FundId == s.FundId && item.Amount == 50_000 && !item.IsSelfDeclared));
        Assert.Equal(3, await db.Notifications.CountAsync(item => item.FundId == s.FundId && item.Type == NotificationType.ChargeDueSoon));
    }

    [Fact]
    public async Task Close_then_close_again_is_409_and_a_closed_charge_takes_no_payments()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);

        Assert.Equal(HttpStatusCode.OK, (await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/close", null)).StatusCode);
        var again = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/close", null);
        var pay = await PayAsync(s.StudentClient, s.FundId, await AssignmentIdAsync(s.StudentClient, s.FundId), new { method = "Cash" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, pay.StatusCode);
        Assert.Equal("payment.charge_not_open", await CodeOfAsync(pay));
    }

    [Fact]
    public async Task Cancel_is_blocked_after_a_payment_with_422()
    {
        var s = await SetupAsync();
        var chargeId = await CreateChargeAsync(s);
        await SubmitAsync(s.StudentClient, s.FundId);

        var response = await s.OwnerClient.PostAsync($"{Base(s.FundId)}/charges/{chargeId}/cancel", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("charge.has_payments", await CodeOfAsync(response));
    }

    // ---------- Thanh toán ----------

    [Fact]
    public async Task A_student_submits_a_treasurer_confirms_once_and_one_income_entry_is_written()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var paymentId = await SubmitAsync(s.StudentClient, s.FundId);

        var duplicate = await PayAsync(s.StudentClient, s.FundId, await AssignmentIdAsync(s.StudentClient, s.FundId), new { method = "Cash" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("payment.not_payable", await CodeOfAsync(duplicate));

        var queue = (await ReadAsync(await s.TreasurerClient.GetAsync($"{Base(s.FundId)}/payments"))).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(paymentId, Assert.Single(queue).GetProperty("id").GetGuid());

        var confirm = await s.TreasurerClient.PostAsync($"{Base(s.FundId)}/payments/{paymentId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("Confirmed", (await ReadAsync(confirm)).GetProperty("status").GetString());

        var second = await s.OwnerClient.PostAsync($"{Base(s.FundId)}/payments/{paymentId}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("payment.already_processed", await CodeOfAsync(second));

        await using var db = host.CreateDbContext();
        var entry = await db.LedgerEntries.SingleAsync(item => item.PaymentId == paymentId);
        Assert.Equal((LedgerType.Income, LedgerSource.Payment, false, 50_000L), (entry.Type, entry.Source, entry.IsSelfDeclared, entry.Amount));
        Assert.Equal(AssignmentStatus.Paid, (await db.ChargeAssignments.SingleAsync(item => item.UserId == s.Student.Id)).Status);
        Assert.True(await db.Notifications.AnyAsync(item => item.UserId == s.Student.Id && item.Type == NotificationType.PaymentConfirmed));
    }

    [Fact]
    public async Task A_treasurer_paying_for_themselves_is_self_declared_in_the_ledger_and_audit()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);

        var response = await PayAsync(s.TreasurerClient, s.FundId, await AssignmentIdAsync(s.TreasurerClient, s.FundId), new { method = "Cash" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("isSelfDeclared").GetBoolean());
        await using var db = host.CreateDbContext();
        Assert.True((await db.LedgerEntries.SingleAsync(item => item.PaymentId == body.GetProperty("id").GetGuid())).IsSelfDeclared);
        Assert.True(await db.AuditLogs.AnyAsync(item => item.Action == "payment.confirm" && item.IsSelfAction && item.ActorUserId == s.Treasurer.Id));
    }

    [Fact]
    public async Task Bank_transfer_needs_proof_and_someone_elses_obligation_is_404()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var mine = await AssignmentIdAsync(s.StudentClient, s.FundId);
        var theirs = await AssignmentIdAsync(s.OtherClient, s.FundId);

        var noProof = await PayAsync(s.StudentClient, s.FundId, mine, new { method = "BankTransfer", transferCode = "FT1" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noProof.StatusCode);
        Assert.Equal("payment.proof_required", await CodeOfAsync(noProof));
        Assert.Equal(HttpStatusCode.NotFound, (await PayAsync(s.StudentClient, s.FundId, theirs, new { method = "Cash" })).StatusCode);

        var upload = await s.StudentClient.PostAsync($"{Base(s.FundId)}/attachments", ApiTestHost.Multipart(ApiTestHost.Png, "bill.png", "image/png", "PaymentProof"));
        var proofId = (await ReadAsync(upload)).GetProperty("id").GetGuid();
        var ok = await PayAsync(s.StudentClient, s.FundId, mine, new { method = "BankTransfer", transferCode = "FT1", attachmentId = proofId });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

        // Thủ quỹ thấy ảnh minh chứng trong hàng chờ và tải được; học sinh khác thì không.
        var item = (await ReadAsync(await s.TreasurerClient.GetAsync($"{Base(s.FundId)}/payments"))).GetProperty("items").EnumerateArray().Single();
        Assert.Equal(proofId, item.GetProperty("attachments")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await s.TreasurerClient.GetAsync($"{Base(s.FundId)}/attachments/{proofId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OtherClient.GetAsync($"{Base(s.FundId)}/attachments/{proofId}")).StatusCode);
    }

    [Fact]
    public async Task Reject_needs_a_reason_then_the_student_can_pay_again()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s);
        var paymentId = await SubmitAsync(s.StudentClient, s.FundId);

        var blank = await s.TreasurerClient.PostAsJsonAsync($"{Base(s.FundId)}/payments/{paymentId}/reject", new { reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var reject = await s.TreasurerClient.PostAsJsonAsync($"{Base(s.FundId)}/payments/{paymentId}/reject", new { reason = "Chưa nhận được tiền" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var mine = (await ReadAsync(await s.StudentClient.GetAsync($"{Base(s.FundId)}/me/assignments"))).EnumerateArray().Single();
        Assert.Equal("Unpaid", mine.GetProperty("status").GetString());
        Assert.Equal("Chưa nhận được tiền", mine.GetProperty("rejectionReason").GetString());

        // Chỉ mục duy nhất một lần nộp đang hoạt động không chặn lần nộp lại sau khi bị từ chối (BR-14).
        Assert.Equal(HttpStatusCode.Created, (await PayAsync(s.StudentClient, s.FundId, mine.GetProperty("assignmentId").GetGuid(), new { method = "Cash" })).StatusCode);
        await using var db = host.CreateDbContext();
        Assert.Equal(0, await db.LedgerEntries.CountAsync(item => item.FundId == s.FundId));
    }
}
