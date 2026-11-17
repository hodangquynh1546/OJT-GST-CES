using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ClassFund.Application.Common.Storage;
using ClassFund.Domain.Enums;

namespace ClassFund.Infrastructure.Storage;

/// <summary>
/// Lưu file thực tế trên Neon Object Storage (S3-compatible, OQ-16 / OQ-17) bằng <c>AWSSDK.S3</c>.
/// Chỉ dùng PutObject / GetObject / DeleteObject (Neon chỉ hỗ trợ một phần S3 API). Bucket để private;
/// file luôn được tải qua API ClassFund (kiểm quyền), không phát URL bucket ra client.
/// </summary>
public sealed class NeonObjectStorage(IAmazonS3 client, string bucket) : IFileStorage
{
    public async Task<string> SaveAsync(AttachmentKind kind, Guid? fundId, string fileName, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var key = StorageKeys.Create(kind, fundId, fileName);
        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);
        return key;
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetObjectAsync(bucket, storageKey, cancellationToken);

            // File ≤ 10 MB (FileRule): chép ra bộ nhớ để giải phóng kết nối HTTP ngay, người gọi tự dispose stream.
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return buffer;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
        client.DeleteObjectAsync(bucket, storageKey, cancellationToken);

    /// <summary>Tạo S3 client trỏ tới endpoint Neon: path-style bắt buộc; checksum chỉ khi API yêu cầu (tương thích S3-compatible).</summary>
    public static NeonObjectStorage Create(StorageOptions options)
    {
        var missing = new (string Name, string? Value)[]
            {
                (nameof(options.Endpoint), options.Endpoint),
                (nameof(options.Bucket), options.Bucket),
                (nameof(options.AccessKeyId), options.AccessKeyId),
                (nameof(options.SecretAccessKey), options.SecretAccessKey),
            }
            .Where(item => string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{StorageOptions.SectionName}:{item.Name}")
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Thiếu cấu hình Neon Object Storage: {string.Join(", ", missing)}. Đặt bằng user-secrets (docs/implementation/neon-database-guide.md mục 3b).");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (!string.IsNullOrWhiteSpace(options.Region))
        {
            config.AuthenticationRegion = options.Region;
        }

        var client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), config);
        return new NeonObjectStorage(client, options.Bucket!);
    }
}
