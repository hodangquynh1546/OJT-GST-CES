using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Notifications;

/// <summary>Một thông báo của chính người dùng (N-01). Tiêu đề đã có tiền tố phạm vi "[Quỹ chung]" / "[Sự kiện: tên]".</summary>
public sealed record NotificationDto(
    Guid Id,
    FinancialScope Scope,
    Guid? EventId,
    NotificationType Type,
    string Title,
    string Body,
    string? ReferenceType,
    Guid? ReferenceId,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationListResult(IReadOnlyList<NotificationDto> Items, int Total, int Page, int PageSize, int UnreadCount);
