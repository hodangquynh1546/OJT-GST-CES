using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Api.ErrorHandling;
using ClassFund.Application.Features.Students;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Students;

/// <summary>
/// M3 – học sinh & phụ huynh của quỹ, chỉ GVCN (backend-api.md mục Students; FR-24, FR-25, FR-26).
/// Mọi response <c>Cache-Control: no-store</c> vì nhiều response chứa mật khẩu tạm (NFR-11).
/// </summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/students")]
[RequirePermission(Permissions.StudentManage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentsController(IStudentsService students) : ApiControllerBase
{
    /// <summary>Danh sách: tên đăng nhập, trạng thái tài khoản, thủ quỹ, phụ huynh đang liên kết.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StudentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudentResponse>>> List(Guid fundId, CancellationToken cancellationToken) =>
        Ok(await students.ListAsync(fundId, cancellationToken));

    /// <summary>Thêm học sinh (chưa có tài khoản); có email phụ huynh thì liên kết luôn, có thể kèm mật khẩu tạm phụ huynh.</summary>
    [HttpPost]
    [ProducesResponseType<StudentCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<IActionResult> Create(Guid fundId, CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var created = await students.CreateAsync(fundId, request.FullName!, request.ParentEmail, request.ParentFullName, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentResponse>> Get(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        Ok(await students.GetAsync(fundId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<IActionResult> Rename(Guid fundId, Guid id, UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        await students.RenameAsync(fundId, id, request.FullName!, cancellationToken);
        return NoContent();
    }

    /// <summary>Cấp tài khoản: tên đăng nhập (bỏ trống → gợi ý) + mật khẩu tạm hiển thị một lần.</summary>
    [HttpPost("{id:guid}/account")]
    [ProducesResponseType<CredentialResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<ActionResult<CredentialResponse>> IssueAccount(Guid fundId, Guid id, IssueAccountRequest request, CancellationToken cancellationToken) =>
        Ok(await students.IssueAccountAsync(fundId, id, request.Username, cancellationToken));

    /// <summary>Cấp lại mật khẩu tạm; học sinh bị đăng xuất mọi nơi.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType<CredentialResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<ActionResult<CredentialResponse>> ResetPassword(Guid fundId, Guid id, CancellationToken cancellationToken) =>
        Ok(await students.ResetPasswordAsync(fundId, id, cancellationToken));

    /// <summary>Liên kết phụ huynh theo email. Hệ thống không gửi email (<c>emailSent = false</c>).</summary>
    [HttpPost("{id:guid}/parent")]
    [ProducesResponseType<ParentLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ApiProblems.ContentType)]
    public async Task<ActionResult<ParentLinkResponse>> LinkParent(Guid fundId, Guid id, LinkParentRequest request, CancellationToken cancellationToken) =>
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
