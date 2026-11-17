using System.Text;
using ClassFund.Application.Common.Storage;
using ClassFund.Domain.Enums;
using ClassFund.Infrastructure.Storage;

namespace ClassFund.UnitTests.Infrastructure;

public class FileStorageTests
{
    [Fact]
    public void Storage_key_ignores_original_path_and_groups_by_kind_and_fund()
    {
        var fundId = Guid.NewGuid();
        var key = StorageKeys.Create(AttachmentKind.Receipt, fundId, "../../etc/passwd.PNG");
        Assert.StartsWith($"receipt/{fundId:N}/", key);
        Assert.EndsWith(".png", key);
        Assert.DoesNotContain("..", key);
    }

    [Fact]
    public void Avatar_key_is_not_fund_scoped() =>
        Assert.StartsWith("avatar/users/", StorageKeys.Create(AttachmentKind.Avatar, null, "me.jpg"));

    [Fact]
    public async Task Local_storage_round_trips_a_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "classfund-test-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalFileStorage(root);
        try
        {
            var key = await storage.SaveAsync(AttachmentKind.Document, Guid.NewGuid(), "a.pdf", "application/pdf", new MemoryStream(Encoding.UTF8.GetBytes("hello")), default);
            await using (var stream = await storage.OpenReadAsync(key, default))
            {
                Assert.NotNull(stream);
                Assert.Equal("hello", await new StreamReader(stream).ReadToEndAsync());
            }

            await storage.DeleteAsync(key, default);
            Assert.Null(await storage.OpenReadAsync(key, default));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Local_storage_rejects_keys_escaping_the_root() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LocalFileStorage(Path.GetTempPath()).OpenReadAsync("../outside.txt", default));
}
