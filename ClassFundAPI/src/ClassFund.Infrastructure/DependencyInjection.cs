using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClassFund.Application.Common.Persistence;
using ClassFund.Application.Common.Security;
using ClassFund.Application.Common.Storage;
using ClassFund.Infrastructure.Persistence;
using ClassFund.Infrastructure.Persistence.Seeding;
using ClassFund.Infrastructure.Security;
using ClassFund.Infrastructure.Storage;

namespace ClassFund.Infrastructure;

public static class DependencyInjection
{
    /// <param name="connectionString">Chuỗi kết nối tài khoản <c>app_user</c> (Neon) – không phải tài khoản migrator.</param>
    /// <param name="passwordPepper">Khóa bí mật <c>SECRET_KEY</c> trộn vào mật khẩu trước khi băm (≥ 32 ký tự).</param>
    /// <param name="storage">Cấu hình lưu file (section <c>Storage</c>).</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string passwordPepper,
        StorageOptions storage)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        services.AddScoped<AppDb>();
        services.AddScoped<IAppDb>(provider => provider.GetRequiredService<AppDb>());
        services.AddScoped<IDatabaseProbe>(provider => provider.GetRequiredService<AppDb>());

        services.AddScoped<ISessionStore, IdentityStore>();
        services.AddSingleton<IPasswordHasher>(new AspNetPasswordHasher(passwordPepper));
        services.AddScoped<IAccessService, AccessService>();
        services.AddSingleton<IFileStorage>(CreateFileStorage(storage));
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    private static IFileStorage CreateFileStorage(StorageOptions storage) => storage.Provider switch
    {
        StorageOptions.LocalProvider => new LocalFileStorage(storage.LocalRoot
            ?? throw new InvalidOperationException("Thiếu Storage:LocalRoot.")),

        // Neon Object Storage (S3-compatible) qua AWSSDK.S3 – OQ-16, OQ-17.
        StorageOptions.NeonProvider => NeonObjectStorage.Create(storage),

        _ => throw new InvalidOperationException($"Storage:Provider không hợp lệ: {storage.Provider}."),
    };
}
