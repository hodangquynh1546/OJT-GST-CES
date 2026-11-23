using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Notifications;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Notifications;

/// <summary>Query danh sách thông báo: <c>?unreadOnly=&amp;page=&amp;pageSize=</c>.</summary>
public sealed record NotificationListRequest : PageRequest
{
    public bool UnreadOnly { get; init; }
}

public sealed class NotificationListRequestValidator : FluentValidation.AbstractValidator<NotificationListRequest>
{
    public NotificationListRequestValidator() => this.ValidatePaging();
}

/// <summary>M11 – Thông báo của tôi (FR-43, FR-44). Chỉ thấy thông báo của chính mình.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/me/notifications")]
[RequirePermission(Permissions.FundView)]
public sealed class NotificationsController(INotificationsService service) : ApiControllerBase
{
    /// <summary>Danh sách mới nhất trước kèm <c>unreadCount</c>; mở danh sách cũng tạo nhắc hạn trong ngày (không trùng).</summary>
    [HttpGet]
    [ProducesResponseType<NotificationListResult>(StatusCodes.Status200OK)]
    public Task<NotificationListResult> List(Guid fundId, [FromQuery] NotificationListRequest request, CancellationToken ct) =>
        service.ListAsync(fundId, request.UnreadOnly, request.Page, request.PageSize, ct);

    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountResult>(StatusCodes.Status200OK)]
    public async Task<UnreadCountResult> UnreadCount(Guid fundId, CancellationToken ct) => new(await service.UnreadCountAsync(fundId, ct));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid fundId, Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(fundId, id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType<MarkedReadResult>(StatusCodes.Status200OK)]
    public async Task<MarkedReadResult> MarkAllRead(Guid fundId, CancellationToken ct) => new(await service.MarkAllReadAsync(fundId, ct));
}

public sealed record UnreadCountResult(int Count);

public sealed record MarkedReadResult(int Marked);
