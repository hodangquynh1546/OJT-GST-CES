using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Funds;

/// <summary>Quỹ: danh sách quỹ của tôi, tạo, xem thông tin công khai, sửa (FR-05 → FR-08).</summary>
public interface IFundsService
{
    /// <summary>Các quỹ người dùng hiện tại là thành viên đang hoạt động (FR-08).</summary>
    Task<IReadOnlyList<FundSummaryDto>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>Tạo quỹ; người tạo là Owner; tạo sẵn danh mục mặc định (FR-05). Quyền <c>Fund.Create</c> kiểm ở Api.</summary>
    Task<FundDto> CreateAsync(CreateFundInput input, CancellationToken cancellationToken);

    Task<FundDto> GetAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Đổi tên / năm học (FR-06). Quỹ đã tổng kết thì chặn (BR-37).</summary>
    Task<FundDto> UpdateAsync(Guid fundId, UpdateFundInput input, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class FundsService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    AuditTrail audit,
    TimeProvider clock) : IFundsService
{
    public async Task<IReadOnlyList<FundSummaryDto>> ListMineAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var funds = await db.ToListAsync(
            from member in db.Query<FundMember>()
            where member.UserId == userId && member.Status == MemberStatus.Active
            join fund in db.Query<Fund>() on member.FundId equals fund.Id
            orderby fund.Status, fund.Name, fund.Id
            select new { fund.Id, fund.Name, fund.SchoolYear, fund.Status, member.Role },
            cancellationToken);

        if (funds.Count == 0)
        {
            return [];
        }

        var fundIds = funds.Select(fund => fund.Id).ToArray();
        var memberCounts = await CountActiveMembersAsync(fundIds, cancellationToken);

        // Số dư quỹ chung: chỉ giao dịch EventId null (BR-02).
        var balances = (await db.ToListAsync(
                from entry in db.Query<LedgerEntry>()
                where fundIds.Contains(entry.FundId) && entry.EventId == null
                group entry by entry.FundId into fundEntries
                select new
                {
                    FundId = fundEntries.Key,
                    Balance = fundEntries.Sum(entry => entry.Type == LedgerType.Income ? entry.Amount : -entry.Amount),
                },
                cancellationToken))
            .ToDictionary(row => row.FundId, row => row.Balance);

        var confirmFundIds = funds
            .Where(fund => RolePermissions.For(fund.Role).Contains(Permissions.PaymentConfirm))
            .Select(fund => fund.Id)
            .ToArray();
        var pending = confirmFundIds.Length == 0
            ? []
            : (await db.ToListAsync(
                    from payment in db.Query<Payment>()
                    where payment.Status == PaymentStatus.Submitted
                    join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
                    join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
                    where confirmFundIds.Contains(charge.FundId)
                    group payment by charge.FundId into fundPayments
                    select new { FundId = fundPayments.Key, Count = fundPayments.Count() },
                    cancellationToken))
                .ToDictionary(row => row.FundId, row => row.Count);

        // "Bạn cần đóng": nghĩa vụ chưa đóng của chính mình ở khoản thu đang mở (chung và sự kiện).
        var due = (await db.ToListAsync(
                from assignment in db.Query<ChargeAssignment>()
                where assignment.UserId == userId && assignment.Status == AssignmentStatus.Unpaid
                join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
                where fundIds.Contains(charge.FundId) && charge.Status == ChargeStatus.Open
                group assignment by charge.FundId into fundDue
                select new { FundId = fundDue.Key, Amount = fundDue.Sum(item => item.AmountDue), Count = fundDue.Count() },
                cancellationToken))
            .ToDictionary(row => row.FundId);

        var myBalances = (await db.ToListAsync(
                db.Query<PersonalBalance>().Where(item => item.UserId == userId && fundIds.Contains(item.FundId)).Select(item => new { item.FundId, item.Balance }),
                cancellationToken))
            .ToDictionary(row => row.FundId, row => row.Balance);

        return funds
            .Select(fund => new FundSummaryDto(
                fund.Id,
                fund.Name,
                fund.SchoolYear,
                fund.Status,
                fund.Role,
                memberCounts.GetValueOrDefault(fund.Id),
                balances.GetValueOrDefault(fund.Id),
                confirmFundIds.Contains(fund.Id) ? pending.GetValueOrDefault(fund.Id) : null,
                due.TryGetValue(fund.Id, out var mine) ? mine.Amount : 0,
                due.TryGetValue(fund.Id, out var mineCount) ? mineCount.Count : 0,
                myBalances.GetValueOrDefault(fund.Id)))
            .ToArray();
    }

