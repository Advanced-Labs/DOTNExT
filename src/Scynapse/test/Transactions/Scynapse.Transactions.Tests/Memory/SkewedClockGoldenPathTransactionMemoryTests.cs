using Scynapse.Transactions.TestKit.xUnit;
using Xunit.Abstractions;
using Xunit;

namespace Scynapse.Transactions.Tests
{
    /// <summary>
    /// Tests for transaction golden path scenarios with skewed clocks using in-memory storage.
    /// </summary>
    [TestCategory("BVT"), TestCategory("Transactions")]
    public class SkewedClockGoldenPathTransactionMemoryTests : GoldenPathTransactionTestRunnerxUnit, IClassFixture<SkewedClockMemoryTransactionsFixture>
    {
        public SkewedClockGoldenPathTransactionMemoryTests(SkewedClockMemoryTransactionsFixture fixture, ITestOutputHelper output)
            : base(fixture.GrainFactory, output)
        {
        }
    }
}
