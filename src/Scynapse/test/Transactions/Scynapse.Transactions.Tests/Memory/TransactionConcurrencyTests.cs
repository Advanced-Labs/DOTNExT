using Scynapse.Transactions.TestKit.xUnit;
using Xunit.Abstractions;
using Xunit;

namespace Scynapse.Transactions.Tests
{

    [TestCategory("BVT"), TestCategory("Transactions")]
    public class TransactionConcurrencyTests : TransactionConcurrencyTestRunnerxUnit, IClassFixture<MemoryTransactionsFixture>
    {
        public TransactionConcurrencyTests(MemoryTransactionsFixture fixture, ITestOutputHelper output)
            : base(fixture.GrainFactory, output)
        {
        }
    }
}
