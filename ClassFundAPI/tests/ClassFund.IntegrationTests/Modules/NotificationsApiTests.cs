using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>M11-T3 trên PostgreSQL thật: chỉ thấy thông báo của mình, đánh dấu đã đọc, đếm chưa đọc, nhắc hạn không tạo trùng.</summary>
[Collection(ApiCollection.Name)]
public class NotificationsApiTests(ApiTestHost host)
{
    private static string Url(Guid fundId, string rest = "") => $"/api/v1/funds/{fundId}/me/notifications{rest}";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<(Guid FundId, HttpClient Owner, HttpClient A, HttpClient B, Guid CategoryId)> SetupAsync()
    {
        var owner = await host.CreateUserAsync();
        var a = await host.CreateUserAsync(UserOrigin.Provisioned);
        var b = await host.CreateUserAsync(UserOrigin.Provisioned);
        var fund = await host.CreateFundAsync(owner);
        await host.AddMemberAsync(fund, a);
        await host.AddMemberAsync(fund, b);
        var ownerClient = await host.LoginAsync(owner);
        var category = await ReadAsync(await ownerClient.PostAsJsonAsync($"/api/v1/funds/{fund.Id}/categories", new { name = "Quỹ lớp", type = "Income" }));
        return (fund.Id, ownerClient, await host.LoginAsync(a), await host.LoginAsync(b), category.GetProperty("id").GetGuid());
    }

    private static async Task CreateChargeAsync(HttpClient owner, Guid fundId, Guid categoryId, string title, int dueInDays)
    {
        var created = await owner.PostAsJsonAsync($"/api/v1/funds/{fundId}/charges", new
        {
            categoryId,
            title,
            purpose = (string?)null,
            amountPerPerson = 50_000,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(dueInDays)).ToString("yyyy-MM-dd"),
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Anonymous_gets_401_and_outsiders_404()
    {
        var s = await SetupAsync();
        var stranger = await host.LoginAsync(await host.CreateUserAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anonymous().GetAsync(Url(s.FundId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Url(s.FundId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Url(s.FundId, "/unread-count"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync(Url(s.FundId, "/read-all"), null)).StatusCode);
    }

    [Fact]
    public async Task A_new_charge_notifies_each_member_and_each_sees_only_their_own()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s.Owner, s.FundId, s.CategoryId, "Quỹ HK1", dueInDays: 20);

        var mine = await ReadAsync(await s.A.GetAsync(Url(s.FundId)));
        var item = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal("GeneralChargeCreated", item.GetProperty("type").GetString());
        Assert.Equal("General", item.GetProperty("scope").GetString());
        Assert.StartsWith("[Quỹ chung]", item.GetProperty("title").GetString());
        Assert.Equal(1, mine.GetProperty("unreadCount").GetInt32());
        Assert.Equal(1, (await ReadAsync(await s.A.GetAsync(Url(s.FundId, "/unread-count")))).GetProperty("count").GetInt32());

        var id = item.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await s.B.PostAsync(Url(s.FundId, $"/{id}/read"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.A.PostAsync(Url(s.FundId, $"/{id}/read"), null)).StatusCode);
        Assert.Equal(0, (await ReadAsync(await s.A.GetAsync(Url(s.FundId, "/unread-count")))).GetProperty("count").GetInt32());
        Assert.Equal(1, (await ReadAsync(await s.B.GetAsync(Url(s.FundId, "/unread-count")))).GetProperty("count").GetInt32());
        Assert.Equal(0, (await ReadAsync(await s.A.GetAsync(Url(s.FundId, "?unreadOnly=true")))).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Read_all_marks_everything_of_mine_and_validates_paging()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s.Owner, s.FundId, s.CategoryId, "Quỹ HK1", dueInDays: 20);
        await CreateChargeAsync(s.Owner, s.FundId, s.CategoryId, "Quỹ HK2", dueInDays: 20);

        var done = await ReadAsync(await s.A.PostAsync(Url(s.FundId, "/read-all"), null));

        Assert.Equal(2, done.GetProperty("marked").GetInt32());
        Assert.Equal(0, (await ReadAsync(await s.A.GetAsync(Url(s.FundId, "/unread-count")))).GetProperty("count").GetInt32());
        Assert.Equal(2, (await ReadAsync(await s.B.GetAsync(Url(s.FundId, "/unread-count")))).GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.A.GetAsync(Url(s.FundId, "?page=0"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.A.GetAsync(Url(s.FundId, "?pageSize=101"))).StatusCode);
    }

    [Fact]
    public async Task Due_soon_reminder_is_created_once_per_day_and_only_for_the_unpaid_person()
    {
        var s = await SetupAsync();
        await CreateChargeAsync(s.Owner, s.FundId, s.CategoryId, "Quỹ HK1", dueInDays: 1);

        await s.A.GetAsync(Url(s.FundId));
        await s.A.GetAsync(Url(s.FundId, "/unread-count"));
        var list = await ReadAsync(await s.A.GetAsync(Url(s.FundId)));

        var reminders = list.GetProperty("items").EnumerateArray().Where(item => item.GetProperty("type").GetString() == "ChargeDueSoon").ToArray();
        Assert.Single(reminders);
        Assert.Equal("ChargeAssignment", reminders[0].GetProperty("referenceType").GetString());

        await using var db = host.CreateDbContext();
        Assert.Equal(1, await db.Notifications.CountAsync(item => item.FundId == s.FundId && item.Type == NotificationType.ChargeDueSoon));
    }
}
