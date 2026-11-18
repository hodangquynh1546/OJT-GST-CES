using ClassFund.Application.Common.Paging;
using ClassFund.Application.Common.Persistence;
using ClassFund.Domain.Entities;

namespace ClassFund.Application.Features.AuditLogs;

/// <summary>Xem nhật ký hoạt động của quỹ (FR-45 → FR-47). Chỉ đọc: AuditLog là append-only, không có thao tác ghi.</summary>
public interface IAuditLogsService
{
    /// <summary>Nhật ký của quỹ, mới nhất trước, có lọc và phân trang. Quyền <c>Audit.View</c> kiểm ở Api.</summary>
    Task<PagedResult<AuditLogDto>> ListAsync(Guid fundId, AuditLogFilter filter, CancellationToken cancellationToken);

    /// <summary>Những người đã có thao tác trong nhật ký của quỹ (cho ô lọc "Người thực hiện").</summary>
    Task<IReadOnlyList<AuditActorDto>> ListActorsAsync(Guid fundId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class AuditLogsService(IAppDb db) : IAuditLogsService
{
    /// <summary>Tên hiển thị khi không có người thực hiện (thao tác của hệ thống).</summary>
    public const string SystemActorName = "Hệ thống";

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<PagedResult<AuditLogDto>> ListAsync(Guid fundId, AuditLogFilter filter, CancellationToken cancellationToken)
    {
        var logs = db.Query<AuditLog>().Where(log => log.FundId == fundId);

        // Ngày nghiệp vụ theo giờ Việt Nam (NFR-07): [from 00:00, to + 1 ngày 00:00) quy về UTC
        // (PostgreSQL timestamptz qua Npgsql chỉ nhận DateTimeOffset offset 0).
        if (filter.From is { } from)
        {
            var start = StartOfVietnamDay(from);
            logs = logs.Where(log => log.At >= start);
        }

        if (filter.To is { } to)
        {
            var end = StartOfVietnamDay(to.AddDays(1));
            logs = logs.Where(log => log.At < end);
        }

        if (filter.ActorId is { } actorId)
        {
            logs = logs.Where(log => log.ActorUserId == actorId);
        }

        if (filter.Scope is { } scope)
        {
            logs = logs.Where(log => log.Scope == scope);
        }

        if (filter.EventId is { } eventId)
        {
            logs = logs.Where(log => log.EventId == eventId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            // "payment." = mọi hành động của nhóm payment; còn lại khớp đúng mã.
            var action = filter.Action.Trim();
            logs = action.EndsWith('.')
                ? logs.Where(log => log.Action.StartsWith(action))
                : logs.Where(log => log.Action == action);
        }

        if (filter.SelfOnly)
        {
            logs = logs.Where(log => log.IsSelfAction);
        }

        var total = await db.CountAsync(logs, cancellationToken);

        var rows = await db.ToListAsync(
            from log in logs
                .OrderByDescending(log => log.At)
                .ThenByDescending(log => log.Id)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
            join user in db.Query<User>() on log.ActorUserId equals (Guid?)user.Id into actors
            from actor in actors.DefaultIfEmpty()
            join item in db.Query<Event>() on log.EventId equals (Guid?)item.Id into events
            from fundEvent in events.DefaultIfEmpty()
            orderby log.At descending, log.Id descending
            select new
            {
                log.Id,
                log.At,
                log.ActorUserId,
                ActorName = actor == null ? null : actor.FullName,
                log.Action,
                log.Scope,
                log.EventId,
                EventName = fundEvent == null ? null : fundEvent.Name,
                log.EntityType,
                log.EntityId,
                log.IsSelfAction,
                log.Note,
                log.BeforeJson,
                log.AfterJson,
                log.IpAddress,
            },
            cancellationToken);

        var items = rows
            .Select(row => new AuditLogDto(
                row.Id,
                row.At,
                row.ActorUserId,
                row.ActorName ?? SystemActorName,
                row.Action,
                row.Scope,
                row.EventId,
                row.EventName,
                row.EntityType,
                row.EntityId,
                AuditSummary.Build(row.BeforeJson, row.AfterJson, row.Note),
                row.IsSelfAction,
                row.Note,
                row.BeforeJson,
                row.AfterJson,
                AuditSummary.MaskIp(row.IpAddress)))
            .ToArray();

        return new PagedResult<AuditLogDto>(items, total, filter.Page, filter.PageSize);
    }

    /// <summary>00:00 giờ Việt Nam của <paramref name="date"/>, biểu diễn bằng UTC.</summary>
    public static DateTimeOffset StartOfVietnamDay(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();

    public async Task<IReadOnlyList<AuditActorDto>> ListActorsAsync(Guid fundId, CancellationToken cancellationToken)
    {
        var actorIds = db.Query<AuditLog>()
            .Where(log => log.FundId == fundId && log.ActorUserId != null)
            .Select(log => log.ActorUserId!.Value)
            .Distinct();

        var rows = await db.ToListAsync(
            from user in db.Query<User>()
            where actorIds.Contains(user.Id)
            orderby user.FullName, user.Id
            select new AuditActorDto(user.Id, user.FullName),
            cancellationToken);

        return rows;
    }
}
