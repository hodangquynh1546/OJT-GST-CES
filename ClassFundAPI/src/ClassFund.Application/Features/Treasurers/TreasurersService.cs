using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Treasurers;

/// <inheritdoc />
/// <remarks>
/// Quyền đã được <c>[RequirePermission(Treasurer.Assign)]</c> kiểm tra ở API; service kiểm tra lại qua <see cref="IAccessService"/>
/// (roles-and-permissions.md mục 3). Mọi thao tác ghi khóa dòng <c>Fund</c> trước: phân công / thu hồi của cùng một quỹ
/// chạy lần lượt, nên kiểm tra "thủ quỹ cuối cùng", "đang là thủ quỹ" đọc dữ liệu đã chốt của lệnh trước.
/// Quy tắc "thủ quỹ cuối cùng" chỉ đếm phân công đang hiệu lực, không xét trạng thái tài khoản.
/// </remarks>
public sealed class TreasurersService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    AuditTrail auditTrail,
    TimeProvider timeProvider) : ITreasurersService
{
    private const string EntityType = nameof(TreasurerAssignment);
    private const string ActiveStudentIndex = "UQ_TreasurerAssignment_Student_Active";

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    public async Task<IReadOnlyList<TreasurerAssignmentResponse>> ListAsync(Guid fundId, CancellationToken cancellationToken)
    {
        await RequireAssignAsync(fundId, cancellationToken);

        var assignments = await db.ToListAsync(
            db.Query<TreasurerAssignment>().Where(assignment => assignment.FundId == fundId),
            cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        var studentIds = assignments.Select(assignment => assignment.StudentId).Distinct().ToArray();
        var studentNames = (await db.ToListAsync(
                db.Query<Student>()
                    .Where(student => student.FundId == fundId && studentIds.Contains(student.Id))
                    .Select(student => new { student.Id, student.FullName }),
                cancellationToken))
            .ToDictionary(student => student.Id, student => student.FullName);

        var actorIds = assignments
            .Select(assignment => assignment.AssignedBy)
            .Concat(assignments.Where(assignment => assignment.RevokedBy != null).Select(assignment => assignment.RevokedBy!.Value))
            .Distinct()
            .ToArray();
        var actorNames = (await db.ToListAsync(
                db.Query<User>().Where(user => actorIds.Contains(user.Id)).Select(user => new { user.Id, user.FullName }),
                cancellationToken))
            .ToDictionary(user => user.Id, user => user.FullName);

        return assignments
            .OrderBy(assignment => assignment.RevokedAt != null)
            .ThenByDescending(assignment => assignment.AssignedAt)
            .ThenByDescending(assignment => assignment.Id)
            .Select(assignment => new TreasurerAssignmentResponse(
                assignment.Id,
                assignment.StudentId,
                studentNames.GetValueOrDefault(assignment.StudentId, string.Empty),
                assignment.RevokedAt == null,
                assignment.AssignedAt,
                actorNames.GetValueOrDefault(assignment.AssignedBy, string.Empty),
                assignment.RevokedAt,
                assignment.RevokedBy is { } revokedBy ? actorNames.GetValueOrDefault(revokedBy, string.Empty) : null))
            .ToArray();
    }

    public async Task<TreasurerAssignedResponse> AssignAsync(Guid fundId, Guid studentId, CancellationToken cancellationToken)
    {
        var userId = UserId;
        await RequireAssignAsync(fundId, cancellationToken);

        return await db.InTransactionAsync(
            async token =>
            {
                await LockFundAsync(fundId, token);

                var student = await db.FirstOrDefaultAsync(
                    db.Query<Student>().Where(item => item.Id == studentId && item.FundId == fundId),
                    token) ?? throw new NotFoundException();

                if (await db.AnyAsync(ActiveAssignments(fundId).Where(assignment => assignment.StudentId == student.Id), token))
                {
                    throw TreasurersErrors.AlreadyAssigned();
                }

                var account = student.UserId is { } accountId
                    ? await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == accountId), token)
                    : null;

                // BR-31: chỉ học sinh có tài khoản đã kích hoạt (đã đổi mật khẩu tạm) và không bị khóa.
                switch (StudentAccount.StatusOf(account))
                {
                    case StudentAccountStatus.None:
                        throw TreasurersErrors.NoAccount();
                    case StudentAccountStatus.Disabled:
                        throw TreasurersErrors.AccountDisabled();
                    case StudentAccountStatus.PendingActivation:
                        throw TreasurersErrors.AccountNotActivated();
                }

                var assignment = new TreasurerAssignment(fundId, student.Id, userId, timeProvider.GetUtcNow());
                db.Add(assignment);
                auditTrail.Record(
                    TreasurersAuditActions.Assign,
                    AuditCategory.Change,
                    userId,
                    EntityType,
                    assignment.Id,
                    newValues: new { fundId, studentId = student.Id, studentName = student.FullName });

                await SaveAsync(token);
                return new TreasurerAssignedResponse(assignment.Id);
            },
            cancellationToken);
    }

    public async Task RevokeAsync(Guid fundId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var userId = UserId;
        await RequireAssignAsync(fundId, cancellationToken);

        await db.InTransactionAsync(
            async token =>
            {
                // Khóa dòng quỹ TRƯỚC khi đếm: lệnh thu hồi thứ hai chờ ở đây tới khi lệnh đầu commit rồi mới đếm lại,
                // nên hai lệnh không thể cùng thấy "còn ≥ 2 thủ quỹ" (treasurer.last – BR-32, NFR-09).
                await LockFundAsync(fundId, token);

                var assignment = await db.FirstOrDefaultAsync(
                    db.Query<TreasurerAssignment>().Where(item => item.Id == assignmentId && item.FundId == fundId),
                    token) ?? throw new NotFoundException();

                if (assignment.RevokedAt is not null)
                {
                    throw TreasurersErrors.AlreadyRevoked();
                }

                if (await db.CountAsync(ActiveAssignments(fundId), token) <= 1)
                {
                    throw TreasurersErrors.Last();
                }

                assignment.Revoke(userId, timeProvider.GetUtcNow());
                auditTrail.Record(
                    TreasurersAuditActions.Revoke,
                    AuditCategory.Change,
                    userId,
                    EntityType,
                    assignment.Id,
                    oldValues: new { fundId, studentId = assignment.StudentId, isActive = true },
                    newValues: new { isActive = false });

                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);
    }

    private IQueryable<TreasurerAssignment> ActiveAssignments(Guid fundId) =>
        db.Query<TreasurerAssignment>().Where(assignment => assignment.FundId == fundId && assignment.RevokedAt == null);

    /// <summary><c>SELECT … FOR UPDATE</c> trên dòng <c>Fund</c>; giữ tới khi transaction kết thúc.</summary>
    private async Task LockFundAsync(Guid fundId, CancellationToken cancellationToken) =>
        _ = await db.LockAsync<Fund>(fundId, cancellationToken) ?? throw new NotFoundException();

    /// <summary>Không phải thành viên → 404 (<see cref="IAccessService.GetFundAccessAsync"/>); thành viên thiếu quyền → 403.</summary>
    private async Task RequireAssignAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.TreasurerAssign))
        {
            throw new ForbiddenAccessException();
        }
    }

    /// <summary>Khóa dòng quỹ đã chặn race thông thường; UNIQUE lọc theo học sinh là chốt chặn cuối.</summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == ActiveStudentIndex)
        {
            throw TreasurersErrors.AlreadyAssigned();
        }
    }
}
