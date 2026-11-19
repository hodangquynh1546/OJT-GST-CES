using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.AuditLogs;
using ClassFund.Application.Features.Ledger;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Events;

/// <summary>
/// Sự kiện – phạm vi tài chính riêng (FR-30 → FR-34, BR-16 → BR-20, OQ-18).
/// Vòng đời Open → Completed → Settled; Open → Cancelled khi chưa có tiền. Quyết toán (Settled) thuộc M9.
/// </summary>
public interface IEventsService
{
    /// <summary>Thủ quỹ: mọi sự kiện của quỹ; học sinh: sự kiện mình được mời.</summary>
    Task<IReadOnlyList<EventSummaryDto>> ListAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Học sinh không được mời → 404.</summary>
    Task<EventDetailDto> GetAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>Tạo và mời mọi thành viên đang hoạt động (BR-17), thông báo <c>EventCreated</c>.</summary>
    Task<EventDetailDto> CreateAsync(Guid fundId, CreateEventInput input, CancellationToken cancellationToken);

    Task<EventDetailDto> UpdateAsync(Guid fundId, Guid eventId, UpdateEventInput input, CancellationToken cancellationToken);

    /// <summary>Chỉ khi Open và chưa có thu / chi (BR-16); khoản thu và nghĩa vụ chưa đóng của sự kiện bị hủy theo.</summary>
    Task<EventDetailDto> CancelAsync(Guid fundId, Guid eventId, string reason, CancellationToken cancellationToken);

    /// <summary>Trả lời tham gia / không (FR-31) – chỉ khi Open; thủ quỹ tự trả lời không cần ai duyệt.</summary>
    Task<EventDetailDto> RespondAsync(Guid fundId, Guid eventId, ParticipationResponse response, CancellationToken cancellationToken);

    /// <summary>Chỉ người đã nhận tham gia (sau khi kết thúc: người còn trong danh sách), lọc theo tình trạng đóng tiền (FR-32).</summary>
    Task<EventParticipantsResult> ListParticipantsAsync(Guid fundId, Guid eventId, ParticipantPaymentStatus? status, CancellationToken cancellationToken);

