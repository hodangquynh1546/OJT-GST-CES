using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Features.Reports;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Reports;

/// <summary>M7b – Báo cáo (backend-api.md mục Funds; FR-08, FR-09). Mọi thành viên quỹ; số tổng giống nhau ở mọi vai trò.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/reports")]
public sealed class ReportsController(IReportsService reports) : ApiControllerBase
{
    /// <summary>
    /// <c>year</c> (1–9999), <c>month</c> (1–12) bắt buộc. Học sinh, phụ huynh: <c>transactions</c> chỉ khoản công khai;
    /// phần không công khai gộp thành dòng "Khoản khác" theo loại, <c>categoryId</c> và <c>count</c> null (P1).
    /// </summary>
    [HttpGet("monthly")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<MonthlyReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<ActionResult<MonthlyReport>> Monthly(
        Guid fundId,
        [FromQuery] MonthlyReportRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reports.GetMonthlyAsync(fundId, request.Year!.Value, request.Month!.Value, cancellationToken));

    /// <summary><c>year</c> (1–9999) bắt buộc. Luôn đủ 12 tháng, chỉ có số tổng.</summary>
    [HttpGet("yearly")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<YearlyReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<ActionResult<YearlyReport>> Yearly(
        Guid fundId,
        [FromQuery] YearlyReportRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reports.GetYearlyAsync(fundId, request.Year!.Value, cancellationToken));
}
