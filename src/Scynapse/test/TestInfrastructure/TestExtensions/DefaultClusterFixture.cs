using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scynapse.Configuration;
using Scynapse.TestingHost;

namespace TestExtensions
{
    public class DefaultClusterFixture : PluginLoadingTestClusterFixture
    {
        static DefaultClusterFixture()
        {
            TestDefaultConfiguration.InitializeDefaults();
        }

        protected override void ConfigureTestCluster(TestClusterBuilder builder)
        {
            base.ConfigureTestCluster(builder);
            builder.AddSiloBuilderConfigurator<SiloHostConfigurator>();
        }

        public class SiloHostConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder hostBuilder)
            {
                hostBuilder
                    .Configure<SiloMessagingOptions>(o => o.ClientGatewayShutdownNotificationTimeout = default)
                    .UseInMemoryReminderService()
                    .UseInMemoryDurableJobs()
                    .AddMemoryGrainStorageAsDefault()
                    .AddMemoryGrainStorage("MemoryStore");
            }
        }
    }
}
