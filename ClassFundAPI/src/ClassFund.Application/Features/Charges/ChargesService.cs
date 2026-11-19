using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.Categories;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Charges;

/// <inheritdoc />
public sealed class ChargesService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    ICategoriesService categories,
    AuditTrail audit,
    Notifier notifier,
    TimeProvider clock) : IChargesService
{
    public async Task<PagedResult<ChargeDto>> ListAsync(Guid fundId, ChargeFilter filter, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);

        var query = db.Query<Charge>().Where(charge => charge.FundId == fundId && charge.EventId == null);
        if (filter.Status is { } status)
        {
            query = query.Where(charge => charge.Status == status);
        }

        var total = await db.CountAsync(query, cancellationToken);
        var charges = await db.ToListAsync(
            query
                .OrderBy(charge => charge.Status == ChargeStatus.Open ? 0 : 1)
                .ThenBy(charge => charge.DueDate)
                .ThenByDescending(charge => charge.CreatedAt)
                .ThenBy(charge => charge.Id)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize),
            cancellationToken);

        var items = await ToDtosAsync(charges, userId, access.Permissions.Contains(Permissions.ChargeManage), cancellationToken);
        return new PagedResult<ChargeDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<ChargeDto> GetAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        var charge = await LoadAsync(fundId, chargeId, cancellationToken);
        return (await ToDtosAsync([charge], userId, access.Permissions.Contains(Permissions.ChargeManage), cancellationToken))[0];
    }

    public async Task<ChargeDto> CreateAsync(Guid fundId, CreateChargeInput input, CancellationToken cancellationToken)
    {
        var actorId = await EnsureManagerAsync(fundId, cancellationToken);
        MoneyRule.EnsureValid(input.AmountPerPerson);
        var title = input.Title.Trim();
        var purpose = string.IsNullOrWhiteSpace(input.Purpose) ? null : input.Purpose.Trim();
        if (input.DueDate < VietnamTime.DateOf(clock.GetUtcNow()))
        {
            throw new BusinessRuleException(ChargesErrors.DueDateInPastCode, ChargesErrors.DueDateInPastMessage);
        }

        var charge = await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);
                await categories.EnsureUsableAsync(fundId, input.CategoryId, LedgerType.Income, token);

                var memberIds = await db.ToListAsync(
                    db.Query<FundMember>()
                        .Where(member => member.FundId == fundId && member.Status == MemberStatus.Active)
                        .Select(member => member.UserId),
                    token);

                var created = new Charge(fundId, null, title, purpose, input.AmountPerPerson, input.DueDate, ChargeKind.Regular, actorId, clock.GetUtcNow()) { CategoryId = input.CategoryId };
                db.Add(created);
                foreach (var memberId in memberIds)
                {
                    db.Add(new ChargeAssignment(created.Id, memberId, input.AmountPerPerson));
                }

                audit.Record(new AuditEntry(
                    ChargesAuditActions.Create,
                    fundId,
                    actorId,
                    FinancialScope.General,
                    EntityType: nameof(Charge),
                    EntityId: created.Id,
                    After: new { title, categoryId = input.CategoryId, amountPerPerson = input.AmountPerPerson, dueDate = input.DueDate.ToString("yyyy-MM-dd"), count = memberIds.Count }));
                notifier.Send(
                    memberIds,
                    new NotificationMessage(
                        fundId,
                        FinancialScope.General,
                        null,
                        NotificationType.GeneralChargeCreated,
                        $"{Notifier.GeneralPrefix} Khoản thu mới: {title}",
                        $"Mỗi người đóng {input.AmountPerPerson:N0}đ, hạn đóng {input.DueDate:dd/MM/yyyy}.",
                        nameof(Charge),
                        created.Id));
                await db.SaveChangesAsync(token);
                return created;
            },
            cancellationToken);

        return (await ToDtosAsync([charge], actorId, canManage: true, cancellationToken))[0];
    }

    public async Task<PagedResult<AssignmentRowDto>> ListAssignmentsAsync(
        Guid fundId, Guid chargeId, AssignmentFilter filter, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(fundId, cancellationToken);
        await LoadAsync(fundId, chargeId, cancellationToken);

        var query = from assignment in db.Query<ChargeAssignment>()
                    where assignment.ChargeId == chargeId && assignment.Status != AssignmentStatus.Cancelled
                    join user in db.Query<User>() on assignment.UserId equals user.Id
                    select new { assignment, user };

        if (filter.Status is { } status)
        {
            query = query.Where(row => row.assignment.Status == status);
        }

        var term = filter.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(row => row.user.FullName.ToLower().Contains(term) || row.user.Email.Contains(term));
        }

        var total = await db.CountAsync(query, cancellationToken);
        var rows = await db.ToListAsync(
            query
                .OrderBy(row => row.assignment.Status == AssignmentStatus.Paid ? 1 : 0)
                .ThenBy(row => row.user.FullName)
                .ThenBy(row => row.assignment.Id)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize),
            cancellationToken);

        var assignmentIds = rows.Select(row => row.assignment.Id).ToArray();
        var userIds = rows.Select(row => row.user.Id).ToArray();
        var payments = (await db.ToListAsync(
                db.Query<Payment>().Where(payment => assignmentIds.Contains(payment.AssignmentId)),
                cancellationToken))
            .GroupBy(payment => payment.AssignmentId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var paymentIds = payments.Values.SelectMany(list => list).Select(payment => payment.Id).ToArray();
        var proofOwners = (await db.ToListAsync(
                db.Query<Attachment>()
                    .Where(item => item.FundId == fundId && item.Kind == AttachmentKind.PaymentProof && item.OwnerId != null && paymentIds.Contains(item.OwnerId.Value))
                    .Select(item => item.OwnerId!.Value),
                cancellationToken)).ToHashSet();
        var roles = (await db.ToListAsync(
                db.Query<FundMember>()
                    .Where(member => member.FundId == fundId && member.Status == MemberStatus.Active && userIds.Contains(member.UserId))
                    .Select(member => new { member.UserId, member.Role }),
                cancellationToken))
            .ToDictionary(item => item.UserId, item => item.Role);

        var items = rows
            .Select(row =>
            {
                var list = payments.GetValueOrDefault(row.assignment.Id) ?? [];
                var current = list.FirstOrDefault(payment => payment.Status != PaymentStatus.Rejected);
                var rejected = current is null && row.assignment.Status == AssignmentStatus.Unpaid
                    ? list.OrderByDescending(payment => payment.SubmittedAt).FirstOrDefault()
                    : null;
                return new AssignmentRowDto(
                    row.assignment.Id,
                    row.user.Id,
                    row.user.FullName,
                    row.user.Email,
                    roles.GetValueOrDefault(row.user.Id, MemberRole.Student),
                    row.assignment.AmountDue,
                    row.assignment.Status,
                    current?.ConfirmationMode == ConfirmationMode.SelfDeclared,
                    current?.Id,
                    current?.Method,
                    current is null ? null : current.ConfirmedAt ?? current.SubmittedAt,
                    current is not null && proofOwners.Contains(current.Id),
                    rejected?.RejectionReason);
            })
            .ToArray();
        return new PagedResult<AssignmentRowDto>(items, total, filter.Page, filter.PageSize);
    }

    public Task<ChargeDto> CloseAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(fundId, chargeId, ChargeStatus.Closed, ChargesAuditActions.Close, cancellationToken);

    public Task<ChargeDto> CancelAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken) =>
        ChangeStatusAsync(fundId, chargeId, ChargeStatus.Cancelled, ChargesAuditActions.Cancel, cancellationToken);

    public async Task<RemindResult> RemindAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken)
    {
        var actorId = await EnsureManagerAsync(fundId, cancellationToken);

        return await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);
                var charge = await db.LockAsync<Charge>(chargeId, token);
                if (charge is null || charge.FundId != fundId || charge.EventId is not null)
                {
                    throw new NotFoundException();
                }

                if (charge.Status != ChargeStatus.Open)
                {
                    throw new ConflictException(ChargesErrors.InvalidStatusCode, ChargesErrors.InvalidStatusMessage);
                }

                var since = clock.GetUtcNow() - ChargesErrors.RemindCooldown;
                var chargeKey = chargeId.ToString();
                if (await db.AnyAsync(
                        db.Query<AuditLog>().Where(log => log.Action == ChargesAuditActions.Remind && log.EntityId == chargeKey && log.At > since),
                        token))
                {
                    throw new ConflictException(ChargesErrors.RemindTooSoonCode, ChargesErrors.RemindTooSoonMessage);
                }

                var unpaid = await db.ToListAsync(
                    db.Query<ChargeAssignment>()
                        .Where(item => item.ChargeId == chargeId && item.Status == AssignmentStatus.Unpaid)
                        .Select(item => item.UserId),
                    token);
                if (unpaid.Count == 0)
                {
                    throw new BusinessRuleException(ChargesErrors.NothingToRemindCode, ChargesErrors.NothingToRemindMessage);
                }

                notifier.Send(
                    unpaid,
                    new NotificationMessage(
                        fundId,
                        FinancialScope.General,
                        null,
                        NotificationType.ChargeDueSoon,
                        $"{Notifier.GeneralPrefix} Nhắc đóng: {charge.Title}",
                        $"Bạn chưa đóng {charge.AmountPerPerson:N0}đ, hạn đóng {charge.DueDate:dd/MM/yyyy}.",
                        nameof(Charge),
                        charge.Id));
                audit.Record(new AuditEntry(
                    ChargesAuditActions.Remind,
                    fundId,
                    actorId,
                    FinancialScope.General,
                    EntityType: nameof(Charge),
                    EntityId: charge.Id,
                    After: new { title = charge.Title, count = unpaid.Count }));
                await db.SaveChangesAsync(token);
                return new RemindResult(unpaid.Count);
            },
            cancellationToken);
    }

    // ---- Khoản thu sự kiện (M6b) ----

    public async Task<EventChargeDto> CreateEventChargeAsync(Guid fundId, Guid eventId, CreateChargeInput input, CancellationToken cancellationToken)
    {
        var actorId = await EnsureManagerAsync(fundId, cancellationToken);
        MoneyRule.EnsureValid(input.AmountPerPerson);
        var title = input.Title.Trim();
        var purpose = string.IsNullOrWhiteSpace(input.Purpose) ? null : input.Purpose.Trim();
        if (input.DueDate < VietnamTime.DateOf(clock.GetUtcNow()))
        {
            throw new BusinessRuleException(ChargesErrors.DueDateInPastCode, ChargesErrors.DueDateInPastMessage);
        }

        return await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);
                await categories.EnsureUsableAsync(fundId, input.CategoryId, LedgerType.Income, token);

                var fundEvent = await db.FirstOrDefaultAsync(
                    db.Query<Event>().Where(e => e.Id == eventId && e.FundId == fundId), token)
                    ?? throw new NotFoundException();
                if (!EventStatusRules.AcceptsNewChargesAndExpenses(fundEvent.Status))
                {
                    throw new BusinessRuleException(EventsErrors.NotOpen, EventsErrors.NotOpenMessage);
                }

                // BR-18: chỉ gán cho người đã xác nhận tham gia
                var acceptedIds = await db.ToListAsync(
                    db.Query<EventParticipant>()
                        .Where(p => p.EventId == eventId && p.Response == ParticipationResponse.Accepted)
                        .Select(p => p.UserId),
                    token);

                var created = new Charge(fundId, eventId, title, purpose, input.AmountPerPerson, input.DueDate, ChargeKind.Regular, actorId, clock.GetUtcNow()) { CategoryId = input.CategoryId };
                db.Add(created);
                foreach (var memberId in acceptedIds)
                {
                    db.Add(new ChargeAssignment(created.Id, memberId, input.AmountPerPerson));
                }

                audit.Record(new AuditEntry(
                    ChargesAuditActions.EventCreate,
                    fundId,
                    actorId,
                    FinancialScope.Event,
                    EventId: eventId,
                    EntityType: nameof(Charge),
                    EntityId: created.Id,
                    After: new { title, categoryId = input.CategoryId, amountPerPerson = input.AmountPerPerson, dueDate = input.DueDate.ToString("yyyy-MM-dd"), count = acceptedIds.Count }));
                notifier.Send(
                    acceptedIds,
                    new NotificationMessage(
                        fundId,
                        FinancialScope.Event,
                        eventId,
                        NotificationType.EventChargeCreated,
                        $"{Notifier.ScopePrefix(FinancialScope.Event, fundEvent.Name)} Khoản thu mới: {title}",
                        $"Mỗi người đóng {input.AmountPerPerson:N0}đ, hạn đóng {input.DueDate:dd/MM/yyyy}.",
                        nameof(Charge),
                        created.Id));
                await db.SaveChangesAsync(token);
                return ToEventChargeDto(created, acceptedIds.Count, 0, 0);
            },
            cancellationToken);
    }

    public Task<EventChargeDto> CloseEventChargeAsync(Guid fundId, Guid eventId, Guid chargeId, CancellationToken cancellationToken) =>
        ChangeEventChargeStatusAsync(fundId, eventId, chargeId, ChargeStatus.Closed, ChargesAuditActions.EventClose, cancellationToken);

    public Task<EventChargeDto> CancelEventChargeAsync(Guid fundId, Guid eventId, Guid chargeId, CancellationToken cancellationToken) =>
        ChangeEventChargeStatusAsync(fundId, eventId, chargeId, ChargeStatus.Cancelled, ChargesAuditActions.EventCancel, cancellationToken);

    public async Task<PagedResult<AssignmentRowDto>> ListEventAssignmentsAsync(
        Guid fundId, Guid eventId, Guid chargeId, AssignmentFilter filter, CancellationToken cancellationToken)
    {
        await EnsureManagerAsync(fundId, cancellationToken);
        // kiểm tra khoản thu thuộc sự kiện thuộc quỹ
        if (!await db.AnyAsync(db.Query<Charge>().Where(c => c.Id == chargeId && c.FundId == fundId && c.EventId == eventId), cancellationToken))
        {
            throw new NotFoundException();
        }

        // dùng lại cùng logic với khoản thu chung
        return await ListAssignmentsAsync(fundId, chargeId, filter, cancellationToken);
    }

    private async Task<EventChargeDto> ChangeEventChargeStatusAsync(
        Guid fundId, Guid eventId, Guid chargeId, ChargeStatus target, string action, CancellationToken cancellationToken)
    {
        var actorId = await EnsureManagerAsync(fundId, cancellationToken);

        return await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);

                var locked = await db.LockAsync<Charge>(chargeId, token);
                if (locked is null || locked.FundId != fundId || locked.EventId != eventId)
                {
                    throw new NotFoundException();
                }

                var allowed = target == ChargeStatus.Closed
                    ? locked.Status == ChargeStatus.Open
                    : locked.Status is ChargeStatus.Open or ChargeStatus.Closed;
                if (!allowed)
                {
                    throw new ConflictException(ChargesErrors.InvalidStatusCode, ChargesErrors.InvalidStatusMessage);
                }

                var before = locked.Status;
                if (target == ChargeStatus.Cancelled)
                {
                    var assignments = await db.ToListAsync(db.Query<ChargeAssignment>().Where(item => item.ChargeId == chargeId), token);
                    var assignmentIds = assignments.Select(item => item.Id).ToArray();
                    var hasPayments = await db.AnyAsync(
                        db.Query<Payment>().Where(payment => assignmentIds.Contains(payment.AssignmentId) && payment.Status != PaymentStatus.Rejected),
                        token);
                    if (hasPayments)
                    {
                        throw new BusinessRuleException(ChargesErrors.HasPaymentsCode, ChargesErrors.HasPaymentsMessage);
                    }

                    foreach (var assignment in assignments)
                    {
                        assignment.Status = AssignmentStatus.Cancelled;
                    }
                }

                locked.Status = target;
                audit.Record(new AuditEntry(
                    action,
                    fundId,
                    actorId,
                    FinancialScope.Event,
                    EventId: eventId,
                    EntityType: nameof(Charge),
                    EntityId: locked.Id,
                    Before: new { status = before.ToString() },
                    After: new { title = locked.Title, status = target.ToString() }));
                await db.SaveChangesAsync(token);

                // tính lại counts để trả về EventChargeDto
                var allAssignments = await db.ToListAsync(
                    db.Query<ChargeAssignment>().Where(item => item.ChargeId == chargeId && item.Status != AssignmentStatus.Cancelled),
                    token);
                return ToEventChargeDto(locked, allAssignments.Count,
                    allAssignments.Count(a => a.Status == AssignmentStatus.Paid),
                    allAssignments.Count(a => a.Status == AssignmentStatus.AwaitingConfirmation));
            },
            cancellationToken);
    }

    private static EventChargeDto ToEventChargeDto(Charge charge, int assigned, int paid, int pending) =>
        new(charge.Id, charge.Title, charge.Purpose, charge.AmountPerPerson, charge.DueDate, charge.Status,
            paid, pending, assigned - paid - pending);

    private async Task<ChargeDto> ChangeStatusAsync(Guid fundId, Guid chargeId, ChargeStatus target, string action, CancellationToken cancellationToken)
    {
        var actorId = await EnsureManagerAsync(fundId, cancellationToken);

        var charge = await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);

                var locked = await db.LockAsync<Charge>(chargeId, token);
                if (locked is null || locked.FundId != fundId || locked.EventId is not null)
                {
                    throw new NotFoundException();
                }

                var allowed = target == ChargeStatus.Closed
                    ? locked.Status == ChargeStatus.Open
                    : locked.Status is ChargeStatus.Open or ChargeStatus.Closed;
                if (!allowed)
                {
                    throw new ConflictException(ChargesErrors.InvalidStatusCode, ChargesErrors.InvalidStatusMessage);
                }

                var before = locked.Status;
                if (target == ChargeStatus.Cancelled)
                {
                    var assignments = await db.ToListAsync(db.Query<ChargeAssignment>().Where(item => item.ChargeId == chargeId), token);
                    var assignmentIds = assignments.Select(item => item.Id).ToArray();
                    var hasPayments = await db.AnyAsync(
                        db.Query<Payment>().Where(payment => assignmentIds.Contains(payment.AssignmentId) && payment.Status != PaymentStatus.Rejected),
                        token);
                    if (hasPayments)
                    {
                        throw new BusinessRuleException(ChargesErrors.HasPaymentsCode, ChargesErrors.HasPaymentsMessage);
                    }

                    foreach (var assignment in assignments)
                    {
                        assignment.Status = AssignmentStatus.Cancelled;
                    }
                }

                locked.Status = target;
                audit.Record(new AuditEntry(
                    action,
                    fundId,
                    actorId,
                    FinancialScope.General,
                    EntityType: nameof(Charge),
                    EntityId: locked.Id,
                    Before: new { status = before.ToString() },
                    After: new { title = locked.Title, status = target.ToString() }));
                await db.SaveChangesAsync(token);
                return locked;
            },
            cancellationToken);

        return (await ToDtosAsync([charge], actorId, canManage: true, cancellationToken))[0];
    }

    private async Task<Charge> LoadAsync(Guid fundId, Guid chargeId, CancellationToken cancellationToken) =>
        await db.FirstOrDefaultAsync(
            db.Query<Charge>().Where(charge => charge.Id == chargeId && charge.FundId == fundId && charge.EventId == null),
            cancellationToken) ?? throw new NotFoundException();

    private async Task<IReadOnlyList<ChargeDto>> ToDtosAsync(
        IReadOnlyList<Charge> charges, Guid userId, bool canManage, CancellationToken cancellationToken)
    {
        if (charges.Count == 0)
        {
            return [];
        }

        var ids = charges.Select(charge => charge.Id).ToArray();
        var progress = new Dictionary<Guid, ChargeProgressDto>();
        if (canManage)
        {
            var rows = await db.ToListAsync(
                db.Query<ChargeAssignment>()
                    .Where(item => ids.Contains(item.ChargeId) && item.Status != AssignmentStatus.Cancelled)
                    .Select(item => new { item.ChargeId, item.Status, item.AmountDue }),
                cancellationToken);
            foreach (var group in rows.GroupBy(row => row.ChargeId))
            {
                progress[group.Key] = new ChargeProgressDto(
                    group.Count(),
                    group.Count(row => row.Status == AssignmentStatus.Paid),
                    group.Count(row => row.Status == AssignmentStatus.AwaitingConfirmation),
                    group.Where(row => row.Status == AssignmentStatus.Paid).Sum(row => row.AmountDue));
            }
        }

        var mine = (await db.ToListAsync(
            db.Query<ChargeAssignment>().Where(item => ids.Contains(item.ChargeId) && item.UserId == userId && item.Status != AssignmentStatus.Cancelled),
            cancellationToken)).ToDictionary(item => item.ChargeId);

        var categoryIds = charges.Where(charge => charge.CategoryId != null).Select(charge => charge.CategoryId!.Value).Distinct().ToArray();
        var categoryNames = (await db.ToListAsync(
                db.Query<Category>().Where(category => categoryIds.Contains(category.Id)).Select(category => new { category.Id, category.Name }),
                cancellationToken))
            .ToDictionary(item => item.Id, item => item.Name);

        return charges
            .Select(charge => new ChargeDto(
                charge.Id,
                charge.Title,
                charge.Purpose,
                charge.AmountPerPerson,
                charge.DueDate,
                charge.Status,
                charge.CreatedAt,
                charge.CategoryId,
                charge.CategoryId is { } categoryId ? categoryNames.GetValueOrDefault(categoryId) : null,
                progress.GetValueOrDefault(charge.Id) ?? (canManage ? new ChargeProgressDto(0, 0, 0, 0) : null),
                mine.TryGetValue(charge.Id, out var assignment) ? new MyAssignmentDto(assignment.Id, assignment.Status, assignment.AmountDue) : null))
            .ToArray();
    }

    /// <summary>Route đã yêu cầu <c>Charge.Manage</c>; kiểm lại ở service để không phụ thuộc vào controller.</summary>
    private async Task<Guid> EnsureManagerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var access = await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return access.Permissions.Contains(Permissions.ChargeManage) ? userId : throw new ForbiddenAccessException();
    }

    private Guid CurrentUserId() => currentUser.UserId ?? throw new ForbiddenAccessException();
}
