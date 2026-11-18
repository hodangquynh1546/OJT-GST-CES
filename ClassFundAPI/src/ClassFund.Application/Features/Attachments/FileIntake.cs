using ClassFund.Application.Common.Storage;
using ClassFund.Domain.Enums;
using ClassFund.Domain.Exceptions;
using ClassFund.Domain.Rules;

namespace ClassFund.Application.Features.Attachments;

/// <summary>Kiểm tra file theo loại (BR-07) rồi lưu lên storage. Dùng chung cho chứng từ và ảnh đại diện.</summary>
internal static class FileIntake
{
    public const int FileNameMaxLength = 255;

    /// <returns>Khóa lưu trữ (ngẫu nhiên, không chứa tên file gốc).</returns>
    public static async Task<string> ValidateAndSaveAsync(
        IFileStorage storage,
        AttachmentKind kind,
        Guid? fundId,
        UploadFileInput file,
        CancellationToken cancellationToken)
    {
        var contentType = file.ContentType.Trim().ToLowerInvariant();
        FileRule.EnsureAllowed(kind, contentType, file.SizeBytes);

        var content = file.Content;
        MemoryStream? buffered = null;
        try
        {
            if (!content.CanSeek)
            {
                buffered = new MemoryStream();
                await content.CopyToAsync(buffered, cancellationToken);
                buffered.Position = 0;
                content = buffered;
            }

            // Dung lượng thật (không tin Content-Length) và chữ ký đầu file phải khớp loại khai báo.
            if (content.Length != file.SizeBytes)
            {
                FileRule.EnsureAllowed(kind, contentType, content.Length);
            }

            var header = await FileSignature.ReadHeaderAsync(content, cancellationToken);
            if (!FileSignature.Matches(contentType, header))
            {
                throw new BusinessRuleException(FileRule.InvalidFileCode, "Nội dung file không khớp loại file. " + $"File phải là {FileRule.Limits[kind].Description}.");
            }

            return await storage.SaveAsync(kind, fundId, file.FileName, contentType, content, cancellationToken);
        }
        finally
        {
            if (buffered is not null)
            {
                await buffered.DisposeAsync();
            }
        }
    }

    /// <summary>Tên hiển thị khi tải về: bỏ đường dẫn, ký tự điều khiển; cắt theo độ dài cột.</summary>
    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (name.Length == 0)
        {
            name = "file";
        }

        return name.Length <= FileNameMaxLength ? name : name[^FileNameMaxLength..];
    }

    /// <summary>Xóa object không còn dùng; lỗi storage không làm hỏng thao tác chính (object mồ côi chỉ tốn chỗ).</summary>
    public static async Task TryDeleteAsync(IFileStorage storage, IEnumerable<string> storageKeys, CancellationToken cancellationToken)
    {
        foreach (var key in storageKeys)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Bỏ qua: metadata đã đánh dấu Discarded nên file không còn truy cập được qua API.
            }
        }
    }
}
