using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Reports;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Reports;

/// <summary>M13 – Tổng quan & báo cáo (FR-48 → FR-51). Chỉ đọc; quỹ chung và sự kiện luôn tách riêng (BR-02).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/reports")]
public sealed class ReportsController(IReportsService service) : ApiControllerBase
{
    /// <summary>S-01 Tổng quan quỹ chung – mọi thành viên; thủ quỹ có thêm giao dịch gần đây và việc cần làm.</summary>
    [HttpGet("overview")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<FundOverviewDto>(StatusCodes.Status200OK)]
    public Task<FundOverviewDto> Overview(Guid fundId, CancellationToken ct) => service.GetOverviewAsync(fundId, ct);

    /// <summary>S-02 Thu chi chung: tổng trong khoảng ngày, chi theo danh mục, số dư theo tháng.</summary>
    [HttpGet("general")]
    [RequirePermission(Permissions.ReportView)]
    [ProducesResponseType<GeneralReportDto>(StatusCodes.Status200OK)]
    public Task<GeneralReportDto> General(Guid fundId, [FromQuery] ReportRangeQuery query, CancellationToken ct) =>
        service.GetGeneralAsync(fundId, query.From, query.To, ct);

    [HttpGet("events")]
    [RequirePermission(Permissions.ReportView)]
    [ProducesResponseType<IReadOnlyList<EventReportRowDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<EventReportRowDto>> Events(Guid fundId, CancellationToken ct) => service.GetEventsAsync(fundId, ct);

    [HttpGet("students")]
    [RequirePermission(Permissions.ReportView)]
    [ProducesResponseType<IReadOnlyList<StudentReportRowDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<StudentReportRowDto>> Students(Guid fundId, CancellationToken ct) => service.GetStudentsAsync(fundId, ct);

    /// <summary>Báo cáo của chính tôi – học sinh chỉ xem được dòng của mình (FR-51).</summary>
    [HttpGet("me")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<StudentReportRowDto>(StatusCodes.Status200OK)]
    public Task<StudentReportRowDto> Me(Guid fundId, CancellationToken ct) => service.GetMineAsync(fundId, ct);
}

/// <summary><c>?from=&amp;to=</c> (<c>yyyy-MM-dd</c>, theo ngày giao dịch).</summary>
public sealed record ReportRangeQuery
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}

public sealed class ReportRangeQueryValidator : AbstractValidator<ReportRangeQuery>
{
    public ReportRangeQueryValidator() =>
        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From)
            .When(query => query.From is not null && query.To is not null)
            .WithMessage("Đến ngày phải sau hoặc bằng Từ ngày.");
}
