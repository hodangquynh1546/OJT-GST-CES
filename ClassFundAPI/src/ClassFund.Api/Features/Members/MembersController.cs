using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Api.Hosting;
using ClassFund.Application.Common.Paging;
using ClassFund.Application.Features.Members;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Members;

/// <summary>Quản lý học sinh / thành viên quỹ (FR-09 → FR-11).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/members")]
[RequirePermission(Permissions.MemberManage)]
public sealed class MembersController(IMembersService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<MemberDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<MemberDto>> List(Guid fundId, [FromQuery] MemberListRequest request, CancellationToken cancellationToken) =>
        service.ListAsync(fundId, new MemberFilter(request.Search, request.Role, request.Status, request.Page, request.PageSize), cancellationToken);

    [HttpPost]
    [ProducesResponseType<AddMemberResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    public async Task<IActionResult> Add(Guid fundId, AddMemberRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await service.AddAsync(fundId, request.Email, request.FullName, cancellationToken));

    [HttpPost("bulk")]
    [EnableRateLimiting(RateLimitPolicies.MembersBulk)]
    [ProducesResponseType<BulkAddMembersResult>(StatusCodes.Status200OK)]
    public Task<BulkAddMembersResult> AddMany(Guid fundId, AddMembersRequest request, CancellationToken cancellationToken) =>
        service.AddManyAsync(fundId, request.Emails, cancellationToken);

    [HttpPut("{memberId:guid}")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status200OK)]
    public Task<MemberDto> Update(Guid fundId, Guid memberId, UpdateMemberRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(fundId, memberId, request.FullName, cancellationToken);

    [HttpPost("{memberId:guid}/reset-password")]
    [ProducesResponseType<TemporaryPasswordResult>(StatusCodes.Status200OK)]
    public Task<TemporaryPasswordResult> ResetPassword(Guid fundId, Guid memberId, CancellationToken cancellationToken) =>
        service.ResetPasswordAsync(fundId, memberId, cancellationToken);

    [HttpGet("{memberId:guid}/removal-preview")]
    [ProducesResponseType<RemovalPreviewDto>(StatusCodes.Status200OK)]
    public Task<RemovalPreviewDto> RemovalPreview(Guid fundId, Guid memberId, CancellationToken cancellationToken) =>
        service.GetRemovalPreviewAsync(fundId, memberId, cancellationToken);

    [HttpPost("{memberId:guid}/remove")]
    [ProducesResponseType<RemoveMemberResult>(StatusCodes.Status200OK)]
    public Task<RemoveMemberResult> Remove(Guid fundId, Guid memberId, CancellationToken cancellationToken) =>
        service.RemoveAsync(fundId, memberId, cancellationToken);
}
