namespace ClassFund.Application.Features.Attachments;

public static class AttachmentsErrors
{
    /// <summary>Gắn file không tồn tại / không thuộc quỹ / đã gắn / không phải do mình tải / sai loại.</summary>
    public const string Unusable = "attachment.unusable";
    public const string UnusableMessage = "Chứng từ không hợp lệ hoặc đã được dùng. Vui lòng tải lên lại.";
}
