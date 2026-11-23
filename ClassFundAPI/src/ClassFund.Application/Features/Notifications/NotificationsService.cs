using ClassFund.Application.Common.Exceptions;
using ClassFund.Application.Common.Notifications;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Domain.Entities;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Notifications;

/// <summary>
/// M11 – Thông báo của tôi (FR-43, FR-44). Chỉ thấy thông báo của chính mình trong quỹ đang mở.
/// Nhắc tự động tạo khi mở danh sách / đếm chưa đọc (chưa cần job nền; thông báo trong ứng dụng chỉ có nghĩa khi người đó mở ứng dụng):
/// khoản thu chung còn hạn (dữ liệu cũ) trong 2 ngày tới, và người tham gia sự kiện chưa đóng đủ trong N ngày trước hạn đóng
/// danh sách (<see cref="ReminderOptions.ListDeadlineDays"/>, mặc định 3) – một lần mỗi ngày, mỗi lần ghi vào lịch sử nhắc.
/// </summary>
public interface INotificationsService
{
    Task<NotificationListResult> ListAsync(Guid fundId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> UnreadCountAsync(Guid fundId, CancellationToken cancellationToken);

    /// <summary>Đánh dấu đã đọc; thông báo của người khác hoặc không tồn tại → 404.</summary>
    Task MarkReadAsync(Guid fundId, Guid notificationId, CancellationToken cancellationToken);

    /// <returns>Số thông báo vừa được đánh dấu.</returns>
    Task<int> MarkAllReadAsync(Guid fundId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class NotificationsService(
    IAppDb db,
    ICurrentUser currentUser,
    IAccessService accessService,
    Notifier notifier,
    ReminderOptions reminders,
    TimeProvider clock) : INotificationsService
{
    /// <summary>Số ngày trước hạn bắt đầu nhắc (OQ-12).</summary>
    public const int DueSoonDays = 2;

    public const string DueSoonReferenceType = nameof(ChargeAssignment);

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<NotificationListResult> ListAsync(Guid fundId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var userId = await RequireMemberAsync(fundId, cancellationToken);
        await GenerateRemindersAsync(fundId, userId, cancellationToken);

        var mine = db.Query<Notification>().Where(item => item.FundId == fundId && item.UserId == userId);
        var query = unreadOnly ? mine.Where(item => !item.IsRead) : mine;

        var total = await db.CountAsync(query, cancellationToken);
        var rows = await db.ToListAsync(
            query
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize),
            cancellationToken);
        var unread = await db.CountAsync(mine.Where(item => !item.IsRead), cancellationToken);

        return new NotificationListResult(rows.Select(ToDto).ToArray(), total, page, pageSize, unread);
    }

    public async Task<int> UnreadCountAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = await RequireMemberAsync(fundId, cancellationToken);
        await GenerateRemindersAsync(fundId, userId, cancellationToken);
        return await db.CountAsync(
            db.Query<Notification>().Where(item => item.FundId == fundId && item.UserId == userId && !item.IsRead),
            cancellationToken);
    }

    public async Task MarkReadAsync(Guid fundId, Guid notificationId, CancellationToken cancellationToken)
    {
        var userId = await RequireMemberAsync(fundId, cancellationToken);
        var item = await db.FirstOrDefaultAsync(
            db.Query<Notification>().Where(row => row.Id == notificationId && row.FundId == fundId && row.UserId == userId),
            cancellationToken) ?? throw new NotFoundException();

        if (!item.IsRead)
        {
            item.IsRead = true;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> MarkAllReadAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = await RequireMemberAsync(fundId, cancellationToken);
        var unread = await db.ToListAsync(
            db.Query<Notification>().Where(row => row.FundId == fundId && row.UserId == userId && !row.IsRead),
            cancellationToken);
        foreach (var item in unread)
        {
            item.IsRead = true;
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return unread.Count;
    }

    private async Task GenerateRemindersAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        await GenerateDueSoonAsync(fundId, userId, cancellationToken);
        await GenerateListDeadlineRemindersAsync(fundId, userId, cancellationToken);
    }

    /// <summary>
    /// Nhắc tự động trước hạn đóng danh sách: người tham gia còn khoản chưa đóng nhận nhắc mỗi ngày một lần trong N ngày cuối
    /// (D-N … D-1). Lịch sử nhắc ghi ở <see cref="PaymentReminder"/>; unique (người, sự kiện, ngày) chống trùng khi hai request đua nhau.
    /// </summary>
    private async Task GenerateListDeadlineRemindersAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = VietnamTime.DateOf(now);
        var last = today.AddDays(reminders.EffectiveListDeadlineDays);

        var owed = await db.ToListAsync(
            from assignment in db.Query<ChargeAssignment>()
            where assignment.UserId == userId && assignment.Status == AssignmentStatus.Unpaid
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.Status == ChargeStatus.Open && charge.EventId != null
            join fundEvent in db.Query<Event>() on charge.EventId equals (Guid?)fundEvent.Id
            where fundEvent.Status == EventStatus.Open && fundEvent.ListDeadline != null
                && fundEvent.ListDeadline > today && fundEvent.ListDeadline <= last
            select new { fundEvent.Id, fundEvent.Name, Deadline = fundEvent.ListDeadline!.Value, assignment.AmountDue },
            cancellationToken);
        if (owed.Count == 0)
        {
            return;
        }

        var eventIds = owed.Select(item => item.Id).Distinct().ToArray();
        var already = (await db.ToListAsync(
                db.Query<PaymentReminder>()
                    .Where(item => item.UserId == userId && item.Kind == ReminderKind.Auto && item.ReminderDate == today && item.EventId != null && eventIds.Contains(item.EventId.Value))
                    .Select(item => item.EventId!.Value),
                cancellationToken))
            .ToHashSet();

        var created = false;
        foreach (var group in owed.GroupBy(item => item.Id).Where(group => !already.Contains(group.Key)))
        {
            var first = group.First();
            var days = first.Deadline.DayNumber - today.DayNumber;
            db.Add(new PaymentReminder(fundId, group.Key, null, userId, ReminderKind.Auto, null, now, today));
            notifier.Send(
                [userId],
                new NotificationMessage(
                    fundId,
                    FinancialScope.Event,
                    group.Key,
                    NotificationType.EventUnpaidReminder,
                    $"{Notifier.ScopePrefix(FinancialScope.Event, first.Name)} Sắp hết hạn đóng danh sách",
                    $"Còn {days} ngày đến hạn đóng danh sách ({first.Deadline:dd/MM/yyyy}). Bạn còn thiếu {group.Sum(item => item.AmountDue):N0}đ; "
                    + "chưa đóng đủ khi đóng danh sách thì bị loại khỏi sự kiện và số tiền đã đóng được hoàn vào số dư cá nhân.",
                    nameof(Event),
                    group.Key));
            created = true;
        }

        if (created)
        {
            await SaveIgnoringDuplicatesAsync(cancellationToken);
        }
    }

    /// <summary>Hai request cùng tạo nhắc một ngày: bản ghi trùng bị unique chặn, bỏ qua (người kia đã tạo).</summary>
    private async Task SaveIgnoringDuplicatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Đã có nhắc của ngày hôm nay.
        }
    }

    /// <summary>Tạo nhắc hạn cho khoản thu chung (dữ liệu cũ còn hạn) của CHÍNH người dùng, tối đa một lần mỗi (nghĩa vụ, ngày Việt Nam).</summary>
    private async Task GenerateDueSoonAsync(Guid fundId, Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = VietnamTime.DateOf(now);
        var horizon = today.AddDays(DueSoonDays);

        var due = await db.ToListAsync(
            from assignment in db.Query<ChargeAssignment>()
            where assignment.UserId == userId && assignment.Status == AssignmentStatus.Unpaid
            join charge in db.Query<Charge>() on assignment.ChargeId equals charge.Id
            where charge.FundId == fundId && charge.EventId == null && charge.Status == ChargeStatus.Open && charge.DueDate >= today && charge.DueDate <= horizon
            select new { AssignmentId = assignment.Id, charge.Title, charge.DueDate, charge.EventId },
            cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var eventIds = due.Where(item => item.EventId.HasValue).Select(item => item.EventId!.Value).Distinct().ToArray();
        var events = eventIds.Length == 0
            ? []
            : (await db.ToListAsync(db.Query<Event>().Where(item => eventIds.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.Status }), cancellationToken))
                .ToDictionary(item => item.Id);

        var startOfToday = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();
        var assignmentIds = due.Select(item => item.AssignmentId).ToArray();
        var alreadyToday = (await db.ToListAsync(
                db.Query<Notification>()
                    .Where(item => item.UserId == userId && item.Type == NotificationType.ChargeDueSoon && item.CreatedAt >= startOfToday
                        && item.ReferenceType == DueSoonReferenceType && item.ReferenceId != null && assignmentIds.Contains(item.ReferenceId.Value))
                    .Select(item => item.ReferenceId!.Value),
                cancellationToken))
            .ToHashSet();

        var created = false;
        foreach (var item in due.Where(item => !alreadyToday.Contains(item.AssignmentId)))
        {
            var scope = item.EventId.HasValue ? FinancialScope.Event : FinancialScope.General;
            string? eventName = null;
            if (item.EventId is { } eventId)
            {
                // Sự kiện đã quyết toán / hủy thì không nhắc nữa.
                if (!events.TryGetValue(eventId, out var fundEvent) || !EventStatusRules.AcceptsPayments(fundEvent.Status))
                {
                    continue;
                }

                eventName = fundEvent.Name;
            }

            var dueDate = item.DueDate!.Value; // truy vấn đã lọc khoản có hạn
            var days = dueDate.DayNumber - today.DayNumber;
            notifier.Send(
                [userId],
                new NotificationMessage(
                    fundId,
                    scope,
                    item.EventId,
                    NotificationType.ChargeDueSoon,
                    $"{Notifier.ScopePrefix(scope, eventName)} Sắp đến hạn đóng: {item.Title}",
                    days == 0 ? $"Hạn đóng là hôm nay ({item.DueDate:dd/MM/yyyy})." : $"Còn {days} ngày đến hạn đóng ({item.DueDate:dd/MM/yyyy}).",
                    DueSoonReferenceType,
                    item.AssignmentId));
            created = true;
        }

        if (created)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static NotificationDto ToDto(Notification item) =>
        new(item.Id, item.Scope, item.EventId, item.Type, item.Title, item.Body, item.ReferenceType, item.ReferenceId, item.IsRead, item.CreatedAt);

    private async Task<Guid> RequireMemberAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException();
        await accessService.GetFundAccessAsync(userId, fundId, cancellationToken);
        return userId;
    }
}
