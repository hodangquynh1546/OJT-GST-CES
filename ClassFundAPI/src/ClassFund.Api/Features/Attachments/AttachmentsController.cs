using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClassFund.Api.Authorization;
using ClassFund.Api.Common;
using ClassFund.Application.Features.Attachments;
using ClassFund.Domain.Authorization;

namespace ClassFund.Api.Features.Attachments;

/// <summary>M5 – Chứng từ, minh chứng, tài liệu của quỹ (FR-15, FR-17). File đi qua API, không lộ URL bucket.</summary>
[ApiVersion(1)]
[Route(ApiRoutes.Fund + "/attachments")]
public sealed class AttachmentsController(IAttachmentsService service) : ApiControllerBase
{
    /// <summary>
    /// Tải file lên (chưa gắn). Quyền theo loại kiểm trong service: Receipt / Document cần <c>Ledger.Manage</c>,
    /// PaymentProof cần <c>Payment.Submit</c>.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.FundView)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.MaxRequestBytes)]
    [ProducesResponseType<AttachmentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid fundId, [FromForm] UploadAttachmentRequest request, CancellationToken ct)
    {
        var file = request.File!;
        await using var content = file.OpenReadStream();
        var attachment = await service.UploadAsync(
            fundId, request.Kind!.Value, new UploadFileInput(file.FileName, file.ContentType ?? string.Empty, file.Length, content), ct);
        return StatusCode(StatusCodes.Status201Created, attachment);
    }

    /// <summary>Tải file về (kiểm AZ-05; không có quyền xem thì 404).</summary>
    [HttpGet("{attachmentId:guid}")]
    [RequirePermission(Permissions.FundView)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid fundId, Guid attachmentId, CancellationToken ct)
    {
        var download = await service.DownloadAsync(fundId, attachmentId, ct);
        return FileResults.Private(this, download);
    }
}
