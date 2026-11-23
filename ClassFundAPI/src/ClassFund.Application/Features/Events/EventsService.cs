using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Features.AuditLogs;
using ClassFund.Application.Features.Balance;
using ClassFund.Application.Features.Ledger;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Events;

/// <summary>
/// Sự kiện – phạm vi tài chính riêng (FR-30 → FR-34, BR-16 → BR-20, OQ-18).
/// Vòng đời Open → Đóng danh sách (Completed) → Settled; Open → Cancelled khi chưa có tiền. Quyết toán (Settled) thuộc M9.
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

    Task<EventCloseListPreviewDto> PreviewCloseListAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Đóng danh sách (BR-19): người chưa đóng đủ TẤT CẢ khoản thu đang mở bị loại, số tiền đã đóng được hoàn vào số dư cá nhân
    /// (kèm bút toán chi hoàn tiền của sự kiện); nghĩa vụ của họ được gỡ. Đóng sớm hơn hạn cần <paramref name="confirmEarly"/>.
    /// Từ đó sự kiện không nhận người mới / khoản thu mới và mới được ghi chi.
    /// </summary>
    Task<EventCloseListPreviewDto> CloseListAsync(Guid fundId, Guid eventId, bool confirmEarly, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class EventsService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    IBalanceService balances,
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
                    item.ListDeadline,
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
        EnsureDeadlineNotInPast(input.ListDeadline);

        var created = await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                YearEndRule.EnsureFundActive(fund.Status);

                var now = clock.GetUtcNow();
                var fundEvent = new Event(fundId, name, description, input.EventDate, userId, now) { ListDeadline = input.ListDeadline };
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
                    After: new { fundEvent.Name, fundEvent.EventDate, fundEvent.ListDeadline, fundEvent.Description, invitedCount = members.Count }));
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

                if (fundEvent.Name == name && fundEvent.Description == description && fundEvent.EventDate == input.EventDate && fundEvent.ListDeadline == input.ListDeadline)
                {
                    return fundEvent;
                }

                if (fundEvent.ListDeadline != input.ListDeadline)
                {
                    EnsureDeadlineNotInPast(input.ListDeadline);
                }

                var before = new { fundEvent.Name, fundEvent.EventDate, fundEvent.ListDeadline, fundEvent.Description };
                fundEvent.Name = name;
                fundEvent.Description = description;
                fundEvent.EventDate = input.EventDate;
                fundEvent.ListDeadline = input.ListDeadline;
                audit.Record(new AuditEntry(
                    EventsAuditActions.Update,
                    fundId,
                    userId,
                    FinancialScope.Event,
                    EventId: fundEvent.Id,
                    EntityType: nameof(Event),
                    EntityId: fundEvent.Id,
                    Before: before,
                    After: new { fundEvent.Name, fundEvent.EventDate, fundEvent.ListDeadline, fundEvent.Description }));
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
                    if (assignments.Any(assignment => assignment.Status == AssignmentStatus.Paid))
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
                    value.HasSelfDeclared);
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
            all.Count(item => item.PaymentStatus == ParticipantPaymentStatus.Unpaid));
    }

    public async Task<EventCloseListPreviewDto> PreviewCloseListAsync(Guid fundId, Guid eventId, CancellationToken cancellationToken)
    {
        await ManagerAsync(fundId, cancellationToken);
        var fundEvent = await db.FirstOrDefaultAsync(
            db.Query<Event>().Where(item => item.Id == eventId && item.FundId == fundId), cancellationToken) ?? throw new NotFoundException();
        EventStatusRules.EnsureCanTransition(fundEvent.Status, EventStatus.Completed);

        var plan = await PlanCloseAsync(fundEvent, cancellationToken);
        return plan.Preview;
    }

    public async Task<EventCloseListPreviewDto> CloseListAsync(Guid fundId, Guid eventId, bool confirmEarly, CancellationToken cancellationToken)
    {
        var (userId, _) = await ManagerAsync(fundId, cancellationToken);

        return await db.InTransactionAsync(
            async token =>
            {
                var fund = await db.LockAsync<Fund>(fundId, token) ?? throw new NotFoundException();
                var fundEvent = await LockEventAsync(fundId, eventId, token);
                YearEndRule.EnsureFundActive(fund.Status);
                EventStatusRules.EnsureCanTransition(fundEvent.Status, EventStatus.Completed);

                var now = clock.GetUtcNow();
                var today = VietnamTime.DateOf(now);
                if (fundEvent.ListDeadline is { } deadline && today < deadline && !confirmEarly)
                {
                    throw new BusinessRuleException(EventStatusRules.ListDeadlineNotReachedCode, EventStatusRules.ListDeadlineNotReachedMessage);
                }

                var plan = await PlanCloseAsync(fundEvent, token);
                var excluded = plan.Preview.Excluded.ToDictionary(person => person.UserId);

                var chargeIds = await db.ToListAsync(db.Query<Charge>().Where(charge => charge.EventId == eventId).Select(charge => charge.Id), token);
                var dropped = excluded.Count == 0
                    ? []
                    : await db.ToListAsync(
                        db.Query<ChargeAssignment>().Where(assignment =>
                            chargeIds.Contains(assignment.ChargeId)
                            && excluded.Keys.Contains(assignment.UserId)
                            && (assignment.Status == AssignmentStatus.Unpaid || assignment.Status == AssignmentStatus.Paid)),
                        token);

                foreach (var participant in plan.Participants)
                {
                    if (participant.Response != ParticipationResponse.Accepted)
                    {
                        participant.IsFinal = false;
                        continue;
                    }

                    if (!excluded.TryGetValue(participant.UserId, out var person))
                    {
                        participant.IsFinal = true;
                        continue;
                    }

                    participant.IsFinal = false;
                    participant.ExclusionReason = person.Reason;
                    participant.ExcludedAt = now;
                    participant.RefundedAmount = person.Refund;

                    if (person.Refund > 0)
                    {
                        // Tiền đã đóng chuyển về số dư cá nhân (giữ hộ) và rời sổ sự kiện bằng một dòng chi hoàn tiền,
                        // để tiền thừa / thiếu của sự kiện không tính cả phần đã hoàn.
                        await balances.CreditAsync(fundId, person.UserId, person.Refund, BalanceTransactionType.ListCloseRefund, eventId, userId, token);
                        db.Add(new LedgerEntry(
                            fundId,
                            eventId,
                            LedgerType.Expense,
                            LedgerSource.Refund,
                            null,
                            $"Hoàn tiền người bị loại: {person.FullName}",
                            "Hoàn vào số dư cá nhân khi đóng danh sách",
                            person.Refund,
                            today,
                            userId,
                            now));
                    }

                    audit.Record(new AuditEntry(
                        EventsAuditActions.Exclude,
                        fundId,
                        userId,
                        FinancialScope.Event,
                        EventId: eventId,
                        EntityType: nameof(EventParticipant),
                        EntityId: participant.Id,
                        After: new { payerName = person.FullName, refundedAmount = person.Refund, reason = person.Reason }));
                    notifier.Send(
                        [person.UserId],
                        new NotificationMessage(
                            fundId,
                            FinancialScope.Event,
                            eventId,
                            NotificationType.EventParticipantExcluded,
                            NotificationTitle(fundEvent.Name, "Bạn không còn trong danh sách tham gia"),
                            person.Refund > 0
                                ? $"Bạn chưa đóng đủ các khoản thu khi đóng danh sách nên không còn trong sự kiện. {AuditSummary.FormatMoney(person.Refund)} bạn đã đóng được hoàn vào số dư cá nhân."
                                : "Bạn chưa đóng khoản nào khi đóng danh sách nên không còn trong sự kiện; khoản phải đóng của bạn đã được gỡ.",
                            nameof(Event),
                            eventId));
                }

                foreach (var assignment in dropped)
                {
                    assignment.Status = AssignmentStatus.Excluded;
                }

                fundEvent.Status = EventStatus.Completed;
                fundEvent.CompletedAt = now;

                audit.Record(new AuditEntry(
                    EventsAuditActions.CloseList,
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
                        total = plan.Preview.TotalRefund,
                        excluded = plan.Preview.Excluded.Select(person => person.FullName).ToArray(),
                    },
                    Note: plan.Preview.BeforeDeadline ? "Đóng danh sách sớm hơn hạn." : null));
                await db.SaveChangesAsync(token);
                return plan.Preview;
            },
            cancellationToken);
    }

    // ---------- Nội bộ ----------

    private sealed record ClosePlan(IReadOnlyList<EventParticipant> Participants, EventCloseListPreviewDto Preview);

    /// <summary>
    /// Ai bị loại khi đóng danh sách (dùng chung cho xem trước và đóng thật): người đã nhận tham gia mà chưa đóng đủ TẤT CẢ khoản thu
    /// đang mở. Sự kiện chưa có khoản thu đang mở thì không ai bị loại. Số tiền hoàn = tổng đã đóng cho mọi khoản thu của sự kiện.
    /// </summary>
    private async Task<ClosePlan> PlanCloseAsync(Event fundEvent, CancellationToken cancellationToken)
    {
        var eventId = fundEvent.Id;
        var participants = await db.ToListAsync(db.Query<EventParticipant>().Where(participant => participant.EventId == eventId), cancellationToken);
        var acceptedIds = participants.Where(participant => participant.Response == ParticipationResponse.Accepted).Select(participant => participant.UserId).ToArray();
        var names = (await db.ToListAsync(
                db.Query<User>().Where(user => acceptedIds.Contains(user.Id)).Select(user => new { user.Id, user.FullName }),
                cancellationToken))
            .ToDictionary(user => user.Id, user => user.FullName);

        var charges = await db.ToListAsync(
            db.Query<Charge>().Where(charge => charge.EventId == eventId && charge.Status != ChargeStatus.Cancelled),
            cancellationToken);
        var openChargeIds = charges.Where(charge => charge.Status == ChargeStatus.Open).Select(charge => charge.Id).ToArray();
        var chargeIds = charges.Select(charge => charge.Id).ToArray();

        var assignments = await db.ToListAsync(
            db.Query<ChargeAssignment>().Where(assignment => chargeIds.Contains(assignment.ChargeId)),
            cancellationToken);
        var paidByUser = (await db.ToListAsync(
                from payment in db.Query<Payment>()
                where payment.Status == PaymentStatus.Confirmed
                join assignment in db.Query<ChargeAssignment>() on payment.AssignmentId equals assignment.Id
                where chargeIds.Contains(assignment.ChargeId)
                select new { payment.PayerUserId, payment.Amount },
                cancellationToken))
            .GroupBy(row => row.PayerUserId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));

        var excluded = new List<ExcludedPersonDto>();
        foreach (var userId in acceptedIds)
        {
            var complete = openChargeIds.All(chargeId =>
                assignments.Any(item => item.ChargeId == chargeId && item.UserId == userId && item.Status == AssignmentStatus.Paid));
            if (openChargeIds.Length == 0 || complete)
            {
                continue;
            }

            var paid = paidByUser.GetValueOrDefault(userId);
            excluded.Add(new ExcludedPersonDto(
                userId,
                names.GetValueOrDefault(userId, string.Empty),
                paid,
                paid,
                paid > 0 ? EventsErrors.ExclusionReasonUnderpaid : EventsErrors.ExclusionReasonNothingPaid));
        }

        var today = VietnamTime.DateOf(clock.GetUtcNow());
        var preview = new EventCloseListPreviewDto(
            excluded.OrderBy(person => person.FullName).ToArray(),
            acceptedIds.Length - excluded.Count,
            openChargeIds.Length,
            excluded.Sum(person => person.Refund),
            fundEvent.ListDeadline,
            fundEvent.ListDeadline is { } deadline && today < deadline);
        return new ClosePlan(participants, preview);
    }

    private void EnsureDeadlineNotInPast(DateOnly deadline)
    {
        if (deadline < VietnamTime.DateOf(clock.GetUtcNow()))
        {
            throw new BusinessRuleException(EventsErrors.ListDeadlineInPast, EventsErrors.ListDeadlineInPastMessage);
        }
    }

    private async Task<EventDetailDto> DetailAsync(Event fundEvent, Guid userId, FundAccess access, CancellationToken cancellationToken)
    {
        var participants = await db.ToListAsync(
            db.Query<EventParticipant>().Where(participant => participant.EventId == fundEvent.Id), cancellationToken);
        var totals = await LedgerBalances.ForScopeAsync(db, fundEvent.FundId, fundEvent.Id, cancellationToken);
        var me = participants.FirstOrDefault(participant => participant.UserId == userId);

        var charges = await db.ToListAsync(
            db.Query<Charge>().Where(charge => charge.EventId == fundEvent.Id && charge.Status != ChargeStatus.Cancelled).OrderBy(charge => charge.CreatedAt),
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
                        mine.Count(item => item.Status == AssignmentStatus.Unpaid));
                })
                .ToArray()
            : [];

        IReadOnlyList<EventExclusionDto> exclusions = CanManage(access)
            ? await ExclusionsAsync(participants, cancellationToken)
            : [];

        return new EventDetailDto(
            fundEvent.Id,
            fundEvent.Name,
            fundEvent.Description,
            fundEvent.EventDate,
            fundEvent.ListDeadline,
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
            chargeSummaries,
            exclusions);
    }

    /// <summary>Lịch sử người bị loại khi đóng danh sách (chỉ thủ quỹ xem): ai, lý do, số tiền hoàn, thời điểm.</summary>
    private async Task<IReadOnlyList<EventExclusionDto>> ExclusionsAsync(IReadOnlyList<EventParticipant> participants, CancellationToken cancellationToken)
    {
        var excluded = participants.Where(participant => participant.ExcludedAt is not null).ToArray();
        if (excluded.Length == 0)
        {
            return [];
        }

        var ids = excluded.Select(participant => participant.UserId).ToArray();
        var names = (await db.ToListAsync(db.Query<User>().Where(user => ids.Contains(user.Id)).Select(user => new { user.Id, user.FullName }), cancellationToken))
            .ToDictionary(user => user.Id, user => user.FullName);
        return excluded
            .OrderBy(participant => participant.ExcludedAt)
            .ThenBy(participant => names.GetValueOrDefault(participant.UserId, string.Empty))
            .Select(participant => new EventExclusionDto(
                participant.UserId,
                names.GetValueOrDefault(participant.UserId, string.Empty),
                participant.ExclusionReason,
                participant.RefundedAmount ?? 0,
                participant.ExcludedAt))
            .ToArray();
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
