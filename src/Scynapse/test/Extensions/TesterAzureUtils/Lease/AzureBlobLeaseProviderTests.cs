using Xunit.Abstractions;
using Scynapse.LeaseProviders;
using TestExtensions.Runners;
using Scynapse.Configuration;
using Microsoft.Extensions.Options;

namespace Tester.AzureUtils.Lease
{
    [TestCategory("Functional"), TestCategory("AzureStorage"), TestCategory("Lease")]
    public class AzureBlobLeaseProviderTests : GoldenPathLeaseProviderTestRunner
    {
        public AzureBlobLeaseProviderTests(ITestOutputHelper output)
            :base(CreateLeaseProvider(), output)
        {
        }

        private static ILeaseProvider CreateLeaseProvider()
        {
            TestUtils.CheckForAzureStorage();
            return new AzureBlobLeaseProvider(Options.Create(new AzureBlobLeaseProviderOptions()
            {
                BlobContainerName = "test-blob-container-name"
            }.ConfigureTestDefaults()));
        }
    }
}

