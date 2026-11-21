using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Events;
using ClassFund.Application.Features.Ledger;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Reports;

/// <summary>
/// Báo cáo & tổng quan (FR-48 → FR-51, BR-02, BR-13). Chỉ đọc. Số liệu quỹ chung chỉ lấy <c>EventId = null</c>,
/// số liệu sự kiện chỉ lấy đúng sự kiện; thu tự ghi nhận được tách riêng.
/// </summary>
public interface IReportsService
{
    /// <summary>S-01 Tổng quan: thủ quỹ có thêm giao dịch gần đây, việc cần làm; học sinh có phần của mình.</summary>
    Task<FundOverviewDto> GetOverviewAsync(Guid fundId, CancellationToken cancellationToken);

    Task<GeneralReportDto> GetGeneralAsync(Guid fundId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<IReadOnlyList<EventReportRowDto>> GetEventsAsync(Guid fundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentReportRowDto>> GetStudentsAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Dòng báo cáo của chính tôi (học sinh chỉ xem của mình, FR-51).</summary>
    Task<StudentReportRowDto> GetMineAsync(Guid fundId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class ReportsService(IAppDb db, ICurrentUser currentUser, IAccessService accessService, IEventsService events) : IReportsService
{
    public const int RecentEntryCount = 5;
    public const int NotificationCount = 3;

    public async Task<FundOverviewDto> GetOverviewAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var (userId, access) = await ViewerAsync(fundId, cancellationToken);
        var fund = await db.FirstOrDefaultAsync(db.Query<Fund>().Where(item => item.Id == fundId), cancellationToken) ?? throw new NotFoundException();
        var manager = access.Permissions.Contains(Permissions.LedgerManage);

        var members = await db.ToListAsync(
            from member in db.Query<FundMember>()
            where member.FundId == fundId && member.Status == MemberStatus.Active
            join user in db.Query<User>() on member.UserId equals user.Id
            select new { member.Role, user.FullName },
            cancellationToken);
        var treasurers = members
            .Where(member => member.Role != MemberRole.Student)
            .OrderBy(member => member.Role)
            .ThenBy(member => member.FullName)
            .Select(member => member.FullName)
            .ToArray();

        var heldBalance = await db.ToListAsync(
            db.Query<PersonalBalance>().Where(item => item.FundId == fundId).Select(item => item.Balance), cancellationToken);

        var eventList = (await events.ListAsync(fundId, cancellationToken))
            .Where(item => item.Status is EventStatus.Open or EventStatus.Completed)
            .Select(item => new OverviewEventDto(item.Id, item.Name, item.EventDate, item.Status, item.ParticipantCount, item.PaidInFullCount, item.MyResponse))
            .ToArray();

        return new FundOverviewDto(
            fund.Name,
            fund.SchoolYear,
            treasurers,
            await GeneralTotalsAsync(fundId, cancellationToken),
            members.Count,
            treasurers.Length,
            heldBalance.Sum(),
            manager ? await RecentEntriesAsync(fundId, cancellationToken) : [],
            await OpenChargesAsync(fundId, cancellationToken),
            eventList,
            manager ? await TodoAsync(fundId, eventList, cancellationToken) : null,
            await MyOverviewAsync(fundId, userId, cancellationToken),
            await NotificationsAsync(fundId, userId, cancellationToken));
    }

    public async Task<GeneralReportDto> GetGeneralAsync(Guid fundId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        await RequireAsync(fundId, Permissions.ReportView, cancellationToken);
        var entries = await GeneralEntriesAsync(fundId, cancellationToken);
        var inRange = entries.Where(entry => (from is null || entry.TransactionDate >= from) && (to is null || entry.TransactionDate <= to)).ToArray();
        var (income, expense) = Totals(inRange);

        // Chi theo danh mục: khoản chi trừ bút toán đảo của chính nó (bút toán đảo mang cùng danh mục).
        var categoryNames = (await db.ToListAsync(
                db.Query<Category>().Where(category => category.FundId == fundId).Select(category => new { category.Id, category.Name }),
                cancellationToken))
            .ToDictionary(category => category.Id, category => category.Name);
        var byCategory = inRange
            .Where(entry => entry.Type == LedgerType.Expense || (entry.Type == LedgerType.Income && entry.ReversesEntryId != null && entries.Any(other => other.Id == entry.ReversesEntryId && other.Type == LedgerType.Expense)))
            .GroupBy(entry => entry.CategoryId)
            .Select(group => new CategoryAmountDto(
                group.Key,
                group.Key is { } id ? categoryNames.GetValueOrDefault(id, "Không rõ") : "Chưa phân loại",
                group.Sum(entry => entry.Type == LedgerType.Expense ? entry.Amount : -entry.Amount)))
            .Where(row => row.Amount > 0)
            .OrderByDescending(row => row.Amount)
            .ToArray();

        // Theo tháng: thu / chi trong tháng (đã trừ bút toán đảo), số dư cuối tháng tính từ đầu sổ.
        var months = new List<MonthRowDto>();
        foreach (var month in inRange.Select(entry => (entry.TransactionDate.Year, entry.TransactionDate.Month)).Distinct().OrderBy(month => month))
        {
            var (monthIncome, monthExpense) = Totals(inRange.Where(entry => entry.TransactionDate.Year == month.Year && entry.TransactionDate.Month == month.Month));
            var endOfMonth = new DateOnly(month.Year, month.Month, 1).AddMonths(1).AddDays(-1);
            var (toDateIncome, toDateExpense) = Totals(entries.Where(entry => entry.TransactionDate <= endOfMonth));
            months.Add(new MonthRowDto(month.Year, month.Month, monthIncome, monthExpense, toDateIncome - toDateExpense));
        }

        var all = Totals(entries);
        return new GeneralReportDto(
            from,
            to,
            income,
            expense,
            inRange.Where(entry => entry.IsSelfDeclared && entry.Type == LedgerType.Income && entry.ReversesEntryId == null).Sum(entry => entry.Amount),
            all.Income - all.Expense,
            (await OpenChargesAsync(fundId, cancellationToken)).Count,
            byCategory,
            months);
    }

    public async Task<IReadOnlyList<EventReportRowDto>> GetEventsAsync(Guid fundId, CancellationToken cancellationToken)
    {
        await RequireAsync(fundId, Permissions.ReportView, cancellationToken);
        var list = await db.ToListAsync(db.Query<Event>().Where(item => item.FundId == fundId), cancellationToken);
        var ids = list.Select(item => item.Id).ToArray();
        var participants = await db.ToListAsync(
            db.Query<EventParticipant>().Where(participant => ids.Contains(participant.EventId) && participant.Response == ParticipationResponse.Accepted),
            cancellationToken);
        var money = await EventMoney.LoadAsync(db, ids, cancellationToken);
        var totals = await LedgerBalances.ForEventsAsync(db, fundId, ids, cancellationToken);
        var settlements = await db.ToListAsync(db.Query<Settlement>().Where(item => item.FundId == fundId && item.EventId != null), cancellationToken);
        var settlementIds = settlements.Select(item => item.Id).ToArray();
        var lines = await db.ToListAsync(db.Query<SettlementLine>().Where(line => settlementIds.Contains(line.SettlementId)), cancellationToken);

        return list
            .OrderBy(item => item.Status is EventStatus.Open or EventStatus.Completed ? 0 : 1)
            .ThenByDescending(item => item.EventDate)
            .Select(item =>
            {
                var accepted = participants.Where(participant => participant.EventId == item.Id).ToArray();
                var completed = item.Status is EventStatus.Completed or EventStatus.Settled;
                var counted = completed ? accepted.Where(participant => participant.IsFinal).ToArray() : accepted;
                var due = counted.Sum(participant => EventMoney.For(money, item.Id, participant.UserId).Due);
                var paid = counted.Sum(participant => EventMoney.For(money, item.Id, participant.UserId).Paid);
                var total = totals.GetValueOrDefault(item.Id, ScopeTotals.Zero);
                var settlement = settlements.FirstOrDefault(row => row.EventId == item.Id);
                var mine = settlement is null ? [] : lines.Where(line => line.SettlementId == settlement.Id).ToArray();
                return new EventReportRowDto(
                    item.Id,
                    item.Name,
                    item.Status,
                    accepted.Length,
                    completed ? counted.Length : null,
                    due,
                    total.TotalIncome,
                    Math.Max(0, due - paid),
                    total.TotalExpense,
                    completed ? total.Balance : null,
                    settlement?.SurplusMethod,
                    mine.Where(line => line.Status == SettlementLineStatus.Refunded).Sum(line => line.RefundAmount),
                    mine.Where(line => line.Status == SettlementLineStatus.CreditedToBalance).Sum(line => line.RefundAmount));
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<StudentReportRowDto>> GetStudentsAsync(Guid fundId, CancellationToken cancellationToken)
    {
        await RequireAsync(fundId, Permissions.ReportView, cancellationToken);
        return await StudentRowsAsync(fundId, null, cancellationToken);
    }

    public async Task<StudentReportRowDto> GetMineAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var (userId, _) = await ViewerAsync(fundId, cancellationToken);
        return (await StudentRowsAsync(fundId, userId, cancellationToken)).Single();
    }

    // ---------- Quỹ chung ----------

    private sealed record EntryRow(Guid Id, LedgerType Type, LedgerSource Source, Guid? CategoryId, long Amount, DateOnly TransactionDate, bool IsSelfDeclared, Guid? ReversesEntryId);

    private async Task<IReadOnlyList<EntryRow>> GeneralEntriesAsync(Guid fundId, CancellationToken cancellationToken) =>
        await db.ToListAsync(
            LedgerBalances.InScope(db, fundId, null)
                .Select(entry => new EntryRow(entry.Id, entry.Type, entry.Source, entry.CategoryId, entry.Amount, entry.TransactionDate, entry.IsSelfDeclared, entry.ReversesEntryId)),
            cancellationToken);

    /// <summary>Thu / chi đã trừ bút toán đảo (bút toán đảo của khoản chi là dòng thu và ngược lại) – cùng cách tính với <see cref="LedgerBalances"/>.</summary>
    private static (long Income, long Expense) Totals(IEnumerable<EntryRow> entries)
    {
        long income = 0, expense = 0;
        foreach (var entry in entries)
        {
            switch (entry.Type, entry.ReversesEntryId is not null)
            {
                case (LedgerType.Income, false): income += entry.Amount; break;
                case (LedgerType.Expense, false): expense += entry.Amount; break;
                case (LedgerType.Income, true): expense -= entry.Amount; break;
                case (LedgerType.Expense, true): income -= entry.Amount; break;
            }
        }

        return (income, expense);
    }

    private async Task<GeneralTotalsDto> GeneralTotalsAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var entries = await GeneralEntriesAsync(fundId, cancellationToken);
        var (income, expense) = Totals(entries);
        var reversed = entries.Where(entry => entry.ReversesEntryId != null).Select(entry => entry.ReversesEntryId!.Value).ToHashSet();
        var original = entries.Where(entry => entry.ReversesEntryId == null && !reversed.Contains(entry.Id)).ToArray();
        return new GeneralTotalsDto(
            income - expense,
            income,
            expense,
            original.Where(entry => entry.Type == LedgerType.Income && entry.IsSelfDeclared).Sum(entry => entry.Amount),
            original.Count(entry => entry.Type == LedgerType.Income && entry.Source == LedgerSource.Payment),
            original.Count(entry => entry.Type == LedgerType.Expense));
    }

    private async Task<IReadOnlyList<OverviewEntryDto>> RecentEntriesAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var reversals = db.Query<LedgerEntry>().Where(item => item.FundId == fundId && item.ReversesEntryId != null);
        var files = db.Query<Attachment>().Where(item => item.FundId == fundId);
        var rows = await db.ToListAsync(
            from entry in LedgerBalances.InScope(db, fundId, null)
                .OrderByDescending(item => item.TransactionDate)
                .ThenByDescending(item => item.CreatedAt)
                .Take(RecentEntryCount)
            join user in db.Query<User>() on entry.PerformedByUserId equals user.Id
            join item in db.Query<Category>() on entry.CategoryId equals (Guid?)item.Id into categories
            from category in categories.DefaultIfEmpty()
            join item in db.Query<Payment>() on entry.PaymentId equals (Guid?)item.Id into payments
            from payment in payments.DefaultIfEmpty()
            select new OverviewEntryDto(
                entry.Id,
                entry.TransactionDate,
                entry.Title,
                category == null ? null : category.Name,
                entry.Type,
                entry.Source,
                entry.Amount,
                user.FullName,
                files.Count(file => file.OwnerId == entry.Id),
                entry.IsSelfDeclared,
                payment == null ? null : payment.Method,
                reversals.Any(reversal => reversal.ReversesEntryId == entry.Id)),
            cancellationToken);
        return rows.OrderByDescending(row => row.TransactionDate).ToArray();
    }

    private async Task<IReadOnlyList<OpenChargeDto>> OpenChargesAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var charges = await db.ToListAsync(
            db.Query<Charge>().Where(charge => charge.FundId == fundId && charge.EventId == null && charge.Status == ChargeStatus.Open).OrderBy(charge => charge.DueDate),
            cancellationToken);
        var ids = charges.Select(charge => charge.Id).ToArray();
        var assignments = await db.ToListAsync(
            db.Query<ChargeAssignment>()
                .Where(assignment => ids.Contains(assignment.ChargeId) && assignment.Status != AssignmentStatus.Cancelled && assignment.Status != AssignmentStatus.Excluded)
                .Select(assignment => new { assignment.ChargeId, assignment.Status }),
            cancellationToken);

        return charges
            .Select(charge =>
            {
                var mine = assignments.Where(assignment => assignment.ChargeId == charge.Id).ToArray();
                return new OpenChargeDto(
                    charge.Id,
                    charge.Title,
                    charge.AmountPerPerson,
                    charge.DueDate,
                    mine.Length,
                    mine.Count(assignment => assignment.Status == AssignmentStatus.Paid),
                    mine.Count(assignment => assignment.Status == AssignmentStatus.AwaitingConfirmation));
            })
            // "Chưa hoàn thành": còn người chưa đóng.
            .Where(charge => charge.PaidCount < charge.AssignedCount)
            .ToArray();
    }

    private async Task<ManagerTodoDto> TodoAsync(Guid fundId, IReadOnlyList<OverviewEventDto> eventList, CancellationToken cancellationToken)
    {
        var pendingPayments = await db.CountAsync(
            from payment in db.Query<Payment>()
            where payment.Status == PaymentStatus.Submitted
            join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId
            select payment.Id,
            cancellationToken);
        var pendingWithdrawals = await db.CountAsync(
            db.Query<WithdrawalRequest>().Where(request => request.FundId == fundId && request.Status == WithdrawalStatus.Requested), cancellationToken);
        var awaiting = eventList.Where(item => item.Status == EventStatus.Completed).Select(item => new NamedItemDto(item.Id, item.Name)).ToArray();
        return new ManagerTodoDto(pendingPayments, pendingWithdrawals, awaiting);
    }

    private async Task<MyOverviewDto> MyOverviewAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.ToListAsync(
            from assignment in db.Query<ChargeAssignment>()
            where assignment.UserId == userId && (assignment.Status == AssignmentStatus.Unpaid || assignment.Status == AssignmentStatus.AwaitingConfirmation)
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.Status == ChargeStatus.Open
            join item in db.Query<Event>() on charge.EventId equals (Guid?)item.Id into events
            from fundEvent in events.DefaultIfEmpty()
            select new { assignment.Status, assignment.AmountDue, charge.Title, charge.DueDate, EventName = fundEvent == null ? null : fundEvent.Name },
            cancellationToken);
        var unpaid = rows.Where(row => row.Status == AssignmentStatus.Unpaid).OrderBy(row => row.DueDate).ToArray();
        var awaiting = rows.Where(row => row.Status == AssignmentStatus.AwaitingConfirmation).ToArray();
        var nearest = unpaid.FirstOrDefault();
        var balance = await db.FirstOrDefaultAsync(
            db.Query<PersonalBalance>().Where(item => item.FundId == fundId && item.UserId == userId).Select(item => (long?)item.Balance),
            cancellationToken) ?? 0;

        return new MyOverviewDto(
            unpaid.Sum(row => row.AmountDue),
            unpaid.Length,
            nearest?.Title,
            nearest?.DueDate,
            nearest?.AmountDue ?? 0,
            nearest?.EventName,
            awaiting.Sum(row => row.AmountDue),
            awaiting.Length == 1 ? awaiting[0].Title : awaiting.Length > 1 ? $"{awaiting.Length} khoản" : null,
            balance);
    }

    private async Task<IReadOnlyList<OverviewNotificationDto>> NotificationsAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.ToListAsync(
            from notification in db.Query<Notification>()
                .Where(item => item.FundId == fundId && item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .Take(NotificationCount)
            join item in db.Query<Event>() on notification.EventId equals (Guid?)item.Id into events
            from fundEvent in events.DefaultIfEmpty()
            select new OverviewNotificationDto(
                notification.Title,
                notification.Body,
                notification.Scope,
                fundEvent == null ? null : fundEvent.Name,
                notification.CreatedAt,
                notification.IsRead),
            cancellationToken);
        return rows.OrderByDescending(item => item.CreatedAt).ToArray();
    }

    // ---------- Học sinh ----------

    private async Task<IReadOnlyList<StudentReportRowDto>> StudentRowsAsync(Guid fundId, Guid? onlyUserId, CancellationToken cancellationToken)
    {
        var membersQuery = from member in db.Query<FundMember>()
                           where member.FundId == fundId && member.Status == MemberStatus.Active
                           join user in db.Query<User>() on member.UserId equals user.Id
                           select new { member.UserId, member.Role, user.FullName };
        if (onlyUserId is { } only)
        {
            membersQuery = membersQuery.Where(member => member.UserId == only);
        }

        var members = await db.ToListAsync(membersQuery, cancellationToken);
        var userIds = members.Select(member => member.UserId).ToArray();

        var paid = await db.ToListAsync(
            from payment in db.Query<Payment>()
            where payment.Status == PaymentStatus.Confirmed && userIds.Contains(payment.PayerUserId)
            join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId
            select new { assignment.UserId, payment.Amount, IsEvent = charge.EventId != null },
            cancellationToken);

        var unpaid = await db.ToListAsync(
            from assignment in db.Query<ChargeAssignment>()
            where userIds.Contains(assignment.UserId)
                && (assignment.Status == AssignmentStatus.Unpaid || assignment.Status == AssignmentStatus.AwaitingConfirmation)
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.EventId == null && charge.Status == ChargeStatus.Open
            orderby charge.DueDate
            select new { assignment.UserId, charge.Title, assignment.Status },
            cancellationToken);

        var transactions = await db.ToListAsync(
            db.Query<BalanceTransaction>()
                .Where(item => item.FundId == fundId && userIds.Contains(item.UserId))
                .Select(item => new { item.UserId, item.Type, item.Amount }),
            cancellationToken);

        var refundedDirect = await db.ToListAsync(
            from line in db.Query<SettlementLine>()
            where userIds.Contains(line.UserId) && line.Status == SettlementLineStatus.Refunded
            join settlement in db.Query<Settlement>() on line.SettlementId equals settlement.Id
            where settlement.FundId == fundId
            select new { line.UserId, line.RefundAmount },
            cancellationToken);

        var balances = await db.ToListAsync(
            db.Query<PersonalBalance>().Where(item => item.FundId == fundId && userIds.Contains(item.UserId)).Select(item => new { item.UserId, item.Balance }),
            cancellationToken);

        return members
            .OrderBy(member => member.FullName)
            .ThenBy(member => member.UserId)
            .Select(member => new StudentReportRowDto(
                member.UserId,
                member.FullName,
                member.Role,
                paid.Where(row => row.UserId == member.UserId && !row.IsEvent).Sum(row => row.Amount),
                unpaid.Where(row => row.UserId == member.UserId).Select(row => new UnpaidChargeDto(row.Title, row.Status)).ToArray(),
                paid.Where(row => row.UserId == member.UserId && row.IsEvent).Sum(row => row.Amount),
                // "Được hoàn" = tiền thừa cộng vào số dư + tiền thừa đã hoàn trực tiếp.
                transactions.Where(row => row.UserId == member.UserId && row.Type == BalanceTransactionType.SettlementCredit).Sum(row => row.Amount)
                    + refundedDirect.Where(row => row.UserId == member.UserId).Sum(row => row.RefundAmount),
                -transactions.Where(row => row.UserId == member.UserId && row.Type == BalanceTransactionType.ChargePayment).Sum(row => row.Amount),
                balances.FirstOrDefault(row => row.UserId == member.UserId)?.Balance ?? 0))
            .ToArray();
    }

    // ---------- Quyền ----------

    private async Task<(Guid UserId, FundAccess Access)> ViewerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException();
        return (userId, await accessService.GetFundAccessAsync(userId, fundId, cancellationToken));
    }

    /// <summary>Controller đã yêu cầu quyền; kiểm lại để service không dựa vào nơi gọi.</summary>
    private async Task RequireAsync(Guid fundId, string permission, CancellationToken cancellationToken)
    {
        var (_, access) = await ViewerAsync(fundId, cancellationToken);
        if (!access.Permissions.Contains(permission))
        {
            throw new ForbiddenAccessException();
        }
    }
}
