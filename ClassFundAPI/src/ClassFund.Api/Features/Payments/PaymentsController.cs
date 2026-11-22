using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.Payments;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Payments;

/// <summary>Nộp và xác nhận thanh toán khoản thu chung (FR-20 → FR-23).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund)]
public sealed class PaymentsController(IPaymentsService service) : ApiControllerBase
{
    [HttpGet("me/assignments")]
    [RequirePermission(Permissions.PaymentSubmit)]
    [ProducesResponseType<IReadOnlyList<MyAssignmentItemDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MyAssignmentItemDto>> Mine(Guid fundId, CancellationToken cancellationToken) =>
        service.ListMineAsync(fundId, cancellationToken);

    [HttpPost("me/assignments/{assignmentId:guid}/payments")]
    [RequirePermission(Permissions.PaymentSubmit)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public async Task<IActionResult> Submit(Guid fundId, Guid assignmentId, SubmitPaymentRequest request, CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await service.SubmitAsync(fundId, assignmentId, new SubmitPaymentInput(request.Method!.Value, request.TransferCode, request.AttachmentId, request.UseBalance), cancellationToken));

    [HttpPost("assignments/{assignmentId:guid}/record-cash")]
    [RequirePermission(Permissions.PaymentConfirm)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public async Task<IActionResult> RecordCash(Guid fundId, Guid assignmentId, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.RecordCashAsync(fundId, assignmentId, cancellationToken));

    [HttpGet("payments")]
    [RequirePermission(Permissions.PaymentConfirm)]
    [ProducesResponseType<PagedResult<PaymentQueueItemDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<PaymentQueueItemDto>> List(Guid fundId, [FromQuery] PaymentListRequest request, CancellationToken cancellationToken) =>
        service.ListAsync(fundId, new PaymentFilter(request.Status, request.Page, request.PageSize), cancellationToken);

    [HttpPost("payments/{paymentId:guid}/confirm")]
    [RequirePermission(Permissions.PaymentConfirm)]
    [ProducesResponseType<PaymentQueueItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<PaymentQueueItemDto> Confirm(Guid fundId, Guid paymentId, CancellationToken cancellationToken) =>
        service.ConfirmAsync(fundId, paymentId, cancellationToken);

    [HttpPost("payments/{paymentId:guid}/reject")]
    [RequirePermission(Permissions.PaymentConfirm)]
    [ProducesResponseType<PaymentQueueItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<PaymentQueueItemDto> Reject(Guid fundId, Guid paymentId, RejectPaymentRequest request, CancellationToken cancellationToken) =>
        service.RejectAsync(fundId, paymentId, request.Reason, cancellationToken);
}
