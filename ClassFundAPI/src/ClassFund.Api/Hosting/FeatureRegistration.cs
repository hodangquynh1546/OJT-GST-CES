using System.Reflection;

namespace ClassFund.Api.Hosting;

/// <summary>
/// Tự đăng ký service của các module để hai người làm hai module không cùng sửa một file DI.
/// Quy ước: class cụ thể, tên kết thúc bằng <c>Service</c>, nằm trong namespace module
/// (<c>ClassFund.Application.Features.*</c>) → scoped; mỗi interface nó cài đặt (cùng assembly) trỏ về cùng instance.
/// </summary>
public static class FeatureRegistration
{
    public const string ApplicationFeaturesNamespace = "ClassFund.Application.Features";

    public static IServiceCollection AddFeatureServices(this IServiceCollection services, Assembly assembly, string namespacePrefix)
    {
        var types = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && type.Name.EndsWith("Service", StringComparison.Ordinal)
                && type.Namespace is not null
                && (type.Namespace == namespacePrefix || type.Namespace.StartsWith(namespacePrefix + ".", StringComparison.Ordinal)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var type in types)
        {
            services.AddScoped(type);
            foreach (var contract in type.GetInterfaces().Where(contract => contract.Assembly == assembly))
            {
                services.AddScoped(contract, provider => provider.GetRequiredService(type));
            }
        }

        return services;
    }
}
