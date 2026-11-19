using ClassFund.Application.Common.Auditing;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Events;

/// <summary>
/// Cho M3 (TD-10, BR-17): thành viên thêm / kích hoạt lại sau khi sự kiện đã tạo được mời vào mọi sự kiện đang mở.
/// </summary>
public interface IEventInvitationService
{
    /// <summary>
    /// Thêm EventParticipant <c>Pending</c> cho các sự kiện <c>Open</c> của quỹ mà người này chưa được mời, gửi thông báo mời
    /// và ghi audit. <b>Chỉ gọi trong <c>IAppDb.InTransactionAsync</c> của module gọi</b>; không tự SaveChanges.
    /// </summary>
    /// <returns>Số sự kiện đã mời.</returns>
    Task<int> InviteToOpenEventsAsync(Guid fundId, Guid userId, Guid? actorUserId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class EventInvitationService(IAppDb db, AuditTrail audit, Notifier notifier) : IEventInvitationService
{
    public async Task<int> InviteToOpenEventsAsync(Guid fundId, Guid userId, Guid? actorUserId, CancellationToken cancellationToken)
    {
        var invited = db.Query<EventParticipant>().Where(participant => participant.UserId == userId).Select(participant => participant.EventId);
        var openEvents = await db.ToListAsync(
            db.Query<Event>().Where(item => item.FundId == fundId && item.Status == EventStatus.Open && !invited.Contains(item.Id)),
            cancellationToken);

        foreach (var fundEvent in openEvents)
        {
            var participant = new EventParticipant(fundEvent.Id, userId);
            db.Add(participant);
            notifier.Send([userId], EventsService.InvitationMessage(fundId, fundEvent));
            audit.Record(new AuditEntry(
                EventsAuditActions.Invite,
                fundId,
                actorUserId,
                FinancialScope.Event,
                EventId: fundEvent.Id,
                EntityType: nameof(EventParticipant),
                EntityId: participant.Id,
                After: new { userId, eventName = fundEvent.Name }));
        }

        return openEvents.Count;
    }
}
