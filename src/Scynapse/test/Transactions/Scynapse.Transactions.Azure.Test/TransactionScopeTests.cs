using Microsoft.Extensions.DependencyInjection;

using Scynapse.Transactions.TestKit.xUnit;

using Xunit;
using Xunit.Abstractions;

namespace Scynapse.Transactions.AzureStorage.Tests
{
    /// <summary>
    /// Tests for scoped transaction functionality with Azure Storage.
    /// </summary>
    [TestCategory("AzureStorage"), TestCategory("Transactions"), TestCategory("Functional")]
    public class TransactionScopeTests : ScopedTransactionsTestRunnerxUnit, IClassFixture<TestFixture>
    {
        public TransactionScopeTests(TestFixture fixture, ITestOutputHelper output)
            : base(fixture.GrainFactory, fixture.HostedCluster.ServiceProvider.GetRequiredService<ITransactionClient>(), output)
        {
            fixture.EnsurePreconditionsMet();
        }
    }
}
