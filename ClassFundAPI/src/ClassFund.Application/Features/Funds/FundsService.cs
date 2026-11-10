using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Funds;

/// <inheritdoc />
/// <remarks>
/// Quyền đã được <c>[RequirePermission]</c> kiểm tra ở API; service kiểm tra lại qua <see cref="IAccessService"/>
/// (phòng thủ nhiều lớp – roles-and-permissions.md mục 3).
/// </remarks>
public sealed class FundsService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    AuditTrail auditTrail,
    TimeProvider timeProvider) : IFundsService
{
    private const string EntityType = nameof(Fund);

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    public async Task<IReadOnlyList<FundSummaryResponse>> ListMineAsync(CancellationToken cancellationToken)
    {
        var memberships = await accessService.GetMembershipsAsync(UserId, cancellationToken);
        if (memberships.Count == 0)
        {
            return [];
        }

        var fundIds = memberships.Select(membership => membership.FundId).ToArray();
        var ownedFundIds = memberships
            .Where(membership => membership.Role == FundRole.Teacher)
            .Select(membership => membership.FundId)
            .ToArray();

        var createdAt = (await db.ToListAsync(
                db.Query<Fund>()
                    .Where(fund => fundIds.Contains(fund.Id))
                    .Select(fund => new { fund.Id, fund.CreatedAt }),
                cancellationToken))
            .ToDictionary(fund => fund.Id, fund => fund.CreatedAt);

        // BR-02: số dư = tổng thu − tổng chi của các khoản chưa hủy; giống nhau ở mọi vai trò (BR-15).
        var balances = (await db.ToListAsync(
                db.Query<FundTransaction>()
                    .Where(transaction => fundIds.Contains(transaction.FundId) && !transaction.IsCancelled)
                    .GroupBy(transaction => transaction.FundId)
                    .Select(group => new
                    {
                        FundId = group.Key,
                        Balance = group.Sum(transaction =>
                            transaction.Type == TransactionType.Income ? transaction.Amount : -transaction.Amount),
                    }),
                cancellationToken))
            .ToDictionary(row => row.FundId, row => row.Balance);

        // BR-23: chỉ họ tên thủ quỹ, không email / tên đăng nhập / liên hệ.
        var treasurers = (await db.ToListAsync(
                from assignment in db.Query<TreasurerAssignment>()
                where fundIds.Contains(assignment.FundId) && assignment.RevokedAt == null
                join student in db.Query<Student>() on assignment.StudentId equals student.Id
                select new { assignment.FundId, student.FullName },
                cancellationToken))
            .ToLookup(row => row.FundId, row => row.FullName);

        // Số học sinh chỉ GVCN thấy (FR-22): gồm cả học sinh chưa có tài khoản.
        var studentCounts = ownedFundIds.Length == 0
            ? []
            : (await db.ToListAsync(
                    db.Query<Student>()
                        .Where(student => ownedFundIds.Contains(student.FundId))
                        .GroupBy(student => student.FundId)
                        .Select(group => new { FundId = group.Key, Count = group.Count() }),
                    cancellationToken))
                .ToDictionary(row => row.FundId, row => row.Count);

        return memberships
            .Select(membership =>
            {
                var treasurerNames = treasurers[membership.FundId].Order(StringComparer.CurrentCulture).ToArray();
                return new FundSummaryResponse(
                    membership.FundId,
                    membership.FundName,
                    membership.Role,
                    membership.Permissions.Order(StringComparer.Ordinal).ToArray(),
                    balances.GetValueOrDefault(membership.FundId),
                    createdAt[membership.FundId],
                    treasurerNames.Length == 0 ? TreasurerStatus.Unassigned : TreasurerStatus.Assigned,
                    treasurerNames,
                    membership.Role == FundRole.Teacher ? studentCounts.GetValueOrDefault(membership.FundId) : null,
                    membership.Role == FundRole.Parent ? membership.LinkedStudents : null);
            })
            .ToArray();
    }

    public async Task<FundCreatedResponse> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var accountPermissions = await accessService.GetAccountPermissionsAsync(userId, cancellationToken);
        if (!accountPermissions.Contains(Permissions.FundCreate))
        {
            throw new ForbiddenAccessException();
        }

        var now = timeProvider.GetUtcNow();
        var fund = new Fund(userId, name.Trim(), now);
        db.Add(fund);
        foreach (var (type, categoryName) in DefaultCategories.All)
        {
            db.Add(new Category(fund.Id, type, categoryName, now));
        }

        auditTrail.Record(
            FundsAuditActions.Create,
            AuditCategory.Change,
            userId,
            EntityType,
            fund.Id,
            newValues: new
            {
                name = fund.Name,
                defaultCategories = DefaultCategories.All.Select(category => $"{category.Type}: {category.Name}").ToArray(),
            });
        await db.SaveChangesAsync(cancellationToken);

        return new FundCreatedResponse(fund.Id);
    }

    public async Task RenameAsync(Guid fundId, string name, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.FundManage))
        {
            throw new ForbiddenAccessException();
        }

        var fund = await db.FirstOrDefaultAsync(
            db.Query<Fund>().Where(item => item.Id == fundId),
            cancellationToken) ?? throw new NotFoundException();

        var newName = name.Trim();
        if (fund.Name == newName)
        {
            return;
        }

        var oldName = fund.Name;
        fund.Rename(newName, timeProvider.GetUtcNow());
        auditTrail.Record(
            FundsAuditActions.Rename,
            AuditCategory.Change,
            userId,
            EntityType,
            fund.Id,
            oldValues: new { name = oldName },
            newValues: new { name = newName });
        await db.SaveChangesAsync(cancellationToken);
    }
}
