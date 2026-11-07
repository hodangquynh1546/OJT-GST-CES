using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;

namespace ClassFund.Application.Features.Treasurers;

/// <inheritdoc />
/// <remarks>
/// Quyền đã được <c>[RequirePermission(Treasurer.Assign)]</c> kiểm tra ở API; service kiểm tra lại (roles-and-permissions.md mục 3.4).
/// Mọi thao tác ghi khóa dòng <c>Fund</c> trước: phân công / thu hồi của cùng một quỹ chạy lần lượt, nên
/// các kiểm tra "thủ quỹ cuối cùng", "đã phân công" đọc dữ liệu đã chốt của lệnh trước (TD-85).
/// Quy tắc "thủ quỹ cuối cùng" chỉ đếm phân công, không xét trạng thái tài khoản (PD-06).
/// </remarks>
public sealed class TreasurerService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    AuditTrail auditTrail,
    TimeProvider timeProvider) : ITreasurerService
{
    public const string AlreadyAssignedCode = "treasurer.already_assigned";
    public const string NoAccountCode = "treasurer.no_account";
    public const string AccountDisabledCode = "treasurer.account_disabled";
    public const string AccountNotActivatedCode = "treasurer.account_not_activated";
    public const string LastTreasurerCode = "treasurer.last";
    public const string AlreadyRevokedCode = "treasurer.already_revoked";

    private const string EntityType = nameof(TreasurerAssignment);

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
            .SelectMany(assignment => new[] { assignment.AssignedBy, assignment.RevokedBy ?? assignment.AssignedBy })
            .Distinct()
            .ToArray();
        var actorNames = (await db.ToListAsync(
                db.Query<User>().Where(user => actorIds.Contains(user.Id)).Select(user => new { user.Id, user.FullName }),
                cancellationToken))
            .ToDictionary(user => user.Id, user => user.FullName);

        return assignments
            .OrderBy(assignment => assignment.RevokedAt is not null)
            .ThenByDescending(assignment => assignment.AssignedAt)
            .ThenBy(assignment => assignment.Id)
            .Select(assignment => new TreasurerAssignmentResponse(
                assignment.Id,
                assignment.StudentId,
                studentNames.GetValueOrDefault(assignment.StudentId, string.Empty),
                assignment.RevokedAt is null,
                assignment.AssignedAt,
                actorNames.GetValueOrDefault(assignment.AssignedBy, string.Empty),
                assignment.RevokedAt,
                assignment.RevokedBy is { } revokedBy ? actorNames.GetValueOrDefault(revokedBy) : null))
            .ToArray();
    }

    public async Task<TreasurerAssignedResponse> AssignAsync(Guid fundId, Guid studentId, CancellationToken cancellationToken)
    {
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
                    throw new BusinessRuleException(AlreadyAssignedCode, "Học sinh này đang là thủ quỹ.");
                }

                var account = student.UserId is { } accountId
                    ? await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == accountId), token)
                    : null;
                if (account is null)
                {
                    throw new BusinessRuleException(NoAccountCode, "Học sinh chưa có tài khoản. Hãy cấp tài khoản trước.");
                }

                if (account.Status == AccountStatus.Disabled)
                {
                    throw new BusinessRuleException(AccountDisabledCode, "Tài khoản của học sinh đang bị khóa.");
                }

                // BR-31: chỉ phân công khi học sinh đã đổi mật khẩu tạm (kích hoạt).
                if (account.MustChangePassword)
                {
                    throw new BusinessRuleException(
                        AccountNotActivatedCode,
                        "Học sinh chưa kích hoạt tài khoản (chưa đổi mật khẩu tạm).");
                }

                var assignment = new TreasurerAssignment(fundId, student.Id, UserId, timeProvider.GetUtcNow());
                db.Add(assignment);
                auditTrail.Record(
                    AuditActions.TreasurerAssign,
                    AuditCategory.Change,
                    UserId,
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
        await RequireAssignAsync(fundId, cancellationToken);

        await db.InTransactionAsync(
            async token =>
            {
                // Khóa dòng quỹ TRƯỚC khi đếm: lệnh thu hồi thứ hai chờ ở đây tới khi lệnh đầu commit,
                // rồi mới đếm lại – nên không thể cả hai cùng thấy "còn ≥ 2 thủ quỹ".
                await LockFundAsync(fundId, token);

                var assignment = await db.FirstOrDefaultAsync(
                    db.Query<TreasurerAssignment>().Where(item => item.Id == assignmentId && item.FundId == fundId),
                    token) ?? throw new NotFoundException();

                if (assignment.RevokedAt is not null)
                {
                    throw new ConflictException(AlreadyRevokedCode, "Phân công này đã được thu hồi trước đó.");
                }

                var activeCount = await db.CountAsync(ActiveAssignments(fundId), token);
                if (activeCount <= 1)
                {
                    throw new BusinessRuleException(
                        LastTreasurerCode,
                        "Không thu hồi được thủ quỹ cuối cùng. Hãy phân công người mới trước rồi mới thu hồi người cũ.");
                }

                assignment.Revoke(UserId, timeProvider.GetUtcNow());
                auditTrail.Record(
                    AuditActions.TreasurerRevoke,
                    AuditCategory.Change,
                    UserId,
                    EntityType,
                    assignment.Id,
                    oldValues: new { studentId = assignment.StudentId, isActive = true },
                    newValues: new { isActive = false });

                await SaveAsync(token);
                return true;
            },
            cancellationToken);
    }

    private IQueryable<TreasurerAssignment> ActiveAssignments(Guid fundId) =>
        db.Query<TreasurerAssignment>().Where(assignment => assignment.FundId == fundId && assignment.RevokedAt == null);

    /// <summary><c>SELECT … FOR UPDATE</c> trên dòng <c>Fund</c>; giữ tới khi transaction kết thúc.</summary>
    private async Task LockFundAsync(Guid fundId, CancellationToken cancellationToken) =>
        _ = await db.LockAsync<Fund>(fundId, cancellationToken) ?? throw new NotFoundException();

    private async Task RequireAssignAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.TreasurerAssign))
        {
            throw new ForbiddenAccessException();
        }
    }

    /// <summary>Khóa dòng quỹ đã chặn race thông thường; UNIQUE lọc là chốt chặn cuối.</summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == "UQ_TreasurerAssignment_Student_Active")
        {
            throw new BusinessRuleException(AlreadyAssignedCode, "Học sinh này đang là thủ quỹ.");
        }
    }
}
