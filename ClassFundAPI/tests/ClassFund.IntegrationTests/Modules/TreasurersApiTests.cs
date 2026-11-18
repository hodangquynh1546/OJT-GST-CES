using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.IntegrationTests.Modules;

/// <summary>M4-T3 – Thủ quỹ: mỗi endpoint 200/201, 400, 401, 403, 404 và các quy tắc FR-12, FR-13, AZ-02, AZ-03.</summary>
[Collection(ApiCollection.Name)]
public class TreasurersApiTests(ApiTestHost host)
{
    private static string Url(Guid fundId, string path = "") => $"/api/v1/funds/{fundId}/treasurers{path}";

    private static string MembersUrl(Guid fundId) => $"/api/v1/funds/{fundId}/members";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    /// <summary>Quỹ có Owner, một học sinh và một thủ quỹ; mỗi người một client đã đăng nhập.</summary>
    private async Task<Setup> SetupAsync()
    {
        var owner = await host.CreateUserAsync();
        var student = await host.CreateUserAsync(UserOrigin.Provisioned);
        var treasurer = await host.CreateUserAsync(UserOrigin.Provisioned);
        var fund = await host.CreateFundAsync(owner);
        await host.AddMemberAsync(fund, student);
        await host.AddMemberAsync(fund, treasurer, MemberRole.Treasurer);

        await using var db = host.CreateDbContext();
        Guid MemberId(User user) => db.FundMembers.Single(member => member.FundId == fund.Id && member.UserId == user.Id).Id;

        return new Setup(
            fund.Id,
            owner,
            student,
            treasurer,
            MemberId(owner),
            MemberId(student),
            MemberId(treasurer),
            await host.LoginAsync(owner),
            await host.LoginAsync(student),
            await host.LoginAsync(treasurer));
    }

    private sealed record Setup(
        Guid FundId,
        User Owner,
        User Student,
        User Treasurer,
        Guid OwnerMemberId,
        Guid StudentMemberId,
        Guid TreasurerMemberId,
        HttpClient OwnerClient,
        HttpClient StudentClient,
        HttpClient TreasurerClient);

    // ---------- 401 / 403 / 404 ----------

