using System.Net;
using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>Nhật ký chỉ ghi thêm; khóa chính bigint identity theo data-dictionary.md.</summary>
public sealed class AuditLog(
    DateTimeOffset occurredAt,
    Guid? actorUserId,
    AuditCategory category,
    string action,
    string? entityType,
    string? entityId,
    string? oldValues,
    string? newValues,
    string? correlationId,
    IPAddress? ipAddress) : Entity<long>
{
    public DateTimeOffset OccurredAt { get; private set; } = occurredAt;

    /// <summary>Null khi đăng nhập thất bại với tài khoản không tồn tại.</summary>
    public Guid? ActorUserId { get; private set; } = actorUserId;

    public AuditCategory Category { get; private set; } = category;

    public string Action { get; private set; } = action;

    public string? EntityType { get; private set; } = entityType;

    public string? EntityId { get; private set; } = entityId;

    public string? OldValues { get; private set; } = oldValues;

    public string? NewValues { get; private set; } = newValues;

    public string? CorrelationId { get; private set; } = correlationId;

    public IPAddress? IpAddress { get; private set; } = ipAddress;
}
