using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Common.Security;

/// <summary>Vai trò và permission của một người dùng trong một quỹ.</summary>
public sealed record FundAccess(Guid FundId, MemberRole Role, IReadOnlySet<string> Permissions);

/// <summary>Một quỹ người dùng là thành viên đang hoạt động (cho <c>GET /auth/me</c>).</summary>
public sealed record FundMembership(Guid FundId, string FundName, MemberRole Role, IReadOnlySet<string> Permissions);

/// <summary>Suy ra quyền từ dữ liệu (FundMember) theo docs/business-analysis/roles-and-permissions.md.</summary>
public interface IAccessService
{
    /// <summary>Quyền cấp tài khoản, không gắn quỹ (ví dụ <c>Fund.Create</c>).</summary>
    Task<IReadOnlySet<string>> GetAccountPermissionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <exception cref="NotFoundException">Quỹ không tồn tại hoặc người dùng không phải thành viên đang hoạt động.</exception>
    Task<FundAccess> GetFundAccessAsync(Guid userId, Guid fundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FundMembership>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class AccessService(IAppDb db) : IAccessService
{
    public async Task<IReadOnlySet<string>> GetAccountPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var origin = await db.FirstOrDefaultAsync(
            db.Query<User>().Where(user => user.Id == userId && user.IsActive).Select(user => (UserOrigin?)user.Origin),
            cancellationToken);

        return origin is null ? new HashSet<string>() : RolePermissions.ForAccount(origin.Value);
    }

    public async Task<FundAccess> GetFundAccessAsync(Guid userId, Guid fundId, CancellationToken cancellationToken)
    {
        var role = await db.FirstOrDefaultAsync(
            db.Query<FundMember>()
                .Where(member => member.FundId == fundId && member.UserId == userId && member.Status == MemberStatus.Active)
                .Select(member => (MemberRole?)member.Role),
            cancellationToken);

        return role is null
            ? throw new NotFoundException()
            : new FundAccess(fundId, role.Value, RolePermissions.For(role.Value));
    }

    public async Task<IReadOnlyList<FundMembership>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.ToListAsync(
            from member in db.Query<FundMember>()
            where member.UserId == userId && member.Status == MemberStatus.Active
            join fund in db.Query<Fund>() on member.FundId equals fund.Id
            orderby fund.Name, fund.Id
            select new { fund.Id, fund.Name, member.Role },
            cancellationToken);

        return rows.Select(row => new FundMembership(row.Id, row.Name, row.Role, RolePermissions.For(row.Role))).ToArray();
    }
}
