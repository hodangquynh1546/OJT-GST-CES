using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SchoolFund.Api.Authorization;
using SchoolFund.Application.Features.Students;
using SchoolFund.Domain.Authorization;

namespace SchoolFund.Api.Features.Students;

/// <summary>
/// Học sinh & phụ huynh của quỹ – chỉ GVCN (docs/api/backend-api.md mục Students; FR-24, FR-25, FR-26).
/// Mọi response <c>Cache-Control: no-store</c> vì nhiều response chứa mật khẩu tạm (NFR-11).
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/funds/{fundId:guid}/students")]
[RequirePermission(Permissions.StudentManage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentsController(IStudentService students) : ControllerBase
{
    /// <summary>Danh sách: tên đăng nhập, trạng thái tài khoản, thủ quỹ, phụ huynh đang liên kết.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudentResponse>>> List(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await students.ListAsync(fundId, cancellationToken));

    /// <summary>Thêm học sinh (chưa có tài khoản); có email phụ huynh thì liên kết luôn, có thể kèm mật khẩu tạm phụ huynh.</summary>
    [HttpPost]
    [ProducesResponseType<StudentCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid fundId, CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var created = await students.CreateAsync(
            fundId,
            request.FullName!,
            request.ParentEmail,
            request.ParentFullName,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudentResponse>> Get(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        Ok(await students.GetAsync(fundId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Rename(Guid fundId, Guid id, UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        await students.RenameAsync(fundId, id, request.FullName!, cancellationToken);
        return NoContent();
    }

    /// <summary>Cấp tài khoản: tên đăng nhập (bỏ trống → gợi ý) + mật khẩu tạm hiển thị một lần.</summary>
    [HttpPost("{id:guid}/account")]
    public async Task<ActionResult<CredentialResponse>> IssueAccount(
        Guid fundId,
        Guid id,
        IssueAccountRequest request,
        CancellationToken cancellationToken) =>
        Ok(await students.IssueAccountAsync(fundId, id, request.Username, cancellationToken));

    /// <summary>Cấp lại mật khẩu tạm; học sinh bị đăng xuất mọi nơi.</summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<CredentialResponse>> ResetPassword(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        Ok(await students.ResetPasswordAsync(fundId, id, cancellationToken));

    /// <summary>Liên kết phụ huynh theo email. Hệ thống không gửi email (<c>emailSent = false</c>).</summary>
    [HttpPost("{id:guid}/parent")]
    public async Task<ActionResult<ParentLinkResponse>> LinkParent(
        Guid fundId,
        Guid id,
        LinkParentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await students.LinkParentAsync(fundId, id, request.Email!, request.FullName, cancellationToken));

    /// <summary>Gỡ liên kết (đánh dấu, giữ lịch sử); tài khoản phụ huynh vẫn còn.</summary>
    [HttpPost("{id:guid}/parent/unlink")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnlinkParent(Guid fundId, Guid id, CancellationToken cancellationToken)
    {
        await students.UnlinkParentAsync(fundId, id, cancellationToken);
        return NoContent();
    }
}
