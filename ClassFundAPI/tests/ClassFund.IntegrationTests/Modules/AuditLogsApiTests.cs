using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>M12 – Nhật ký hoạt động (M12-T3): quyền, lọc trên PostgreSQL thật, và checklist "mọi API ghi đều sinh audit".</summary>
[Collection(ApiCollection.Name)]
public class AuditLogsApiTests(ApiTestHost host)
{
    private static readonly DateTimeOffset Day = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private static string Url(Guid fundId, string query = "") => $"/api/v1/funds/{fundId}/audit-logs{query}";

    [Fact]
    public async Task Permissions_owner_200_student_403_outsider_404_anonymous_401_TC_M12_01()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(fund, student);

        Assert.Equal(HttpStatusCode.OK, (await (await host.LoginAsync(owner)).GetAsync(Url(fund.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await host.LoginAsync(student)).GetAsync(Url(fund.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await host.LoginAsync(student)).GetAsync(Url(fund.Id, "/actors"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await host.LoginAsync(await host.CreateUserAsync())).GetAsync(Url(fund.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Anonymous().GetAsync(Url(fund.Id))).StatusCode);
    }

    [Fact]
    public async Task Invalid_filters_are_400()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var client = await host.LoginAsync(owner);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Url(fund.Id, "?from=2026-11-17&to=2026-11-01"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Url(fund.Id, "?pageSize=500"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Url(fund.Id, $"?scope=General&eventId={Guid.NewGuid()}"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Url(fund.Id, "?from=khong-phai-ngay"))).StatusCode);
    }

    [Fact]
    public async Task Filters_paging_names_and_masked_ip_on_postgres_FR47()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var treasurer = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(fund, treasurer, MemberRole.Treasurer);

        Guid eventId;
        await using (var db = host.CreateDbContext())
        {
            var picnic = new Event(fund.Id, "Dã ngoại Ba Vì", null, null, owner.Id, Day);
            db.Events.Add(picnic);
            eventId = picnic.Id;
            db.AuditLogs.AddRange(
                new AuditLog(Day, fund.Id, owner.Id, FinancialScope.General, null, "fund.update", "Fund", fund.Id.ToString())
                {
                    BeforeJson = """{"Name":"Lớp A"}""",
                    AfterJson = """{"Name":"Lớp B"}""",
                    IpAddress = IPAddress.Parse("113.190.25.7"),
                },
                new AuditLog(Day.AddHours(1), fund.Id, treasurer.Id, FinancialScope.Event, picnic.Id, "payment.confirm", "Payment", null)
                {
                    AfterJson = """{"amount":350000}""",
                },
                new AuditLog(Day.AddDays(1), fund.Id, owner.Id, FinancialScope.General, null, "payment.confirm", "Payment", null)
                {
                    IsSelfAction = true,
                });
            await db.SaveChangesAsync();
        }

        var client = await host.LoginAsync(owner);

        // CreateFundAsync không đi qua API nên chỉ có 3 bản ghi vừa thêm.
        var all = await client.GetFromJsonAsync<JsonElement>(Url(fund.Id, "?pageSize=2"));
        Assert.Equal(3, all.GetProperty("total").GetInt32());
        var first = all.GetProperty("items")[0];
        Assert.True(first.GetProperty("isSelfAction").GetBoolean());
        Assert.Equal(owner.FullName, first.GetProperty("actorName").GetString());

        var eventRows = await client.GetFromJsonAsync<JsonElement>(Url(fund.Id, $"?scope=Event&eventId={eventId}"));
        var eventRow = Assert.Single(eventRows.GetProperty("items").EnumerateArray());
        Assert.Equal("Dã ngoại Ba Vì", eventRow.GetProperty("eventName").GetString());
        Assert.Equal("350.000 đ", eventRow.GetProperty("summary").GetString());

        var byActor = await client.GetFromJsonAsync<JsonElement>(Url(fund.Id, $"?actorId={treasurer.Id}"));
        Assert.Equal(1, byActor.GetProperty("total").GetInt32());

        var byGroup = await client.GetFromJsonAsync<JsonElement>(Url(fund.Id, "?action=payment."));
        Assert.Equal(2, byGroup.GetProperty("total").GetInt32());

        var selfOnly = await client.GetFromJsonAsync<JsonElement>(Url(fund.Id, "?selfOnly=true"));
        Assert.Equal(1, selfOnly.GetProperty("total").GetInt32());

        // 16/11 theo giờ VN: hai bản ghi đầu (10:00 và 11:00 giờ VN).
        var day16 = await client.GetFromJsonAsync<JsonElement>(Url(fund.Id, "?from=2026-11-16&to=2026-11-16"));
        Assert.Equal(2, day16.GetProperty("total").GetInt32());
        var update = day16.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("action").GetString() == "fund.update");
        Assert.Equal("Lớp A → Lớp B", update.GetProperty("summary").GetString());
        Assert.Equal("113.190.xx.xx", update.GetProperty("ipAddress").GetString());
        Assert.Contains("Lớp A", update.GetProperty("before").GetString());

        var actors = await client.GetFromJsonAsync<JsonElement>(Url(fund.Id, "/actors"));
        Assert.Equal(2, actors.GetArrayLength());
    }

    /// <summary>
    /// Checklist M12-T3: mọi API ghi của các module đã merge (M1 → M5) đều sinh audit, đúng quỹ / phạm vi,
    /// và không lộ mật khẩu. Module mới thêm API ghi thì bổ sung vào đây.
    /// </summary>
    [Fact]
    public async Task Every_write_api_of_merged_modules_records_audit_checklist()
    {
        var owner = await host.CreateUserAsync();
        var client = await host.LoginAsync(owner);

        // M2 – tạo quỹ, sửa quỹ.
        var created = await client.PostAsJsonAsync("/api/v1/funds", new { name = "Quỹ audit", schoolYear = "2026-2027" });
        var fundId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/v1/funds/{fundId}", new { name = "Quỹ audit (sửa)", schoolYear = "2026-2027" });

        // M5 – danh mục, chứng từ.
        var category = await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/categories", new { name = "Văn nghệ", type = "Expense" });
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/v1/funds/{fundId}/categories/{categoryId}", new { name = "Văn nghệ 20/11", isActive = false });
        await client.PostAsync($"/api/v1/funds/{fundId}/attachments", ApiTestHost.Multipart(ApiTestHost.Pdf, "hd.pdf", "application/pdf", "Receipt"));

        // M3 – thêm, thêm nhiều, sửa tên, cấp lại mật khẩu, ngừng.
        var added = await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/members", new { email = $"hs-{Guid.NewGuid():N}@it.local", fullName = "Học Sinh Một" });
        var addedBody = await added.Content.ReadFromJsonAsync<JsonElement>();
        var memberId = addedBody.GetProperty("member").GetProperty("id").GetGuid();
        var secrets = new List<string> { addedBody.GetProperty("temporaryPassword").GetString()! };
        await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/members/bulk", new { emails = new[] { $"hs-{Guid.NewGuid():N}@it.local" } });
        await client.PutAsJsonAsync($"/api/v1/funds/{fundId}/members/{memberId}", new { fullName = "Học Sinh Một (sửa)" });
        var reset = await client.PostAsync($"/api/v1/funds/{fundId}/members/{memberId}/reset-password", null);
        secrets.Add((await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("temporaryPassword").GetString()!);

        // M4 – phân quyền, thu hồi thủ quỹ.
        var other = await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/members", new { email = $"tq-{Guid.NewGuid():N}@it.local", fullName = "Thủ Quỹ Hai" });
        var otherId = (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("member").GetProperty("id").GetGuid();
        await client.PostAsJsonAsync($"/api/v1/funds/{fundId}/treasurers", new { memberId = otherId });
        await client.PostAsync($"/api/v1/funds/{fundId}/treasurers/{otherId}/revoke", null);

        await client.PostAsync($"/api/v1/funds/{fundId}/members/{memberId}/remove", null);

        // M1 / M5 cấp tài khoản (FundId null, phạm vi System).
        await client.PutAsJsonAsync("/api/v1/auth/me", new { fullName = "Thủ Quỹ Audit" });
        await client.PutAsync("/api/v1/auth/me/avatar", ApiTestHost.Multipart(ApiTestHost.Png, "a.png", "image/png"));

        string[] expectedFundActions =
        [
            "fund.create", "fund.update", "category.create", "category.update", "attachment.upload",
            "member.add", "member.update", "member.reset_password", "member.remove", "treasurer.grant", "treasurer.revoke",
        ];
        string[] expectedAccountActions = ["auth.update_profile", "user.avatar_update"];

        // Qua API viewer (đúng quỹ, đủ hành động).
        var page = await client.GetFromJsonAsync<JsonElement>(Url(fundId, "?pageSize=100"));
        var actions = page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("action").GetString()).ToHashSet();
        Assert.All(expectedFundActions, action => Assert.Contains(action, actions));
        // Viewer hiện tên hiện tại của người thao tác (đã đổi qua PUT /auth/me ở trên).
        Assert.All(page.GetProperty("items").EnumerateArray(), item => Assert.Equal("Thủ Quỹ Audit", item.GetProperty("actorName").GetString()));

        // Thao tác cấp tài khoản và không lộ mật khẩu trong audit.
        await using var db = host.CreateDbContext();
        var accountActions = await db.AuditLogs.Where(log => log.ActorUserId == owner.Id && log.FundId == null).Select(log => log.Action).ToListAsync();
        Assert.All(expectedAccountActions, action => Assert.Contains(action, accountActions));
        var payloads = (await db.AuditLogs
                .Where(log => log.ActorUserId == owner.Id)
                .Select(log => new { log.BeforeJson, log.AfterJson, log.Note })
                .ToListAsync())
            .Select(log => $"{log.BeforeJson}{log.AfterJson}{log.Note}")
            .ToList();
        Assert.All(secrets, secret => Assert.All(payloads, json => Assert.DoesNotContain(secret, json)));
        Assert.All(payloads, json => Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase));
    }
}
