using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Charges;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Payments;

/// <inheritdoc />
public sealed class PaymentsService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    AuditTrail audit,
    Notifier notifier,
    TimeProvider clock) : IPaymentsService
{
    public async Task<IReadOnlyList<MyAssignmentItemDto>> ListMineAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);

        var rows = await db.ToListAsync(
            from assignment in db.Query<ChargeAssignment>()
            where assignment.UserId == userId
                && assignment.Status != AssignmentStatus.Cancelled && assignment.Status != AssignmentStatus.Excluded
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId
            select new { assignment, charge },
            cancellationToken);

        var eventIds = rows.Where(row => row.charge.EventId.HasValue).Select(row => row.charge.EventId!.Value).Distinct().ToArray();
        var eventNames = eventIds.Length == 0
            ? new Dictionary<Guid, string>()
            : (await db.ToListAsync(db.Query<Event>().Where(item => eventIds.Contains(item.Id)).Select(item => new { item.Id, item.Name }), cancellationToken))
                .ToDictionary(item => item.Id, item => item.Name);

        var assignmentIds = rows.Select(row => row.assignment.Id).ToArray();
        var confirmed = (await db.ToListAsync(
                db.Query<Payment>().Where(payment => assignmentIds.Contains(payment.AssignmentId) && payment.Status == PaymentStatus.Confirmed),
                cancellationToken))
            .GroupBy(payment => payment.AssignmentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(payment => payment.ConfirmedAt).First());

        return rows
            .OrderBy(row => row.assignment.Status == AssignmentStatus.Paid ? 1 : 0)
            .ThenBy(row => row.charge.DueDate ?? DateOnly.MaxValue)
            .ThenBy(row => row.charge.CreatedAt)
            .ThenBy(row => row.assignment.Id)
            .Select(row =>
            {
                confirmed.TryGetValue(row.assignment.Id, out var payment);
                var eventId = row.charge.EventId;
                return new MyAssignmentItemDto(
                    row.assignment.Id,
                    row.charge.Id,
                    row.charge.Title,
                    row.charge.Purpose,
                    row.assignment.AmountDue,
                    row.charge.DueDate,
                    row.charge.Status,
                    row.assignment.Status,
                    payment?.Method,
                    payment?.ConfirmedAt,
                    payment?.ConfirmationMode == ConfirmationMode.SelfDeclared,
                    eventId,
                    eventId.HasValue ? eventNames.GetValueOrDefault(eventId.Value) : null);
            })
            .ToArray();
    }

    public async Task<PaymentDto> CollectAsync(Guid fundId, Guid assignmentId, CollectPaymentInput input, CancellationToken cancellationToken)
    {
        var actorId = await EnsureCollectorAsync(fundId, cancellationToken);
        if (input.Method is not (PaymentMethod.Cash or PaymentMethod.BankTransfer))
        {
            throw new BusinessRuleException(PaymentsErrors.MethodInvalidCode, PaymentsErrors.MethodInvalidMessage);
        }

        var transferCode = input.Method == PaymentMethod.BankTransfer && !string.IsNullOrWhiteSpace(input.TransferCode) ? input.TransferCode.Trim() : null;

        try
        {
            return await db.InTransactionAsync(
                async token =>
                {
                    await EnsureFundActiveAsync(fundId, token);

                    var assignment = await db.LockAsync<ChargeAssignment>(assignmentId, token) ?? throw new NotFoundException();
                    var charge = await LoadChargeAsync(assignment.ChargeId, fundId, token);
                    if (assignment.Status is AssignmentStatus.Cancelled or AssignmentStatus.Excluded)
                    {
                        throw new NotFoundException();
                    }

                    if (assignment.Status != AssignmentStatus.Unpaid)
                    {
                        throw new ConflictException(PaymentsErrors.NotPayableCode, PaymentsErrors.NotPayableMessage);
                    }

                    await EnsureChargeOpenAsync(charge, token);

                    var payer = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == assignment.UserId), token) ?? throw new NotFoundException();
                    var now = clock.GetUtcNow();
                    var decision = PaymentConfirmationPolicy.Decide(actorId, payer.Id, actorCanConfirm: true);
                    var payment = new Payment(assignment.Id, payer.Id, assignment.AmountDue, input.Method, now)
                    {
                        TransferCode = transferCode,
                        Status = PaymentStatus.Confirmed,
                        ConfirmedByUserId = actorId,
                        ConfirmedAt = now,
                        ConfirmationMode = decision.Mode,
                    };
                    db.Add(payment);
                    assignment.Status = AssignmentStatus.Paid;

                    var categoryId = await IncomeCategoryAsync(fundId, charge, token);
                    var selfDeclared = decision.Mode == ConfirmationMode.SelfDeclared;
                    db.Add(new LedgerEntry(
                        fundId,
                        charge.EventId,
                        LedgerType.Income,
                        LedgerSource.Payment,
                        categoryId,
                        charge.Title,
                        charge.Purpose,
                        payment.Amount,
                        VietnamTime.DateOf(now),
                        actorId,
                        now)
                    {
                        PaymentId = payment.Id,
                        TransferCode = transferCode,
                        IsSelfDeclared = selfDeclared,
                    });

                    var scope = ScopeOf(charge);
                    audit.Record(new AuditEntry(
                        PaymentsAuditActions.Collect,
                        fundId,
                        actorId,
                        scope,
                        EventId: charge.EventId,
                        EntityType: nameof(Payment),
                        EntityId: payment.Id,
                        Before: new { status = nameof(AssignmentStatus.Unpaid) },
                        After: new { chargeTitle = charge.Title, payerName = payer.FullName, amount = payment.Amount, method = input.Method.ToString(), status = nameof(PaymentStatus.Confirmed) },
                        IsSelfAction: selfDeclared,
                        Note: selfDeclared ? "Thủ quỹ tự ghi nhận khoản đóng của chính mình." : null));

                    if (!selfDeclared)
                    {
                        notifier.Send(
                            [payer.Id],
                            new NotificationMessage(
                                fundId,
                                scope,
                                charge.EventId,
                                NotificationType.PaymentConfirmed,
                                $"{await PrefixAsync(charge, token)} Đã ghi nhận khoản đóng: {charge.Title}",
                                $"Thủ quỹ đã ghi nhận bạn đóng {payment.Amount:N0}đ.",
                                nameof(Payment),
                                payment.Id));
                    }

                    await db.SaveChangesAsync(token);
                    return ToDto(payment);
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            throw new ConflictException(PaymentsErrors.NotPayableCode, PaymentsErrors.NotPayableMessage);
        }
    }

    public async Task<PaymentDto> UncollectAsync(Guid fundId, Guid assignmentId, string reason, CancellationToken cancellationToken)
    {
        var actorId = await EnsureCollectorAsync(fundId, cancellationToken);
        var cleanReason = reason?.Trim();
        if (string.IsNullOrEmpty(cleanReason))
        {
            throw new BusinessRuleException(PaymentsErrors.ReasonRequiredCode, PaymentsErrors.ReasonRequiredMessage);
        }

        return await db.InTransactionAsync(
            async token =>
            {
                await EnsureFundActiveAsync(fundId, token);

                var assignment = await db.LockAsync<ChargeAssignment>(assignmentId, token) ?? throw new NotFoundException();
                var charge = await LoadChargeAsync(assignment.ChargeId, fundId, token);
                if (assignment.Status != AssignmentStatus.Paid)
                {
                    throw new ConflictException(PaymentsErrors.NotCollectedCode, PaymentsErrors.NotCollectedMessage);
                }

                await EnsureChargeOpenAsync(charge, token);

                var payment = await db.FirstOrDefaultAsync(
                    db.Query<Payment>().Where(item => item.AssignmentId == assignment.Id && item.Status == PaymentStatus.Confirmed),
                    token) ?? throw new ConflictException(PaymentsErrors.NotCollectedCode, PaymentsErrors.NotCollectedMessage);
                var entry = await db.FirstOrDefaultAsync(db.Query<LedgerEntry>().Where(item => item.PaymentId == payment.Id), token);
                var payer = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == assignment.UserId), token) ?? throw new NotFoundException();
                var now = clock.GetUtcNow();

                // Sổ chỉ ghi thêm (BR-08): hủy thu = bút toán đảo (chi cùng số tiền, cùng phạm vi) + lý do.
                if (entry is not null)
                {
                    db.Add(new LedgerEntry(
                        fundId,
                        entry.EventId,
                        LedgerType.Expense,
                        LedgerSource.Manual,
                        entry.CategoryId,
                        $"Đảo: {entry.Title}",
                        purpose: null,
                        entry.Amount,
                        VietnamTime.DateOf(now),
                        actorId,
                        now)
                    {
                        ReversesEntryId = entry.Id,
                        Note = cleanReason,
                    });
                }

                payment.Status = PaymentStatus.Rejected;
                payment.RejectionReason = $"Thủ quỹ hủy ghi nhận thu: {cleanReason}";
                assignment.Status = AssignmentStatus.Unpaid;

                var scope = ScopeOf(charge);
                audit.Record(new AuditEntry(
                    PaymentsAuditActions.Uncollect,
                    fundId,
                    actorId,
                    scope,
                    EventId: charge.EventId,
                    EntityType: nameof(Payment),
                    EntityId: payment.Id,
                    Before: new { status = nameof(PaymentStatus.Confirmed) },
                    After: new { chargeTitle = charge.Title, payerName = payer.FullName, amount = payment.Amount, status = nameof(AssignmentStatus.Unpaid), reason = cleanReason },
                    IsSelfAction: actorId == payer.Id));

                if (actorId != payer.Id)
                {
                    notifier.Send(
                        [payer.Id],
                        new NotificationMessage(
                            fundId,
                            scope,
                            charge.EventId,
                            NotificationType.PaymentRejected,
                            $"{await PrefixAsync(charge, token)} Đã hủy ghi nhận khoản đóng: {charge.Title}",
                            $"Lý do: {cleanReason}. Vui lòng liên hệ thủ quỹ.",
                            nameof(Payment),
                            payment.Id));
                }

                await db.SaveChangesAsync(token);
                return ToDto(payment);
            },
            cancellationToken);
    }

    public async Task<RemindResult> RemindAsync(Guid fundId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.ChargeManage))
        {
            throw new ForbiddenAccessException();
        }

        return await db.InTransactionAsync(
            async token =>
            {
                await EnsureFundActiveAsync(fundId, token);

                var assignment = await db.FirstOrDefaultAsync(db.Query<ChargeAssignment>().Where(item => item.Id == assignmentId), token) ?? throw new NotFoundException();
                var charge = await LoadChargeAsync(assignment.ChargeId, fundId, token);
                if (assignment.Status is AssignmentStatus.Cancelled or AssignmentStatus.Excluded)
                {
                    throw new NotFoundException();
                }

                if (assignment.Status != AssignmentStatus.Unpaid)
                {
                    throw new BusinessRuleException(ChargesErrors.NothingToRemindCode, "Người này không còn khoản nào chưa đóng.");
                }

                await EnsureChargeOpenAsync(charge, token);

                var now = clock.GetUtcNow();
                var since = now - ChargesErrors.RemindCooldown;
                if (await db.AnyAsync(
                        db.Query<PaymentReminder>().Where(item =>
                            item.UserId == assignment.UserId && item.ChargeId == charge.Id && item.Kind == ReminderKind.Manual && item.SentAt > since),
                        token))
                {
                    throw new ConflictException(ChargesErrors.RemindTooSoonCode, ChargesErrors.RemindTooSoonMessage);
                }

                var payer = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == assignment.UserId), token) ?? throw new NotFoundException();
                var scope = ScopeOf(charge);
                notifier.Send(
                    [assignment.UserId],
                    new NotificationMessage(
                        fundId,
                        scope,
                        charge.EventId,
                        charge.EventId.HasValue ? NotificationType.EventUnpaidReminder : NotificationType.ChargeDueSoon,
                        $"{await PrefixAsync(charge, token)} Nhắc đóng: {charge.Title}",
                        $"Thủ quỹ nhắc bạn đóng {assignment.AmountDue:N0}đ.",
                        nameof(Charge),
                        charge.Id));
                db.Add(new PaymentReminder(fundId, charge.EventId, charge.Id, assignment.UserId, ReminderKind.Manual, userId, now, VietnamTime.DateOf(now)));
                audit.Record(new AuditEntry(
                    PaymentsAuditActions.Remind,
                    fundId,
                    userId,
                    scope,
                    EventId: charge.EventId,
                    EntityType: nameof(ChargeAssignment),
                    EntityId: assignment.Id,
                    After: new { chargeTitle = charge.Title, payerName = payer.FullName, amount = assignment.AmountDue }));
                await db.SaveChangesAsync(token);
                return new RemindResult(1);
            },
            cancellationToken);
    }

    // ---------- Nội bộ ----------

    /// <summary>Khoản thu phải còn mở; khoản thu sự kiện thì sự kiện phải còn mở (chưa đóng danh sách).</summary>
    private async Task EnsureChargeOpenAsync(Charge charge, CancellationToken cancellationToken)
    {
        if (charge.Status != ChargeStatus.Open)
        {
            throw new ConflictException(PaymentsErrors.ChargeNotOpenCode, PaymentsErrors.ChargeNotOpenMessage);
        }

        if (charge.EventId is { } eventId)
        {
            var status = await db.FirstOrDefaultAsync(db.Query<Event>().Where(item => item.Id == eventId).Select(item => (EventStatus?)item.Status), cancellationToken);
            if (status is null || !EventStatusRules.AcceptsPayments(status.Value))
            {
                throw new ConflictException(PaymentsErrors.ChargeNotOpenCode, PaymentsErrors.ChargeNotOpenMessage);
            }
        }
    }

    /// <summary>Danh mục thu do thủ quỹ chọn khi tạo khoản thu; khoản cũ chưa có danh mục dùng danh mục mặc định nếu còn.</summary>
    private async Task<Guid?> IncomeCategoryAsync(Guid fundId, Charge charge, CancellationToken cancellationToken)
    {
        if (charge.CategoryId is { } categoryId)
        {
            return categoryId;
        }

        var legacyName = DefaultCategories.All.First(item => item.Type == LedgerType.Income).Name;
        return await db.FirstOrDefaultAsync(
            db.Query<Category>()
                .Where(category => category.FundId == fundId && category.Type == LedgerType.Income && category.Name == legacyName)
                .Select(category => (Guid?)category.Id),
            cancellationToken);
    }

    private static FinancialScope ScopeOf(Charge charge) => charge.EventId.HasValue ? FinancialScope.Event : FinancialScope.General;

    private async Task<string> PrefixAsync(Charge charge, CancellationToken cancellationToken)
    {
        if (charge.EventId is not { } eventId)
        {
            return Notifier.GeneralPrefix;
        }

        var name = await db.FirstOrDefaultAsync(db.Query<Event>().Where(item => item.Id == eventId).Select(item => item.Name), cancellationToken);
        return Notifier.ScopePrefix(FinancialScope.Event, name);
    }

    private async Task<Charge> LoadChargeAsync(Guid chargeId, Guid fundId, CancellationToken cancellationToken) =>
        await db.FirstOrDefaultAsync(
            db.Query<Charge>().Where(charge => charge.Id == chargeId && charge.FundId == fundId),
            cancellationToken) ?? throw new NotFoundException();

    private async Task EnsureFundActiveAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var status = await db.FirstOrDefaultAsync(
            db.Query<Fund>().Where(fund => fund.Id == fundId).Select(fund => (FundStatus?)fund.Status),
            cancellationToken) ?? throw new NotFoundException();
        YearEndRule.EnsureFundActive(status);
    }

    private static PaymentDto ToDto(Payment payment) =>
        new(payment.Id, payment.AssignmentId, payment.Amount, payment.Method, payment.TransferCode, payment.Status, payment.ConfirmedAt,
            payment.ConfirmationMode == ConfirmationMode.SelfDeclared);

    /// <summary>Controller đã yêu cầu <c>Payment.Confirm</c>; kiểm lại để service không dựa vào nơi gọi.</summary>
    private async Task<Guid> EnsureCollectorAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(Permissions.PaymentConfirm) ? userId : throw new ForbiddenAccessException();
    }

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
