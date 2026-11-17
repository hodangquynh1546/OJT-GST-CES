using System.Reflection;
using ClassFund.Api.Hosting;
using ClassFund.Application.Common.Auditing;
using ClassFund.Domain.Common;
using ClassFund.Infrastructure.Persistence;

namespace ClassFund.UnitTests.Architecture;

/// <summary>Quy tắc phụ thuộc Clean Architecture (training-doc.md mục 2c).</summary>
public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Entity).Assembly;
    private static readonly Assembly Application = typeof(AuditTrail).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(ServiceRegistration).Assembly;

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!).Where(name => name.StartsWith("ClassFund", StringComparison.Ordinal));

    [Fact]
    public void Domain_references_nothing_of_the_solution() => Assert.Empty(References(Domain));

    [Fact]
    public void Application_references_only_Domain() => Assert.Equal(["ClassFund.Domain"], References(Application));

    [Fact]
    public void Infrastructure_references_Application_and_Domain_only() =>
        Assert.Empty(References(Infrastructure).Except(["ClassFund.Application", "ClassFund.Domain"]));

    [Fact]
    public void Api_does_not_use_EntityFrameworkCore_directly() =>
        Assert.DoesNotContain(Api.GetReferencedAssemblies(), name => name.Name == "Microsoft.EntityFrameworkCore");
}
