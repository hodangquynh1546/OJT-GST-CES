using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Application.Features.Treasurers;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Treasurers;

/// <summary>Phân công / thu hồi thủ quỹ – chỉ GVCN (docs/api/backend-api.md mục Treasurers; FR-27, P-15).</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/funds/{fundId:guid}/treasurers")]
[RequirePermission(Permissions.TreasurerAssign)]
public sealed class TreasurersController(ITreasurerService treasurers) : ControllerBase
{
    /// <summary>Đang hiệu lực trước, rồi lịch sử phân công / thu hồi (người, thời điểm).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TreasurerAssignmentResponse>>> List(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await treasurers.ListAsync(fundId, cancellationToken));

    /// <summary>Học sinh phải thuộc quỹ, có tài khoản đã kích hoạt (đã đổi mật khẩu tạm), chưa bị khóa, chưa là thủ quỹ.</summary>
    [HttpPost]
    [ProducesResponseType<TreasurerAssignedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Assign(Guid fundId, AssignTreasurerRequest request, CancellationToken cancellationToken)
    {
        var created = await treasurers.AssignAsync(fundId, request.StudentId!.Value, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Không thu hồi được thủ quỹ cuối cùng (422 <c>treasurer.last</c>): phân công người mới trước, thu hồi người cũ sau.</summary>
    [HttpPost("{assignmentId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid fundId, Guid assignmentId, CancellationToken cancellationToken)
    {
        await treasurers.RevokeAsync(fundId, assignmentId, cancellationToken);
        return NoContent();
    }
}
