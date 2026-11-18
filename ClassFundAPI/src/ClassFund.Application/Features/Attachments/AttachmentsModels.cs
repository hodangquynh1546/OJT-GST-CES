using ClassFund.Domain.Enums;

namespace ClassFund.Application.Features.Attachments;

/// <summary>Một file người dùng gửi lên (đọc từ multipart ở Api). <see cref="Content"/> do Api sở hữu và đóng.</summary>
public sealed record UploadFileInput(string FileName, string ContentType, long SizeBytes, Stream Content);

/// <summary>Metadata file đã tải lên; không bao giờ chứa khóa lưu trữ / URL bucket.</summary>
public sealed record AttachmentDto(Guid Id, AttachmentKind Kind, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

/// <summary>Nội dung file để Api stream về client. Người nhận phải dispose <see cref="Content"/>.</summary>
public sealed record FileDownload(Stream Content, string ContentType, string FileName);

public sealed record AvatarDto(Guid AttachmentId);

/// <summary>Giá trị đặc biệt của <c>Attachment.OwnerId</c>.</summary>
public static class AttachmentOwners
{
    /// <summary>
    /// File đã bị dọn (tải lên quá 24h không gắn, hoặc ảnh đại diện cũ bị thay): object đã xóa khỏi storage,
    /// dòng metadata giữ lại vì <c>app_user</c> không có quyền DELETE. Không tải, không gắn được nữa.
    /// </summary>
    public static readonly Guid Discarded = Guid.Empty;
}
