using ClassFund.Domain.Common;
using ClassFund.Domain.Enums;

namespace ClassFund.Domain.Entities;

/// <summary>Thông báo trong hệ thống; luôn mang phạm vi (Quỹ chung / Sự kiện) để người dùng không nhầm lẫn.</summary>
public sealed class Notification(Guid userId, Guid fundId, FinancialScope scope, Guid? eventId, NotificationType type, string title, string body, DateTimeOffset createdAt) : Entity
{
    public Guid UserId { get; init; } = userId;

    public Guid FundId { get; init; } = fundId;

    public FinancialScope Scope { get; init; } = scope;

    public Guid? EventId { get; init; } = eventId;

    public NotificationType Type { get; init; } = type;

    public string Title { get; init; } = title;

    public string Body { get; init; } = body;

    /// <summary>Đối tượng liên quan (Charge, Settlement, WithdrawalRequest…) để điều hướng.</summary>
    public string? ReferenceType { get; init; }

    public Guid? ReferenceId { get; init; }

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = createdAt;
}

/// <summary>Nhật ký hoạt động (append-only, không sửa/xóa).</summary>
public sealed class AuditLog(DateTimeOffset at, Guid? fundId, Guid? actorUserId, FinancialScope scope, Guid? eventId, string action, string? entityType, string? entityId) : Entity
{
    public DateTimeOffset At { get; init; } = at;

    public Guid? FundId { get; init; } = fundId;

    public Guid? ActorUserId { get; init; } = actorUserId;

    public FinancialScope Scope { get; init; } = scope;

    public Guid? EventId { get; init; } = eventId;

    /// <summary>Mã hành động dạng "payment.confirm", "treasurer.grant"…</summary>
    public string Action { get; init; } = action;

    public string? EntityType { get; init; } = entityType;

    public string? EntityId { get; init; } = entityId;

    /// <summary>Trạng thái trước / sau (JSON, không chứa mật khẩu, hash, token).</summary>
    public string? BeforeJson { get; init; }

    public string? AfterJson { get; init; }

    /// <summary>Thủ quỹ thao tác trên giao dịch của chính mình.</summary>
    public bool IsSelfAction { get; init; }

    public string? Note { get; init; }

    public string? CorrelationId { get; init; }

    public System.Net.IPAddress? IpAddress { get; init; }
}
