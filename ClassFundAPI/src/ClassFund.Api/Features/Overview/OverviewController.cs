using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Application.Features.Overview;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Overview;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/funds/{fundId:guid}")]
public sealed class OverviewController(OverviewService overview) : ControllerBase
{
    /// <summary>Số liệu giống nhau ở mọi vai trò; 5 giao dịch gần nhất theo phạm vi người xem.</summary>
    [HttpGet]
    [RequirePermission(Permissions.FundView)]
    public async Task<ActionResult<FundOverviewResponse>> Get(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await overview.GetAsync(fundId, cancellationToken));

    /// <summary>GVCN, thủ quỹ. Chỉ họ tên và vai trò.</summary>
    [HttpGet("members")]
    [RequirePermission(Permissions.MemberView)]
    public async Task<ActionResult<IReadOnlyList<FundMemberResponse>>> Members(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await overview.ListMembersAsync(fundId, cancellationToken));
}
