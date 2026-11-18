namespace ClassFund.Application.Features.Attachments;

/// <summary>
/// Đối chiếu vài byte đầu với Content-Type khai báo: chặn file đổi đuôi / khai sai loại
/// (ví dụ file .exe gửi kèm Content-Type image/png). Bổ sung cho <c>FileRule</c> (BR-07).
/// </summary>
public static class FileSignature
{
    public const int HeaderLength = 12;

    public static bool Matches(string contentType, ReadOnlySpan<byte> header) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
        "image/png" => header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "image/webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        "application/pdf" => header.StartsWith("%PDF-"u8),

        // docx / xlsx là gói ZIP.
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
            header.StartsWith((ReadOnlySpan<byte>)[0x50, 0x4B, 0x03, 0x04]),
        _ => false,
    };

    /// <summary>Đọc tối đa <see cref="HeaderLength"/> byte đầu rồi tua lại đầu stream (stream phải seek được).</summary>
    public static async Task<byte[]> ReadHeaderAsync(Stream content, CancellationToken cancellationToken)
    {
        var buffer = new byte[HeaderLength];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await content.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        content.Position = 0;
        return buffer[..read];
    }
}
