using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Treasurers;

/// <inheritdoc />
public sealed class TreasurersService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    AuditTrail audit) : ITreasurersService
{
    public async Task<IReadOnlyList<TreasurerDto>> ListAsync(Guid fundId, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(fundId, cancellationToken);

        var rows = await db.ToListAsync(
            from member in db.Query<FundMember>()
            where member.FundId == fundId && member.Status == MemberStatus.Active && member.Role != MemberRole.Student
            join user in db.Query<User>() on member.UserId equals user.Id
            orderby member.Role == MemberRole.Owner ? 0 : 1, user.FullName, member.Id
            select new { member, user },
            cancellationToken);

        var grants = await LatestGrantTimesAsync(rows.Select(row => row.member.Id).ToArray(), cancellationToken);
        return rows.Select(row => ToDto(row.member, row.user, GrantedAt(grants, row.member.Id))).ToArray();
    }

    public async Task<TreasurerDto> GrantAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken)
    {
        var actorId = await EnsureOwnerAsync(fundId, cancellationToken);

        var result = await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                var member = await db.LockAsync<FundMember>(memberId, token);
                if (member is null || member.FundId != fundId)
                {
                    throw new NotFoundException();
                }

                // Quỹ đã tổng kết chỉ được xem: không cấp thêm quyền mới (BR-37).
                YearEndRule.EnsureFundActive(fund.Status);

                if (member.Status != MemberStatus.Active)
                {
                    throw new BusinessRuleException(TreasurersErrors.MemberNotActiveCode, TreasurersErrors.MemberNotActiveMessage);
                }

                if (member.Role != MemberRole.Student)
                {
                    throw new ConflictException(TreasurersErrors.AlreadyTreasurerCode, TreasurersErrors.AlreadyTreasurerMessage);
                }

                member.Role = MemberRole.Treasurer;
                audit.Record(new AuditEntry(
                    TreasurersAuditActions.Grant,
                    fundId,
                    actorId,
                    FinancialScope.General,
                    EntityType: nameof(FundMember),
                    EntityId: member.Id,
                    Before: new { role = nameof(MemberRole.Student) },
                    After: new { role = nameof(MemberRole.Treasurer) }));
                await db.SaveChangesAsync(token);
                return member;
            },
            cancellationToken);

        return await ToDtoAsync(result, cancellationToken);
    }

    public async Task<TreasurerDto> RevokeAsync(Guid fundId, Guid memberId, CancellationToken cancellationToken)
    {
        var actorId = await EnsureOwnerAsync(fundId, cancellationToken);

        var result = await db.InTransactionAsync(
            async token =>
            {
                var member = await db.LockAsync<FundMember>(memberId, token);
                if (member is null || member.FundId != fundId)
                {
                    throw new NotFoundException();
                }

                if (member.Role == MemberRole.Owner)
                {
                    throw new BusinessRuleException(TreasurersErrors.CannotRevokeOwnerCode, TreasurersErrors.CannotRevokeOwnerMessage);
                }

                if (member.Role != MemberRole.Treasurer)
                {
                    throw new ConflictException(TreasurersErrors.NotTreasurerCode, TreasurersErrors.NotTreasurerMessage);
                }

                member.Role = MemberRole.Student;
                audit.Record(new AuditEntry(
                    TreasurersAuditActions.Revoke,
                    fundId,
                    actorId,
                    FinancialScope.General,
                    EntityType: nameof(FundMember),
                    EntityId: member.Id,
                    Before: new { role = nameof(MemberRole.Treasurer) },
                    After: new { role = nameof(MemberRole.Student) }));
                await db.SaveChangesAsync(token);
                return member;
            },
            cancellationToken);

        return await ToDtoAsync(result, cancellationToken);
    }

    /// <summary>Chỉ thủ quỹ chính được dùng module này (AZ-02, FR-13). Controller đã yêu cầu <c>Treasurer.Manage</c>; kiểm lại để service không dựa vào nơi gọi.</summary>
    private async Task<Guid> EnsureOwnerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId
            ?? throw new AuthenticationFailedException(AuthenticationFailedException.UnauthorizedCode, "Bạn chưa đăng nhập.");
        var access = await accessService.GetFundAccessAsync(actorId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.TreasurerManage))
        {
            throw new ForbiddenAccessException();
        }

        return actorId;
    }

    private async Task<TreasurerDto> ToDtoAsync(FundMember member, CancellationToken cancellationToken)
    {
        var user = await db.FirstOrDefaultAsync(db.Query<User>().Where(item => item.Id == member.UserId), cancellationToken)
            ?? throw new NotFoundException();
        var grants = await LatestGrantTimesAsync([member.Id], cancellationToken);
        return ToDto(member, user, GrantedAt(grants, member.Id));
    }

    /// <summary>Thời điểm phân quyền gần nhất của từng thành viên, lấy từ nhật ký (FundMember không lưu riêng).</summary>
    private async Task<Dictionary<Guid, DateTimeOffset>> LatestGrantTimesAsync(Guid[] memberIds, CancellationToken cancellationToken)
    {
        if (memberIds.Length == 0)
        {
            return [];
        }

        var ids = memberIds.Select(id => id.ToString()).ToArray();
        var rows = await db.ToListAsync(
            db.Query<AuditLog>()
                .Where(log => log.Action == TreasurersAuditActions.Grant && log.EntityId != null && ids.Contains(log.EntityId))
                .Select(log => new { log.EntityId, log.At }),
            cancellationToken);

        return rows
            .GroupBy(row => Guid.Parse(row.EntityId!))
            .ToDictionary(group => group.Key, group => group.Max(row => row.At));
    }

    /// <summary>null khi chưa có nhật ký phân quyền (không dùng GetValueOrDefault: sẽ ra ngày 0001-01-01).</summary>
    private static DateTimeOffset? GrantedAt(Dictionary<Guid, DateTimeOffset> grants, Guid memberId) =>
        grants.TryGetValue(memberId, out var at) ? at : null;

    private static TreasurerDto ToDto(FundMember member, User user, DateTimeOffset? grantedAt) =>
        new(member.Id, user.Id, user.FullName, user.Email, member.Role, member.JoinedAt, grantedAt);
}
