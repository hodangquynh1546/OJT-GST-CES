using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Features.Treasurers;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.UnitTests.Application;

/// <summary>M4-T3: TreasurersService (FR-12, FR-13, AZ-02, AZ-03).</summary>
public class TreasurersServiceTests
{
    private readonly ServiceTestContext _ctx = new();

    private TreasurersService CreateService() => new(_ctx.Db, _ctx.CurrentUser, _ctx.Access, _ctx.Audit);

    private FundMember MemberOf(User user) =>
        _ctx.Db.Query<FundMember>().Single(member => member.FundId == _ctx.Fund.Id && member.UserId == user.Id);

    // ---------- Danh sách ----------

    [Fact]
    public async Task List_shows_owner_first_then_treasurers_and_never_students()
    {
        var list = await CreateService().ListAsync(_ctx.Fund.Id, default);

        Assert.Equal([MemberRole.Owner, MemberRole.Treasurer], list.Select(item => item.Role).ToArray());
        Assert.Equal(_ctx.Owner.Id, list[0].UserId);
        Assert.Equal(_ctx.Treasurer.Id, list[1].UserId);
        Assert.DoesNotContain(list, item => item.UserId == _ctx.StudentA.Id);
    }

    [Fact]
    public async Task List_leaves_out_removed_treasurers_and_treasurers_of_other_funds()
    {
        MemberOf(_ctx.Treasurer).Status = MemberStatus.Removed;
        var other = new Fund("Quỹ khác", _ctx.Owner.Id, "2026-2027", ServiceTestContext.Start);
        _ctx.Db.Seed(other);
        _ctx.Db.Seed(new FundMember(other.Id, _ctx.StudentA.Id, MemberRole.Treasurer, ServiceTestContext.Start));

        var list = await CreateService().ListAsync(_ctx.Fund.Id, default);

        Assert.Equal(_ctx.Owner.Id, Assert.Single(list).UserId);
    }

    [Fact]
    public async Task List_reports_when_a_treasurer_was_granted_from_the_audit_log_and_null_for_the_owner()
    {
        var grantedAt = ServiceTestContext.Start.AddDays(3);
        _ctx.Clock.Now = grantedAt;
        await CreateService().GrantAsync(_ctx.Fund.Id, MemberOf(_ctx.StudentA).Id, default);

        var list = await CreateService().ListAsync(_ctx.Fund.Id, default);

        Assert.Null(list.Single(item => item.Role == MemberRole.Owner).GrantedAt);
        Assert.Equal(grantedAt, list.Single(item => item.UserId == _ctx.StudentA.Id).GrantedAt);
        Assert.Null(list.Single(item => item.UserId == _ctx.Treasurer.Id).GrantedAt); // phân quyền trước khi có nhật ký
    }

    // ---------- Chỉ thủ quỹ chính (AZ-02, FR-13) ----------

