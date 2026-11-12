using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Members;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Members;

/// <summary>M5 – Thành viên quỹ, chỉ xem (backend-api.md mục Funds – <c>members</c>; FR-12).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/members")]
[RequirePermission(Permissions.MemberView)]
public sealed class MembersController(IMembersService members) : ApiControllerBase
{
    /// <summary>Chỉ <c>fullName</c> và <c>role</c> (<c>Teacher</c>, <c>Treasurer</c>, <c>Student</c>); không gồm phụ huynh.</summary>
    [HttpGet]
    [ResponseCache(NoStore = true)]
    [ProducesResponseType<IReadOnlyList<MemberResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MemberResponse>>> List(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await members.ListAsync(fundId, cancellationToken));
}
