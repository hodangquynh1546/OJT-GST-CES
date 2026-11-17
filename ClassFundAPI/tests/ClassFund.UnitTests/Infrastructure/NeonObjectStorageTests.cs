using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Storage;
using Moq;

namespace ClassFund.UnitTests.Infrastructure;

/// <summary>M5-T0: adapter Neon Object Storage với S3 client giả – test tự động không gọi Neon thật.</summary>
public class NeonObjectStorageTests
{
    private const string Bucket = "classfund-files";
    private readonly Mock<IAmazonS3> _s3 = new();

    [Fact]
    public async Task Save_puts_object_in_bucket_with_generated_key_and_content_type()
    {
        PutObjectRequest? sent = null;
        _s3.Setup(client => client.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new PutObjectResponse());
        var fundId = Guid.NewGuid();
        var content = new MemoryStream(Encoding.UTF8.GetBytes("bill"));

        var key = await new NeonObjectStorage(_s3.Object, Bucket).SaveAsync(AttachmentKind.PaymentProof, fundId, "bill.jpg", "image/jpeg", content, default);

        Assert.NotNull(sent);
        Assert.Equal(Bucket, sent.BucketName);
        Assert.Equal(key, sent.Key);
        Assert.StartsWith($"paymentproof/{fundId:N}/", key);
        Assert.Equal("image/jpeg", sent.ContentType);
        Assert.False(sent.AutoCloseStream);
    }

    [Fact]
    public async Task Open_returns_content_copied_to_memory()
    {
        _s3.Setup(client => client.GetObjectAsync(Bucket, "k", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes("hello")) });

        await using var stream = await new NeonObjectStorage(_s3.Object, Bucket).OpenReadAsync("k", default);

        Assert.NotNull(stream);
        Assert.Equal("hello", await new StreamReader(stream).ReadToEndAsync());
    }

    [Fact]
    public async Task Open_missing_object_returns_null()
    {
        _s3.Setup(client => client.GetObjectAsync(Bucket, "missing", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("not found") { StatusCode = HttpStatusCode.NotFound });

        Assert.Null(await new NeonObjectStorage(_s3.Object, Bucket).OpenReadAsync("missing", default));
    }

    [Fact]
    public async Task Other_s3_errors_are_not_swallowed()
    {
        _s3.Setup(client => client.GetObjectAsync(Bucket, "k", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("denied") { StatusCode = HttpStatusCode.Forbidden });

        await Assert.ThrowsAsync<AmazonS3Exception>(() => new NeonObjectStorage(_s3.Object, Bucket).OpenReadAsync("k", default));
    }

    [Fact]
    public async Task Delete_removes_object_from_bucket()
    {
        _s3.Setup(client => client.DeleteObjectAsync(Bucket, "k", It.IsAny<CancellationToken>())).ReturnsAsync(new DeleteObjectResponse());

        await new NeonObjectStorage(_s3.Object, Bucket).DeleteAsync("k", default);

        _s3.Verify(client => client.DeleteObjectAsync(Bucket, "k", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Create_requires_endpoint_bucket_and_keys()
    {
        var error = Assert.Throws<InvalidOperationException>(() => NeonObjectStorage.Create(new StorageOptions { Provider = StorageOptions.NeonProvider }));
        Assert.Contains("Storage:Endpoint", error.Message);
        Assert.Contains("Storage:SecretAccessKey", error.Message);
    }

    [Fact]
    public void Create_with_full_options_builds_client_without_network_call() =>
        Assert.NotNull(NeonObjectStorage.Create(new StorageOptions
        {
            Provider = StorageOptions.NeonProvider,
            Endpoint = "https://storage.example.invalid",
            Bucket = Bucket,
            Region = "us-east-1",
            AccessKeyId = "id",
            SecretAccessKey = "secret",
        }));
}
