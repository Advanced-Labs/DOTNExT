using Microsoft.Extensions.Logging;
using Scynapse.EventSourcing.CustomStorage;
using Scynapse.Storage;
using Scynapse.TestingHost;
using TestExtensions;

namespace Tester.EventSourcingTests
{
    /// <summary>
    /// We use a special fixture for event sourcing tests 
    /// so we can add the required log consistency providers, and 
    /// do more tracing
    /// </summary>
    public class EventSourcingClusterFixture : PluginLoadingTestClusterFixture
    {
        protected override void ConfigureTestCluster(TestClusterBuilder builder)
        {
            base.ConfigureTestCluster(builder);
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        }

        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder hostBuilder)
            {
                // we use a slowed-down memory storage provider
                hostBuilder
                    .AddLogStorageBasedLogConsistencyProvider("LogStorage")
                    .AddStateStorageBasedLogConsistencyProvider("StateStorage")
                    .ConfigureLogging(builder =>
                    {
                        builder.AddFilter(typeof(MemoryGrainStorage).FullName, LogLevel.Debug);
                        builder.AddFilter(typeof(LogConsistencyProvider).Namespace, LogLevel.Debug);
                    })
                    .AddMemoryGrainStorageAsDefault()
                    .AddMemoryGrainStorage("MemoryStore")
                    .AddFaultInjectionMemoryStorage("SlowMemoryStore", options=>options.NumStorageGrains = 10, faultyOptions => faultyOptions.Latency = TimeSpan.FromMilliseconds(15));
            }
        }
    }
}
