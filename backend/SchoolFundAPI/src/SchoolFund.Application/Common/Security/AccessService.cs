using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Domain.Authorization;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Enums;

namespace SchoolFund.Application.Common.Security;

/// <inheritdoc />
public sealed class AccessService(IAppDb db) : IAccessService
{
    public async Task<IReadOnlySet<string>> GetAccountPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var kind = await db.FirstOrDefaultAsync(
            db.Query<User>().Where(user => user.Id == userId).Select(user => (UserKind?)user.Kind),
            cancellationToken);

        return kind is null ? new HashSet<string>() : RolePermissions.ForAccount(kind.Value);
    }

    public async Task<FundAccess> GetFundAccessAsync(Guid userId, Guid fundId, CancellationToken cancellationToken)
    {
        var role = await FindRoleAsync(userId, fundId, cancellationToken) ?? throw new NotFoundException();
        return new FundAccess(fundId, role, RolePermissions.For(role));
    }

    private async Task<FundRole?> FindRoleAsync(Guid userId, Guid fundId, CancellationToken cancellationToken)
    {
        var ownerUserId = await db.FirstOrDefaultAsync(
            db.Query<Fund>().Where(fund => fund.Id == fundId).Select(fund => (Guid?)fund.OwnerUserId),
            cancellationToken);

        if (ownerUserId is null)
        {
            return null;
        }

        if (ownerUserId == userId)
        {
            return FundRole.Teacher;
        }

        var fundStudents = db.Query<Student>().Where(student => student.FundId == fundId);

        var studentId = await db.FirstOrDefaultAsync(
            fundStudents.Where(student => student.UserId == userId).Select(student => (Guid?)student.Id),
            cancellationToken);

        if (studentId is not null)
        {
            var isTreasurer = await db.AnyAsync(
                db.Query<TreasurerAssignment>().Where(assignment =>
                    assignment.FundId == fundId
                    && assignment.StudentId == studentId
                    && assignment.RevokedAt == null),
                cancellationToken);

            return isTreasurer ? FundRole.Treasurer : FundRole.Student;
        }

        var isParent = await db.AnyAsync(
            db.Query<ParentLink>().Where(link =>
                link.IsActive
                && link.ParentUserId == userId
                && fundStudents.Any(student => student.Id == link.StudentId)),
            cancellationToken);

        return isParent ? FundRole.Parent : null;
    }
}
