using ClassFund.Application.Common.Storage;
using ClassFund.Domain.Enums;

namespace ClassFund.Infrastructure.Storage;

/// <summary>
/// Lưu file trên đĩa – CHỈ cho test tự động và dev offline (<c>Storage:Provider = Local</c>).
/// Môi trường dùng chung lưu trên Neon Object Storage (<c>Storage:Provider = Neon</c>, task M5-T0).
/// </summary>
public sealed class LocalFileStorage(string rootPath) : IFileStorage
{
    public async Task<string> SaveAsync(AttachmentKind kind, Guid? fundId, string fileName, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var key = StorageKeys.Create(kind, fundId, fileName);
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        var root = Path.GetFullPath(rootPath);
        var full = Path.GetFullPath(Path.Combine(root, storageKey));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : throw new InvalidOperationException("Khóa lưu trữ không hợp lệ.");
    }
}