    [Fact]
    public async Task Anonymous_gets_401_on_every_endpoint()
    {
        var s = await SetupAsync();
        var anonymous = host.Anonymous();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(s.FundId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Url(s.FundId, $"/{s.TreasurerMemberId}/revoke"), null)).StatusCode);
    }

    [Fact]
    public async Task A_treasurer_gets_403_on_every_endpoint_FR13()
    {
        var s = await SetupAsync();

        var responses = new[]
        {
            await s.TreasurerClient.GetAsync(Url(s.FundId)),
            await s.TreasurerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId }),
            await s.TreasurerClient.PostAsync(Url(s.FundId, $"/{s.TreasurerMemberId}/revoke"), null),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        Assert.Equal("forbidden", await CodeOfAsync(responses[0]));
        await using var db = host.CreateDbContext();
        Assert.Equal(MemberRole.Student, (await db.FundMembers.SingleAsync(member => member.Id == s.StudentMemberId)).Role);
    }

    [Fact]
    public async Task A_student_gets_403_and_a_person_outside_the_fund_gets_404()
    {
        var s = await SetupAsync();
        var stranger = await host.LoginAsync(await host.CreateUserAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await s.StudentClient.GetAsync(Url(s.FundId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.StudentClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Url(s.FundId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId })).StatusCode);
    }

    [Fact]
    public async Task Grant_to_someone_not_in_this_fund_or_unknown_is_404()
    {
        var s = await SetupAsync();
        var otherOwner = await host.CreateUserAsync();
        var otherFund = await host.CreateFundAsync(otherOwner);
        var foreignUser = await host.CreateUserAsync(UserOrigin.Provisioned);
        await host.AddMemberAsync(otherFund, foreignUser);
        await using var db = host.CreateDbContext();
        var foreignMemberId = (await db.FundMembers.SingleAsync(member => member.FundId == otherFund.Id && member.UserId == foreignUser.Id)).Id;

        Assert.Equal(HttpStatusCode.NotFound, (await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = foreignMemberId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OwnerClient.PostAsync(Url(s.FundId, $"/{foreignMemberId}/revoke"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OwnerClient.PostAsync(Url(s.FundId, $"/{Guid.NewGuid()}/revoke"), null)).StatusCode);
        Assert.Equal(MemberRole.Student, (await db.FundMembers.SingleAsync(member => member.Id == foreignMemberId)).Role);
    }

    [Fact]
    public async Task Grant_with_an_empty_member_id_is_400()
    {
        var s = await SetupAsync();

        var response = await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeOfAsync(response));
        Assert.True((await ReadAsync(response)).GetProperty("errors").TryGetProperty("memberId", out _));
    }

    // ---------- Danh sách ----------

    [Fact]
    public async Task Owner_lists_the_team_with_owner_first_and_no_students()
    {
        var s = await SetupAsync();

        var response = await s.OwnerClient.GetAsync(Url(s.FundId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await ReadAsync(response)).EnumerateArray().ToArray();
        Assert.Equal(["Owner", "Treasurer"], rows.Select(row => row.GetProperty("role").GetString()!).ToArray());
        Assert.Equal(s.OwnerMemberId, rows[0].GetProperty("memberId").GetGuid());
        Assert.Equal(s.Treasurer.Email, rows[1].GetProperty("email").GetString());
        Assert.DoesNotContain(rows, row => row.GetProperty("memberId").GetGuid() == s.StudentMemberId);
    }

    // ---------- Phân quyền (FR-12) ----------

    [Fact]
    public async Task Grant_makes_a_student_a_treasurer_and_takes_effect_on_the_next_request_without_signing_in_again()
    {
        var s = await SetupAsync();
        // Trước khi phân quyền: học sinh không quản lý được thành viên.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.StudentClient.GetAsync(MembersUrl(s.FundId))).StatusCode);

        var grant = await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId });

        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        var body = await ReadAsync(grant);
        Assert.Equal("Treasurer", body.GetProperty("role").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("grantedAt").ValueKind);

        // Cùng một phiên (cookie cũ), không đăng nhập lại: quyền mới có hiệu lực ngay.
        Assert.Equal(HttpStatusCode.OK, (await s.StudentClient.GetAsync(MembersUrl(s.FundId))).StatusCode);
        // Nhưng thủ quỹ không phân quyền thủ quỹ khác (FR-13).
        Assert.Equal(HttpStatusCode.Forbidden, (await s.StudentClient.GetAsync(Url(s.FundId))).StatusCode);

        var team = (await ReadAsync(await s.OwnerClient.GetAsync(Url(s.FundId)))).EnumerateArray().ToArray();
        Assert.Equal(3, team.Length);
    }

    [Fact]
    public async Task Grant_is_audited_for_the_fund_with_the_owner_as_actor()
    {
        var s = await SetupAsync();
        await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId });

        await using var db = host.CreateDbContext();
        var log = await db.AuditLogs.SingleAsync(item => item.EntityId == s.StudentMemberId.ToString() && item.Action == "treasurer.grant");
        Assert.Equal(s.FundId, log.FundId);
        Assert.Equal(s.Owner.Id, log.ActorUserId);
        Assert.Equal(FinancialScope.General, log.Scope);
        Assert.Contains("Student", log.BeforeJson);
        Assert.Contains("Treasurer", log.AfterJson);
    }

    [Fact]
    public async Task Grant_twice_or_to_the_owner_is_409()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId })).StatusCode);

        var again = await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("treasurer.already_treasurer", await CodeOfAsync(again));

        var existing = await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.TreasurerMemberId });
        Assert.Equal(HttpStatusCode.Conflict, existing.StatusCode);
        var owner = await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.OwnerMemberId });
        Assert.Equal(HttpStatusCode.Conflict, owner.StatusCode);
    }

    [Fact]
    public async Task Grant_to_a_removed_member_is_422()
    {
        var s = await SetupAsync();
        await using (var db = host.CreateDbContext())
        {
            var member = await db.FundMembers.SingleAsync(item => item.Id == s.StudentMemberId);
            member.Status = MemberStatus.Removed;
            await db.SaveChangesAsync();
        }

        var response = await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("treasurer.member_not_active", await CodeOfAsync(response));
    }

    [Fact]
    public async Task Grant_in_an_archived_fund_is_422()
    {
        var s = await SetupAsync();
        await using (var db = host.CreateDbContext())
        {
            (await db.Funds.SingleAsync(fund => fund.Id == s.FundId)).Status = FundStatus.Archived;
            await db.SaveChangesAsync();
        }

        var response = await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fund.archived", await CodeOfAsync(response));
    }

    // ---------- Thu hồi (FR-12, AZ-03) ----------

    [Fact]
    public async Task Revoke_returns_the_treasurer_to_student_and_takes_effect_on_the_next_request()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.OK, (await s.TreasurerClient.GetAsync(MembersUrl(s.FundId))).StatusCode);

        var revoke = await s.OwnerClient.PostAsync(Url(s.FundId, $"/{s.TreasurerMemberId}/revoke"), null);

        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal("Student", (await ReadAsync(revoke)).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await s.TreasurerClient.GetAsync(MembersUrl(s.FundId))).StatusCode); // cùng phiên, mất quyền ngay
        Assert.Equal(HttpStatusCode.OK, (await s.TreasurerClient.GetAsync("/api/v1/auth/me")).StatusCode); // vẫn đăng nhập, vẫn là thành viên

        var team = (await ReadAsync(await s.OwnerClient.GetAsync(Url(s.FundId)))).EnumerateArray().ToArray();
        Assert.Single(team);
        await using var db = host.CreateDbContext();
        Assert.Equal(MemberStatus.Active, (await db.FundMembers.SingleAsync(member => member.Id == s.TreasurerMemberId)).Status);
        Assert.True(await db.AuditLogs.AnyAsync(log => log.EntityId == s.TreasurerMemberId.ToString() && log.Action == "treasurer.revoke" && log.ActorUserId == s.Owner.Id));
    }

    [Fact]
    public async Task The_owner_cannot_be_revoked_AZ03()
    {
        var s = await SetupAsync();

        var response = await s.OwnerClient.PostAsync(Url(s.FundId, $"/{s.OwnerMemberId}/revoke"), null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("treasurer.cannot_revoke_owner", await CodeOfAsync(response));
        await using var db = host.CreateDbContext();
        Assert.Equal(MemberRole.Owner, (await db.FundMembers.SingleAsync(member => member.Id == s.OwnerMemberId)).Role);
    }

    [Fact]
    public async Task Revoke_a_student_is_409()
    {
        var s = await SetupAsync();

        var response = await s.OwnerClient.PostAsync(Url(s.FundId, $"/{s.StudentMemberId}/revoke"), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("treasurer.not_treasurer", await CodeOfAsync(response));
    }

    [Fact]
    public async Task Grant_revoke_and_grant_again_works_and_keeps_one_owner()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerClient.PostAsync(Url(s.FundId, $"/{s.StudentMemberId}/revoke"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await s.OwnerClient.PostAsJsonAsync(Url(s.FundId), new { memberId = s.StudentMemberId })).StatusCode);

        await using var db = host.CreateDbContext();
        Assert.Equal(1, await db.FundMembers.CountAsync(member => member.FundId == s.FundId && member.Role == MemberRole.Owner));
        Assert.Equal(3, await db.AuditLogs.CountAsync(log => log.EntityId == s.StudentMemberId.ToString()));
    }
}
