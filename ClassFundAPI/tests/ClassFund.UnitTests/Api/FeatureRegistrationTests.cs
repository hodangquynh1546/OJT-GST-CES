using Microsoft.Extensions.DependencyInjection;
using ClassFund.Api.Hosting;
using ClassFund.UnitTests.Api.FakeFeatures.Sample;

namespace ClassFund.UnitTests.Api
{
    public class FeatureRegistrationTests
    {
        private const string Prefix = "ClassFund.UnitTests.Api.FakeFeatures";

        private static ServiceProvider Build() =>
            new ServiceCollection()
                .AddFeatureServices(typeof(FeatureRegistrationTests).Assembly, Prefix)
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        [Fact]
        public void Registers_services_by_convention_with_their_interfaces_as_one_scoped_instance()
        {
            using var provider = Build();
            using var scope = provider.CreateScope();

            var service = scope.ServiceProvider.GetRequiredService<SampleService>();
            Assert.Same(service, scope.ServiceProvider.GetRequiredService<ISampleService>());

            using var other = provider.CreateScope();
            Assert.NotSame(service, other.ServiceProvider.GetRequiredService<SampleService>());
        }

        [Fact]
        public void Ignores_types_not_matching_the_convention()
        {
            using var provider = Build();
            Assert.Null(provider.GetService<SampleHelper>());
            Assert.Null(provider.GetService<AbstractSampleService>());
            Assert.Null(provider.GetService<OutsideService>());
        }

        [Fact]
        public void Application_features_namespace_is_the_module_root()
        {
            Assert.Equal("ClassFund.Application.Features", FeatureRegistration.ApplicationFeaturesNamespace);
        }
    }

    public sealed class OutsideService;
}

namespace ClassFund.UnitTests.Api.FakeFeatures.Sample
{
    public interface ISampleService;

    public sealed class SampleService : ISampleService;

    public sealed class SampleHelper;

    public abstract class AbstractSampleService;
}
