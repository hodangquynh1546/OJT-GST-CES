using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using ClassFund.Application.Features.Attachments;

namespace ClassFund.Api.Features.Attachments;

internal static class FileResults
{
    /// <summary>
    /// Trả file kèm tên gốc (Content-Disposition: attachment), không cho trình duyệt đoán loại,
    /// không cache ở proxy dùng chung (file có kiểm quyền).
    /// </summary>
    public static FileStreamResult Private(ControllerBase controller, FileDownload download)
    {
        var headers = controller.Response.Headers;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers[HeaderNames.CacheControl] = "private, no-store";
        return controller.File(download.Content, download.ContentType, download.FileName);
    }
}
