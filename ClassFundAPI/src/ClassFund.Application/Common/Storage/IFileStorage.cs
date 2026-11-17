using ClassFund.Domain.Enums;

namespace ClassFund.Application.Common.Storage;

/// <summary>
/// Port lưu file thực tế (ảnh hóa đơn, bill chuyển khoản, ảnh đại diện, tài liệu).
/// Cài đặt chính: Neon Object Storage (S3-compatible, OQ-16); <c>Local</c> chỉ dùng cho test / dev offline.
/// Chỉ trả về <c>storageKey</c> để lưu vào Attachment – không lộ URL/bucket ra client; tải file luôn đi qua API (kiểm quyền).
/// </summary>
public interface IFileStorage
{
    /// <param name="fundId">Null với ảnh đại diện.</param>
    /// <returns>Khóa lưu trữ, dạng <c>{kind}/{fundId|users}/{guid}{ext}</c>.</returns>
    Task<string> SaveAsync(AttachmentKind kind, Guid? fundId, string fileName, string contentType, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

/// <summary>Sinh khóa lưu trữ an toàn: không dùng tên file gốc làm đường dẫn (chống path traversal).</summary>
public static class StorageKeys
{
    public static string Create(AttachmentKind kind, Guid? fundId, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var safeExtension = extension.Length is > 1 and <= 6 && extension[1..].All(char.IsAsciiLetterOrDigit)
            ? extension.ToLowerInvariant()
            : string.Empty;
        var scope = fundId is { } id ? id.ToString("N") : "users";
        return $"{kind.ToString().ToLowerInvariant()}/{scope}/{Guid.CreateVersion7():N}{safeExtension}";
    }
}
