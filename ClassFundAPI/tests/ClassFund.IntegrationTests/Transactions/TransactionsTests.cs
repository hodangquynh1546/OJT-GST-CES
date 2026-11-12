using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Transactions;

/// <summary>
/// M6 – Thu chi qua HTTP thật (module-plan.md mục M6; FR-02 → FR-07, BR-01 → BR-07, BR-11, BR-12, BR-14, BR-20, BR-22,
/// NFR-03, NFR-05, NFR-08, NFR-09; test case T-04, T-06, TR-02, TR-08, S-07, S-08, P-06, P-08, G-04, R-01, M-02).
/// Quỹ tạo bằng <c>POST /funds</c> (M2) để có 6 danh mục mặc định; thủ quỹ, học sinh, phụ huynh seed bằng <see cref="TestData"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TransactionsTests(PostgresFixture fixture)
{
    private readonly TestData _data = fixture.Data;

    private sealed record Ledger(
        Guid FundId,
        SeededFund Fund,
        ApiClient Teacher,
        ApiClient Treasurer,
        Guid TreasurerUserId,
        Dictionary<string, Guid> Categories);

    private static DateOnly Today => VietnamTime.DateOf(DateTimeOffset.UtcNow);

    // ---------- POST /transactions ----------

    [Fact]
    public async Task Treasurer_records_income_and_expense_with_full_details_and_audit()
    {
        var ledger = await LedgerAsync();

        var income = await CreateAsync(ledger, "Income", 3_200_000, "Đóng quỹ", "Đóng quỹ tháng 11", note: "  Còn 2 phụ huynh  ");
        Assert.Equal("Income", income.GetProperty("type").GetString());
        Assert.Equal(3_200_000, income.GetProperty("amount").GetInt64());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), income.GetProperty("occurredOn").GetString());
        Assert.Equal("Đóng quỹ", income.GetProperty("categoryName").GetString());
        Assert.Equal("Đóng quỹ tháng 11", income.GetProperty("description").GetString());
        Assert.Equal("Còn 2 phụ huynh", income.GetProperty("note").GetString());
        Assert.True(income.GetProperty("isPublic").GetBoolean());
        Assert.False(income.GetProperty("isCancelled").GetBoolean());
        Assert.Equal("Nguyễn Minh Khang", income.GetProperty("createdByName").GetString());
        Assert.False(income.GetProperty("replayed").GetBoolean());
        Assert.False(income.GetProperty("isNegativeBalance").GetBoolean());

        var expense = await CreateAsync(ledger, "Expense", 180_000, "Văn phòng phẩm", "Mua giấy A4", isPublic: false);
        Assert.False(expense.GetProperty("isPublic").GetBoolean());
        Assert.False(expense.TryGetProperty("note", out _));

        var list = await ListAsync(ledger.Treasurer, ledger.FundId);
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal(1, list.GetProperty("page").GetInt32());
        Assert.Equal(20, list.GetProperty("pageSize").GetInt32());

        var audit = await AuditAsync(Id(income));
        var created = Assert.Single(audit);
        Assert.Equal("Transaction.Create", created.Action);
        Assert.Equal(ledger.TreasurerUserId, created.ActorUserId);
        Assert.Contains("Đóng quỹ tháng 11", created.NewValues);

        // Số dư = tổng thu − tổng chi đang hiệu lực (BR-02).
        Assert.Equal(3_020_000, await BalanceAsync(ledger));
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_saved_transaction_even_for_concurrent_requests()
    {
        var ledger = await LedgerAsync();
        var key = Guid.NewGuid();
        var body = Body(ledger, "Expense", 65_000, "Văn phòng phẩm", "Băng dính, kéo", key: key);

        var first = await ledger.Treasurer.PostAsync($"/funds/{ledger.FundId}/transactions", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var again = await ledger.Treasurer.PostAsync($"/funds/{ledger.FundId}/transactions", body);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var replayed = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replayed.GetProperty("replayed").GetBoolean());
        Assert.Equal(Id(await first.Content.ReadFromJsonAsync<JsonElement>()), Id(replayed));

        // R-01: 5 request đồng thời cùng một khóa mới → đúng một khoản.
        var concurrentKey = Guid.NewGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            ledger.Treasurer.PostAsync($"/funds/{ledger.FundId}/transactions", Body(ledger, "Income", 100_000, "Thu khác", "Ủng hộ", key: concurrentKey))));
        Assert.All(responses, response => Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(2, (await ListAsync(ledger.Treasurer, ledger.FundId)).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Expense_larger_than_balance_is_saved_with_negative_balance_warning()
    {
        var ledger = await LedgerAsync();
        await CreateAsync(ledger, "Income", 100_000, "Đóng quỹ", "Đóng quỹ");

        var expense = await CreateAsync(ledger, "Expense", 250_000, "Chi khác", "Mua bóng đá");

        Assert.True(expense.GetProperty("isNegativeBalance").GetBoolean());
        Assert.Equal(-150_000, await BalanceAsync(ledger));
    }

    [Fact]
    public async Task Create_validates_fields_date_and_category()
    {
        var ledger = await LedgerAsync();
        var url = $"/funds/{ledger.FundId}/transactions";

        var invalid = await (await ledger.Treasurer.PostAsync(url, new
        {
            type = "Expense",
            amount = 0,
            occurredOn = Today,
            categoryId = ledger.Categories["Chi khác"],
            description = "   ",
            note = new string('a', 1001),
        })).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var errors = invalid.GetProperty("errors");
        Assert.Equal(["Số tiền phải lớn hơn 0."], Strings(errors.GetProperty("amount")));
        Assert.Equal(["Nhập nội dung."], Strings(errors.GetProperty("description")));
        Assert.Equal(["Ghi chú tối đa 1000 ký tự."], Strings(errors.GetProperty("note")));
        Assert.Equal(["Thiếu khóa chống lưu trùng."], Strings(errors.GetProperty("idempotencyKey")));

        var tooLong = await (await ledger.Treasurer.PostAsync(url, Body(ledger, "Expense", 1, "Chi khác", new string('a', 201))))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(["Nội dung tối đa 200 ký tự."], Strings(tooLong.GetProperty("errors").GetProperty("description")));

        // BR-07: ngày theo giờ Việt Nam; hôm nay được, ngày mai không.
        await (await ledger.Treasurer.PostAsync(url, Body(ledger, "Expense", 1, "Chi khác", "Ngày mai", Today.AddDays(1))))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.future_date");

        // Danh mục khác loại, của quỹ khác, đã ẩn → transaction.category_invalid.
        await (await ledger.Treasurer.PostAsync(url, Body(ledger, "Income", 1, "Chi khác", "Sai loại")))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.category_invalid");
        var other = await LedgerAsync();
        await (await ledger.Treasurer.PostAsync(url, new
        {
            type = "Expense", amount = 1, occurredOn = Today, categoryId = other.Categories["Chi khác"],
            description = "Danh mục quỹ khác", idempotencyKey = Guid.NewGuid(),
        })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.category_invalid");
        await HideCategoryAsync(ledger.Categories["Khen thưởng"]);
        await (await ledger.Treasurer.PostAsync(url, Body(ledger, "Expense", 1, "Khen thưởng", "Danh mục ẩn")))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.category_invalid");

        Assert.Equal(0, (await ListAsync(ledger.Treasurer, ledger.FundId)).GetProperty("total").GetInt32());
    }

    // ---------- PUT /transactions/{id} ----------

    [Fact]
    public async Task Update_keeps_type_records_updated_by_and_old_new_values()
    {
        var ledger = await LedgerAsync();
        var created = await CreateAsync(ledger, "Expense", 1_200_000, "Sinh hoạt, liên hoan", "Liên hoan 20/11");
        await HideCategoryAsync(ledger.Categories["Sinh hoạt, liên hoan"]);

        // Gửi kèm "type" cũng không đổi được loại (BR-05); danh mục đã ẩn đang dùng vẫn giữ được.
        var response = await ledger.Treasurer.PutAsync($"/funds/{ledger.FundId}/transactions/{Id(created)}", new
        {
            type = "Income",
            amount = 1_250_000,
            occurredOn = Today.AddDays(-1),
            categoryId = ledger.Categories["Sinh hoạt, liên hoan"],
            description = "Liên hoan 20/11 ",
            note = "Bánh, nước, trang trí lớp",
            isPublic = false,
        });
        var updated = await ReadAsync(response, HttpStatusCode.OK);

        Assert.Equal("Expense", updated.GetProperty("type").GetString());
        Assert.Equal(1_250_000, updated.GetProperty("amount").GetInt64());
        Assert.Equal("Liên hoan 20/11", updated.GetProperty("description").GetString());
        Assert.False(updated.GetProperty("isPublic").GetBoolean());
        Assert.Equal("Nguyễn Minh Khang", updated.GetProperty("updatedByName").GetString());
        Assert.True(updated.TryGetProperty("updatedAt", out _));

        var update = Assert.Single(await AuditAsync(Id(created)), log => log.Action == "Transaction.Update");
        Assert.Contains("1200000", update.OldValues);
        Assert.Contains("1250000", update.NewValues);

        // Đổi sang danh mục ẩn khác thì không được.
        await HideCategoryAsync(ledger.Categories["Khen thưởng"]);
        await (await ledger.Treasurer.PutAsync($"/funds/{ledger.FundId}/transactions/{Id(created)}", UpdateBody(ledger, 1, "Khen thưởng")))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.category_invalid");
    }

    [Fact]
    public async Task Update_validates_and_unchanged_values_write_no_audit()
    {
        var ledger = await LedgerAsync();
        var created = await CreateAsync(ledger, "Income", 500_000, "Thu khác", "Ủng hộ");
        var url = $"/funds/{ledger.FundId}/transactions/{Id(created)}";

        var missing = await (await ledger.Treasurer.PutAsync(url, new { amount = -1, categoryId = ledger.Categories["Thu khác"], description = "x" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(missing.GetProperty("errors").TryGetProperty("occurredOn", out _));
        Assert.True(missing.GetProperty("errors").TryGetProperty("isPublic", out _));
        await (await ledger.Treasurer.PutAsync(url, UpdateBody(ledger, 500_000, "Thu khác", "Ủng hộ", Today.AddDays(1))))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.future_date");

        await ReadAsync(await ledger.Treasurer.PutAsync(url, UpdateBody(ledger, 500_000, "Thu khác", "Ủng hộ")), HttpStatusCode.OK);
        Assert.DoesNotContain(await AuditAsync(Id(created)), log => log.Action == "Transaction.Update");
    }

    // ---------- POST /transactions/{id}/cancel ----------

    [Fact]
    public async Task Cancel_requires_reason_records_cancelled_by_and_cannot_be_repeated_or_edited()
    {
        var ledger = await LedgerAsync();
        await CreateAsync(ledger, "Income", 1_000_000, "Đóng quỹ", "Đóng quỹ");
        var expense = await CreateAsync(ledger, "Expense", 250_000, "Chi khác", "Mua bóng đá");
        var url = $"/funds/{ledger.FundId}/transactions/{Id(expense)}";

        var missing = await (await ledger.Treasurer.PostAsync($"{url}/cancel", new { reason = "  " }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(["Nhập lý do hủy."], Strings(missing.GetProperty("errors").GetProperty("reason")));
        var tooLong = await (await ledger.Treasurer.PostAsync($"{url}/cancel", new { reason = new string('a', 501) }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(["Lý do hủy tối đa 500 ký tự."], Strings(tooLong.GetProperty("errors").GetProperty("reason")));

        var cancelled = await ReadAsync(await ledger.Treasurer.PostAsync($"{url}/cancel", new { reason = " Nhập nhầm số tiền " }), HttpStatusCode.OK);
        Assert.True(cancelled.GetProperty("isCancelled").GetBoolean());
        Assert.Equal("Nhập nhầm số tiền", cancelled.GetProperty("cancelReason").GetString());
        Assert.Equal("Nguyễn Minh Khang", cancelled.GetProperty("cancelledByName").GetString());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), cancelled.GetProperty("cancelledOn").GetString());
        Assert.True(cancelled.TryGetProperty("cancelledAt", out _));
        Assert.False(cancelled.GetProperty("isNegativeBalance").GetBoolean());
        Assert.Equal(1_000_000, await BalanceAsync(ledger));

        await (await ledger.Treasurer.PostAsync($"{url}/cancel", new { reason = "Lần hai" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.cancelled");
        await (await ledger.Treasurer.PutAsync(url, UpdateBody(ledger, 1, "Chi khác")))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.cancelled");

        // Mặc định chỉ khoản đang hiệu lực; lọc Đã hủy / Tất cả.
        Assert.Equal(1, (await ListAsync(ledger.Treasurer, ledger.FundId)).GetProperty("total").GetInt32());
        var onlyCancelled = await ListAsync(ledger.Treasurer, ledger.FundId, "status=Cancelled");
        Assert.Equal(Id(expense), Id(Assert.Single(onlyCancelled.GetProperty("items").EnumerateArray())));
        Assert.Equal(2, (await ListAsync(ledger.Treasurer, ledger.FundId, "status=All")).GetProperty("total").GetInt32());

        var log = Assert.Single(await AuditAsync(Id(expense)), item => item.Action == "Transaction.Cancel");
        Assert.Contains("Nhập nhầm số tiền", log.NewValues);
    }

    [Fact]
    public async Task Concurrent_cancels_let_exactly_one_succeed_and_concurrent_creates_keep_balance()
    {
        var ledger = await LedgerAsync();
        var second = await _data.CreateStudentAccountAsync(ledger.Fund, "Phạm Thu Trang", treasurer: true);
        using var secondClient = await SignInAsync(second.UserId);
        var expense = await CreateAsync(ledger, "Expense", 100_000, "Chi khác", "Khoản sẽ hủy");
        var url = $"/funds/{ledger.FundId}/transactions/{Id(expense)}/cancel";

        var cancels = await Task.WhenAll(
            ledger.Treasurer.PostAsync(url, new { reason = "Thủ quỹ 1" }),
            secondClient.PostAsync(url, new { reason = "Thủ quỹ 2" }));
        Assert.Single(cancels, response => response.StatusCode == HttpStatusCode.OK);
        await Assert.Single(cancels, response => response.StatusCode != HttpStatusCode.OK)
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "transaction.cancelled");
        Assert.Single(await AuditAsync(Id(expense)), log => log.Action == "Transaction.Cancel");

        // NFR-09: hai thủ quỹ ghi đồng thời không làm sai số dư.
        await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
            (index % 2 == 0 ? ledger.Treasurer : secondClient).PostAsync(
                $"/funds/{ledger.FundId}/transactions", Body(ledger, "Income", 10_000, "Đóng quỹ", $"Đóng quỹ {index}"))));
        Assert.Equal(100_000, await BalanceAsync(ledger));
    }

    // ---------- GET /transactions/{id}/history ----------

    [Fact]
    public async Task History_lists_create_update_cancel_with_actor_and_values_for_teacher_and_treasurer()
    {
        var ledger = await LedgerAsync();
        var created = await CreateAsync(ledger, "Expense", 1_200_000, "Sinh hoạt, liên hoan", "Liên hoan");
        var url = $"/funds/{ledger.FundId}/transactions/{Id(created)}";
        await ReadAsync(await ledger.Treasurer.PutAsync(url, UpdateBody(ledger, 1_250_000, "Sinh hoạt, liên hoan", "Liên hoan")), HttpStatusCode.OK);
        await ReadAsync(await ledger.Treasurer.PostAsync($"{url}/cancel", new { reason = "Hoãn liên hoan" }), HttpStatusCode.OK);

        foreach (var client in new[] { ledger.Treasurer, ledger.Teacher })
        {
            var history = (await ReadAsync(await client.GetAsync($"{url}/history"), HttpStatusCode.OK)).EnumerateArray().ToArray();
            Assert.Equal(["Transaction.Create", "Transaction.Update", "Transaction.Cancel"], history.Select(item => item.GetProperty("action").GetString()));
            Assert.All(history, item => Assert.Equal("Nguyễn Minh Khang", item.GetProperty("actorName").GetString()));
            Assert.Equal(JsonValueKind.Null, history[0].GetProperty("oldValues").ValueKind);
            Assert.Equal(1_200_000, history[0].GetProperty("newValues").GetProperty("amount").GetInt64());
            Assert.Equal(1_200_000, history[1].GetProperty("oldValues").GetProperty("amount").GetInt64());
            Assert.Equal(1_250_000, history[1].GetProperty("newValues").GetProperty("amount").GetInt64());
            Assert.Equal("Hoãn liên hoan", history[2].GetProperty("newValues").GetProperty("reason").GetString());
        }
    }

    // ---------- Vai trò và phạm vi ----------

    [Fact]
    public async Task Teacher_sees_full_details_but_cannot_record_update_or_cancel()
    {
        var ledger = await LedgerAsync();
        var created = await CreateAsync(ledger, "Expense", 500_000, "Khen thưởng", "Quà 20/11", isPublic: false, note: "Có hóa đơn");
        var url = $"/funds/{ledger.FundId}/transactions/{Id(created)}";
        await ReadAsync(await ledger.Treasurer.PostAsync($"{url}/cancel", new { reason = "Ghi trùng" }), HttpStatusCode.OK);

        var detail = await ReadAsync(await ledger.Teacher.GetAsync(url), HttpStatusCode.OK);
        Assert.Equal("Có hóa đơn", detail.GetProperty("note").GetString());
        Assert.False(detail.GetProperty("isPublic").GetBoolean());
        Assert.Equal("Ghi trùng", detail.GetProperty("cancelReason").GetString());
        Assert.Equal("Nguyễn Minh Khang", detail.GetProperty("createdByName").GetString());
        Assert.Equal("Nguyễn Minh Khang", detail.GetProperty("cancelledByName").GetString());

        await (await ledger.Teacher.PostAsync($"/funds/{ledger.FundId}/transactions", Body(ledger, "Income", 1, "Đóng quỹ", "GVCN ghi")))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await ledger.Teacher.PutAsync(url, UpdateBody(ledger, 1, "Khen thưởng"))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await ledger.Teacher.PostAsync($"{url}/cancel", new { reason = "GVCN hủy" })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Student_and_parent_see_only_public_transactions_without_internal_fields()
    {
        var ledger = await LedgerAsync();
        var student = await _data.CreateStudentAccountAsync(ledger.Fund, "Lê Gia Bảo", activated: true);
        var parent = await _data.CreateParentAccountAsync(ledger.Fund, student.StudentId!.Value);
        await ActivateAsync(parent.UserId);

        var publicIncome = await CreateAsync(ledger, "Income", 3_000_000, "Đóng quỹ", "Đóng quỹ tháng 10", note: "Bí mật nội bộ");
        var privateGift = await CreateAsync(ledger, "Expense", 500_000, "Khen thưởng", "Quà tặng cô giáo", isPublic: false, note: "Hoa và phong bì");
        var publicCancelled = await CreateAsync(ledger, "Expense", 90_000, "Chi khác", "Phô tô đề cương");
        await ReadAsync(await ledger.Treasurer.PostAsync($"/funds/{ledger.FundId}/transactions/{Id(publicCancelled)}/cancel", new { reason = "Trường phô tô miễn phí" }), HttpStatusCode.OK);

        foreach (var userId in new[] { student.UserId, parent.UserId })
        {
            using var client = await SignInAsync(userId);
            var all = await ListAsync(client, ledger.FundId, "status=All");
            Assert.Equal(2, all.GetProperty("total").GetInt32());
            var items = all.GetProperty("items").EnumerateArray().ToArray();
            Assert.DoesNotContain(items, item => Id(item) == Id(privateGift));
            string[] publicFields = ["id", "type", "amount", "occurredOn", "categoryId", "categoryName", "description", "isCancelled"];
            foreach (var item in items)
            {
                var expected = item.GetProperty("isCancelled").GetBoolean() ? [.. publicFields, "cancelledOn"] : publicFields;
                Assert.Equal(expected, item.EnumerateObject().Select(property => property.Name));
            }

            var cancelled = items.Single(item => Id(item) == Id(publicCancelled));
            Assert.True(cancelled.GetProperty("isCancelled").GetBoolean());
            Assert.Equal(Today.ToString("yyyy-MM-dd"), cancelled.GetProperty("cancelledOn").GetString());

            // Tìm chỉ trong nội dung: ghi chú (kể cả của khoản công khai) không tìm được.
            Assert.Equal(0, (await ListAsync(client, ledger.FundId, "search=" + Uri.EscapeDataString("Bí mật"))).GetProperty("total").GetInt32());
            Assert.Equal(0, (await ListAsync(client, ledger.FundId, "search=" + Uri.EscapeDataString("Quà tặng"))).GetProperty("total").GetInt32());
            Assert.Equal(1, (await ListAsync(client, ledger.FundId, "search=" + Uri.EscapeDataString("đóng QUỸ"))).GetProperty("total").GetInt32());

            var detail = await ReadAsync(await client.GetAsync($"/funds/{ledger.FundId}/transactions/{Id(publicIncome)}"), HttpStatusCode.OK);
            Assert.False(detail.TryGetProperty("note", out _));
            Assert.False(detail.TryGetProperty("createdByName", out _));
            await (await client.GetAsync($"/funds/{ledger.FundId}/transactions/{Id(privateGift)}")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
            await (await client.GetAsync($"/funds/{ledger.FundId}/transactions/{Id(publicIncome)}/history")).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PostAsync($"/funds/{ledger.FundId}/transactions", Body(ledger, "Income", 1, "Đóng quỹ", "Ghi hộ")))
                .AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PostAsync($"/funds/{ledger.FundId}/transactions/{Id(publicIncome)}/cancel", new { reason = "Hủy hộ" }))
                .AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        // Treasurer tìm được theo ghi chú (bản đầy đủ).
        Assert.Equal(1, (await ListAsync(ledger.Treasurer, ledger.FundId, "search=" + Uri.EscapeDataString("phong bì"))).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_filters_by_type_category_dates_and_pages_with_total_after_filter()
    {
        var ledger = await LedgerAsync();
        var lastMonth = Today.AddMonths(-1);
        await CreateAsync(ledger, "Income", 4_800_000, "Đóng quỹ", "Đóng quỹ đầu năm", occurredOn: lastMonth);
        for (var index = 1; index <= 3; index++)
        {
            await CreateAsync(ledger, "Expense", index * 10_000, "Văn phòng phẩm", $"Văn phòng phẩm {index}", occurredOn: Today.AddDays(-index + 1));
        }

        await CreateAsync(ledger, "Expense", 300_000, "Chi khác", "Chi khác");

        var expenses = await ListAsync(ledger.Treasurer, ledger.FundId, "type=Expense");
        Assert.Equal(4, expenses.GetProperty("total").GetInt32());
        var stationery = await ListAsync(ledger.Treasurer, ledger.FundId, $"categoryId={ledger.Categories["Văn phòng phẩm"]}");
        Assert.Equal(3, stationery.GetProperty("total").GetInt32());
        var range = await ListAsync(ledger.Treasurer, ledger.FundId, $"from={lastMonth:yyyy-MM-dd}&to={lastMonth:yyyy-MM-dd}");
        Assert.Equal("Đóng quỹ đầu năm", Assert.Single(range.GetProperty("items").EnumerateArray()).GetProperty("description").GetString());

        // Mới nhất trước (ngày giao dịch, rồi thời điểm ghi); trang 2 với pageSize 2.
        var page2 = await ListAsync(ledger.Treasurer, ledger.FundId, "page=2&pageSize=2");
        Assert.Equal(5, page2.GetProperty("total").GetInt32());
        Assert.Equal(2, page2.GetProperty("page").GetInt32());
        Assert.Equal(
            ["Văn phòng phẩm 2", "Văn phòng phẩm 3"],
            page2.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("description").GetString()));

        var invalid = await (await ledger.Treasurer.GetAsync($"/funds/{ledger.FundId}/transactions?page=0&pageSize=101&from={Today:yyyy-MM-dd}&to={lastMonth:yyyy-MM-dd}"))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(invalid.GetProperty("errors").TryGetProperty("page", out _));
        Assert.True(invalid.GetProperty("errors").TryGetProperty("pageSize", out _));
        Assert.True(invalid.GetProperty("errors").TryGetProperty("to", out _));
    }

    [Fact]
    public async Task Treasurer_of_another_fund_cannot_reach_this_funds_transactions()
    {
        var ledgerA = await LedgerAsync();
        var ledgerB = await LedgerAsync();
        var created = await CreateAsync(ledgerA, "Income", 100_000, "Đóng quỹ", "Quỹ A");
        var id = Id(created);

        await (await ledgerB.Treasurer.GetAsync($"/funds/{ledgerA.FundId}/transactions")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await ledgerB.Treasurer.PostAsync($"/funds/{ledgerA.FundId}/transactions", Body(ledgerA, "Income", 1, "Đóng quỹ", "Ghi sang quỹ A")))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        foreach (var path in new[] { $"/funds/{ledgerB.FundId}/transactions/{id}", $"/funds/{ledgerB.FundId}/transactions/{id}/history" })
        {
            await (await ledgerB.Treasurer.GetAsync(path)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        }

        await (await ledgerB.Treasurer.PutAsync($"/funds/{ledgerB.FundId}/transactions/{id}", UpdateBody(ledgerB, 1, "Đóng quỹ")))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await ledgerB.Treasurer.PostAsync($"/funds/{ledgerB.FundId}/transactions/{id}/cancel", new { reason = "Hủy chéo" }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Single(await AuditAsync(id));
    }

    [Fact]
    public async Task Revoked_treasurer_cannot_write_on_next_request_and_new_treasurer_can()
    {
        // TR-05 (phần ghi thu chi): người cũ → 403 ngay request kế tiếp (cùng phiên), người mới ghi được.
        var ledger = await LedgerAsync();
        var trang = await _data.CreateStudentAccountAsync(ledger.Fund, "Phạm Thu Trang", activated: true);
        await CreateAsync(ledger, "Income", 100_000, "Đóng quỹ", "Trước khi đổi thủ quỹ");

        await ReadAsync(await ledger.Teacher.PostAsync($"/funds/{ledger.FundId}/treasurers", new { studentId = trang.StudentId }), HttpStatusCode.Created);
        var assignments = await ReadAsync(await ledger.Teacher.GetAsync($"/funds/{ledger.FundId}/treasurers"), HttpStatusCode.OK);
        var oldAssignment = assignments.EnumerateArray().Single(item => item.GetProperty("studentName").GetString() == "Nguyễn Minh Khang");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await ledger.Teacher.PostAsync($"/funds/{ledger.FundId}/treasurers/{oldAssignment.GetProperty("id").GetGuid()}/revoke")).StatusCode);

        await (await ledger.Treasurer.PostAsync($"/funds/{ledger.FundId}/transactions", Body(ledger, "Income", 1, "Đóng quỹ", "Người cũ ghi")))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        using var newTreasurer = await SignInAsync(trang.UserId);
        await ReadAsync(
            await newTreasurer.PostAsync($"/funds/{ledger.FundId}/transactions", Body(ledger, "Income", 50_000, "Đóng quỹ", "Người mới ghi")),
            HttpStatusCode.Created);

        // Người cũ vẫn xem được bản công khai (học sinh chưa phân công).
        var list = await ListAsync(ledger.Treasurer, ledger.FundId);
        Assert.False(list.GetProperty("items")[0].TryGetProperty("createdByName", out _));
    }

    [Fact]
    public async Task Fund_transaction_cannot_be_hard_deleted()
    {
        var ledger = await LedgerAsync();
        var created = await CreateAsync(ledger, "Income", 100_000, "Đóng quỹ", "Không xóa được");

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""DELETE FROM "FundTransaction" WHERE "Id" = @id""", connection);
        command.Parameters.AddWithValue("id", Id(created));

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("P0001", error.SqlState);
    }

    // ---------- Helper ----------

    /// <summary>GVCN tạo quỹ bằng API (có 6 danh mục mặc định) + một thủ quỹ "Nguyễn Minh Khang" đã kích hoạt.</summary>
    private async Task<Ledger> LedgerAsync()
    {
        var teacher = await _data.CreateTeacherAsync("Cô Hạnh");
        var teacherClient = await SignInAsync(teacher.UserId);
        var created = await ReadAsync(await teacherClient.PostAsync("/funds", new { name = TestData.Unique("Quỹ lớp ") }), HttpStatusCode.Created);
        var fundId = created.GetProperty("id").GetGuid();
        var fund = new SeededFund(fundId, "Quỹ", teacher.UserId);
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Nguyễn Minh Khang", treasurer: true);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var categories = await db.Categories.AsNoTracking().Where(category => category.FundId == fundId)
            .ToDictionaryAsync(category => category.Name, category => category.Id);
        return new Ledger(fundId, fund, teacherClient, await SignInAsync(treasurer.UserId), treasurer.UserId, categories);
    }

    private async Task<ApiClient> SignInAsync(Guid userId)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.SignInAsAsync(userId);
        return client;
    }

    private static object Body(
        Ledger ledger,
        string type,
        long amount,
        string category,
        string description,
        DateOnly? occurredOn = null,
        bool isPublic = true,
        string? note = null,
        Guid? key = null) => new
        {
            type,
            amount,
            occurredOn = occurredOn ?? Today,
            categoryId = ledger.Categories[category],
            description,
            note,
            isPublic,
            idempotencyKey = key ?? Guid.NewGuid(),
        };

    private static object UpdateBody(Ledger ledger, long amount, string category, string description = "Sửa", DateOnly? occurredOn = null) => new
    {
        amount,
        occurredOn = occurredOn ?? Today,
        categoryId = ledger.Categories[category],
        description,
        note = (string?)null,
        isPublic = true,
    };

    private static async Task<JsonElement> CreateAsync(
        Ledger ledger,
        string type,
        long amount,
        string category,
        string description,
        DateOnly? occurredOn = null,
        bool isPublic = true,
        string? note = null) =>
        await ReadAsync(
            await ledger.Treasurer.PostAsync(
                $"/funds/{ledger.FundId}/transactions",
                Body(ledger, type, amount, category, description, occurredOn, isPublic, note)),
            HttpStatusCode.Created);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Mong đợi {(int)expected}, nhận {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<JsonElement> ListAsync(ApiClient client, Guid fundId, string query = "") =>
        await ReadAsync(await client.GetAsync($"/funds/{fundId}/transactions{(query.Length > 0 ? "?" + query : "")}"), HttpStatusCode.OK);

    private static Guid Id(JsonElement transaction) => transaction.GetProperty("id").GetGuid();

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    /// <summary>Số dư của quỹ theo <c>GET /funds</c> (M2) của GVCN.</summary>
    private static async Task<long> BalanceAsync(Ledger ledger)
    {
        var funds = await ReadAsync(await ledger.Teacher.GetAsync("/funds"), HttpStatusCode.OK);
        return funds.EnumerateArray().Single(fund => fund.GetProperty("id").GetGuid() == ledger.FundId).GetProperty("currentBalance").GetInt64();
    }

    /// <summary>Ẩn danh mục (M5 chưa có API).</summary>
    private async Task HideCategoryAsync(Guid categoryId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = await db.Categories.SingleAsync(item => item.Id == categoryId);
        category.Update(category.Name, isHidden: true, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task<List<AuditLog>> AuditAsync(Guid transactionId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = transactionId.ToString();
        return await db.AuditLogs.AsNoTracking().Where(log => log.EntityId == id).OrderBy(log => log.Id).ToListAsync();
    }

    /// <summary>Phụ huynh đã đổi mật khẩu tạm.</summary>
    private async Task ActivateAsync(Guid userId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId);
        user.ChangePassword(hasher.Hash("Matkhau-456"), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }
}
