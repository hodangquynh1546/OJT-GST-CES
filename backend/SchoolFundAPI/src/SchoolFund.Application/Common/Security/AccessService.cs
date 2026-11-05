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

    public async Task<IReadOnlyList<FundMembership>> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var funds = db.Query<Fund>();
        var students = db.Query<Student>();
        var activeAssignments = db.Query<TreasurerAssignment>().Where(assignment => assignment.RevokedAt == null);

        var owned = await db.ToListAsync(
            funds.Where(fund => fund.OwnerUserId == userId).Select(fund => new { fund.Id, fund.Name }),
            cancellationToken);

        var asStudent = await db.ToListAsync(
            from student in students
            where student.UserId == userId
            join fund in funds on student.FundId equals fund.Id
            select new
            {
                fund.Id,
                fund.Name,
                IsTreasurer = activeAssignments.Any(assignment =>
                    assignment.FundId == fund.Id && assignment.StudentId == student.Id),
            },
            cancellationToken);

        var asParent = await db.ToListAsync(
            from link in db.Query<ParentLink>()
            where link.IsActive && link.ParentUserId == userId
            join student in students on link.StudentId equals student.Id
            join fund in funds on student.FundId equals fund.Id
            select new { fund.Id, fund.Name, Child = student.FullName },
            cancellationToken);

        // Thứ tự ưu tiên vai trò theo roles-and-permissions.md mục 1: GVCN → Thủ quỹ → Học sinh → Phụ huynh.
        var memberships = new Dictionary<Guid, FundMembership>();
        foreach (var fund in owned)
        {
            memberships.TryAdd(fund.Id, Membership(fund.Id, fund.Name, FundRole.Teacher));
        }

        foreach (var row in asStudent)
        {
            memberships.TryAdd(row.Id, Membership(row.Id, row.Name, row.IsTreasurer ? FundRole.Treasurer : FundRole.Student));
        }

        foreach (var group in asParent.GroupBy(row => (row.Id, row.Name)))
        {
            var children = group.Select(row => row.Child).Order(StringComparer.CurrentCulture).ToArray();
            memberships.TryAdd(group.Key.Id, Membership(group.Key.Id, group.Key.Name, FundRole.Parent, children));
        }

        return memberships.Values
            .OrderBy(membership => membership.FundName, StringComparer.CurrentCulture)
            .ThenBy(membership => membership.FundId)
            .ToArray();
    }

    private static FundMembership Membership(Guid fundId, string fundName, FundRole role, IReadOnlyList<string>? linkedStudents = null) =>
        new(fundId, fundName, role, RolePermissions.For(role), linkedStudents ?? []);

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
