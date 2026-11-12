using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Persistence;
using ClassFund.IntegrationTests.Infrastructure;

namespace ClassFund.IntegrationTests.Categories;

/// <summary>
/// M5 – Danh mục thu / chi qua HTTP thật (docs/implementation/module-plan.md mục M5; FR-10, BR-08, BR-25, BR-37, NFR-08,
/// X-01; test case T-04, TR-02, P-07).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CategoriesTests(PostgresFixture fixture)
{
    private readonly TestData _data = fixture.Data;

    // ---------- POST ----------

    [Fact]
    public async Task Treasurer_creates_category_and_it_is_listed_with_audit()
    {
        var (fund, treasurer, client) = await TreasurerFundAsync();

        var id = await CreateAsync(client, fund.Id, "Income", "  Tiền quỹ hè  ");

        var item = Assert.Single((await ListAsync(client, fund.Id)).EnumerateArray());
        Assert.Equal(["id", "type", "name", "isHidden", "transactionCount"], item.EnumerateObject().Select(property => property.Name));
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("Income", item.GetProperty("type").GetString());
        Assert.Equal("Tiền quỹ hè", item.GetProperty("name").GetString());
        Assert.False(item.GetProperty("isHidden").GetBoolean());
        Assert.Equal(0, item.GetProperty("transactionCount").GetInt32());

        var audit = Assert.Single(await AuditAsync(id));
        Assert.Equal("Category.Create", audit.Action);
        Assert.Equal(treasurer.UserId, audit.ActorUserId);
        Assert.Contains("Tiền quỹ hè", audit.NewValues);
    }

    [Fact]
    public async Task Duplicate_name_in_same_type_is_rejected_ignoring_vietnamese_case_but_other_type_is_allowed()
    {
        var (fund, _, client) = await TreasurerFundAsync();
        await CreateAsync(client, fund.Id, "Expense", "Văn phòng phẩm");

        await (await client.PostAsync($"/funds/{fund.Id}/categories", new { type = "Expense", name = "VĂN PHÒNG PHẨM" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "category.name_taken");
        await (await client.PostAsync($"/funds/{fund.Id}/categories", new { type = "Expense", name = " văn phòng phẩm " }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "category.name_taken");

        await CreateAsync(client, fund.Id, "Income", "Văn phòng phẩm");
        Assert.Equal(2, (await ListAsync(client, fund.Id)).GetArrayLength());
    }

    [Fact]
    public async Task Same_name_is_allowed_in_another_fund()
    {
        var (fund, _, client) = await TreasurerFundAsync();
        var (otherFund, _, otherClient) = await TreasurerFundAsync();

        await CreateAsync(client, fund.Id, "Expense", "Quà 20/11");
        await CreateAsync(otherClient, otherFund.Id, "Expense", "Quà 20/11");
    }

    [Fact]
    public async Task Concurrent_creates_of_same_name_leave_exactly_one()
    {
        // BR-08 / NFR-09: cả hai request qua bước kiểm tra tên; unique index là chốt chặn cuối.
        for (var round = 0; round < 5; round++)
        {
            var (fund, treasurer, first) = await TreasurerFundAsync();
            using var second = await SignInAsync(treasurer.UserId);

            var responses = await Task.WhenAll(
                first.PostAsync($"/funds/{fund.Id}/categories", new { type = "Expense", name = "Trùng tên" }),
                second.PostAsync($"/funds/{fund.Id}/categories", new { type = "Expense", name = "trùng tên" }));

            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            await Assert.Single(responses, response => response.StatusCode != HttpStatusCode.Created)
                .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "category.name_taken");
            Assert.Single((await ListAsync(first, fund.Id)).EnumerateArray());
            first.Dispose();
        }
    }

    [Theory]
    [InlineData("""{ "name": "Quà" }""", "type", "Chọn loại danh mục.")]
    [InlineData("""{ "type": "Expense" }""", "name", "Nhập tên danh mục.")]
    [InlineData("""{ "type": "Expense", "name": "   " }""", "name", "Nhập tên danh mục.")]
    [InlineData("""{ "type": "Expense", "name": "%LONG%" }""", "name", "Tên danh mục tối đa 100 ký tự.")]
    public async Task Create_with_invalid_body_is_validation_error(string body, string field, string message)
    {
        var (fund, _, client) = await TreasurerFundAsync();
        var json = JsonDocument.Parse(body.Replace("%LONG%", new string('a', 101))).RootElement;

        var problem = await (await client.PostAsync($"/funds/{fund.Id}/categories", json))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        Assert.Equal([message], problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(item => item.GetString()!));
        Assert.Empty((await ListAsync(client, fund.Id)).EnumerateArray());
    }

    [Fact]
    public async Task Create_accepts_name_of_exactly_100_characters_after_trim()
    {
        var (fund, _, client) = await TreasurerFundAsync();

        await CreateAsync(client, fund.Id, "Income", " " + new string('a', 100) + " ");
    }

    // ---------- PUT ----------

    [Fact]
    public async Task Treasurer_renames_and_hides_category_with_audit_and_hidden_one_keeps_its_transactions()
    {
        var (fund, treasurer, client) = await TreasurerFundAsync();
        var id = await CreateAsync(client, fund.Id, "Expense", "Photo");
        await _data.CreateTransactionAsync(fund, new SeededCategory(id, TransactionType.Expense, "Photo", false), createdBy: treasurer.UserId);

        var rename = await client.PutAsync($"/funds/{fund.Id}/categories/{id}", new { name = "Photo tài liệu", isHidden = false });
        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);
        var hide = await client.PutAsync($"/funds/{fund.Id}/categories/{id}", new { name = "Photo tài liệu", isHidden = true });
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

        Assert.Empty((await ListAsync(client, fund.Id)).EnumerateArray());
        var hidden = Assert.Single((await ListAsync(client, fund.Id, "?includeHidden=true")).EnumerateArray());
        Assert.Equal("Photo tài liệu", hidden.GetProperty("name").GetString());
        Assert.True(hidden.GetProperty("isHidden").GetBoolean());
        Assert.Equal(1, hidden.GetProperty("transactionCount").GetInt32());

        var audit = await AuditAsync(id);
        Assert.Equal(["Category.Create", "Category.Update", "Category.Update"], audit.Select(log => log.Action));
        Assert.Contains("Photo tài liệu", audit[1].NewValues);
        Assert.Contains("Photo", audit[1].OldValues);
        Assert.Contains("true", audit[2].NewValues);
    }

    [Fact]
    public async Task Update_without_change_is_no_content_and_writes_no_audit()
    {
        var (fund, _, client) = await TreasurerFundAsync();
        var id = await CreateAsync(client, fund.Id, "Expense", "Quà");

        var response = await client.PutAsync($"/funds/{fund.Id}/categories/{id}", new { name = " Quà ", isHidden = false });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["Category.Create"], (await AuditAsync(id)).Select(log => log.Action));
    }

    [Fact]
    public async Task Rename_to_existing_name_of_same_type_is_rejected_including_hidden_categories()
    {
        var (fund, _, client) = await TreasurerFundAsync();
        await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Đã ẩn", isHidden: true);
        var id = await CreateAsync(client, fund.Id, "Expense", "Quà");

        await (await client.PutAsync($"/funds/{fund.Id}/categories/{id}", new { name = "đã ẨN", isHidden = false }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "category.name_taken");

        Assert.Equal("Quà", (await ListAsync(client, fund.Id)).EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id).GetProperty("name").GetString());
        Assert.Single(await AuditAsync(id));
    }

    [Fact]
    public async Task Update_with_missing_field_is_validation_error()
    {
        var (fund, _, client) = await TreasurerFundAsync();
        var id = await CreateAsync(client, fund.Id, "Expense", "Quà");

        var problem = await (await client.PutAsync($"/funds/{fund.Id}/categories/{id}", new { name = "Quà mới" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        Assert.Equal(["Chọn trạng thái ẩn hoặc hiện."], problem.GetProperty("errors").GetProperty("isHidden").EnumerateArray().Select(item => item.GetString()!));
    }

    [Fact]
    public async Task Category_of_another_fund_or_unknown_is_not_found()
    {
        var (fund, _, client) = await TreasurerFundAsync();
        var otherFund = await _data.CreateFundAsync();
        var other = await _data.CreateCategoryAsync(otherFund, TransactionType.Expense, "Của quỹ khác");

        await (await client.PutAsync($"/funds/{fund.Id}/categories/{other.Id}", new { name = "Đổi tên", isHidden = true }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await client.PutAsync($"/funds/{fund.Id}/categories/{Guid.NewGuid()}", new { name = "Đổi tên", isHidden = true }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        Assert.Empty(await AuditAsync(other.Id));
    }

    // ---------- GET: bản đầy đủ ----------

    [Fact]
    public async Task Full_view_counts_every_transaction_including_cancelled_and_filters_by_type_and_hidden()
    {
        var (fund, _, client) = await TreasurerFundAsync();
        var defaults = await _data.SeedDefaultCategoriesAsync(fund);
        var dongQuy = defaults["Đóng quỹ"];
        await _data.CreateTransactionAsync(fund, dongQuy);
        await _data.CreateTransactionAsync(fund, dongQuy, isPublic: false);
        await _data.CreateTransactionAsync(fund, dongQuy, cancelled: true);
        var hidden = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Photo tài liệu", isHidden: true);
        await _data.CreateTransactionAsync(fund, hidden);

        var visible = (await ListAsync(client, fund.Id)).EnumerateArray().ToArray();
        Assert.Equal(6, visible.Length);
        Assert.Equal(3, Count(visible, "Đóng quỹ"));
        Assert.Equal(0, Count(visible, "Thu khác"));
        Assert.DoesNotContain(visible, item => item.GetProperty("name").GetString() == "Photo tài liệu");
        Assert.Equal(["Expense", "Expense", "Expense", "Expense", "Income", "Income"], visible.Select(item => item.GetProperty("type").GetString()));

        var all = (await ListAsync(client, fund.Id, "?includeHidden=true")).EnumerateArray().ToArray();
        Assert.Equal(7, all.Length);
        Assert.Equal(1, Count(all, "Photo tài liệu"));
        Assert.True(all.Single(item => item.GetProperty("name").GetString() == "Photo tài liệu").GetProperty("isHidden").GetBoolean());

        var incomeOnly = (await ListAsync(client, fund.Id, "?type=Income&includeHidden=true")).EnumerateArray().ToArray();
        Assert.Equal(2, incomeOnly.Length);
        Assert.All(incomeOnly, item => Assert.Equal("Income", item.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task Teacher_gets_the_full_view_but_cannot_write()
    {
        var teacher = await _data.CreateTeacherAsync();
        var fund = await _data.CreateFundAsync(teacher.UserId);
        var category = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Quà");
        await _data.CreateTransactionAsync(fund, category, isPublic: false);
        using var client = await SignInAsync(teacher.UserId);

        var item = Assert.Single((await ListAsync(client, fund.Id)).EnumerateArray());
        Assert.Equal(1, item.GetProperty("transactionCount").GetInt32());
        Assert.False(item.GetProperty("isHidden").GetBoolean());

        await (await client.PostAsync($"/funds/{fund.Id}/categories", new { type = "Expense", name = "Mới" })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await client.PutAsync($"/funds/{fund.Id}/categories/{category.Id}", new { name = "Đổi", isHidden = true })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    // ---------- GET: bản công khai (X-01, BR-25, NFR-08) ----------

    [Fact]
    public async Task Public_view_lists_only_visible_categories_with_a_public_transaction_and_hides_internal_fields()
    {
        var (fund, student, parent) = await FundWithStudentAndParentAsync();
        var withPublic = await _data.CreateCategoryAsync(fund, TransactionType.Income, "Đóng quỹ");
        var onlyPrivate = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Chi nội bộ");
        var empty = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Chưa dùng");
        var hiddenWithPublic = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Đã ẩn", isHidden: true);
        var mixed = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Quà 20/11");
        await _data.CreateTransactionAsync(fund, withPublic);
        await _data.CreateTransactionAsync(fund, onlyPrivate, isPublic: false);
        await _data.CreateTransactionAsync(fund, hiddenWithPublic);
        await _data.CreateTransactionAsync(fund, mixed, isPublic: false);
        await _data.CreateTransactionAsync(fund, mixed);
        Assert.NotNull(empty);

        foreach (var userId in new[] { student.UserId, parent.UserId })
        {
            using var client = await SignInAsync(userId);

            var items = (await ListAsync(client, fund.Id, "?includeHidden=true")).EnumerateArray().ToArray();

            Assert.Equal(new[] { "Quà 20/11", "Đóng quỹ" }.Order(), items.Select(item => item.GetProperty("name").GetString()!).Order());
            Assert.All(items, item => Assert.Equal(["id", "type", "name"], item.EnumerateObject().Select(property => property.Name)));
        }
    }

    [Fact]
    public async Task Public_view_counts_a_cancelled_public_transaction_as_still_having_one()
    {
        // X-01, D-22: xét toàn bộ lịch sử quỹ, không loại khoản đã hủy.
        var (fund, student, _) = await FundWithStudentAndParentAsync();
        var onlyCancelled = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Đã hủy hết");
        var onlyCancelledPrivate = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Hủy không công khai");
        await _data.CreateTransactionAsync(fund, onlyCancelled, cancelled: true);
        await _data.CreateTransactionAsync(fund, onlyCancelledPrivate, isPublic: false, cancelled: true);
        using var client = await SignInAsync(student.UserId);

        var item = Assert.Single((await ListAsync(client, fund.Id)).EnumerateArray());

        Assert.Equal("Đã hủy hết", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Public_view_filters_by_type_and_does_not_see_other_funds_transactions()
    {
        var (fund, student, _) = await FundWithStudentAndParentAsync();
        var otherFund = await _data.CreateFundAsync();
        var income = await _data.CreateCategoryAsync(fund, TransactionType.Income, "Thu");
        var expense = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Chi");
        var otherCategory = await _data.CreateCategoryAsync(otherFund, TransactionType.Expense, "Quỹ khác");
        await _data.CreateTransactionAsync(fund, income);
        await _data.CreateTransactionAsync(fund, expense);
        await _data.CreateTransactionAsync(otherFund, otherCategory);
        using var client = await SignInAsync(student.UserId);

        var items = (await ListAsync(client, fund.Id, "?type=Expense")).EnumerateArray().ToArray();

        Assert.Equal(["Chi"], items.Select(item => item.GetProperty("name").GetString()!));
    }

    // ---------- Phạm vi & quyền ----------

    [Fact]
    public async Task Student_and_parent_cannot_write()
    {
        var (fund, student, parent) = await FundWithStudentAndParentAsync();
        var category = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Quà");

        foreach (var userId in new[] { student.UserId, parent.UserId })
        {
            using var client = await SignInAsync(userId);
            await (await client.PostAsync($"/funds/{fund.Id}/categories", new { type = "Expense", name = "Mới" })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await client.PutAsync($"/funds/{fund.Id}/categories/{category.Id}", new { name = "Đổi", isHidden = true })).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        Assert.Empty(await AuditAsync(category.Id));
    }

    [Fact]
    public async Task Outsider_gets_not_found_and_anonymous_gets_unauthorized()
    {
        var (fund, _, _) = await TreasurerFundAsync();
        var category = await _data.CreateCategoryAsync(fund, TransactionType.Expense, "Quà");
        var outsider = await _data.CreateTeacherAsync("Cô khác");
        using var client = await SignInAsync(outsider.UserId);

        await (await client.GetAsync($"/funds/{fund.Id}/categories")).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await client.PostAsync($"/funds/{fund.Id}/categories", new { type = "Expense", name = "Mới" })).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await client.PutAsync($"/funds/{fund.Id}/categories/{category.Id}", new { name = "Đổi", isHidden = true })).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        using var anonymous = fixture.Factory.CreateApiClient();
        await (await anonymous.GetAsync($"/funds/{fund.Id}/categories")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task List_response_is_not_cacheable()
    {
        var (fund, _, client) = await TreasurerFundAsync();

        var response = await client.GetAsync($"/funds/{fund.Id}/categories");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    // ---------- Helper ----------

    private static int Count(IEnumerable<JsonElement> items, string name) =>
        items.Single(item => item.GetProperty("name").GetString() == name).GetProperty("transactionCount").GetInt32();

    private async Task<(SeededFund Fund, SeededAccount Treasurer, ApiClient Client)> TreasurerFundAsync()
    {
        var fund = await _data.CreateFundAsync();
        var treasurer = await _data.CreateStudentAccountAsync(fund, "Phạm Thu Trang", treasurer: true);
        return (fund, treasurer, await SignInAsync(treasurer.UserId));
    }

    private async Task<(SeededFund Fund, SeededAccount Student, SeededAccount Parent)> FundWithStudentAndParentAsync()
    {
        var fund = await _data.CreateFundAsync();
        var student = await _data.CreateStudentAccountAsync(fund, "Nguyễn Minh Khang", activated: true);
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);
        await ActivateAsync(parent.UserId);
        return (fund, student, parent);
    }

    private async Task<ApiClient> SignInAsync(Guid userId)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.SignInAsAsync(userId);
        return client;
    }

    private static async Task<Guid> CreateAsync(ApiClient client, Guid fundId, string type, string name)
    {
        var response = await client.PostAsync($"/funds/{fundId}/categories", new { type, name });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Mong đợi 201, nhận {(int)response.StatusCode}: {body}");
        var created = JsonDocument.Parse(body).RootElement;
        Assert.Single(created.EnumerateObject());
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ListAsync(ApiClient client, Guid fundId, string query = "")
    {
        var response = await client.GetAsync($"/funds/{fundId}/categories{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<List<AuditLog>> AuditAsync(Guid categoryId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = categoryId.ToString();
        return await db.AuditLogs.AsNoTracking().Where(log => log.EntityId == id).OrderBy(log => log.Id).ToListAsync();
    }

    /// <summary>Phụ huynh đã đổi mật khẩu tạm (seed của <see cref="TestData"/> tạo phụ huynh còn mật khẩu tạm).</summary>
    private async Task ActivateAsync(Guid userId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<ClassFund.Application.Common.Security.IPasswordHasher>();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId);
        user.ChangePassword(hasher.Hash("Matkhau-456"), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }
}
