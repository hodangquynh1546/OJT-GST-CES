using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Api.Features.Charges;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.Charges;
using ClassFund.Application.Features.Events;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Events;

/// <summary>M8 – Sự kiện (FR-30 → FR-34). Học sinh chỉ thấy sự kiện được mời (không thì 404).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/events")]
public sealed class EventsController(IEventsService service, IChargesService charges) : ApiControllerBase
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
        var created = await service.CreateAsync(fundId, new CreateEventInput(request.Name, request.Description, request.EventDate!.Value, request.ListDeadline!.Value), ct);
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
        service.UpdateAsync(fundId, eventId, new UpdateEventInput(request.Name, request.Description, request.EventDate!.Value, request.ListDeadline!.Value), ct);

    /// <summary>Hủy khi đang mở và chưa có thu / chi (BR-16); 422 <c>event.has_money</c>.</summary>
    [HttpPost("{eventId:guid}/cancel")]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    public Task<EventDetailDto> Cancel(Guid fundId, Guid eventId, CancelEventRequest request, CancellationToken ct) =>
        service.CancelAsync(fundId, eventId, request.Reason, ct);

    /// <summary>Xem trước khi đóng danh sách: ai bị loại (chưa đóng đủ), số tiền hoàn vào số dư, hạn đóng danh sách (BR-19).</summary>
    [HttpGet("{eventId:guid}/close-list-preview")]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventCloseListPreviewDto>(StatusCodes.Status200OK)]
    public Task<EventCloseListPreviewDto> PreviewCloseList(Guid fundId, Guid eventId, CancellationToken ct) =>
        service.PreviewCloseListAsync(fundId, eventId, ct);

    /// <summary>
    /// Đóng danh sách (Open → Completed, BR-19): loại người chưa đóng đủ, hoàn tiền đã đóng vào số dư cá nhân.
    /// 422 <c>event.list_deadline_not_reached</c> khi đóng sớm hơn hạn mà thiếu <c>confirmEarly</c>.
    /// </summary>
    [HttpPost("{eventId:guid}/close-list")]
    [RequirePermission(Permissions.EventManage)]
    [ProducesResponseType<EventCloseListPreviewDto>(StatusCodes.Status200OK)]
    public Task<EventCloseListPreviewDto> CloseList(Guid fundId, Guid eventId, CloseListRequest request, CancellationToken ct) =>
        service.CloseListAsync(fundId, eventId, request.ConfirmEarly, ct);

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

    // ---- Khoản thu sự kiện (M6b) ----

    /// <summary>Tạo khoản thu sự kiện, chỉ gán cho người đã xác nhận tham gia (BR-18).</summary>
    [HttpPost("{eventId:guid}/charges")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<EventChargeDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEventCharge(Guid fundId, Guid eventId, CreateChargeRequest request, CancellationToken ct)
    {
        var result = await charges.CreateEventChargeAsync(
            fundId, eventId,
            new CreateChargeInput(request.Title, request.Purpose, request.AmountPerPerson, request.DueDate, request.CategoryId!.Value),
            ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{eventId:guid}/charges/{chargeId:guid}/close")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<EventChargeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<EventChargeDto> CloseEventCharge(Guid fundId, Guid eventId, Guid chargeId, CancellationToken ct) =>
        charges.CloseEventChargeAsync(fundId, eventId, chargeId, ct);

    [HttpPost("{eventId:guid}/charges/{chargeId:guid}/cancel")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<EventChargeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<EventChargeDto> CancelEventCharge(Guid fundId, Guid eventId, Guid chargeId, CancellationToken ct) =>
        charges.CancelEventChargeAsync(fundId, eventId, chargeId, ct);

    /// <summary>"Nhắc đóng" một người tham gia còn khoản chưa đóng; 409 nhắc quá sớm, 422 không còn gì để nhắc.</summary>
    [HttpPost("{eventId:guid}/participants/{userId:guid}/remind")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<RemindResult>(StatusCodes.Status200OK)]
    public Task<RemindResult> RemindParticipant(Guid fundId, Guid eventId, Guid userId, CancellationToken ct) =>
        charges.RemindEventParticipantAsync(fundId, eventId, userId, ct);

    /// <summary>Danh sách đã đóng / chưa đóng của một khoản thu sự kiện.</summary>
    [HttpGet("{eventId:guid}/charges/{chargeId:guid}/assignments")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<PagedResult<AssignmentRowDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<AssignmentRowDto>> EventChargeAssignments(
        Guid fundId, Guid eventId, Guid chargeId, [FromQuery] AssignmentListRequest request, CancellationToken ct) =>
        charges.ListEventAssignmentsAsync(fundId, eventId, chargeId, new AssignmentFilter(request.Status, request.Search, request.Page, request.PageSize), ct);
}
