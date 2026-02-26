using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Runtime;
using Scynapse.Runtime.Placement;
using Scynapse.TestingHost;
using UnitTests.GrainInterfaces;
using UnitTests.Grains;

namespace TestVersionGrains
{
    public class VersionGrainsSiloBuilderConfigurator : IHostConfigurator
    {
        public void Configure(IHostBuilder hostBuilder)
        {
            var cfg = hostBuilder.GetConfiguration();
            var siloCount = int.Parse(cfg["SiloCount"]);
            hostBuilder.UseScynapse((ctx, siloBuilder) =>
            {
                siloBuilder.Configure<SiloMessagingOptions>(options => options.AssumeHomogenousSilosForTesting = false);
                siloBuilder.Configure<GrainVersioningOptions>(options =>
                {
                    options.DefaultCompatibilityStrategy = cfg["CompatibilityStrategy"];
                    options.DefaultVersionSelectorStrategy = cfg["VersionSelectorStrategy"];
                });

                siloBuilder.ConfigureServices(ConfigureServices)
                    .AddMemoryGrainStorageAsDefault();
            });
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddPlacementDirector<VersionAwarePlacementStrategy, VersionAwarePlacementDirector>();
        }
    }
}
