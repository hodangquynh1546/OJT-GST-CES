using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;

namespace ClassFund.Application.Features.Members;

/// <inheritdoc />
public sealed class MembersService(IAppDb db, IAccessService accessService, ICurrentUser currentUser) : IMembersService
{
    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    public async Task<IReadOnlyList<MemberResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.MemberView))
        {
            throw new ForbiddenAccessException();
        }

        var teacherName = await db.FirstOrDefaultAsync(
            db.Query<Fund>()
                .Where(fund => fund.Id == fundId)
                .Join(db.Query<User>(), fund => fund.OwnerUserId, user => user.Id, (fund, user) => user.FullName),
            cancellationToken) ?? throw new NotFoundException();

        var students = await db.ToListAsync(
            db.Query<Student>().Where(student => student.FundId == fundId).Select(student => new { student.Id, student.FullName }),
            cancellationToken);
        var treasurerIds = (await db.ToListAsync(
                db.Query<TreasurerAssignment>()
                    .Where(assignment => assignment.FundId == fundId && assignment.RevokedAt == null)
                    .Select(assignment => assignment.StudentId),
                cancellationToken))
            .ToHashSet();

        var members = new List<MemberResponse> { new(teacherName, FundRole.Teacher) };
        members.AddRange(students
            .Where(student => treasurerIds.Contains(student.Id))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .Select(student => new MemberResponse(student.FullName, FundRole.Treasurer)));
        members.AddRange(students
            .Where(student => !treasurerIds.Contains(student.Id))
            .OrderBy(student => student.FullName, StringComparer.CurrentCulture)
            .Select(student => new MemberResponse(student.FullName, FundRole.Student)));
        return members;
    }
}
