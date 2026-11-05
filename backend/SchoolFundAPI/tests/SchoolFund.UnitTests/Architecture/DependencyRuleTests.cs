using System.Reflection;
using SchoolFund.Api.Hosting;
using SchoolFund.Application.Common.Security;
using SchoolFund.Domain.Authorization;
using SchoolFund.Infrastructure;

namespace SchoolFund.UnitTests.Architecture;

public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(FundRole).Assembly;
    private static readonly Assembly Application = typeof(ICurrentUser).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(ServiceRegistration).Assembly;

    private static string[] References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).ToArray();

    private static bool IsRuntime(string name) =>
        name is "netstandard" or "mscorlib" || name.StartsWith("System", StringComparison.Ordinal);

    [Fact]
    public void Domain_depends_on_nothing()
    {
        Assert.All(References(Domain), name => Assert.True(IsRuntime(name), name));
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        Assert.All(References(Application), name => Assert.True(IsRuntime(name) || name == Domain.GetName().Name, name));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        Assert.DoesNotContain(Api.GetName().Name!, References(Infrastructure));
    }

    [Fact]
    public void Api_does_not_use_EF_Core_directly()
    {
        Assert.DoesNotContain(References(Api), name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
