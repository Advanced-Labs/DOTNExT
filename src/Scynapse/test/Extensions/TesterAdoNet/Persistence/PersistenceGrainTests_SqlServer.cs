using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Scynapse.TestingHost;
using Scynapse.Tests.SqlUtils;
using TestExtensions;
using TestExtensions.Runners;
using UnitTests.General;
using Xunit;
using Xunit.Abstractions;

namespace Tester.AdoNet.Persistence
{
    /// <summary>
    /// Tests for Scynapse grain state persistence functionality using SQL Server as the storage provider.
    /// </summary>
    [TestCategory("Persistence"), TestCategory("SqlServer")]
    public class PersistenceGrainTests_SqlServer : GrainPersistenceTestsRunner, IClassFixture<PersistenceGrainTests_SqlServer.Fixture>
    {
        public const string TestDatabaseName = "ScynapseTest_SqlServer_Storage";
        public static string AdoInvariant = AdoNetInvariants.InvariantNameSqlServer;
        public static string ConnectionStringKey = "AdoNetConnectionString";
        public class Fixture : BaseTestClusterFixture
        {
            protected override void CheckPreconditionsOrThrow()
            {
                if (string.IsNullOrEmpty(TestDefaultConfiguration.MsSqlConnectionString))
                {
                    throw new SkipException("SQL Server connection string is not specified.");
                }
            }

            protected override void ConfigureTestCluster(TestClusterBuilder builder)
            {
                var relationalStorage = RelationalStorageForTesting.SetupInstance(AdoInvariant, TestDatabaseName).Result;
                builder.ConfigureHostConfiguration(configBuilder => configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        {ConnectionStringKey, relationalStorage.CurrentConnectionString}
                    }));
                builder.AddSiloBuilderConfigurator<MySiloBuilderConfigurator>();
            }

            private class MySiloBuilderConfigurator : IHostConfigurator
            {
                public void Configure(IHostBuilder hostBuilder)
                {
                    var connectionString = hostBuilder.GetConfiguration()[ConnectionStringKey];
                    hostBuilder.UseScynapse((ctx, siloBuilder) =>
                    {
                        siloBuilder
                            .AddAdoNetGrainStorage("GrainStorageForTest", options =>
                            {
                                options.ConnectionString = connectionString;
                                options.Invariant = AdoInvariant;
                            })
                            .AddMemoryGrainStorage("MemoryStore");
                    });
                }
            }
        }

        public PersistenceGrainTests_SqlServer(ITestOutputHelper output, Fixture fixture) : base(output, fixture)
        {
            DistinguishesGenericGrainTypeParameters = false;
            fixture.EnsurePreconditionsMet();
        }
    }
}