    [Fact]
    public async Task A_treasurer_cannot_list_grant_or_revoke_AZ02()
    {
        _ctx.ActAs(_ctx.Treasurer);
        var student = MemberOf(_ctx.StudentA);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().ListAsync(_ctx.Fund.Id, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().GrantAsync(_ctx.Fund.Id, student.Id, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().RevokeAsync(_ctx.Fund.Id, MemberOf(_ctx.Treasurer).Id, default));
        Assert.Equal(MemberRole.Student, student.Role);
    }

    [Fact]
    public async Task A_student_or_an_outsider_gets_no_access()
    {
        _ctx.ActAs(_ctx.StudentA);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService().ListAsync(_ctx.Fund.Id, default));

        _ctx.ActAs(_ctx.Outsider);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().ListAsync(_ctx.Fund.Id, default));
    }

    // ---------- Phân quyền ----------

    [Fact]
    public async Task Grant_turns_an_active_student_into_a_treasurer_and_audits_before_and_after()
    {
        var student = MemberOf(_ctx.StudentA);

        var dto = await CreateService().GrantAsync(_ctx.Fund.Id, student.Id, default);

        Assert.Equal(MemberRole.Treasurer, student.Role);
        Assert.Equal(MemberRole.Treasurer, dto.Role);
        Assert.Equal(_ctx.StudentA.Email, dto.Email);
        var log = Assert.Single(_ctx.AuditLogs);
        Assert.Equal(TreasurersAuditActions.Grant, log.Action);
        Assert.Equal(FinancialScope.General, log.Scope);
        Assert.Equal(_ctx.Fund.Id, log.FundId);
        Assert.Equal(_ctx.Owner.Id, log.ActorUserId);
        Assert.Equal(student.Id.ToString(), log.EntityId);
        Assert.Contains("Student", log.BeforeJson);
        Assert.Contains("Treasurer", log.AfterJson);
    }

    [Fact]
    public async Task Grant_takes_effect_immediately_without_signing_in_again()
    {
        var before = await _ctx.Access.GetFundAccessAsync(_ctx.StudentA.Id, _ctx.Fund.Id, default);
        Assert.DoesNotContain(Permissions.MemberManage, before.Permissions);

        await CreateService().GrantAsync(_ctx.Fund.Id, MemberOf(_ctx.StudentA).Id, default);

        var after = await _ctx.Access.GetFundAccessAsync(_ctx.StudentA.Id, _ctx.Fund.Id, default);
        Assert.Contains(Permissions.MemberManage, after.Permissions);
        Assert.Contains(Permissions.PaymentConfirm, after.Permissions);
        Assert.DoesNotContain(Permissions.TreasurerManage, after.Permissions); // thủ quỹ không phân quyền thủ quỹ khác
    }

    [Fact]
    public async Task Grant_twice_or_to_an_existing_treasurer_or_owner_is_409()
    {
        var student = MemberOf(_ctx.StudentA);
        await CreateService().GrantAsync(_ctx.Fund.Id, student.Id, default);

        foreach (var target in new[] { student, MemberOf(_ctx.Treasurer), MemberOf(_ctx.Owner) })
        {
            var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().GrantAsync(_ctx.Fund.Id, target.Id, default));
            Assert.Equal(TreasurersErrors.AlreadyTreasurerCode, error.Code);
        }

        Assert.Single(_ctx.AuditLogs); // lần thất bại không ghi nhật ký
    }

    [Fact]
    public async Task Grant_to_someone_not_in_this_fund_is_404()
    {
        var other = new Fund("Quỹ khác", _ctx.Outsider.Id, "2026-2027", ServiceTestContext.Start);
        var foreign = new FundMember(other.Id, _ctx.StudentB.Id, MemberRole.Student, ServiceTestContext.Start);
        _ctx.Db.Seed(other);
        _ctx.Db.Seed(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GrantAsync(_ctx.Fund.Id, foreign.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GrantAsync(_ctx.Fund.Id, Guid.NewGuid(), default));
        Assert.Equal(MemberRole.Student, foreign.Role);
    }

    [Fact]
    public async Task Grant_to_a_removed_member_is_refused()
    {
        var student = MemberOf(_ctx.StudentA);
        student.Status = MemberStatus.Removed;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().GrantAsync(_ctx.Fund.Id, student.Id, default));

        Assert.Equal(TreasurersErrors.MemberNotActiveCode, error.Code);
        Assert.Equal(MemberRole.Student, student.Role);
    }

    [Fact]
    public async Task Grant_in_an_archived_fund_is_refused_but_revoke_still_works()
    {
        _ctx.Fund.Status = FundStatus.Archived;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().GrantAsync(_ctx.Fund.Id, MemberOf(_ctx.StudentA).Id, default));
        Assert.Equal("fund.archived", error.Code);

        await CreateService().RevokeAsync(_ctx.Fund.Id, MemberOf(_ctx.Treasurer).Id, default);
        Assert.Equal(MemberRole.Student, MemberOf(_ctx.Treasurer).Role);
    }

    // ---------- Thu hồi ----------

    [Fact]
    public async Task Revoke_turns_a_treasurer_back_into_a_student_and_audits()
    {
        var treasurer = MemberOf(_ctx.Treasurer);

        var dto = await CreateService().RevokeAsync(_ctx.Fund.Id, treasurer.Id, default);

        Assert.Equal(MemberRole.Student, treasurer.Role);
        Assert.Equal(MemberRole.Student, dto.Role);
        var log = Assert.Single(_ctx.AuditLogs);
        Assert.Equal(TreasurersAuditActions.Revoke, log.Action);
        Assert.Contains("Treasurer", log.BeforeJson);
        Assert.Contains("Student", log.AfterJson);
    }

    [Fact]
    public async Task Revoke_takes_effect_immediately_and_the_person_stays_a_member()
    {
        await CreateService().RevokeAsync(_ctx.Fund.Id, MemberOf(_ctx.Treasurer).Id, default);

        var access = await _ctx.Access.GetFundAccessAsync(_ctx.Treasurer.Id, _ctx.Fund.Id, default);
        Assert.Equal(MemberRole.Student, access.Role);
        Assert.DoesNotContain(Permissions.MemberManage, access.Permissions);
        Assert.Equal(MemberStatus.Active, MemberOf(_ctx.Treasurer).Status);
    }

    [Fact]
    public async Task The_owner_cannot_be_revoked_not_even_by_themselves_AZ03()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().RevokeAsync(_ctx.Fund.Id, MemberOf(_ctx.Owner).Id, default));

        Assert.Equal(TreasurersErrors.CannotRevokeOwnerCode, error.Code);
        Assert.Equal(MemberRole.Owner, MemberOf(_ctx.Owner).Role);
        Assert.Empty(_ctx.AuditLogs);
    }

    [Fact]
    public async Task Revoke_a_student_is_409_and_a_foreign_member_is_404()
    {
        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateService().RevokeAsync(_ctx.Fund.Id, MemberOf(_ctx.StudentA).Id, default));
        Assert.Equal(TreasurersErrors.NotTreasurerCode, error.Code);

        var other = new Fund("Quỹ khác", _ctx.Outsider.Id, "2026-2027", ServiceTestContext.Start);
        var foreign = new FundMember(other.Id, _ctx.StudentB.Id, MemberRole.Treasurer, ServiceTestContext.Start);
        _ctx.Db.Seed(other);
        _ctx.Db.Seed(foreign);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().RevokeAsync(_ctx.Fund.Id, foreign.Id, default));
        Assert.Equal(MemberRole.Treasurer, foreign.Role);
    }

    [Fact]
    public async Task Grant_revoke_grant_again_reports_the_latest_grant_time()
    {
        var student = MemberOf(_ctx.StudentA);
        await CreateService().GrantAsync(_ctx.Fund.Id, student.Id, default);
        await CreateService().RevokeAsync(_ctx.Fund.Id, student.Id, default);
        var again = ServiceTestContext.Start.AddDays(10);
        _ctx.Clock.Now = again;

        var dto = await CreateService().GrantAsync(_ctx.Fund.Id, student.Id, default);

        Assert.Equal(again, dto.GrantedAt);
        Assert.Equal(3, _ctx.AuditLogs.Count());
    }
}
