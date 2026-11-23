using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Features.Charges;
using ClassFund.Application.Features.Payments;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Payments;

/// <summary>Thu tiền: học sinh xem khoản cần đóng; thủ quỹ tích "đã thu" (Tiền mặt / Chuyển khoản) cho khoản thu chung và sự kiện.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund)]
public sealed class PaymentsController(IPaymentsService service) : ApiControllerBase
{
    [HttpGet("me/assignments")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<IReadOnlyList<MyAssignmentItemDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MyAssignmentItemDto>> Mine(Guid fundId, CancellationToken cancellationToken) =>
        service.ListMineAsync(fundId, cancellationToken);

    /// <summary>Ghi nhận đã thu. 409 <c>payment.not_payable</c> (đã thu) / <c>payment.charge_not_open</c>; 422 <c>payment.method_invalid</c>.</summary>
    [HttpPost("assignments/{assignmentId:guid}/collect")]
    [RequirePermission(Permissions.PaymentConfirm)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public async Task<IActionResult> Collect(Guid fundId, Guid assignmentId, CollectPaymentRequest request, CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await service.CollectAsync(fundId, assignmentId, new CollectPaymentInput(request.Method!.Value, request.TransferCode), cancellationToken));

    /// <summary>Hủy ghi nhận thu (tích nhầm): bút toán đảo, nghĩa vụ về Chưa nộp. 409 <c>payment.not_collected</c>.</summary>
    [HttpPost("assignments/{assignmentId:guid}/uncollect")]
    [RequirePermission(Permissions.PaymentConfirm)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<PaymentDto> Uncollect(Guid fundId, Guid assignmentId, UncollectPaymentRequest request, CancellationToken cancellationToken) =>
        service.UncollectAsync(fundId, assignmentId, request.Reason, cancellationToken);

    /// <summary>"Nhắc đóng" một người còn khoản chưa nộp; 409 nhắc quá sớm, 422 không còn gì để nhắc.</summary>
    [HttpPost("assignments/{assignmentId:guid}/remind")]
    [RequirePermission(Permissions.ChargeManage)]
    [ProducesResponseType<RemindResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<RemindResult> Remind(Guid fundId, Guid assignmentId, CancellationToken cancellationToken) =>
        service.RemindAsync(fundId, assignmentId, cancellationToken);
}
