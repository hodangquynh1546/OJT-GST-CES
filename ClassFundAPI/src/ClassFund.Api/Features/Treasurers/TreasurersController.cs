using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Features.Treasurers;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Treasurers;

/// <summary>M4 – Phân công / thu hồi thủ quỹ, chỉ GVCN của quỹ (backend-api.md mục Treasurers; FR-27, P-15).</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/treasurers")]
[RequirePermission(Permissions.TreasurerAssign)]
public sealed class TreasurersController(ITreasurersService treasurers) : ApiControllerBase
{
    /// <summary>Đang hiệu lực trước, rồi lịch sử phân công / thu hồi (họ tên người thực hiện, thời điểm).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TreasurerAssignmentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TreasurerAssignmentResponse>>> List(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await treasurers.ListAsync(fundId, cancellationToken));

    /// <summary>
    /// Học sinh phải thuộc quỹ (404), có tài khoản đã kích hoạt, không bị khóa, chưa là thủ quỹ
    /// (422 <c>treasurer.no_account</c>, <c>treasurer.account_not_activated</c>, <c>treasurer.account_disabled</c>, <c>treasurer.already_assigned</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<TreasurerAssignedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<IActionResult> Assign(Guid fundId, AssignTreasurerRequest request, CancellationToken cancellationToken)
    {
        var created = await treasurers.AssignAsync(fundId, request.StudentId!.Value, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// Không thu hồi được thủ quỹ cuối cùng (422 <c>treasurer.last</c>): phân công người mới trước, thu hồi người cũ sau.
    /// Đã thu hồi → 409 <c>treasurer.already_revoked</c>.
    /// </summary>
    [HttpPost("{assignmentId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<IActionResult> Revoke(Guid fundId, Guid assignmentId, CancellationToken cancellationToken)
    {
        await treasurers.RevokeAsync(fundId, assignmentId, cancellationToken);
        return NoContent();
    }
}