    Task<EventCompletionPreviewDto> PreviewCompletionAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>Kết thúc (BR-19): loại người chưa đóng khoản nào, nhắc người chưa đóng đủ.</summary>
    Task<EventCompletionPreviewDto> CompleteAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class EventsService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    AuditTrail audit,
    Notifier notifier,
    TimeProvider clock) : IEventsService
{
    public async Task<IReadOnlyList<EventSummaryDto>> ListAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var (userId, access) = await ViewerAsync(fundId, cancellationToken);
        var events = db.Query<Event>().Where(item => item.FundId == fundId);
        if (!CanManage(access))
        {
            var invited = db.Query<EventParticipant>().Where(participant => participant.UserId == userId).Select(participant => participant.EventId);
            events = events.Where(item => invited.Contains(item.Id));
        }

        var rows = await db.ToListAsync(events, cancellationToken);
        var ids = rows.Select(item => item.Id).ToArray();
        var participants = await db.ToListAsync(db.Query<EventParticipant>().Where(participant => ids.Contains(participant.EventId)), cancellationToken);
        var money = await EventMoney.LoadAsync(db, ids, cancellationToken);
        var totals = await LedgerBalances.ForEventsAsync(db, fundId, ids, cancellationToken);

        return rows
            .OrderBy(item => item.Status is EventStatus.Open or EventStatus.Completed ? 0 : 1)
            .ThenByDescending(item => item.EventDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item =>
            {
                var mine = participants.Where(participant => participant.EventId == item.Id).ToArray();
                var counted = mine.Where(participant => IsCounted(item, participant)).ToArray();
                var total = totals.GetValueOrDefault(item.Id, ScopeTotals.Zero);
                return new EventSummaryDto(
                    item.Id,
                    item.Name,
                    item.Description,
                    item.EventDate,
                    item.Status,
                    mine.Length,
                    counted.Length,
                    counted.Count(participant => EventMoney.For(money, item.Id, participant.UserId).Status == ParticipantPaymentStatus.Paid),
                    total.TotalIncome,
                    total.TotalExpense,
                    total.Balance,
                    mine.FirstOrDefault(participant => participant.UserId == userId)?.Response,
                    item.CreatedAt,
                    item.CompletedAt);
            })
            .ToArray();
    }

    public async Task<EventDetailDto> GetAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken)
    {
        var (userId, access) = await ViewerAsync(fundId, cancellationToken);
        var fundEvent = await FindVisibleAsync(fundId, eventId, userId, access, cancellationToken);
        return await DetailAsync(fundEvent, userId, access, cancellationToken);
    }

    public async Task<EventDetailDto> CreateAsync(Guid fundId, CreateEventInput input, CancellationToken cancellationToken)
    {
        var (userId, access) = await ManagerAsync(fundId, cancellationToken);
        var name = Clean(input.Name)!;
        var description = Clean(input.Description);

        var created = await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);

                var now = clock.GetUtcNow();
                var fundEvent = new Event(fundId, name, description, input.EventDate, userId, now);
                db.Add(fundEvent);

                var members = await db.ToListAsync(
                    db.Query<FundMember>()
                        .Where(member => member.FundId == fundId && member.Status == MemberStatus.Active)
                        .Select(member => member.UserId),
                    token);
                foreach (var memberId in members)
                {
                    db.Add(new EventParticipant(fundEvent.Id, memberId));
                }

                notifier.Send(
                    members.Where(memberId => memberId != userId),
                    InvitationMessage(fundId, fundEvent));
                audit.Record(new AuditEntry(
                    EventsAuditActions.Create,
                    fundId,
                    userId,
                    FinancialScope.Event,
                    EventId: fundEvent.Id,
                    EntityType: nameof(Event),
                    EntityId: fundEvent.Id,
                    After: new { fundEvent.Name, fundEvent.EventDate, fundEvent.Description, invitedCount = members.Count }));
                await db.SaveChangesAsync(token);
                return fundEvent;
            },
            cancellationToken);

        return await DetailAsync(created, userId, access, cancellationToken);
    }

    public async Task<EventDetailDto> UpdateAsync(Guid fundId, Guid eventId, UpdateEventInput input, CancellationToken cancellationToken)
    {
        var (userId, access) = await ManagerAsync(fundId, cancellationToken);
        var name = Clean(input.Name)!;
        var description = Clean(input.Description);

        var updated = await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                var fundEvent = await LockEventAsync(fundId, eventId, token);
                YearEndRule.EnsureFundActive(fund.Status);
                if (fundEvent.Status is EventStatus.Settled or EventStatus.Cancelled)
                {
                    throw new BusinessRuleException(EventsErrors.NotEditable, EventsErrors.NotEditableMessage);
                }

                if (fundEvent.Name == name && fundEvent.Description == description && fundEvent.EventDate == input.EventDate)
                {
                    return fundEvent;
                }

                var before = new { fundEvent.Name, fundEvent.EventDate, fundEvent.Description };
                fundEvent.Name = name;
                fundEvent.Description = description;
                fundEvent.EventDate = input.EventDate;
                audit.Record(new AuditEntry(
                    EventsAuditActions.Update,
                    fundId,
                    userId,
                    FinancialScope.Event,
                    EventId: fundEvent.Id,
                    EntityType: nameof(Event),
                    EntityId: fundEvent.Id,
                    Before: before,
                    After: new { fundEvent.Name, fundEvent.EventDate, fundEvent.Description }));
                await db.SaveChangesAsync(token);
                return fundEvent;
            },
            cancellationToken);

        return await DetailAsync(updated, userId, access, cancellationToken);
    }

    public async Task<EventDetailDto> CancelAsync(Guid fundId, Guid eventId, string reason, CancellationToken cancellationToken)
    {
        var (userId, access) = await ManagerAsync(fundId, cancellationToken);
        var note = Clean(reason);

        var cancelled = await db.InTransactionAsync(
            async token =>
            {
                var fundEvent = await LockEventAsync(fundId, eventId, token);
                EventStatusRules.EnsureCanTransition(fundEvent.Status, EventStatus.Cancelled);

                var hasLedger = await db.AnyAsync(db.Query<LedgerEntry>().Where(entry => entry.EventId == eventId), token);
                var hasPayments = await db.AnyAsync(
                    from payment in db.Query<Payment>()
                    join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
                    join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
                    where charge.EventId == eventId && payment.Status != PaymentStatus.Rejected
                    select payment.Id,
                    token);
                if (hasLedger || hasPayments)
                {
                    throw new BusinessRuleException(EventsErrors.HasMoney, EventsErrors.HasMoneyMessage);
                }

                var charges = await db.ToListAsync(
                    db.Query<Charge>().Where(charge => charge.EventId == eventId && charge.Status != ChargeStatus.Cancelled), token);
                var chargeIds = charges.Select(charge => charge.Id).ToArray();
                var assignments = await db.ToListAsync(
                    db.Query<ChargeAssignment>().Where(assignment =>
                        chargeIds.Contains(assignment.ChargeId) && assignment.Status == AssignmentStatus.Unpaid),
                    token);
                foreach (var charge in charges)
                {
                    charge.Status = ChargeStatus.Cancelled;
                }

                foreach (var assignment in assignments)
                {
                    assignment.Status = AssignmentStatus.Cancelled;
                }

                fundEvent.Status = EventStatus.Cancelled;
                audit.Record(new AuditEntry(
                    EventsAuditActions.Cancel,
                    fundId,
                    userId,
                    FinancialScope.Event,
                    EventId: fundEvent.Id,
                    EntityType: nameof(Event),
                    EntityId: fundEvent.Id,
                    Before: new { status = nameof(EventStatus.Open) },
                    After: new { status = nameof(EventStatus.Cancelled), cancelledCharges = charges.Count },
                    Note: note));
                await db.SaveChangesAsync(token);
                return fundEvent;
            },
            cancellationToken);

        return await DetailAsync(cancelled, userId, access, cancellationToken);
    }

    public async Task<EventDetailDto> RespondAsync(Guid fundId, Guid eventId, ParticipationResponse response, CancellationToken cancellationToken)
    {
        var (userId, access) = await ViewerAsync(fundId, cancellationToken);
        if (!access.Permissions.Contains(Permissions.EventParticipate))
        {
            throw new ForbiddenAccessException();
        }

        if (response is not (ParticipationResponse.Accepted or ParticipationResponse.Declined))
        {
            throw new BusinessRuleException(EventsErrors.InvalidResponse, EventsErrors.InvalidResponseMessage);
        }

        var result = await db.InTransactionAsync(
            async token =>
            {
                var fundEvent = await LockEventAsync(fundId, eventId, token);
                var participant = await db.FirstOrDefaultAsync(
                    db.Query<EventParticipant>().Where(item => item.EventId == eventId && item.UserId == userId), token)
                    ?? throw new NotFoundException();
                participant = await db.LockAsync<EventParticipant>(participant.Id, token) ?? throw new NotFoundException();

                if (fundEvent.Status != EventStatus.Open)
                {
                    throw new BusinessRuleException(EventsErrors.NotOpen, EventsErrors.NotOpenMessage);
                }

                if (participant.Response == response)
                {
                    return fundEvent;
                }

                var charges = await db.ToListAsync(
                    db.Query<Charge>().Where(charge => charge.EventId == eventId && charge.Status == ChargeStatus.Open), token);
                var chargeIds = charges.Select(charge => charge.Id).ToArray();
                var assignments = await db.ToListAsync(
                    db.Query<ChargeAssignment>().Where(assignment => assignment.UserId == userId && chargeIds.Contains(assignment.ChargeId)),
                    token);

                if (response == ParticipationResponse.Declined)
                {
                    var assignmentIds = assignments.Select(assignment => assignment.Id).ToArray();
                    var hasPaid = assignments.Any(assignment => assignment.Status is AssignmentStatus.Paid or AssignmentStatus.AwaitingConfirmation)
                        || await db.AnyAsync(
                            db.Query<Payment>().Where(payment => assignmentIds.Contains(payment.AssignmentId) && payment.Status != PaymentStatus.Rejected),
                            token);
                    if (hasPaid)
                    {
                        throw new BusinessRuleException(EventsErrors.ResponseLocked, EventsErrors.ResponseLockedMessage);
                    }

                    // Không tham gia thì không còn nghĩa vụ với khoản thu của sự kiện (BR-18).
                    foreach (var assignment in assignments.Where(assignment => assignment.Status == AssignmentStatus.Unpaid))
                    {
                        assignment.Status = AssignmentStatus.Cancelled;
                    }
                }
                else
                {
                    // Nhận tham gia khi sự kiện đã có khoản thu đang mở: áp các khoản đó cho người này (BR-18).
                    foreach (var charge in charges)
                    {
                        var existing = assignments.FirstOrDefault(assignment => assignment.ChargeId == charge.Id);
                        if (existing is null)
                        {
                            db.Add(new ChargeAssignment(charge.Id, userId, charge.AmountPerPerson));
                        }
                        else if (existing.Status == AssignmentStatus.Cancelled)
                        {
                            existing.Status = AssignmentStatus.Unpaid;
                            existing.AmountDue = charge.AmountPerPerson;
                        }
                    }
                }

                var before = participant.Response;
                participant.Response = response;
                participant.RespondedAt = clock.GetUtcNow();
                audit.Record(new AuditEntry(
                    EventsAuditActions.Respond,
                    fundId,
                    userId,
                    FinancialScope.Event,
                    EventId: eventId,
                    EntityType: nameof(EventParticipant),
                    EntityId: participant.Id,
                    Before: new { response = before.ToString() },
                    After: new { response = response.ToString() }));
                await db.SaveChangesAsync(token);
                return fundEvent;
            },
            cancellationToken);

        return await DetailAsync(result, userId, access, cancellationToken);
    }

    public async Task<EventParticipantsResult> ListParticipantsAsync(
        Guid fundId,
        Guid eventId,
        ParticipantPaymentStatus? status,
        CancellationToken cancellationToken)
    {
        var (userId, access) = await ViewerAsync(fundId, cancellationToken);
        var fundEvent = await FindVisibleAsync(fundId, eventId, userId, access, cancellationToken);

        var rows = await db.ToListAsync(
            from participant in db.Query<EventParticipant>()
            where participant.EventId == eventId && participant.Response == ParticipationResponse.Accepted
            join user in db.Query<User>() on participant.UserId equals user.Id
            join member in db.Query<FundMember>().Where(member => member.FundId == fundId) on participant.UserId equals member.UserId into members
            from member in members.DefaultIfEmpty()
            select new { participant, user.FullName, Role = member == null ? (MemberRole?)null : member.Role },
            cancellationToken);
        rows = rows.Where(row => IsCounted(fundEvent, row.participant)).ToList();

        var manager = CanManage(access);
        var money = manager
            ? await EventMoney.LoadAsync(db, [eventId], cancellationToken)
            : new Dictionary<(Guid, Guid), ParticipantMoney>();

        var all = rows
            .Select(row =>
            {
                var value = EventMoney.For(money, eventId, row.participant.UserId);
                return new EventParticipantDto(
                    row.participant.UserId,
                    row.FullName,
                    row.Role,
                    value.Paid,
                    value.Due,
                    value.Status,
                    value.HasSelfDeclared,
                    value.PendingCount);
            })
            .OrderBy(item => item.Role == MemberRole.Owner ? 0 : item.Role == MemberRole.Treasurer ? 1 : 2)
            .ThenBy(item => item.FullName)
            .ThenBy(item => item.UserId)
            .ToArray();

        var filtered = status is { } wanted ? all.Where(item => item.PaymentStatus == wanted).ToArray() : all;
        return new EventParticipantsResult(
            filtered,
            all.Length,
            all.Count(item => item.PaymentStatus == ParticipantPaymentStatus.Paid),
            all.Count(item => item.PaymentStatus == ParticipantPaymentStatus.Pending),
            all.Count(item => item.PaymentStatus == ParticipantPaymentStatus.Unpaid));
    }

    public async Task<EventCompletionPreviewDto> PreviewCompletionAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken)
    {
        await ManagerAsync(fundId, cancellationToken);
        var fundEvent = await db.FirstOrDefaultAsync(
            db.Query<Event>().Where(item => item.Id == eventId && item.FundId == fundId), cancellationToken) ?? throw new NotFoundException();
        EventStatusRules.EnsureCanTransition(fundEvent.Status, EventStatus.Completed);

        var plan = await PlanCompletionAsync(eventId, cancellationToken);
        return plan.Preview;
    }

    public async Task<EventCompletionPreviewDto> CompleteAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken)
    {
        var (userId, _) = await ManagerAsync(fundId, cancellationToken);

        return await db.InTransactionAsync(
            async token =>
            {
                var fundEvent = await LockEventAsync(fundId, eventId, token);
                EventStatusRules.EnsureCanTransition(fundEvent.Status, EventStatus.Completed);

                var plan = await PlanCompletionAsync(eventId, token);
                var excludedIds = plan.Preview.Excluded.Select(person => person.UserId).ToHashSet();

                foreach (var participant in plan.Participants)
                {
                    var stays = participant.Response == ParticipationResponse.Accepted && !excludedIds.Contains(participant.UserId);
                    participant.IsFinal = stays;
                    participant.ExclusionReason = excludedIds.Contains(participant.UserId) ? EventsErrors.ExclusionReason : null;
                }

                if (excludedIds.Count > 0)
                {
                    var chargeIds = db.Query<Charge>().Where(charge => charge.EventId == eventId).Select(charge => charge.Id);
                    var dropped = await db.ToListAsync(
                        db.Query<ChargeAssignment>().Where(assignment =>
                            chargeIds.Contains(assignment.ChargeId)
                            && excludedIds.Contains(assignment.UserId)
                            && assignment.Status == AssignmentStatus.Unpaid),
                        token);
                    foreach (var assignment in dropped)
                    {
                        assignment.Status = AssignmentStatus.Excluded;
                    }
                }

                fundEvent.Status = EventStatus.Completed;
                fundEvent.CompletedAt = clock.GetUtcNow();

                
                notifier.Send(
                    excludedIds,
                    new NotificationMessage(
                        fundId,
                        FinancialScope.Event,
                        eventId,
                        NotificationType.EventParticipantExcluded,
                        NotificationTitle(fundEvent.Name, "Bạn không còn trong danh sách tham gia"),
                        "Sự kiện đã kết thúc khi bạn chưa đóng khoản nào nên bạn không còn trong danh sách tham gia; khoản phải đóng của bạn đã được hủy.",
                        nameof(Event),
                        eventId));
                foreach (var person in plan.Preview.Underpaid)
                {
                    notifier.Send(
                        [person.UserId],
                        new NotificationMessage(
                            fundId,
                            FinancialScope.Event,
                            eventId,
                            NotificationType.EventUnpaidReminder,
                            NotificationTitle(fundEvent.Name, "Nhắc đóng phần còn lại"),
                            $"Sự kiện đã kết thúc, bạn còn thiếu {AuditSummary.FormatMoney(person.Missing)}. Bạn vẫn nộp được phần còn lại.",
                            nameof(Event),
                            eventId));
                }

                audit.Record(new AuditEntry(
                    EventsAuditActions.Complete,
                    fundId,
                    userId,
                    FinancialScope.Event,
                    EventId: eventId,
                    EntityType: nameof(Event),
                    EntityId: eventId,
                    Before: new { status = nameof(EventStatus.Open) },
                    After: new
                    {
                        status = nameof(EventStatus.Completed),
                        participantCount = plan.Preview.RemainingCount,
                        excludedCount = plan.Preview.Excluded.Count,
                        remindedCount = plan.Preview.Underpaid.Count,
                        excluded = plan.Preview.Excluded.Select(person => person.FullName).ToArray(),
                    }));
                await db.SaveChangesAsync(token);
                return plan.Preview;
            },
            cancellationToken);
    }

    // ---------- Nội bộ ----------

    private sealed record CompletionPlan(IReadOnlyList<EventParticipant> Participants, EventCompletionPreviewDto Preview);

    /// <summary>Tính ai bị loại / được nhắc theo BR-19 (dùng chung cho xem trước và kết thúc thật).</summary>
    private async Task<CompletionPlan> PlanCompletionAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var participants = await db.ToListAsync(db.Query<EventParticipant>().Where(participant => participant.EventId == eventId), cancellationToken);
        var acceptedIds = participants.Where(participant => participant.Response == ParticipationResponse.Accepted).Select(participant => participant.UserId).ToArray();
        var names = (await db.ToListAsync(
                db.Query<User>().Where(user => acceptedIds.Contains(user.Id)).Select(user => new { user.Id, user.FullName }),
                cancellationToken))
            .ToDictionary(user => user.Id, user => user.FullName);
        var money = await EventMoney.LoadAsync(db, [eventId], cancellationToken);

        var excluded = new List<PersonDto>();
        var underpaid = new List<UnderpaidPersonDto>();
        var pending = 0;
        foreach (var userId in acceptedIds)
        {
            var value = EventMoney.For(money, eventId, userId);
            var name = names.GetValueOrDefault(userId, string.Empty);
            pending += value.PendingCount;
            if (value.ExcludedOnCompletion)
            {
                excluded.Add(new PersonDto(userId, name));
            }
            else if (value.UnpaidAmount > 0)
            {
                underpaid.Add(new UnderpaidPersonDto(userId, name, value.UnpaidAmount));
            }
        }

        var preview = new EventCompletionPreviewDto(
            excluded.OrderBy(person => person.FullName).ToArray(),
            underpaid.OrderBy(person => person.FullName).ToArray(),
            pending,
            acceptedIds.Length - excluded.Count);
        return new CompletionPlan(participants, preview);
    }

    private async Task<EventDetailDto> DetailAsync(Event fundEvent, Guid userId, FundAccess access, CancellationToken cancellationToken)
    {
        var participants = await db.ToListAsync(
            db.Query<EventParticipant>().Where(participant => participant.EventId == fundEvent.Id), cancellationToken);
        var totals = await LedgerBalances.ForScopeAsync(db, fundEvent.FundId, fundEvent.Id, cancellationToken);
        var me = participants.FirstOrDefault(participant => participant.UserId == userId);

        var charges = await db.ToListAsync(
            db.Query<Charge>().Where(charge => charge.EventId == fundEvent.Id && charge.Status != ChargeStatus.Cancelled).OrderBy(charge => charge.DueDate).ThenBy(charge => charge.CreatedAt),
            cancellationToken);
        var chargeIds = charges.Select(charge => charge.Id).ToArray();
        var assignments = await db.ToListAsync(
            db.Query<ChargeAssignment>().Where(assignment => chargeIds.Contains(assignment.ChargeId)),
            cancellationToken);

        var myCharges = charges
            .Select(charge => (charge, assignment: assignments.FirstOrDefault(item => item.ChargeId == charge.Id && item.UserId == userId)))
            .Where(pair => pair.assignment is not null && pair.assignment.Status != AssignmentStatus.Cancelled)
            .Select(pair => new MyEventChargeDto(pair.charge.Id, pair.charge.Title, pair.assignment!.AmountDue, pair.charge.DueDate, pair.assignment.Status))
            .ToArray();

        IReadOnlyList<EventChargeDto> chargeSummaries = CanManage(access)
            ? charges.Select(charge =>
                {
                    var mine = assignments.Where(item => item.ChargeId == charge.Id).ToArray();
                    return new EventChargeDto(
                        charge.Id,
                        charge.Title,
                        charge.Purpose,
                        charge.AmountPerPerson,
                        charge.DueDate,
                        charge.Status,
                        mine.Count(item => item.Status == AssignmentStatus.Paid),
                        mine.Count(item => item.Status == AssignmentStatus.AwaitingConfirmation),
                        mine.Count(item => item.Status == AssignmentStatus.Unpaid));
                })
                .ToArray()
            : [];

        return new EventDetailDto(
            fundEvent.Id,
            fundEvent.Name,
            fundEvent.Description,
            fundEvent.EventDate,
            fundEvent.Status,
            fundEvent.CreatedAt,
            fundEvent.CompletedAt,
            participants.Count,
            participants.Count(participant => participant.Response == ParticipationResponse.Accepted),
            participants.Count(participant => participant.Response == ParticipationResponse.Declined),
            participants.Count(participant => participant.Response == ParticipationResponse.Pending),
            participants.Count(participant => participant.ExclusionReason is not null),
            participants.Count(participant => IsCounted(fundEvent, participant)),
            totals.TotalIncome,
            totals.TotalExpense,
            totals.Balance,
            charges.Where(charge => charge.Status == ChargeStatus.Open).Sum(charge => charge.AmountPerPerson),
            me is null ? null : new MyParticipationDto(me.Response, me.IsFinal, me.ExclusionReason),
            myCharges,
            chargeSummaries);
    }

    /// <summary>Người được tính là người tham gia: đã nhận tham gia; sau khi kết thúc chỉ người còn trong danh sách (IsFinal).</summary>
    private static bool IsCounted(Event fundEvent, EventParticipant participant) =>
        participant.Response == ParticipationResponse.Accepted
        && (fundEvent.Status is EventStatus.Open or EventStatus.Cancelled || participant.IsFinal);

    private async Task<Event> FindVisibleAsync(Guid fundId, Guid eventId, Guid userId, FundAccess access, CancellationToken cancellationToken)
    {
        var fundEvent = await db.FirstOrDefaultAsync(
            db.Query<Event>().Where(item => item.Id == eventId && item.FundId == fundId), cancellationToken) ?? throw new NotFoundException();
        if (CanManage(access))
        {
            return fundEvent;
        }

        var invited = await db.AnyAsync(
            db.Query<EventParticipant>().Where(participant => participant.EventId == eventId && participant.UserId == userId), cancellationToken);
        return invited ? fundEvent : throw new NotFoundException();
    }

    private async Task<Event> LockEventAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken)
    {
        var fundEvent = await db.LockAsync<Event>(eventId, cancellationToken);
        return fundEvent is null || fundEvent.FundId != fundId ? throw new NotFoundException() : fundEvent;
    }

    internal static NotificationMessage InvitationMessage(Guid fundId, Event fundEvent) =>
        new(
            fundId,
            FinancialScope.Event,
            fundEvent.Id,
            NotificationType.EventCreated,
            NotificationTitle(fundEvent.Name, "Lời mời tham gia"),
            fundEvent.EventDate is { } date
                ? $"Sự kiện diễn ra ngày {date:dd/MM/yyyy}. Hãy xác nhận tham gia hoặc không tham gia."
                : "Hãy xác nhận tham gia hoặc không tham gia.",
            nameof(Event),
            fundEvent.Id);

    /// <summary>Tiêu đề có tiền tố phạm vi "[Sự kiện: tên]"; tên dài thì rút gọn để vừa cột Title (200 ký tự).</summary>
    public static string NotificationTitle(string eventName, string text)
    {
        const int maxLength = 200;
        var title = $"{Notifier.ScopePrefix(FinancialScope.Event, eventName)} {text}";
        if (title.Length <= maxLength)
        {
            return title;
        }

        var keep = Math.Max(1, eventName.Length - (title.Length - maxLength) - 1);
        return $"{Notifier.ScopePrefix(FinancialScope.Event, eventName[..keep] + "…")} {text}";
    }

    private async Task<(Guid UserId, FundAccess Access)> ViewerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException();
        return (userId, await accessService.GetFundAccessAsync(userId, fundId, cancellationToken));
    }

    /// <summary>Controller đã yêu cầu <c>Event.Manage</c>; kiểm lại để service không dựa vào nơi gọi.</summary>
    private async Task<(Guid UserId, FundAccess Access)> ManagerAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(fundId, cancellationToken);
        return CanManage(viewer.Access) ? viewer : throw new ForbiddenAccessException();
    }

    private static bool CanManage(FundAccess access) => access.Permissions.Contains(Permissions.EventManage);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
