using SchoolFund.Application.Common.Exceptions;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Application.Common.Security;
using SchoolFund.Application.Features.Funds;
using SchoolFund.Application.Features.Reports;
using SchoolFund.Application.Features.Transactions;
using SchoolFund.Domain.Authorization;
using SchoolFund.Domain.Entities;
using SchoolFund.Domain.Rules;

namespace SchoolFund.Application.Features.Overview;

/// <summary>Tổng quan quỹ và danh sách thành viên (FR-01, FR-12; BR-02, BR-15, BR-23).</summary>
public sealed class OverviewService(
    IAppDb db,
    IAccessService accessService,
    ICurrentUser currentUser,
    TransactionService transactions,
    TimeProvider clock)
{
    private const int RecentTransactionCount = 5;

    private Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("Thao tác cần người dùng đã đăng nhập.");

    /// <exception cref="NotFoundException">Không thuộc quỹ.</exception>
    public async Task<FundOverviewResponse> GetAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);

        var today = VietnamTime.DateOf(clock.GetUtcNow());
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        var lastDay = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        var active = db.ActiveTransactions(fundId);
        var total = await db.TotalsAsync(active, cancellationToken);
        var currentMonth = await db.TotalsAsync(
            active.Where(transaction => transaction.OccurredOn >= firstDay && transaction.OccurredOn <= lastDay),
            cancellationToken);

        var managers = await PeopleAsync(fundId, managersOnly: true, cancellationToken);
        var recent = await transactions.ListAsync(
            fundId,
            new TransactionFilter(null, null, null, null, null, TransactionStatusFilter.Active, 1, RecentTransactionCount),
            cancellationToken);

        return new FundOverviewResponse(
            access.Role,
            access.Permissions.Order(StringComparer.Ordinal).ToArray(),
            total.Net,
            total.Income,
            total.Expense,
            currentMonth,
            managers.Any(manager => manager.Role == FundRole.Treasurer) ? TreasurerStatus.Assigned : TreasurerStatus.Unassigned,
            managers,
            recent.Items);
    }

    /// <exception cref="NotFoundException">Không thuộc quỹ.</exception>
    /// <exception cref="ForbiddenAccessException">Học sinh chưa phân công, phụ huynh.</exception>
    public async Task<IReadOnlyList<FundMemberResponse>> ListMembersAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var access = await accessService.GetFundAccessAsync(UserId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.MemberView))
        {
            throw new ForbiddenAccessException();
        }

        return await PeopleAsync(fundId, managersOnly: false, cancellationToken);
    }

    /// <summary>GVCN trước, rồi thủ quỹ, rồi học sinh; học sinh chưa có tài khoản vẫn là thành viên lớp.</summary>
    private async Task<IReadOnlyList<FundMemberResponse>> PeopleAsync(
        Guid fundId,
        bool managersOnly,
        CancellationToken cancellationToken)
    {
        var teacherName = await db.FirstOrDefaultAsync(
            from fund in db.Query<Fund>()
            where fund.Id == fundId
            join user in db.Query<User>() on fund.OwnerUserId equals user.Id
            select user.FullName,
            cancellationToken) ?? throw new NotFoundException();

        var activeAssignments = db.Query<TreasurerAssignment>()
            .Where(assignment => assignment.FundId == fundId && assignment.RevokedAt == null);

        var students = db.Query<Student>()
            .Where(student => student.FundId == fundId)
            .Select(student => new
            {
                student.FullName,
                IsTreasurer = activeAssignments.Any(assignment => assignment.StudentId == student.Id),
            });

        var rows = await db.ToListAsync(
            managersOnly ? students.Where(student => student.IsTreasurer) : students,
            cancellationToken);

        return rows
            .OrderByDescending(student => student.IsTreasurer)
            .ThenBy(student => student.FullName, StringComparer.CurrentCulture)
            .Select(student => new FundMemberResponse(student.FullName, student.IsTreasurer ? FundRole.Treasurer : FundRole.Student))
            .Prepend(new FundMemberResponse(teacherName, FundRole.Teacher))
            .ToArray();
    }
}
