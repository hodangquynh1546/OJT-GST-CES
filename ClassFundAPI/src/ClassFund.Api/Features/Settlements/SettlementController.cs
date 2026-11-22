using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Settlements;
using ClassFund.Domain.Authorization;
using ClassFund.Domain.Enums;

namespace ClassFund.Api.Features.Settlements;

/// <summary><c>surplusMethod</c> bắt buộc khi có tiền thừa (hoàn trực tiếp / cộng số dư); bỏ qua khi thu chi vừa đủ.</summary>
public sealed record ExecuteSettlementRequest(SurplusMethod? SurplusMethod);

public sealed class ExecuteSettlementRequestValidator : AbstractValidator<ExecuteSettlementRequest>
{
    public ExecuteSettlementRequestValidator() =>
        RuleFor(request => request.SurplusMethod).IsInEnum().WithMessage("Cách xử lý tiền thừa không hợp lệ.");
}

/// <summary>M9 – Quyết toán sự kiện (FR-35 → FR-39). Học sinh chỉ xem phần của mình.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/events/{eventId:guid}/settlement")]
public sealed class SettlementController(ISettlementService service) : ApiControllerBase
{
    /// <summary>Xem trước: tổng thu / chi / thừa, phần mỗi người theo tỷ lệ đã đóng, số thanh toán chờ (chặn quyết toán).</summary>
    [HttpGet("preview")]
    [RequirePermission(Permissions.SettlementExecute)]
    [ProducesResponseType<SettlementPreviewDto>(StatusCodes.Status200OK)]
    public Task<SettlementPreviewDto> Preview(Guid fundId, Guid eventId, CancellationToken ct) => service.PreviewAsync(fundId, eventId, ct);

    /// <summary>Quyết toán một lần: 422 chưa kết thúc / còn thanh toán chờ / thiếu cách xử lý; 409 đã quyết toán.</summary>
    [HttpPost]
    [RequirePermission(Permissions.SettlementExecute)]
    [ProducesResponseType<SettlementResultDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Execute(Guid fundId, Guid eventId, ExecuteSettlementRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.ExecuteAsync(fundId, eventId, request.SurplusMethod, ct));

    [HttpGet]
    [RequirePermission(Permissions.EventParticipate)]
    [ProducesResponseType<SettlementResultDto>(StatusCodes.Status200OK)]
    public Task<SettlementResultDto> Get(Guid fundId, Guid eventId, CancellationToken ct) => service.GetAsync(fundId, eventId, ct);

    /// <summary>Thủ quỹ xác nhận đã hoàn tiền trực tiếp cho một người; 409 khi dòng đã xử lý.</summary>
    [HttpPost("lines/{lineId:guid}/refunded")]
    [RequirePermission(Permissions.SettlementExecute)]
    [ProducesResponseType<SettlementLineDto>(StatusCodes.Status200OK)]
    public Task<SettlementLineDto> ConfirmRefund(Guid fundId, Guid eventId, Guid lineId, CancellationToken ct) =>
        service.ConfirmRefundAsync(fundId, eventId, lineId, ct);
}
