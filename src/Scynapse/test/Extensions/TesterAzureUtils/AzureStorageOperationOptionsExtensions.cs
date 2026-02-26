using Azure.Core.Diagnostics;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using TestExtensions;

namespace Tester.AzureUtils
{
    public static class AzureStorageOperationOptionsExtensions
    {
        public static DefaultAzureCredential Credential = new DefaultAzureCredential();

        public static Scynapse.Clustering.AzureStorage.AzureStorageOperationOptions ConfigureTestDefaults(this Scynapse.Clustering.AzureStorage.AzureStorageOperationOptions options)
        {
            options.TableServiceClient = GetTableServiceClient();

            return options;
        }

        public static TableServiceClient GetTableServiceClient()
        {
            return TestDefaultConfiguration.UseAadAuthentication
                ? new(TestDefaultConfiguration.TableEndpoint, TestDefaultConfiguration.TokenCredential)
                : new(TestDefaultConfiguration.DataConnectionString);
        }

        public static Scynapse.GrainDirectory.AzureStorage.AzureStorageOperationOptions ConfigureTestDefaults(this Scynapse.GrainDirectory.AzureStorage.AzureStorageOperationOptions options)
        {
            options.TableServiceClient = GetTableServiceClient();

            return options;
        }

        public static Scynapse.Persistence.AzureStorage.AzureStorageOperationOptions ConfigureTestDefaults(this Scynapse.Persistence.AzureStorage.AzureStorageOperationOptions options)
        {
            options.TableServiceClient = GetTableServiceClient();

            return options;
        }

        public static Scynapse.Reminders.AzureStorage.AzureStorageOperationOptions ConfigureTestDefaults(this Scynapse.Reminders.AzureStorage.AzureStorageOperationOptions options)
        {
            options.TableServiceClient = GetTableServiceClient();

            return options;
        }

        public static Scynapse.Configuration.AzureBlobStorageOptions ConfigureTestDefaults(this Scynapse.Configuration.AzureBlobStorageOptions options)
        {
            if (TestDefaultConfiguration.UseAadAuthentication)
            {
                options.BlobServiceClient = new(TestDefaultConfiguration.DataBlobUri, TestDefaultConfiguration.TokenCredential);
            }
            else
            {
                options.BlobServiceClient = new(TestDefaultConfiguration.DataConnectionString);
            }

            return options;
        }

        public static AzureStorageJobShardOptions ConfigureTestDefaults(this AzureStorageJobShardOptions options)
        {
            if (TestDefaultConfiguration.UseAadAuthentication)
            {
                options.BlobServiceClient = new(TestDefaultConfiguration.DataBlobUri, TestDefaultConfiguration.TokenCredential);
            }
            else
            {
                options.BlobServiceClient = new(TestDefaultConfiguration.DataConnectionString);
            }

            return options;
        }

        public static Scynapse.Configuration.AzureQueueOptions ConfigureTestDefaults(this Scynapse.Configuration.AzureQueueOptions options)
        {
            if (TestDefaultConfiguration.UseAadAuthentication)
            {
                options.QueueServiceClient = new(TestDefaultConfiguration.DataQueueUri, TestDefaultConfiguration.TokenCredential);
            }
            else
            {
                options.QueueServiceClient = new(TestDefaultConfiguration.DataConnectionString);
            }

            return options;
        }

        public static Scynapse.Configuration.AzureBlobLeaseProviderOptions ConfigureTestDefaults(this Scynapse.Configuration.AzureBlobLeaseProviderOptions options)
        {
            if (TestDefaultConfiguration.UseAadAuthentication)
            {
                options.BlobServiceClient = new(TestDefaultConfiguration.DataBlobUri, TestDefaultConfiguration.TokenCredential);
            }
            else
            {
                options.BlobServiceClient = new(TestDefaultConfiguration.DataConnectionString);
            }

            return options;
        }
    }
}
