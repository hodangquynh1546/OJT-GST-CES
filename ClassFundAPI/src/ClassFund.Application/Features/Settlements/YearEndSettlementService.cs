using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Balance;
using ClassFund.Application.Features.Ledger;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Settlements;

/// <summary>
/// M14 – Tổng kết cuối năm quỹ chung (FR-53, FR-54, BR-34 → BR-37, OQ-13, OQ-19). Làm MỘT lần; chia tiền thừa cho cả lớp
/// ĐỀU theo đầu người (mọi người đã đóng đủ các khoản thu chung); xong quỹ chuyển <c>Archived</c> (vẫn xác nhận hoàn tiền và rút số dư cá nhân).
/// </summary>
public interface IYearEndSettlementService
{
    Task<YearEndPreviewDto> PreviewAsync(Guid fundId, CancellationToken cancellationToken);

    /// <param name="surplusMethod">Bắt buộc khi có tiền thừa; bỏ qua khi vừa đủ.</param>
    Task<YearEndResultDto> ExecuteAsync(Guid fundId, SurplusMethod? surplusMethod, CancellationToken cancellationToken);

    /// <summary>Kết quả tổng kết; 404 khi chưa tổng kết. Học sinh chỉ thấy phần của mình.</summary>
    Task<YearEndResultDto> GetAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Thủ quỹ xác nhận đã hoàn tiền trực tiếp cho một người → ghi chi <c>Refund</c> của quỹ chung (được phép khi quỹ đã Archived, BR-37).</summary>
    Task<SettlementLineDto> ConfirmRefundAsync(Guid fundId, Guid lineId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class YearEndSettlementService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    IBalanceService balances,
    AuditTrail audit,
    Notifier notifier,
    TimeProvider clock) : IYearEndSettlementService
{
    public async Task<YearEndPreviewDto> PreviewAsync(Guid fundId, CancellationToken cancellationToken)
    {
        await EnsureExecutorAsync(fundId, cancellationToken);
        var fund = await LoadFundAsync(fundId, cancellationToken);
        var calc = await CalculateAsync(fund, cancellationToken);
        var canExecute = fund.Status == FundStatus.Active && calc.UnsettledEventCount == 0 && calc.UnpaidObligationCount == 0;
        return new YearEndPreviewDto(
            fund.Name,
            fund.SchoolYear,
            fund.Status,
            calc.Shares.Count,
            calc.Totals.TotalIncome,
            calc.Totals.TotalExpense,
            calc.Totals.Balance,
            calc.Totals.Balance == 0 ? SettlementOutcome.Balanced : SettlementOutcome.Surplus,
            calc.UnsettledEventCount,
            calc.OpenChargeCount,
            calc.UnpaidObligationCount,
            canExecute,
            calc.Shares.Select(share => new SettlementLineDto(null, share.UserId, calc.Names[share.UserId], share.Contributed, share.Amount, null)).ToArray());
    }

    public async Task<YearEndResultDto> ExecuteAsync(Guid fundId, SurplusMethod? surplusMethod, CancellationToken cancellationToken)
    {
        var actorId = await EnsureExecutorAsync(fundId, cancellationToken);

        try
        {
            await db.InTransactionAsync(
                async token =>
                {
                    // Khóa dòng quỹ: hai thủ quỹ bấm cùng lúc thì lần sau thấy quỹ đã Archived.
                    var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                    var calc = await CalculateAsync(fund, token);
                    YearEndRule.EnsureCanClose(fund.Status, calc.UnsettledEventCount);

                    // Thủ quỹ phải nhắc đóng cho tới khi mọi người đóng đủ khoản thu chung mới tổng kết (mentor, 2026-11):
                    // nhờ vậy ai cũng đóng như nhau và tiền thừa chia đều theo đầu người.
                    if (calc.UnpaidObligationCount > 0)
                    {
                        throw new BusinessRuleException(
                            SettlementErrors.YearEndUnpaidCode,
                            $"Còn {calc.UnpaidObligationCount} khoản thu chung chưa đóng. Hãy nhắc đóng đủ rồi mới tổng kết cuối năm.");
                    }

                    var surplus = calc.Totals.Balance;
                    var outcome = surplus == 0 ? SettlementOutcome.Balanced : SettlementOutcome.Surplus;
                    if (surplus > 0 && surplusMethod is null)
                    {
                        throw new BusinessRuleException(SettlementErrors.MethodRequiredCode, SettlementErrors.MethodRequiredMessage);
                    }

                    var now = clock.GetUtcNow();
                    var settlement = new Settlement(fundId, null, calc.Shares.Count, calc.Totals.TotalIncome, calc.Totals.TotalExpense, actorId, now)
                    {
                        Outcome = outcome,
                        SurplusMethod = surplus > 0 ? surplusMethod : null,
                        AllocationRule = surplus > 0 ? AllocationRule.Proportional : null,
                    };
                    db.Add(settlement);

                    foreach (var share in calc.Shares.Where(share => share.Amount > 0))
                    {
                        var line = new SettlementLine(settlement.Id, share.UserId, share.Contributed, share.Amount);
                        db.Add(line);
                        if (surplusMethod == SurplusMethod.PersonalBalance)
                        {
                            await balances.CreditAsync(fundId, share.UserId, share.Amount, BalanceTransactionType.SettlementCredit, line.Id, actorId, token);
                            line.Status = SettlementLineStatus.CreditedToBalance;
                            line.ConfirmedByUserId = actorId;
                            line.ConfirmedAt = now;
                            notifier.Send(
                                [share.UserId],
                                new NotificationMessage(
                                    fundId,
                                    FinancialScope.General,
                                    null,
                                    NotificationType.BalanceCredited,
                                    $"{Notifier.GeneralPrefix} Số dư được cộng thêm {share.Amount:N0}đ",
                                    "Tiền thừa sau tổng kết cuối năm đã được cộng vào số dư cá nhân của bạn.",
                                    nameof(SettlementLine),
                                    line.Id));
                        }
                    }

                    // Khoản thu chung còn mở đóng hẳn (mọi nghĩa vụ đã đóng đủ nên không còn gì để đóng lại).
                    foreach (var charge in await db.ToListAsync(
                                 db.Query<Charge>().Where(item => item.FundId == fundId && item.EventId == null && item.Status == ChargeStatus.Open), token))
                    {
                        charge.Status = ChargeStatus.Closed;
                    }

                    fund.Status = FundStatus.Archived;

                    audit.Record(new AuditEntry(
                        SettlementAuditActions.YearEndSettle,
                        fundId,
                        actorId,
                        FinancialScope.General,
                        EntityType: nameof(Settlement),
                        EntityId: settlement.Id,
                        Before: new { status = nameof(FundStatus.Active) },
                        After: new
                        {
                            status = nameof(FundStatus.Archived),
                            outcome = outcome.ToString(),
                            method = surplus > 0 ? surplusMethod.ToString() : null,
                            participantCount = calc.Shares.Count,
                            total = calc.Totals.TotalIncome,
                            surplus,
                        }));

                    notifier.Send(
                        calc.Shares.Select(share => share.UserId),
                        new NotificationMessage(
                            fundId,
                            FinancialScope.General,
                            null,
                            NotificationType.SettlementResult,
                            $"{Notifier.GeneralPrefix} Quỹ đã tổng kết cuối năm",
                            surplus == 0
                                ? "Thu chi vừa đủ, không có tiền thừa để chia. Quỹ chuyển sang chỉ xem."
                                : surplusMethod == SurplusMethod.PersonalBalance
                                    ? "Tiền thừa đã được cộng vào số dư cá nhân theo tỷ lệ bạn đã đóng. Quỹ chuyển sang chỉ xem."
                                    : "Tiền thừa sẽ được thủ quỹ hoàn trực tiếp cho bạn theo tỷ lệ đã đóng. Quỹ chuyển sang chỉ xem.",
                            nameof(Settlement),
                            settlement.Id));

                    await db.SaveChangesAsync(token);
                    return settlement.Id;
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            throw new BusinessRuleException(YearEndRule.AlreadyClosedCode, "Quỹ đã tổng kết cuối năm.");
        }

        return await GetAsync(fundId, cancellationToken);
    }

    public async Task<YearEndResultDto> GetAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        var manager = access.Permissions.Contains(Permissions.SettlementExecute);
        var fund = await LoadFundAsync(fundId, cancellationToken);

        var settlement = await db.FirstOrDefaultAsync(
            db.Query<Settlement>().Where(item => item.FundId == fundId && item.EventId == null),
            cancellationToken) ?? throw new NotFoundException();
        var settledBy = await db.FirstOrDefaultAsync(
            db.Query<User>().Where(user => user.Id == settlement.SettledByUserId).Select(user => user.FullName),
            cancellationToken) ?? string.Empty;

        var lines = await db.ToListAsync(
            from line in db.Query<SettlementLine>()
            where line.SettlementId == settlement.Id && (manager || line.UserId == userId)
            join user in db.Query<User>() on line.UserId equals user.Id
            orderby user.FullName, line.Id
            select new SettlementLineDto(line.Id, line.UserId, user.FullName, line.Contributed, line.RefundAmount, line.Status),
            cancellationToken);

        return new YearEndResultDto(
            settlement.Id,
            fund.Name,
            fund.SchoolYear,
            settlement.Outcome,
            settlement.SurplusMethod,
            settlement.ParticipantCount,
            settlement.TotalCollected,
            settlement.TotalSpent,
            settlement.Difference,
            settlement.SettledAt,
            settledBy,
            lines);
    }

    public async Task<SettlementLineDto> ConfirmRefundAsync(Guid fundId, Guid lineId, CancellationToken cancellationToken)
    {
        var actorId = await EnsureExecutorAsync(fundId, cancellationToken);

        await db.InTransactionAsync(
            async token =>
            {
                var line = await db.LockAsync<SettlementLine>(lineId, token) ?? throw new NotFoundException();
                var settlement = await db.FirstOrDefaultAsync(
                    db.Query<Settlement>().Where(item => item.Id == line.SettlementId && item.FundId == fundId && item.EventId == null),
                    token) ?? throw new NotFoundException();

                if (line.Status != SettlementLineStatus.PendingRefund)
                {
                    throw new ConflictException(SettlementErrors.LineProcessedCode, SettlementErrors.LineProcessedMessage);
                }

                var person = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == line.UserId), token) ?? throw new NotFoundException();
                var now = clock.GetUtcNow();

                db.Add(new LedgerEntry(
                    fundId,
                    null,
                    LedgerType.Expense,
                    LedgerSource.Refund,
                    null,
                    $"Hoàn tiền thừa cuối năm: {person.FullName}",
                    "Hoàn tiền thừa sau tổng kết cuối năm quỹ chung",
                    line.RefundAmount,
                    VietnamTime.DateOf(now),
                    actorId,
                    now));

                line.Status = SettlementLineStatus.Refunded;
                line.ConfirmedByUserId = actorId;
                line.ConfirmedAt = now;

                audit.Record(new AuditEntry(
                    SettlementAuditActions.YearEndRefund,
                    fundId,
                    actorId,
                    FinancialScope.General,
                    EntityType: nameof(SettlementLine),
                    EntityId: line.Id,
                    Before: new { status = nameof(SettlementLineStatus.PendingRefund) },
                    After: new { payerName = person.FullName, amount = line.RefundAmount, status = nameof(SettlementLineStatus.Refunded) },
                    IsSelfAction: actorId == line.UserId));

                await db.SaveChangesAsync(token);
                return line.Id;
            },
            cancellationToken);

        return await db.FirstOrDefaultAsync(
            from line in db.Query<SettlementLine>()
            where line.Id == lineId
            join user in db.Query<User>() on line.UserId equals user.Id
            select new SettlementLineDto(line.Id, line.UserId, user.FullName, line.Contributed, line.RefundAmount, line.Status),
            cancellationToken) ?? throw new NotFoundException();
    }

