using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Attachments;

namespace ClassFund.Api.Features.Attachments;

/// <summary>
/// M5 – Ảnh đại diện (FR-04). Chỉ cần đăng nhập (AuthorizeFilter mặc định); route nằm dưới <c>/auth/me</c> và <c>/users</c>
/// theo api-catalogue nhưng code thuộc module file (M5), không đụng AuthController của M1.
/// </summary>
[ApiVersion(1)]
[Route(ApiRoutes.Prefix)]
public sealed class AvatarController(IAvatarService service) : ApiControllerBase
{
    [HttpPut("auth/me/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimits.MaxAvatarRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.MaxAvatarRequestBytes)]
    [ProducesResponseType<AvatarDto>(StatusCodes.Status200OK)]
    public async Task<AvatarDto> SetMine([FromForm] UploadAvatarRequest request, CancellationToken ct)
    {
        var file = request.File!;
        await using var content = file.OpenReadStream();
        return await service.SetMineAsync(new UploadFileInput(file.FileName, file.ContentType ?? string.Empty, file.Length, content), ct);
    }

    /// <summary>Ảnh của mình hoặc người cùng quỹ; không có / không cùng quỹ → 404.</summary>
    [HttpGet("users/{userId:guid}/avatar")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct) =>
        FileResults.Private(this, await service.GetAsync(userId, ct));
}
