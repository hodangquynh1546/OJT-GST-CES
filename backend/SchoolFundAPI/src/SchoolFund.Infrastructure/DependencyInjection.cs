using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Application.Common.Security;
using SchoolFund.Infrastructure.Persistence;
using SchoolFund.Infrastructure.Security;

namespace SchoolFund.Infrastructure;

public static class DependencyInjection
{
    /// <param name="passwordPepper">Khóa bí mật <c>SECRET_KEY</c> trộn vào mật khẩu trước khi băm (≥ 32 ký tự).</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string passwordPepper)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        services.AddScoped<AppDb>();
        services.AddScoped<IAppDb>(provider => provider.GetRequiredService<AppDb>());
        services.AddScoped<IDatabaseProbe>(provider => provider.GetRequiredService<AppDb>());

        services.AddScoped<ISessionStore, IdentityStore>();
        services.AddSingleton<IPasswordHasher>(new AspNetPasswordHasher(passwordPepper));
        services.AddScoped<IAccessService, AccessService>();

        return services;
    }
}
