namespace ClassFund.Infrastructure.Storage;

/// <summary>
/// Cấu hình lưu file (section <c>Storage</c>). Khóa bí mật đặt bằng user-secrets / biến môi trường, không commit.
/// Neon Object Storage là S3-compatible: dùng endpoint của branch, bắt buộc path-style addressing.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public const string LocalProvider = "Local";
    public const string NeonProvider = "Neon";

    /// <summary><c>Neon</c> (mặc định cho môi trường dùng chung) hoặc <c>Local</c> (test / dev offline).</summary>
    public string Provider { get; set; } = LocalProvider;

    /// <summary>Thư mục gốc khi Provider = Local.</summary>
    public string? LocalRoot { get; set; }

    /// <summary>S3 endpoint của branch Neon (Neon Console → Object Storage).</summary>
    public string? Endpoint { get; set; }

    public string? Bucket { get; set; }

    public string? Region { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }
}
