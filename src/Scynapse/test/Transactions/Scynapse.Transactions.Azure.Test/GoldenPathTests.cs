using Scynapse.Transactions.TestKit.xUnit;
using Xunit;
using Xunit.Abstractions;

namespace Scynapse.Transactions.AzureStorage.Tests
{
    /// <summary>
    /// Tests for transaction golden path scenarios with Azure Storage.
    /// </summary>
    [TestCategory("AzureStorage"), TestCategory("Transactions"), TestCategory("Functional")]
    public class GoldenPathTests : GoldenPathTransactionTestRunnerxUnit, IClassFixture<TestFixture>
    {
        public GoldenPathTests(TestFixture fixture, ITestOutputHelper output)
            : base(fixture.GrainFactory, output)
        {
            fixture.EnsurePreconditionsMet();
        }
    }
}
