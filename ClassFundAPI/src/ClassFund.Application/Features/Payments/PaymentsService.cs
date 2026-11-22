using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Attachments;
using ClassFund.Application.Features.Balance;
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
    IAttachmentsService attachments,
    IBalanceService balances,
    AuditTrail audit,
    Notifier notifier,
    TimeProvider clock) : IPaymentsService
{
    private static readonly IReadOnlySet<AttachmentKind> ProofKinds = new HashSet<AttachmentKind> { AttachmentKind.PaymentProof };

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

        // Với khoản thu sự kiện, cần tên sự kiện để hiển thị ở H-03
        var eventIds = rows.Where(row => row.charge.EventId.HasValue).Select(row => row.charge.EventId!.Value).Distinct().ToArray();
        var eventNames = eventIds.Length == 0
            ? new Dictionary<Guid, string>()
            : (await db.ToListAsync(
                    db.Query<Event>().Where(e => eventIds.Contains(e.Id)).Select(e => new { e.Id, e.Name }),
                    cancellationToken))
                .ToDictionary(e => e.Id, e => e.Name);

        var assignmentIds = rows.Select(row => row.assignment.Id).ToArray();
        var latest = (await db.ToListAsync(
                db.Query<Payment>().Where(payment => assignmentIds.Contains(payment.AssignmentId)),
                cancellationToken))
            .GroupBy(payment => payment.AssignmentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(payment => payment.Status != PaymentStatus.Rejected).ThenByDescending(payment => payment.SubmittedAt).First());

        var applied = await BalanceAppliedAsync(latest.Values.Select(payment => payment.Id).ToArray(), cancellationToken);
        return rows
            .OrderBy(row => row.assignment.Status == AssignmentStatus.Paid ? 1 : 0)
            .ThenBy(row => row.charge.DueDate)
            .ThenBy(row => row.assignment.Id)
            .Select(row =>
            {
                latest.TryGetValue(row.assignment.Id, out var payment);
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
                    payment?.Id,
                    payment?.Method,
                    payment?.Status,
                    payment?.Status == PaymentStatus.Rejected ? payment.RejectionReason : null,
                    payment?.ConfirmationMode == ConfirmationMode.SelfDeclared,
                    eventId,
                    eventId.HasValue ? eventNames.GetValueOrDefault(eventId.Value) : null,
                    payment is null ? 0 : applied.GetValueOrDefault(payment.Id));
            })
            .ToArray();
    }

    public async Task<PaymentDto> SubmitAsync(Guid fundId, Guid assignmentId, SubmitPaymentInput input, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.PaymentSubmit))
        {
            throw new ForbiddenAccessException();
        }

        if (input.Method == PaymentMethod.BankTransfer && input.AttachmentId is null && !input.UseBalance)
        {
            throw new BusinessRuleException(PaymentsErrors.ProofRequiredCode, PaymentsErrors.ProofRequiredMessage);
        }

        var transferCode = string.IsNullOrWhiteSpace(input.TransferCode) ? null : input.TransferCode.Trim();

        try
        {
            return await db.InTransactionAsync(
                async token =>
                {
                    await EnsureFundActiveAsync(fundId, token);

                    var assignment = await db.LockAsync<ChargeAssignment>(assignmentId, token);
                    if (assignment is null || assignment.UserId != userId)
                    {
                        throw new NotFoundException(); // AZ-04: nghĩa vụ của người khác như không tồn tại
                    }

                    var charge = await LoadChargeAsync(assignment.ChargeId, fundId, token);
                    if (assignment.Status == AssignmentStatus.Cancelled || assignment.Status == AssignmentStatus.Excluded)
                    {
                        throw new NotFoundException();
                    }

                    if (assignment.Status != AssignmentStatus.Unpaid)
                    {
                        throw new ConflictException(PaymentsErrors.NotPayableCode, PaymentsErrors.NotPayableMessage);
                    }

                    if (charge.Status != ChargeStatus.Open)
                    {
                        throw new ConflictException(PaymentsErrors.ChargeNotOpenCode, PaymentsErrors.ChargeNotOpenMessage);
                    }

                    // Khoản thu sự kiện: sự kiện phải còn AcceptsPayments (Open hoặc Completed)
                    if (charge.EventId is { } submitEventId)
                    {
                        var eventStatus = await db.FirstOrDefaultAsync(
                            db.Query<Event>().Where(e => e.Id == submitEventId).Select(e => (EventStatus?)e.Status), token);
                        if (eventStatus is null || !EventStatusRules.AcceptsPayments(eventStatus.Value))
                        {
                            throw new ConflictException(PaymentsErrors.ChargeNotOpenCode, PaymentsErrors.ChargeNotOpenMessage);
                        }
                    }

                    // Dùng số dư: toàn bộ (PersonalBalance) hoặc một phần rồi đóng nốt bằng tiền mặt / chuyển khoản (UseBalance).
                    var method = input.Method;
                    long balancePart = 0;
                    if (method == PaymentMethod.PersonalBalance)
                    {
                        balancePart = assignment.AmountDue;
                    }
                    else if (input.UseBalance)
                    {
                        var available = await balances.GetBalanceForUpdateAsync(fundId, userId, token);
                        if (available <= 0)
                        {
                            throw new BusinessRuleException(BalanceErrors.Insufficient, "Bạn chưa có số dư để sử dụng.");
                        }

                        balancePart = Math.Min(available, assignment.AmountDue);
                        if (balancePart == assignment.AmountDue)
                        {
                            method = PaymentMethod.PersonalBalance;
                        }
                    }

                    var viaBalance = method == PaymentMethod.PersonalBalance;
                    if (!viaBalance && method == PaymentMethod.BankTransfer && input.AttachmentId is null)
                    {
                        throw new BusinessRuleException(PaymentsErrors.ProofRequiredCode, PaymentsErrors.ProofRequiredMessage);
                    }

                    var scope = charge.EventId.HasValue ? FinancialScope.Event : FinancialScope.General;
                    var now = clock.GetUtcNow();
                    var payment = new Payment(assignment.Id, userId, assignment.AmountDue, method, now) { TransferCode = viaBalance ? null : transferCode };
                    db.Add(payment);

                    if (balancePart > 0)
                    {
                        await balances.DebitAsync(fundId, userId, balancePart, BalanceTransactionType.ChargePayment, payment.Id, userId, token);
                    }

                    var proofCount = 0;
                    if (!viaBalance && input.AttachmentId is { } attachmentId)
                    {
                        proofCount = await attachments.AttachToAsync(fundId, [attachmentId], payment.Id, ProofKinds, token);
                    }

                    var payer = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == userId), token) ?? throw new NotFoundException();
                    audit.Record(new AuditEntry(
                        PaymentsAuditActions.Submit,
                        fundId,
                        userId,
                        scope,
                        EventId: charge.EventId,
                        EntityType: nameof(Payment),
                        EntityId: payment.Id,
                        After: new
                        {
                            chargeTitle = charge.Title,
                            payerName = payer.FullName,
                            amount = payment.Amount,
                            method = method.ToString(),
                            hasProof = proofCount > 0,
                            balanceUsed = balancePart,
                        }));

                    if (viaBalance)
                    {
                        // Số dư đã trừ ở trên (không đủ → 422, đang chờ rút → 422); xác nhận ngay trong cùng transaction.
                        await ConfirmCoreAsync(fundId, payment, assignment, charge, payer, userId, ConfirmationMode.Verified, token, viaBalance: true);
                        await db.SaveChangesAsync(token);
                        return ToDto(payment, balancePart);
                    }

                    // BR-12: thủ quỹ nộp cho mình = gửi + tự xác nhận trong một thao tác.
                    var decision = PaymentConfirmationPolicy.Decide(userId, userId, access.Permissions.Contains(Permissions.PaymentConfirm));
                    if (decision.Allowed)
                    {
                        await ConfirmCoreAsync(fundId, payment, assignment, charge, payer, userId, decision.Mode, token);
                    }
                    else
                    {
                        assignment.Status = AssignmentStatus.AwaitingConfirmation;
                    }

                    await db.SaveChangesAsync(token);
                    return ToDto(payment, balancePart);
                },
                cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // BR-14: hai lần nộp đồng thời cho cùng nghĩa vụ – chỉ một lần thắng.
            throw new ConflictException(PaymentsErrors.NotPayableCode, PaymentsErrors.NotPayableMessage);
        }
    }

    public async Task<PaymentDto> RecordCashAsync(Guid fundId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var actorId = await EnsureConfirmerAsync(fundId, cancellationToken);

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

                    if (charge.Status != ChargeStatus.Open)
                    {
                        throw new ConflictException(PaymentsErrors.ChargeNotOpenCode, PaymentsErrors.ChargeNotOpenMessage);
                    }

                    // Khoản thu sự kiện: sự kiện phải còn AcceptsPayments
                    if (charge.EventId is { } recordEventId)
                    {
                        var eventStatus = await db.FirstOrDefaultAsync(
                            db.Query<Event>().Where(e => e.Id == recordEventId).Select(e => (EventStatus?)e.Status), token);
                        if (eventStatus is null || !EventStatusRules.AcceptsPayments(eventStatus.Value))
                        {
                            throw new ConflictException(PaymentsErrors.ChargeNotOpenCode, PaymentsErrors.ChargeNotOpenMessage);
                        }
                    }

                    var recordScope = charge.EventId.HasValue ? FinancialScope.Event : FinancialScope.General;
                    var payer = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == assignment.UserId), token)
                        ?? throw new NotFoundException();
                    var payment = new Payment(assignment.Id, payer.Id, assignment.AmountDue, PaymentMethod.Cash, clock.GetUtcNow());
                    db.Add(payment);
                    audit.Record(new AuditEntry(
                        PaymentsAuditActions.Submit,
                        fundId,
                        actorId,
                        recordScope,
                        EventId: charge.EventId,
                        EntityType: nameof(Payment),
                        EntityId: payment.Id,
                        After: new { chargeTitle = charge.Title, payerName = payer.FullName, amount = payment.Amount, method = nameof(PaymentMethod.Cash) },
                        Note: "Thủ quỹ ghi nhận tiền mặt."));

                    var decision = PaymentConfirmationPolicy.Decide(actorId, payer.Id, actorCanConfirm: true);
                    await ConfirmCoreAsync(fundId, payment, assignment, charge, payer, actorId, decision.Mode, token);
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

    public async Task<PagedResult<PaymentQueueItemDto>> ListAsync(Guid fundId, PaymentFilter filter, CancellationToken cancellationToken)
    {
        await EnsureConfirmerAsync(fundId, cancellationToken);
        var status = filter.Status ?? PaymentStatus.Submitted;

        var query = QueueQuery(fundId).Where(row => row.Payment.Status == status);
        var total = await db.CountAsync(query, cancellationToken);
        var rows = await db.ToListAsync(
            query
                .OrderBy(row => row.Payment.SubmittedAt)
                .ThenBy(row => row.Payment.Id)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize),
            cancellationToken);

        return new PagedResult<PaymentQueueItemDto>(await ToQueueItemsAsync(fundId, rows, cancellationToken), total, filter.Page, filter.PageSize);
    }

    public async Task<PaymentQueueItemDto> ConfirmAsync(Guid fundId, Guid paymentId, CancellationToken cancellationToken)
    {
        var actorId = await EnsureConfirmerAsync(fundId, cancellationToken);

        await db.InTransactionAsync(
            async token =>
            {
                await EnsureFundActiveAsync(fundId, token);
                var (payment, assignment, charge, payer) = await LockSubmittedAsync(fundId, paymentId, token);

                var decision = PaymentConfirmationPolicy.Decide(actorId, payment.PayerUserId, actorCanConfirm: true);
                await ConfirmCoreAsync(fundId, payment, assignment, charge, payer, actorId, decision.Mode, token);
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

        return await LoadQueueItemAsync(fundId, paymentId, cancellationToken);
    }

    public async Task<PaymentQueueItemDto> RejectAsync(Guid fundId, Guid paymentId, string reason, CancellationToken cancellationToken)
    {
        var actorId = await EnsureConfirmerAsync(fundId, cancellationToken);
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new BusinessRuleException(PaymentsErrors.ReasonRequiredCode, PaymentsErrors.ReasonRequiredMessage);
        }

        await db.InTransactionAsync(
            async token =>
            {
                await EnsureFundActiveAsync(fundId, token);
                var (payment, assignment, charge, payer) = await LockSubmittedAsync(fundId, paymentId, token);

                payment.Status = PaymentStatus.Rejected;
                payment.RejectionReason = trimmed;
                assignment.Status = AssignmentStatus.Unpaid;

                // Phần đã trừ từ số dư cá nhân được hoàn lại để người nộp nộp lại.
                var refunded = (await BalanceAppliedAsync([payment.Id], token)).GetValueOrDefault(payment.Id);
                if (refunded > 0)
                {
                    await balances.CreditAsync(fundId, payment.PayerUserId, refunded, BalanceTransactionType.ChargeRefund, payment.Id, actorId, token);
                }

                var rejectScope = charge.EventId.HasValue ? FinancialScope.Event : FinancialScope.General;
                audit.Record(new AuditEntry(
                    PaymentsAuditActions.Reject,
                    fundId,
                    actorId,
                    rejectScope,
                    EventId: charge.EventId,
                    EntityType: nameof(Payment),
                    EntityId: payment.Id,
                    Before: new { status = nameof(PaymentStatus.Submitted) },
                    After: new { chargeTitle = charge.Title, payerName = payer.FullName, amount = payment.Amount, status = nameof(PaymentStatus.Rejected), reason = trimmed }));
                notifier.Send(
                    [payment.PayerUserId],
                    new NotificationMessage(
                        fundId,
                        rejectScope,
                        charge.EventId,
                        NotificationType.PaymentRejected,
                        $"{Notifier.GeneralPrefix} Thanh toán bị từ chối: {charge.Title}",
                        refunded > 0 ? $"Lý do: {trimmed}. Số dư {refunded:N0}đ đã dùng được hoàn lại, bạn có thể nộp lại." : $"Lý do: {trimmed}. Bạn có thể nộp lại.",
                        nameof(Payment),
                        payment.Id));
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

        return await LoadQueueItemAsync(fundId, paymentId, cancellationToken);
    }

    /// <summary>Chuyển Payment sang Confirmed, nghĩa vụ → Paid, ghi bút toán thu + audit (+ thông báo nếu người xác nhận khác người nộp).</summary>
    private async Task ConfirmCoreAsync(
        Guid fundId, Payment payment, ChargeAssignment assignment, Charge charge, User payer, Guid actorId, ConfirmationMode mode, CancellationToken cancellationToken, bool viaBalance = false)
    {
        var now = clock.GetUtcNow();
        var selfDeclared = mode == ConfirmationMode.SelfDeclared;

        payment.Status = PaymentStatus.Confirmed;
        payment.ConfirmedByUserId = actorId;
        payment.ConfirmedAt = now;
        payment.ConfirmationMode = mode;
        assignment.Status = AssignmentStatus.Paid;

        // Danh mục do thủ quỹ chọn khi tạo khoản thu; khoản thu cũ (chưa có danh mục) dùng danh mục mặc định nếu còn.
        var categoryId = charge.CategoryId;
        if (categoryId is null)
        {
            var legacyName = DefaultCategories.All.First(item => item.Type == LedgerType.Income).Name;
            categoryId = await db.FirstOrDefaultAsync(
                db.Query<Category>()
                    .Where(category => category.FundId == fundId && category.Type == LedgerType.Income && category.Name == legacyName)
                    .Select(category => (Guid?)category.Id),
                cancellationToken);
        }

        var confirmScope = charge.EventId.HasValue ? FinancialScope.Event : FinancialScope.General;
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
            TransferCode = payment.TransferCode,
            IsSelfDeclared = selfDeclared,
        });

        audit.Record(new AuditEntry(
            viaBalance ? PaymentsAuditActions.PayWithBalance : PaymentsAuditActions.Confirm,
            fundId,
            actorId,
            confirmScope,
            EventId: charge.EventId,
            EntityType: nameof(Payment),
            EntityId: payment.Id,
            Before: new { status = nameof(PaymentStatus.Submitted) },
            After: new { chargeTitle = charge.Title, payerName = payer.FullName, amount = payment.Amount, status = nameof(PaymentStatus.Confirmed) },
            IsSelfAction: selfDeclared,
            Note: selfDeclared ? "Thủ quỹ tự ghi nhận khoản đóng của chính mình." : null));

        if (!selfDeclared && !viaBalance)
        {
            notifier.Send(
                [payment.PayerUserId],
                new NotificationMessage(
                    fundId,
                    confirmScope,
                    charge.EventId,
                    NotificationType.PaymentConfirmed,
                    $"{Notifier.GeneralPrefix} Đã xác nhận khoản đóng: {charge.Title}",
                    $"Thủ quỹ đã xác nhận bạn đóng {payment.Amount:N0}đ.",
                    nameof(Payment),
                    payment.Id));
        }
    }

    /// <summary>Khóa Payment + nghĩa vụ; chỉ xử lý được khi đang <c>Submitted</c> (chống xác nhận đôi).</summary>
    private async Task<(Payment Payment, ChargeAssignment Assignment, Charge Charge, User Payer)> LockSubmittedAsync(
        Guid fundId, Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await db.LockAsync<Payment>(paymentId, cancellationToken) ?? throw new NotFoundException();
        var assignment = await db.LockAsync<ChargeAssignment>(payment.AssignmentId, cancellationToken) ?? throw new NotFoundException();
        var charge = await LoadChargeAsync(assignment.ChargeId, fundId, cancellationToken);

        if (payment.Status != PaymentStatus.Submitted)
        {
            throw new ConflictException(PaymentsErrors.AlreadyProcessedCode, PaymentsErrors.AlreadyProcessedMessage);
        }

        var payer = await db.FirstOrDefaultAsync(db.Query<User>().Where(user => user.Id == payment.PayerUserId), cancellationToken)
            ?? throw new NotFoundException();
        return (payment, assignment, charge, payer);
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

    /// <summary>Dòng truy vấn hàng chờ; dùng khởi tạo thuộc tính (không dùng constructor) để EF Core dịch được phép chiếu.</summary>
    private sealed class QueueRow
    {
        public required Payment Payment { get; init; }

        public required Charge Charge { get; init; }

        public required User Payer { get; init; }
    }

    private IQueryable<QueueRow> QueueQuery(Guid fundId) =>
        from payment in db.Query<Payment>()
        join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
        join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
        where charge.FundId == fundId
        join user in db.Query<User>() on payment.PayerUserId equals user.Id
        select new QueueRow { Payment = payment, Charge = charge, Payer = user };

    private async Task<PaymentQueueItemDto> LoadQueueItemAsync(Guid fundId, Guid paymentId, CancellationToken cancellationToken)
    {
        var rows = await db.ToListAsync(QueueQuery(fundId).Where(row => row.Payment.Id == paymentId), cancellationToken);
        return (await ToQueueItemsAsync(fundId, rows, cancellationToken)).SingleOrDefault() ?? throw new NotFoundException();
    }

    private async Task<IReadOnlyList<PaymentQueueItemDto>> ToQueueItemsAsync(Guid fundId, List<QueueRow> rows, CancellationToken cancellationToken)
    {
        var paymentIds = rows.Select(row => row.Payment.Id).ToArray();
        var files = (await db.ToListAsync(
                db.Query<Attachment>().Where(item => item.FundId == fundId && item.OwnerId != null && paymentIds.Contains(item.OwnerId.Value)),
                cancellationToken))
            .GroupBy(item => item.OwnerId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AttachmentDto>)group.OrderBy(item => item.UploadedAt).Select(AttachmentsService.ToDto).ToArray());

        var applied = await BalanceAppliedAsync(paymentIds, cancellationToken);
        var eventIds = rows.Where(row => row.Charge.EventId.HasValue).Select(row => row.Charge.EventId!.Value).Distinct().ToArray();
        var eventNames = eventIds.Length == 0
            ? new Dictionary<Guid, string>()
            : (await db.ToListAsync(db.Query<Event>().Where(item => eventIds.Contains(item.Id)).Select(item => new { item.Id, item.Name }), cancellationToken))
                .ToDictionary(item => item.Id, item => item.Name);
        return rows
            .Select(row => new PaymentQueueItemDto(
                row.Payment.Id,
                row.Payment.AssignmentId,
                row.Charge.Id,
                row.Charge.Title,
                row.Payer.Id,
                row.Payer.FullName,
                row.Payer.Email,
                row.Payment.Amount,
                row.Payment.Method,
                row.Payment.TransferCode,
                row.Payment.Status,
                row.Payment.SubmittedAt,
                row.Payment.ConfirmedAt,
                row.Payment.ConfirmationMode == ConfirmationMode.SelfDeclared,
                row.Payment.RejectionReason,
                files.GetValueOrDefault(row.Payment.Id) ?? [],
                applied.GetValueOrDefault(row.Payment.Id),
                row.Charge.EventId,
                row.Charge.EventId.HasValue ? eventNames.GetValueOrDefault(row.Charge.EventId.Value) : null))
            .ToArray();
    }

    private static PaymentDto ToDto(Payment payment, long balanceApplied = 0) =>
        new(payment.Id, payment.AssignmentId, payment.Amount, payment.Method, payment.TransferCode, payment.Status, payment.SubmittedAt,
            payment.ConfirmationMode == ConfirmationMode.SelfDeclared, balanceApplied);

    /// <summary>Số dư cá nhân đã dùng cho mỗi thanh toán (đã trừ hoàn lại khi bị từ chối).</summary>
    private async Task<Dictionary<Guid, long>> BalanceAppliedAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken cancellationToken)
    {
        if (paymentIds.Count == 0)
        {
            return [];
        }

        var rows = await db.ToListAsync(
            db.Query<BalanceTransaction>().Where(item =>
                item.ReferenceId != null && paymentIds.Contains(item.ReferenceId.Value)
                && (item.Type == BalanceTransactionType.ChargePayment || item.Type == BalanceTransactionType.ChargeRefund)),
            cancellationToken);
        return rows.GroupBy(item => item.ReferenceId!.Value).ToDictionary(group => group.Key, group => -group.Sum(item => item.Amount));
    }

    private async Task<Guid> EnsureConfirmerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(Permissions.PaymentConfirm) ? userId : throw new ForbiddenAccessException();
    }

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
