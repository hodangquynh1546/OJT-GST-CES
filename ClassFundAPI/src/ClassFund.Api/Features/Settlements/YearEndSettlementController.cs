using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Settlements;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Settlements;

/// <summary>M14 – Tổng kết cuối năm quỹ chung (FR-53, FR-54). Làm một lần; xong quỹ chuyển "Đã tổng kết" (chỉ xem).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/year-end-settlement")]
public sealed class YearEndSettlementController(IYearEndSettlementService service) : ApiControllerBase
{
    /// <summary>Xem trước: tổng thu / chi / thừa quỹ chung, phần mỗi thành viên theo tỷ lệ đã đóng, số thứ đang chặn (sự kiện chưa xong, thanh toán chờ).</summary>
    [HttpGet("preview")]
    [RequirePermission(Permissions.SettlementExecute)]
    [ProducesResponseType<YearEndPreviewDto>(StatusCodes.Status200OK)]
    public Task<YearEndPreviewDto> Preview(Guid fundId, CancellationToken ct) => service.PreviewAsync(fundId, ct);

    /// <summary>Tổng kết một lần: 422 còn sự kiện chưa xong / còn thanh toán chờ / thiếu cách xử lý / đã tổng kết.</summary>
    [HttpPost]
    [RequirePermission(Permissions.SettlementExecute)]
    [ProducesResponseType<YearEndResultDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Execute(Guid fundId, ExecuteSettlementRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.ExecuteAsync(fundId, request.SurplusMethod, ct));

    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<YearEndResultDto>(StatusCodes.Status200OK)]
    public Task<YearEndResultDto> Get(Guid fundId, CancellationToken ct) => service.GetAsync(fundId, ct);

    /// <summary>Thủ quỹ xác nhận đã hoàn tiền trực tiếp cho một người (vẫn làm được khi quỹ đã Archived); 409 khi đã xử lý.</summary>
    [HttpPost("lines/{lineId:guid}/refunded")]
    [RequirePermission(Permissions.SettlementExecute)]
    [ProducesResponseType<SettlementLineDto>(StatusCodes.Status200OK)]
    public Task<SettlementLineDto> ConfirmRefund(Guid fundId, Guid lineId, CancellationToken ct) => service.ConfirmRefundAsync(fundId, lineId, ct);
}
