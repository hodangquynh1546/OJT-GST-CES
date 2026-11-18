using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.AuditLogs;

/// <summary>Bộ lọc nhật ký (FR-47). Ngày theo giờ Việt Nam, cả hai đầu đều tính trọn ngày.</summary>
public sealed record AuditLogFilter(
    DateOnly? From,
    DateOnly? To,
    Guid? ActorId,
    FinancialScope? Scope,
    Guid? EventId,
    string? Action,
    bool SelfOnly,
    int Page,
    int PageSize);

/// <summary>
/// Một dòng nhật ký (U-01). <see cref="Before"/> / <see cref="After"/> là JSON đã lưu (không chứa mật khẩu, hash, token).
/// <see cref="Summary"/> là mô tả ngắn cho cột "Chi tiết"; <see cref="IpAddress"/> đã che hai phần cuối.
/// </summary>
public sealed record AuditLogDto(
    Guid Id,
    DateTimeOffset At,
    Guid? ActorUserId,
    string ActorName,
    string Action,
    FinancialScope Scope,
    Guid? EventId,
    string? EventName,
    string? EntityType,
    string? EntityId,
    string Summary,
    bool IsSelfAction,
    string? Note,
    string? Before,
    string? After,
    string? IpAddress);

/// <summary>Người đã thao tác trong quỹ (ô "Người thực hiện").</summary>
public sealed record AuditActorDto(Guid UserId, string FullName);
