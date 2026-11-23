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
/// M9 – Quyết toán sự kiện (FR-35 → FR-39, BR-21 → BR-24). Chỉ chia tiền thừa theo tỷ lệ số đã đóng (OQ-18);
/// nghĩa vụ chưa đóng bị đóng lại (OQ-19); còn thanh toán chờ xác nhận thì chặn.
/// </summary>
public interface ISettlementService
{
    Task<SettlementPreviewDto> PreviewAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken);

    /// <param name="surplusMethod">Bắt buộc khi có tiền thừa; bỏ qua khi vừa đủ.</param>
    Task<SettlementResultDto> ExecuteAsync(Guid fundId, Guid eventId, SurplusMethod? surplusMethod, CancellationToken cancellationToken);

    /// <summary>Kết quả quyết toán; 404 khi chưa quyết toán. Học sinh chỉ thấy phần của mình.</summary>
    Task<SettlementResultDto> GetAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>Thủ quỹ xác nhận đã hoàn tiền trực tiếp cho một người → ghi chi <c>Refund</c> của sự kiện.</summary>
    Task<SettlementLineDto> ConfirmRefundAsync(Guid fundId, Guid eventId, Guid lineId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class SettlementService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    IBalanceService balances,
    AuditTrail audit,
    Notifier notifier,
    TimeProvider clock) : ISettlementService
{
    public async Task<SettlementPreviewDto> PreviewAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken)
    {
        await EnsureExecutorAsync(fundId, cancellationToken);
        var fundEvent = await LoadEventAsync(fundId, eventId, cancellationToken);
        var calc = await CalculateAsync(fundId, fundEvent, cancellationToken);
        var canExecute = fundEvent.Status == EventStatus.Completed && calc.PendingPaymentCount == 0;
        return new SettlementPreviewDto(
            fundEvent.Id,
            fundEvent.Name,
            fundEvent.Status,
            calc.Shares.Count,
            calc.Totals.TotalIncome,
            calc.Totals.TotalExpense,
            calc.Totals.Balance,
            calc.Totals.Balance == 0 ? SettlementOutcome.Balanced : SettlementOutcome.Surplus,
            calc.PendingPaymentCount,
            calc.UnpaidObligationCount,
            canExecute,
            calc.Shares.Select(share => new SettlementLineDto(null, share.UserId, calc.Names[share.UserId], share.Contributed, share.Amount, null)).ToArray());
    }

    public async Task<SettlementResultDto> ExecuteAsync(Guid fundId, Guid eventId, SurplusMethod? surplusMethod, CancellationToken cancellationToken)
    {
        var actorId = await EnsureExecutorAsync(fundId, cancellationToken);

        try
        {
            await db.InTransactionAsync(
                async token =>
                {
                    await EnsureFundActiveAsync(fundId, token);
                    var fundEvent = await db.LockAsync<Event>(eventId, token);
                    if (fundEvent is null || fundEvent.FundId != fundId)
                    {
                        throw new NotFoundException();
                    }

                    if (fundEvent.Status == EventStatus.Settled)
                    {
                        throw new ConflictException(SettlementErrors.AlreadySettledCode, SettlementErrors.AlreadySettledMessage);
                    }

                    if (fundEvent.Status != EventStatus.Completed)
                    {
                        throw new BusinessRuleException(SettlementErrors.NotCompletedCode, SettlementErrors.NotCompletedMessage);
                    }

                    var calc = await CalculateAsync(fundId, fundEvent, token);
                    if (calc.PendingPaymentCount > 0)
                    {
                        throw new BusinessRuleException(
                            SettlementErrors.PendingPaymentsCode,
                            $"Còn {calc.PendingPaymentCount} thanh toán chờ xác nhận. Hãy xác nhận hoặc từ chối hết trước khi quyết toán.");
                    }

                    var surplus = calc.Totals.Balance;
                    var outcome = surplus == 0 ? SettlementOutcome.Balanced : SettlementOutcome.Surplus;
                    if (surplus > 0 && surplusMethod is null)
                    {
                        throw new BusinessRuleException(SettlementErrors.MethodRequiredCode, SettlementErrors.MethodRequiredMessage);
                    }

                    var now = clock.GetUtcNow();
                    var settlement = new Settlement(fundId, eventId, calc.Shares.Count, calc.Totals.TotalIncome, calc.Totals.TotalExpense, actorId, now)
                    {
                        Outcome = outcome,
                        SurplusMethod = surplus > 0 ? surplusMethod : null,
                        AllocationRule = surplus > 0 ? AllocationRule.Proportional : null,
                    };
                    db.Add(settlement);

                    var recipients = calc.Shares.Select(share => share.UserId).ToArray();
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
                                    FinancialScope.Event,
                                    eventId,
                                    NotificationType.BalanceCredited,
                                    $"{Notifier.ScopePrefix(FinancialScope.Event, fundEvent.Name)} Số dư được cộng thêm {share.Amount:N0}đ",
                                    "Tiền thừa sau quyết toán đã được cộng vào số dư cá nhân của bạn.",
                                    nameof(SettlementLine),
                                    line.Id));
                        }
                    }

                    // OQ-19: nghĩa vụ chưa đóng bị đóng lại; khoản thu của sự kiện đóng hẳn.
                    var chargeIds = await db.ToListAsync(
                        db.Query<Charge>().Where(charge => charge.FundId == fundId && charge.EventId == eventId).Select(charge => charge.Id),
                        token);
                    var unpaid = await db.ToListAsync(
                        db.Query<ChargeAssignment>().Where(item => chargeIds.Contains(item.ChargeId) && item.Status == AssignmentStatus.Unpaid),
                        token);
                    foreach (var assignment in unpaid)
                    {
                        assignment.Status = AssignmentStatus.Excluded;
                    }

                    foreach (var charge in await db.ToListAsync(db.Query<Charge>().Where(item => chargeIds.Contains(item.Id) && item.Status == ChargeStatus.Open), token))
                    {
                        charge.Status = ChargeStatus.Closed;
                    }

                    EventStatusRules.EnsureCanTransition(fundEvent.Status, EventStatus.Settled);
                    fundEvent.Status = EventStatus.Settled;

                    audit.Record(new AuditEntry(
                        SettlementAuditActions.Execute,
                        fundId,
                        actorId,
                        FinancialScope.Event,
                        EventId: eventId,
                        EntityType: nameof(Settlement),
                        EntityId: settlement.Id,
                        Before: new { status = nameof(EventStatus.Completed) },
                        After: new
                        {
                            eventName = fundEvent.Name,
                            status = nameof(EventStatus.Settled),
                            outcome = outcome.ToString(),
                            method = surplus > 0 ? surplusMethod.ToString() : null,
                            participantCount = calc.Shares.Count,
                            total = calc.Totals.TotalIncome,
                            surplus,
                        }));

                    notifier.Send(
                        recipients,
                        new NotificationMessage(
                            fundId,
                            FinancialScope.Event,
                            eventId,
                            NotificationType.SettlementResult,
                            $"{Notifier.ScopePrefix(FinancialScope.Event, fundEvent.Name)} Sự kiện đã quyết toán",
                            surplus == 0
                                ? "Thu chi vừa đủ, không có tiền thừa để chia."
                                : surplusMethod == SurplusMethod.PersonalBalance
                                    ? "Tiền thừa đã được cộng vào số dư cá nhân theo tỷ lệ bạn đã đóng."
                                    : "Tiền thừa sẽ được thủ quỹ hoàn trực tiếp cho bạn theo tỷ lệ bạn đã đóng.",
                            nameof(Settlement),
                            settlement.Id));

                    await db.SaveChangesAsync(token);
                    return settlement.Id;
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // BR-24: hai thủ quỹ bấm cùng lúc – chỉ một lần thắng.
            throw new ConflictException(SettlementErrors.AlreadySettledCode, SettlementErrors.AlreadySettledMessage);
        }

        return await GetAsync(fundId, eventId, cancellationToken);
    }

    public async Task<SettlementResultDto> GetAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        var manager = access.Permissions.Contains(Permissions.SettlementExecute);
        var fundEvent = await LoadEventAsync(fundId, eventId, cancellationToken);

        if (!manager)
        {
            var participated = await db.AnyAsync(
                db.Query<EventParticipant>().Where(item => item.EventId == eventId && item.UserId == userId && item.Response == ParticipationResponse.Accepted && item.IsFinal),
                cancellationToken);
            if (!participated)
            {
                throw new NotFoundException();
            }
        }

        var settlement = await db.FirstOrDefaultAsync(
            db.Query<Settlement>().Where(item => item.FundId == fundId && item.EventId == eventId),
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

        return new SettlementResultDto(
            settlement.Id,
            eventId,
            fundEvent.Name,
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

    public async Task<SettlementLineDto> ConfirmRefundAsync(Guid fundId, Guid eventId, Guid lineId, CancellationToken cancellationToken)
    {
        var actorId = await EnsureExecutorAsync(fundId, cancellationToken);

        await db.InTransactionAsync(
            async token =>
            {
                // Không kiểm tra quỹ Active: sau tổng kết cuối năm vẫn xác nhận hoàn tiền được (BR-37).
                var line = await db.LockAsync<SettlementLine>(lineId, token) ?? throw new NotFoundException();
                var settlement = await db.FirstOrDefaultAsync(
                    db.Query<Settlement>().Where(item => item.Id == line.SettlementId && item.FundId == fundId && item.EventId == eventId),
                    token) ?? throw new NotFoundException();

                if (line.Status != SettlementLineStatus.PendingRefund)
                {
                    throw new ConflictException(SettlementErrors.LineProcessedCode, SettlementErrors.LineProcessedMessage);
                }

                var person = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == line.UserId), token) ?? throw new NotFoundException();
                var eventName = await db.FirstOrDefaultAsync(db.Query<Event>().Where(item => item.Id == eventId).Select(item => item.Name), token) ?? string.Empty;
                var now = clock.GetUtcNow();

                db.Add(new LedgerEntry(
                    fundId,
                    eventId,
                    LedgerType.Expense,
                    LedgerSource.Refund,
                    null,
                    $"Hoàn tiền thừa: {person.FullName}",
                    $"Hoàn tiền thừa sau quyết toán sự kiện {eventName}",
                    line.RefundAmount,
                    VietnamTime.DateOf(now),
                    actorId,
                    now));

                line.Status = SettlementLineStatus.Refunded;
                line.ConfirmedByUserId = actorId;
                line.ConfirmedAt = now;

                audit.Record(new AuditEntry(
                    SettlementAuditActions.Refund,
                    fundId,
                    actorId,
                    FinancialScope.Event,
                    EventId: eventId,
                    EntityType: nameof(SettlementLine),
                    EntityId: line.Id,
                    Before: new { status = nameof(SettlementLineStatus.PendingRefund) },
                    After: new { eventName, payerName = person.FullName, amount = line.RefundAmount, status = nameof(SettlementLineStatus.Refunded) },
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
        int PendingPaymentCount,
        int UnpaidObligationCount);

    /// <summary>Tổng thu chi của sự kiện, phần mỗi người (người <c>IsFinal</c> đã đóng) và số thứ chặn / bị đóng.</summary>
    private async Task<Calculation> CalculateAsync(Guid fundId, Event fundEvent, CancellationToken cancellationToken)
    {
        var totals = await LedgerBalances.ForScopeAsync(db, fundId, fundEvent.Id, cancellationToken);

        var finalPeople = await db.ToListAsync(
            from participant in db.Query<EventParticipant>()
            where participant.EventId == fundEvent.Id && participant.Response == ParticipationResponse.Accepted && participant.IsFinal
            join user in db.Query<User>() on participant.UserId equals user.Id
            select new { user.Id, user.FullName },
            cancellationToken);

        var paid = await db.ToListAsync(
            from payment in db.Query<Payment>()
            where payment.Status == PaymentStatus.Confirmed
            join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.EventId == fundEvent.Id
            select new { payment.PayerUserId, payment.Amount },
            cancellationToken);
        var contributed = paid.GroupBy(item => item.PayerUserId).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        var contributions = finalPeople.Select(person => new Contribution(person.Id, contributed.GetValueOrDefault(person.Id))).ToArray();
        var shares = SettlementCalculator.Allocate(totals.Balance < 0 ? 0 : totals.Balance, contributions);

        var pending = await db.CountAsync(
            from payment in db.Query<Payment>()
            where payment.Status == PaymentStatus.Submitted
            join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.EventId == fundEvent.Id
            select payment.Id,
            cancellationToken);

        var unpaid = await db.CountAsync(
            from assignment in db.Query<ChargeAssignment>()
            where assignment.Status == AssignmentStatus.Unpaid
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.EventId == fundEvent.Id
            select assignment.Id,
            cancellationToken);

        return new Calculation(totals, shares, finalPeople.ToDictionary(person => person.Id, person => person.FullName), pending, unpaid);
    }

    private async Task<Event> LoadEventAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken) =>
        await db.FirstOrDefaultAsync(db.Query<Event>().Where(item => item.Id == eventId && item.FundId == fundId), cancellationToken)
        ?? throw new NotFoundException();

    private async Task EnsureFundActiveAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var status = await db.FirstOrDefaultAsync(
            db.Query<Fund>().Where(fund => fund.Id == fundId).Select(fund => (FundStatus?)fund.Status),
            cancellationToken) ?? throw new NotFoundException();
        YearEndRule.EnsureFundActive(status);
    }

    private async Task<Guid> EnsureExecutorAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(Permissions.SettlementExecute) ? userId : throw new ForbiddenAccessException();
    }

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
