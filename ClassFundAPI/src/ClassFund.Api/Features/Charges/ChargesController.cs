using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.Charges;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Charges;

/// <summary>Khoản thu chung (FR-18, FR-24). Mọi thành viên xem; tạo / theo dõi / đóng chỉ thủ quỹ.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/charges")]
public sealed class ChargesController(IChargesService service) : ApiControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<PagedResult<ChargeDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ChargeDto>> List(Guid fundId, [FromQuery] ChargeListRequest request, CancellationToken cancellationToken) =>
        service.ListAsync(fundId, new ChargeFilter(request.Status, request.Page, request.PageSize), cancellationToken);

    [HttpGet("{chargeId:guid}")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<ChargeDto>(StatusCodes.Status200OK)]
    public Task<ChargeDto> Get(Guid fundId, Guid chargeId, CancellationToken cancellationToken) =>
        service.GetAsync(fundId, chargeId, cancellationToken);

    [HttpPost]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<ChargeDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid fundId, CreateChargeRequest request, CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await service.CreateAsync(fundId, new CreateChargeInput(request.Title, request.Purpose, request.AmountPerPerson, request.DueDate!.Value, request.CategoryId!.Value), cancellationToken));

    [HttpGet("{chargeId:guid}/assignments")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<PagedResult<AssignmentRowDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<AssignmentRowDto>> Assignments(
        Guid fundId, Guid chargeId, [FromQuery] AssignmentListRequest request, CancellationToken cancellationToken) =>
        service.ListAssignmentsAsync(fundId, chargeId, new AssignmentFilter(request.Status, request.Search, request.Page, request.PageSize), cancellationToken);

    [HttpPost("{chargeId:guid}/close")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<ChargeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<ChargeDto> Close(Guid fundId, Guid chargeId, CancellationToken cancellationToken) =>
        service.CloseAsync(fundId, chargeId, cancellationToken);

    [HttpPost("{chargeId:guid}/cancel")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<ChargeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<ChargeDto> Cancel(Guid fundId, Guid chargeId, CancellationToken cancellationToken) =>
        service.CancelAsync(fundId, chargeId, cancellationToken);

    [HttpPost("{chargeId:guid}/remind")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<RemindResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<RemindResult> Remind(Guid fundId, Guid chargeId, CancellationToken cancellationToken) =>
        service.RemindAsync(fundId, chargeId, cancellationToken);
}
