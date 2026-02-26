using Microsoft.Extensions.DependencyInjection;
using Scynapse.Runtime;
using Scynapse.TestingHost;
using Scynapse.Transactions.TestKit;
using TestExtensions;

namespace Scynapse.Transactions.Tests
{
    public class MemoryTransactionsFixture : BaseTestClusterFixture
    {
        protected override void ConfigureTestCluster(TestClusterBuilder builder)
        {
            builder.AddSiloBuilderConfigurator<SiloBuilderConfigurator>();
        }

        public class SiloBuilderConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder hostBuilder)
            {
                hostBuilder
                    .ConfigureServices(services => services.AddKeyedSingleton<IRemoteCommitService, RemoteCommitService>(TransactionTestConstants.RemoteCommitService))
                    .AddMemoryGrainStorage(TransactionTestConstants.TransactionStore)
                    .UseTransactions();
            }
        }
    }

    public class SkewedClockMemoryTransactionsFixture : MemoryTransactionsFixture
    {
        protected override void ConfigureTestCluster(TestClusterBuilder builder)
        {
            builder.AddSiloBuilderConfigurator<SkewedClockConfigurator>();
            base.ConfigureTestCluster(builder);
        }
    }
}
