using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Members;
using ClassFund.Application.Features.Transactions;
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
    ITransactionsService transactions,
    TimeProvider timeProvider) : IFundsService
{
    private const string EntityType = nameof(Fund);
    private const int RecentTransactionCount = 5;

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

    public async Task<FundOverviewResponse> GetAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.FundView))
        {
            throw new ForbiddenAccessException();
        }

        // Tháng hiện tại theo giờ Việt Nam; ngày ghi nhận (OccurredOn) cũng là ngày Việt Nam.
        var today = VietnamTime.DateOf(timeProvider.GetUtcNow());
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // BR-02: số dư = tổng thu − tổng chi của các khoản chưa hủy; giống nhau ở mọi vai trò (BR-15).
        var totals = await db.ToListAsync(
            db.Query<FundTransaction>()
                .Where(transaction => transaction.FundId == fundId && !transaction.IsCancelled)
                .GroupBy(transaction => new
                {
                    transaction.Type,
                    InMonth = transaction.OccurredOn >= monthStart && transaction.OccurredOn <= monthEnd,
                })
                .Select(group => new { group.Key.Type, group.Key.InMonth, Total = group.Sum(transaction => transaction.Amount) }),
            cancellationToken);

        long Sum(TransactionType type, bool monthOnly) => totals
            .Where(row => row.Type == type && (!monthOnly || row.InMonth))
            .Sum(row => row.Total);

        var income = Sum(TransactionType.Income, monthOnly: false);
        var expense = Sum(TransactionType.Expense, monthOnly: false);
        var monthIncome = Sum(TransactionType.Income, monthOnly: true);
        var monthExpense = Sum(TransactionType.Expense, monthOnly: true);

        // BR-23: chỉ họ tên và vai trò của GVCN và thủ quỹ đang hiệu lực.
        var teacherName = await db.FirstOrDefaultAsync(
            db.Query<Fund>()
                .Where(fund => fund.Id == fundId)
                .Join(db.Query<User>(), fund => fund.OwnerUserId, user => user.Id, (fund, user) => user.FullName),
            cancellationToken) ?? throw new NotFoundException();
        var treasurerNames = (await db.ToListAsync(
                from assignment in db.Query<TreasurerAssignment>()
                where assignment.FundId == fundId && assignment.RevokedAt == null
                join student in db.Query<Student>() on assignment.StudentId equals student.Id
                select student.FullName,
                cancellationToken))
            .Order(StringComparer.CurrentCulture)
            .ToArray();
        var managers = new List<MemberResponse> { new(teacherName, FundRole.Teacher) };
        managers.AddRange(treasurerNames.Select(name => new MemberResponse(name, FundRole.Treasurer)));

        var recent = await transactions.ListAsync(
            fundId,
            new TransactionFilter(null, null, null, null, null, TransactionStatusFilter.Active, 1, RecentTransactionCount),
            cancellationToken);

        return new FundOverviewResponse(
            access.Role,
            access.Permissions.Order(StringComparer.Ordinal).ToArray(),
            income - expense,
            income,
            expense,
            new MonthTotals(monthIncome, monthExpense, monthIncome - monthExpense),
            treasurerNames.Length == 0 ? TreasurerStatus.Unassigned : TreasurerStatus.Assigned,
            managers,
            recent.Items);
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