    // ---------- Nội bộ ----------

    private sealed record Calculation(
        ScopeTotals Totals,
        IReadOnlyList<Share> Shares,
        IReadOnlyDictionary<Guid, string> Names,
        int UnsettledEventCount,
        int OpenChargeCount,
        int UnpaidObligationCount);

    /// <summary>Tổng thu chi quỹ chung (không gồm sự kiện), phần chia cho thành viên đang hoạt động và các số chặn / bị đóng.</summary>
    private async Task<Calculation> CalculateAsync(Fund fund, CancellationToken cancellationToken)
    {
        var fundId = fund.Id;
        var totals = await LedgerBalances.ForScopeAsync(db, fundId, null, cancellationToken);

        var members = await db.ToListAsync(
            from member in db.Query<FundMember>()
            where member.FundId == fundId && member.Status == MemberStatus.Active
            join user in db.Query<User>() on member.UserId equals user.Id
            select new { user.Id, user.FullName },
            cancellationToken);

        var paid = await db.ToListAsync(
            from payment in db.Query<Payment>()
            where payment.Status == PaymentStatus.Confirmed
            join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.EventId == null
            select new { payment.PayerUserId, payment.Amount },
            cancellationToken);
        var contributed = paid.GroupBy(item => item.PayerUserId).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        var contributions = members.Select(member => new Contribution(member.Id, contributed.GetValueOrDefault(member.Id))).ToArray();
        var shares = SettlementCalculator.AllocateEqually(totals.Balance < 0 ? 0 : totals.Balance, contributions);

        var unsettledEvents = await db.CountAsync(
            db.Query<Event>().Where(item => item.FundId == fundId && (item.Status == EventStatus.Open || item.Status == EventStatus.Completed)),
            cancellationToken);

        var openCharges = await db.CountAsync(
            db.Query<Charge>().Where(charge => charge.FundId == fundId && charge.EventId == null && charge.Status == ChargeStatus.Open),
            cancellationToken);

        var unpaid = await db.CountAsync(
            from assignment in db.Query<ChargeAssignment>()
            where assignment.Status == AssignmentStatus.Unpaid
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.EventId == null && charge.Status == ChargeStatus.Open
            select assignment.Id,
            cancellationToken);

        return new Calculation(totals, shares, members.ToDictionary(member => member.Id, member => member.FullName), unsettledEvents, openCharges, unpaid);
    }

    private async Task<Fund> LoadFundAsync(Guid fundId, CancellationToken cancellationToken) =>
        await db.FirstOrDefaultAsync(db.Query<Fund>().Where(item => item.Id == fundId), cancellationToken) ?? throw new NotFoundException();

    private async Task<Guid> EnsureExecutorAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(Permissions.SettlementExecute) ? userId : throw new ForbiddenAccessException();
    }

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
