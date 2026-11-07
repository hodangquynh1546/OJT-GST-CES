using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Application.Features.Reports;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Reports;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/funds/{fundId:guid}/reports")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Học sinh, phụ huynh: phần không công khai gộp thành dòng "Khoản khác" theo loại, không có số lượng.</summary>
    [HttpGet("monthly")]
    [RequirePermission(Permissions.FundView)]
    public async Task<ActionResult<MonthlyReport>> Monthly(
        Guid fundId,
        [FromQuery] MonthlyReportRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reports.GetMonthlyAsync(fundId, request.Year!.Value, request.Month!.Value, cancellationToken));

    /// <summary>12 tháng của năm, chỉ có số tổng.</summary>
    [HttpGet("yearly")]
    [RequirePermission(Permissions.FundView)]
    public async Task<ActionResult<YearlyReport>> Yearly(
        Guid fundId,
        [FromQuery] YearlyReportRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reports.GetYearlyAsync(fundId, request.Year!.Value, cancellationToken));
}
