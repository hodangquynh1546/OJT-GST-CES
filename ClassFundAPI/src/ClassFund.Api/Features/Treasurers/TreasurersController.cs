using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Features.Treasurers;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Treasurers;

/// <summary>Phân quyền / thu hồi thủ quỹ (FR-12, FR-13): chỉ thủ quỹ chính có <c>Treasurer.Manage</c>.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/treasurers")]
[RequirePermission(Permissions.TreasurerManage)]
public sealed class TreasurersController(ITreasurersService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TreasurerDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<TreasurerDto>> List(Guid fundId, CancellationToken cancellationToken) =>
        service.ListAsync(fundId, cancellationToken);

    [HttpPost]
    [ProducesResponseType<TreasurerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public async Task<IActionResult> Grant(Guid fundId, GrantTreasurerRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.GrantAsync(fundId, request.MemberId, cancellationToken));

    [HttpPost("{memberId:guid}/revoke")]
    [ProducesResponseType<TreasurerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public Task<TreasurerDto> Revoke(Guid fundId, Guid memberId, CancellationToken cancellationToken) =>
        service.RevokeAsync(fundId, memberId, cancellationToken);
}
