using Microsoft.EntityFrameworkCore;
using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;
using SchoolFund.Infrastructure.Persistence;

namespace SchoolFund.Infrastructure.Security;

/// <summary>
/// Kiểm tra phiên ở mỗi request (roles-and-permissions.md mục 3.1): tài khoản còn hoạt động và SecurityStamp còn khớp.
/// Đổi / đặt lại mật khẩu hoặc khóa tài khoản đổi stamp → cookie cũ hết hiệu lực ngay.
/// </summary>
public sealed class IdentityStore(AppDbContext context) : ISessionStore
{
    public Task<ActiveSession?> FindActiveAsync(Guid userId, string securityStamp, CancellationToken cancellationToken) =>
        context.Set<User>()
            .AsNoTracking()
            .Where(user => user.Id == userId
                && user.Status == AccountStatus.Active
                && user.SecurityStamp == securityStamp)
            .Select(user => new ActiveSession(user.Id, user.MustChangePassword))
            .FirstOrDefaultAsync(cancellationToken);
}
