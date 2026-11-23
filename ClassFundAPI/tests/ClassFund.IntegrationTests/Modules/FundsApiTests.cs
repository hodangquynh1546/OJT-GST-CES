using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.IntegrationTests.Modules;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiTestHost>
{
    public const string Name = "api";
}

/// <summary>M2 – Quỹ: mỗi endpoint 200/201, 400, 401, 403, 404 (M2-T3).</summary>
[Collection(ApiCollection.Name)]
public class FundsApiTests(ApiTestHost host)
{
    [Fact]
    public async Task Anonymous_is_401()
    {
        var response = await host.Anonymous().GetAsync("/api/v1/funds");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Self_registered_user_creates_fund_and_becomes_owner_with_default_categories_FR05()
    {
        var user = await host.CreateUserAsync();
        var client = await host.LoginAsync(user);

        var response = await client.PostAsJsonAsync("/api/v1/funds", new { name = "Lớp 10A2", schoolYear = "2026-2027" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fundId = body.GetProperty("id").GetGuid();
        Assert.Equal("Owner", body.GetProperty("myRole").GetString());
        Assert.NotNull(response.Headers.Location);

        await using var db = host.CreateDbContext();
        Assert.Equal(DefaultCategories.All.Count, await db.Categories.CountAsync(category => category.FundId == fundId));
        Assert.True(await db.AuditLogs.AnyAsync(log => log.FundId == fundId && log.Action == "fund.create"));

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/funds");
        var card = Assert.Single(list.EnumerateArray());
        Assert.Equal(0, card.GetProperty("generalBalance").GetInt64());
        Assert.Equal(0, card.GetProperty("myDueCount").GetInt32());
        Assert.Equal(0, card.GetProperty("myBalance").GetInt64());
    }

    [Fact]
    public async Task Provisioned_account_cannot_create_fund_403()
    {
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        var client = await host.LoginAsync(student);

        var response = await client.PostAsJsonAsync("/api/v1/funds", new { name = "Lớp X", schoolYear = "2026-2027" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_fund_body_is_400_with_field_errors()
    {
        var client = await host.LoginAsync(await host.CreateUserAsync());

        var response = await client.PostAsJsonAsync("/api/v1/funds", new { name = " ", schoolYear = "2026-2030" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("schoolYear", out _));
    }

    [Fact]
    public async Task Outsider_viewing_fund_is_404_member_is_200_FR07()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(fund, student);

        var outsider = await host.LoginAsync(await host.CreateUserAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/funds/{fund.Id}")).StatusCode);

        var asStudent = await host.LoginAsync(student);
        var body = await asStudent.GetFromJsonAsync<JsonElement>($"/api/v1/funds/{fund.Id}");
        Assert.Equal("Student", body.GetProperty("myRole").GetString());
        Assert.Equal(2, body.GetProperty("memberCount").GetInt32());
        Assert.Equal(owner.FullName, body.GetProperty("ownerName").GetString());
    }

    [Fact]
    public async Task Update_requires_fund_manage_FR06()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(fund, student);
        var body = new { name = "Lớp 10A1 (mới)", schoolYear = "2026-2027" };

        var asStudent = await host.LoginAsync(student);
        Assert.Equal(HttpStatusCode.Forbidden, (await asStudent.PutAsJsonAsync($"/api/v1/funds/{fund.Id}", body)).StatusCode);

        var asOwner = await host.LoginAsync(owner);
        var response = await asOwner.PutAsJsonAsync($"/api/v1/funds/{fund.Id}", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Lớp 10A1 (mới)", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await asOwner.PutAsJsonAsync($"/api/v1/funds/{Guid.NewGuid()}", body)).StatusCode);
    }

    [Fact]
    public async Task Write_without_antiforgery_token_is_400()
    {
        var user = await host.CreateUserAsync();
        var client = await host.LoginAsync(user);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var response = await client.PostAsJsonAsync("/api/v1/funds", new { name = "Lớp Y", schoolYear = "2026-2027" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Temporary_password_user_is_403_password_change_required()
    {
        var owner = await host.CreateUserAsync();
        var fund = await host.CreateFundAsync(owner);
        var student = await host.CreateUserAsync(UserOrigin.Provisioned, mustChangePassword: true);
        await host.AddMemberAsync(fund, student);

        var client = await host.LoginAsync(student);
        var response = await client.GetAsync($"/api/v1/funds/{fund.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("password.change_required", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
