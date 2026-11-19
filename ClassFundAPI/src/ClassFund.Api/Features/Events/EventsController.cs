using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Events;

/// <summary>M8 – Sự kiện (FR-30 → FR-34). Học sinh chỉ thấy sự kiện được mời (không thì 404).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/events")]
public sealed class EventsController(IEventsService service) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.EventParticipate)]
    [ProducesResponseType<IReadOnlyList<EventSummaryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<EventSummaryDto>> List(Guid fundId, CancellationToken ct) => service.ListAsync(fundId, ct);

    [HttpPost]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EventDetailDto>> Create(Guid fundId, SaveEventRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(fundId, new CreateEventInput(request.Name, request.Description, request.EventDate!.Value), ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("{eventId:guid}")]
    [RequirePermission(Permissions.EventParticipate)]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    public Task<EventDetailDto> Get(Guid fundId, Guid eventId, CancellationToken ct) => service.GetAsync(fundId, eventId, ct);

    [HttpPut("{eventId:guid}")]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    public Task<EventDetailDto> Update(Guid fundId, Guid eventId, SaveEventRequest request, CancellationToken ct) =>
        service.UpdateAsync(fundId, eventId, new UpdateEventInput(request.Name, request.Description, request.EventDate!.Value), ct);

    /// <summary>Hủy khi đang mở và chưa có thu / chi (BR-16); 422 <c>event.has_money</c>.</summary>
    [HttpPost("{eventId:guid}/cancel")]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    public Task<EventDetailDto> Cancel(Guid fundId, Guid eventId, CancelEventRequest request, CancellationToken ct) =>
        service.CancelAsync(fundId, eventId, request.Reason, ct);

    /// <summary>Xem trước khi kết thúc: ai bị loại, ai được nhắc (BR-19).</summary>
    [HttpGet("{eventId:guid}/completion-preview")]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventCompletionPreviewDto>(StatusCodes.Status200OK)]
    public Task<EventCompletionPreviewDto> PreviewCompletion(Guid fundId, Guid eventId, CancellationToken ct) =>
        service.PreviewCompletionAsync(fundId, eventId, ct);

    /// <summary>Kết thúc sự kiện (Open → Completed, BR-19); trả về danh sách đã loại / đã nhắc.</summary>
    [HttpPost("{eventId:guid}/complete")]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventCompletionPreviewDto>(StatusCodes.Status200OK)]
    public Task<EventCompletionPreviewDto> Complete(Guid fundId, Guid eventId, CancellationToken ct) =>
        service.CompleteAsync(fundId, eventId, ct);

    /// <summary>Người đã nhận tham gia kèm tình trạng đóng tiền (thủ quỹ); học sinh chỉ nhận danh sách tên.</summary>
    [HttpGet("{eventId:guid}/participants")]
    [RequirePermission(Permissions.EventParticipate)]
    [ProducesResponseType<EventParticipantsResult>(StatusCodes.Status200OK)]
    public Task<EventParticipantsResult> Participants(Guid fundId, Guid eventId, [FromQuery] EventParticipantsQuery query, CancellationToken ct) =>
        service.ListParticipantsAsync(fundId, eventId, query.PaymentStatus, ct);

    [HttpPut("{eventId:guid}/me/response")]
    [RequirePermission(Permissions.EventParticipate)]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    public Task<EventDetailDto> Respond(Guid fundId, Guid eventId, RespondEventRequest request, CancellationToken ct) =>
        service.RespondAsync(fundId, eventId, request.Response!.Value, ct);
}
