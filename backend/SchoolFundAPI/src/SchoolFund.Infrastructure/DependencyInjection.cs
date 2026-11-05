using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolFund.Application.Common.Persistence;
using SchoolFund.Application.Common.Security;
using SchoolFund.Infrastructure.Persistence;
using SchoolFund.Infrastructure.Security;

namespace SchoolFund.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

        services.AddScoped<AppDb>();
        services.AddScoped<IAppDb>(provider => provider.GetRequiredService<AppDb>());
        services.AddScoped<IDatabaseProbe>(provider => provider.GetRequiredService<AppDb>());

        services.AddScoped<ISessionStore, NoIdentityStore>();
        services.AddScoped<IAccessService, AccessService>();

        return services;
    }
}