    public async Task<FundDto> CreateAsync(CreateFundInput input, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var name = input.Name.Trim();
        var schoolYear = SchoolYearFormat.Normalize(input.SchoolYear);

        var fundId = await db.InTransactionAsync(
            async token =>
            {
                var now = clock.GetUtcNow();
                var fund = new Fund(name, userId, schoolYear, now);
                db.Add(fund);
                db.Add(new FundMember(fund.Id, userId, MemberRole.Owner, now));
                foreach (var (categoryName, type) in DefaultCategories.All)
                {
                    db.Add(new Category(fund.Id, categoryName, type, isSystem: true));
                }

                audit.Record(new AuditEntry(
                    FundsAuditActions.Create,
                    fund.Id,
                    userId,
                    FinancialScope.General,
                    EntityType: nameof(Fund),
                    EntityId: fund.Id,
                    After: new { fund.Name, fund.SchoolYear, defaultCategories = DefaultCategories.All.Count }));
                await db.SaveChangesAsync(token);
                return fund.Id;
            },
            cancellationToken);

        return await GetAsync(fundId, cancellationToken);
    }

    public async Task<FundDto> GetAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(CurrentUserId(), fundId, cancellationToken);
        var fund = await db.FirstOrDefaultAsync(db.Query<Fund>().Where(item => item.Id == fundId), cancellationToken)
            ?? throw new NotFoundException();

        var ownerName = await db.FirstOrDefaultAsync(
            from member in db.Query<FundMember>()
            where member.FundId == fundId && member.Role == MemberRole.Owner && member.Status == MemberStatus.Active
            join user in db.Query<User>() on member.UserId equals user.Id
            select user.FullName,
            cancellationToken);
        var memberCounts = await CountActiveMembersAsync([fundId], cancellationToken);

        return new FundDto(
            fund.Id,
            fund.Name,
            fund.SchoolYear,
            fund.Status,
            fund.CreatedAt,
            ownerName ?? string.Empty,
            memberCounts.GetValueOrDefault(fundId),
            access.Role);
    }

    public async Task<FundDto> UpdateAsync(Guid fundId, UpdateFundInput input, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var name = input.Name.Trim();
        var schoolYear = SchoolYearFormat.Normalize(input.SchoolYear);

        await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);

                if (fund.Name == name && fund.SchoolYear == schoolYear)
                {
                    return true;
                }

                var before = new { fund.Name, fund.SchoolYear };
                fund.Name = name;
                fund.SchoolYear = schoolYear;
                audit.Record(new AuditEntry(
                    FundsAuditActions.Update,
                    fund.Id,
                    userId,
                    FinancialScope.General,
                    EntityType: nameof(Fund),
                    EntityId: fund.Id,
                    Before: before,
                    After: new { fund.Name, fund.SchoolYear }));
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

        return await GetAsync(fundId, cancellationToken);
    }

    private async Task<Dictionary<Guid, int>> CountActiveMembersAsync(Guid[] fundIds, CancellationToken cancellationToken) =>
        (await db.ToListAsync(
            from member in db.Query<FundMember>()
            where fundIds.Contains(member.FundId) && member.Status == MemberStatus.Active
            group member by member.FundId into fundMembers
            select new { FundId = fundMembers.Key, Count = fundMembers.Count() },
            cancellationToken))
        .ToDictionary(row => row.FundId, row => row.Count);

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
