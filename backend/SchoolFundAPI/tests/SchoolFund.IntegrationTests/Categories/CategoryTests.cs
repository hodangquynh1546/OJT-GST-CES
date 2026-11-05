using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;
using SchoolFund.Infrastructure.Persistence;
using SchoolFund.IntegrationTests.Infrastructure;

namespace SchoolFund.IntegrationTests.Categories;

/// <summary>Danh mục qua HTTP thật (đăng nhập, cookie, antiforgery, PostgreSQL) – FR-10, BR-08, BR-25, X-01, NFR-08.</summary>
[Collection(ApiCollection.Name)]
public sealed class CategoryTests(PostgresFixture fixture)
{
    private const string NewPassword = "Matkhau-456";

    private readonly TestData _data = fixture.Data;

    private sealed record World(SeededFund Fund, ApiClient Teacher, ApiClient Treasurer, ApiClient Student, ApiClient Parent, Guid TreasurerId);

    private static string Url(SeededFund fund, string rest = "") => $"/funds/{fund.Id}/categories{rest}";

    private async Task<ApiClient> SignInAsync(SeededAccount account)
    {
        var client = fixture.Factory.CreateApiClient();
        await client.LoginAsync(account.Login, account.Password);
        var changed = await client.PostAsync("/auth/change-password", new { currentPassword = account.Password, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        return client;
    }

    private async Task<World> SeedAsync()
    {
        var teacher = fixture.Factory.CreateApiClient();
        var password = "Matkhau123";
        var session = await (await teacher.PostAsync("/auth/register-teacher", new
        {
            fullName = "Cô Hạnh",
            email = TestData.NewEmail(),
            password,
            confirmPassword = password,
        })).ReadSessionAsync(HttpStatusCode.Created);

        var fund = await _data.CreateFundAsync(session.User.Id);
        var treasurer = await _data.CreateStudentAccountAsync(fund, treasurer: true);
        var student = await _data.CreateStudentAccountAsync(fund);
        var parent = await _data.CreateParentAccountAsync(fund, student.StudentId!.Value);

        return new World(fund, teacher, await SignInAsync(treasurer), await SignInAsync(student), await SignInAsync(parent), treasurer.UserId);
    }

    private static async Task<Guid> CreateAsync(ApiClient client, SeededFund fund, string type, string name)
    {
        var response = await client.PostAsync(Url(fund), new { type, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task AddTransactionAsync(World world, Guid categoryId, TransactionType type, bool isPublic, bool cancelled = false)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transaction = new FundTransaction(
            world.Fund.Id,
            type,
            1000,
            new DateOnly(2026, 11, 5),
            categoryId,
            "Nội dung",
            null,
            isPublic,
            world.TreasurerId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        db.Add(transaction);
        await db.SaveChangesAsync();

        if (cancelled)
        {
            await db.FundTransactions
                .Where(item => item.Id == transaction.Id)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.IsCancelled, true)
                    .SetProperty(item => item.CancelledAt, DateTimeOffset.UtcNow)
                    .SetProperty(item => item.CancelledBy, world.TreasurerId)
                    .SetProperty(item => item.CancelReason, "Nhập nhầm"));
        }
    }

    private async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<JsonElement> ListAsync(ApiClient client, SeededFund fund, string query = "")
    {
        var response = await client.GetAsync(Url(fund, query));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonElement Row(JsonElement list, Guid id) =>
        list.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task Treasurer_creates_a_category_and_the_audit_row_is_saved_with_it()
    {
        var world = await SeedAsync();

        var id = await CreateAsync(world.Treasurer, world.Fund, "Income", "  Đóng quỹ  ");

        var category = await ReadAsync(db => db.Categories.SingleAsync(item => item.Id == id));
        Assert.Equal("Đóng quỹ", category.Name);
        Assert.Equal(TransactionType.Income, category.Type);

        var log = await ReadAsync(db => db.AuditLogs.SingleAsync(item => item.EntityId == id.ToString()));
        Assert.Equal("Category.Create", log.Action);
        Assert.Equal(world.TreasurerId, log.ActorUserId);
        Assert.Contains("Đóng quỹ", log.NewValues);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_ignoring_vietnamese_case()
    {
        var world = await SeedAsync();
        await CreateAsync(world.Treasurer, world.Fund, "Income", "Đóng quỹ");

        var response = await world.Treasurer.PostAsync(Url(world.Fund), new { type = "Income", name = "đóng QUỸ" });

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "category.name_taken");
        await CreateAsync(world.Treasurer, world.Fund, "Expense", "Đóng quỹ");
    }

    [Fact]
    public async Task Concurrent_creates_keep_one_category_and_never_fail_with_500()
    {
        var world = await SeedAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            world.Treasurer.PostAsync(Url(world.Fund), new { type = "Expense", name = "Liên hoan" })));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.Created),
            response => Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode));
        Assert.Equal(1, await ReadAsync(db => db.Categories.CountAsync(item => item.FundId == world.Fund.Id)));
    }

    [Theory]
    [InlineData("""{ "type": 1, "name": "A" }""")]
    [InlineData("""{ "type": "Other", "name": "A" }""")]
    [InlineData("""{ "name": "A" }""")]
    [InlineData("""{ "type": "Income" }""")]
    [InlineData("""{ "type": "Income", "name": "   " }""")]
    public async Task Invalid_body_is_rejected(string json)
    {
        var world = await SeedAsync();

        var response = await world.Treasurer.PostAsync(Url(world.Fund), JsonDocument.Parse(json).RootElement);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Only_treasurer_can_write_and_outsider_sees_nothing()
    {
        var world = await SeedAsync();
        var body = new { type = "Income", name = "Không được" };

        foreach (var client in new[] { world.Teacher, world.Student, world.Parent })
        {
            await (await client.PostAsync(Url(world.Fund), body)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        var otherFund = await _data.CreateFundAsync();
        await (await world.Treasurer.GetAsync(Url(otherFund))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        using var anonymous = fixture.Factory.CreateApiClient();
        await (await anonymous.GetAsync(Url(world.Fund))).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Treasurer_renames_hides_and_cannot_reach_another_fund()
    {
        var world = await SeedAsync();
        var id = await CreateAsync(world.Treasurer, world.Fund, "Expense", "photo");
        await CreateAsync(world.Treasurer, world.Fund, "Expense", "Khen thưởng");

        Assert.Equal(HttpStatusCode.NoContent, (await world.Treasurer.PutAsync(Url(world.Fund, $"/{id}"), new { name = "Photo", isHidden = true })).StatusCode);
        await (await world.Treasurer.PutAsync(Url(world.Fund, $"/{id}"), new { name = "KHEN THƯỞNG", isHidden = true }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "category.name_taken");
        await (await world.Treasurer.PutAsync(Url(world.Fund, $"/{Guid.NewGuid()}"), new { name = "X", isHidden = false }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        var category = await ReadAsync(db => db.Categories.SingleAsync(item => item.Id == id));
        Assert.Equal("Photo", category.Name);
        Assert.True(category.IsHidden);
        Assert.Equal(1, await ReadAsync(db => db.AuditLogs.CountAsync(item => item.EntityId == id.ToString() && item.Action == "Category.Update")));
    }

    [Fact]
    public async Task Full_view_counts_transactions_and_public_view_hides_internal_data()
    {
        var world = await SeedAsync();
        var shown = await CreateAsync(world.Treasurer, world.Fund, "Income", "Đóng quỹ");
        var hidden = await CreateAsync(world.Treasurer, world.Fund, "Expense", "Photo");
        var privateOnly = await CreateAsync(world.Treasurer, world.Fund, "Expense", "Khen thưởng");
        await CreateAsync(world.Treasurer, world.Fund, "Expense", "Chưa dùng");
        var cancelled = await CreateAsync(world.Treasurer, world.Fund, "Expense", "Nhập nhầm");

        await AddTransactionAsync(world, shown, TransactionType.Income, isPublic: true);
        await AddTransactionAsync(world, shown, TransactionType.Income, isPublic: false);
        await AddTransactionAsync(world, hidden, TransactionType.Expense, isPublic: true);
        await AddTransactionAsync(world, privateOnly, TransactionType.Expense, isPublic: false);
        await AddTransactionAsync(world, cancelled, TransactionType.Expense, isPublic: true, cancelled: true);
        await world.Treasurer.PutAsync(Url(world.Fund, $"/{hidden}"), new { name = "Photo", isHidden = true });

        var full = await ListAsync(world.Treasurer, world.Fund);
        Assert.Equal(4, full.GetArrayLength());
        Assert.Equal(2, Row(full, shown).GetProperty("transactionCount").GetInt32());
        Assert.Equal("Income", Row(full, shown).GetProperty("type").GetString());

        var withHidden = await ListAsync(world.Teacher, world.Fund, "?includeHidden=true");
        Assert.Equal(5, withHidden.GetArrayLength());
        Assert.True(Row(withHidden, hidden).GetProperty("isHidden").GetBoolean());
        Assert.Equal(1, (await ListAsync(world.Treasurer, world.Fund, "?type=Income")).GetArrayLength());

        foreach (var viewer in new[] { world.Parent, world.Student })
        {
            foreach (var query in new[] { "", "?includeHidden=true" })
            {
                var response = await viewer.GetAsync(Url(world.Fund, query));
                var raw = await response.Content.ReadAsStringAsync();

                Assert.DoesNotContain("transactionCount", raw);
                Assert.DoesNotContain("isHidden", raw);
                Assert.Equal(
                    new HashSet<Guid> { shown, cancelled },
                    JsonDocument.Parse(raw).RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToHashSet());
            }
        }
    }
}
