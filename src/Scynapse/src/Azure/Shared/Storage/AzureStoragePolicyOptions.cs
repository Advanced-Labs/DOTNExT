using System;
using System.Threading;

#if SCYNAPSE_CLUSTERING
namespace Scynapse.Clustering.AzureStorage
#elif SCYNAPSE_PERSISTENCE
namespace Scynapse.Persistence.AzureStorage
#elif SCYNAPSE_REMINDERS
namespace Scynapse.Reminders.AzureStorage
#elif SCYNAPSE_STREAMING
namespace Scynapse.Streaming.AzureStorage
#elif SCYNAPSE_EVENTHUBS
namespace Scynapse.Streaming.EventHubs
#elif TESTER_AZUREUTILS
namespace Scynapse.Tests.AzureUtils
#elif SCYNAPSE_TRANSACTIONS
namespace Scynapse.Transactions.AzureStorage
#elif SCYNAPSE_DIRECTORY
namespace Scynapse.GrainDirectory.AzureStorage
#else
// No default namespace intentionally to cause compile errors if something is not defined
#endif
{
    public class AzureStoragePolicyOptions
    {
        private TimeSpan? creationTimeout;
        private TimeSpan? operationTimeout;

        public int MaxBulkUpdateRows { get; set; } = 100;
        public int MaxCreationRetries { get; set; } = 60;
        public int MaxOperationRetries { get; set; } = 5;

        public TimeSpan PauseBetweenCreationRetries { get; set; } = TimeSpan.FromSeconds(1);

        public TimeSpan PauseBetweenOperationRetries { get; set; } = TimeSpan.FromMilliseconds(100);

        public TimeSpan CreationTimeout
        {
            get => this.creationTimeout ?? TimeSpan.FromMilliseconds(this.PauseBetweenCreationRetries.TotalMilliseconds * this.MaxCreationRetries * 3);
            set => SetIfValidTimeout(ref this.creationTimeout, value, nameof(CreationTimeout));
        }

        public TimeSpan OperationTimeout
        {
            get => this.operationTimeout ?? TimeSpan.FromMilliseconds(this.PauseBetweenOperationRetries.TotalMilliseconds * this.MaxOperationRetries * 6);
            set => SetIfValidTimeout(ref this.operationTimeout, value, nameof(OperationTimeout));
        }

        private static void SetIfValidTimeout(ref TimeSpan? field, TimeSpan value, string propertyName)
        {
            if (value > TimeSpan.Zero || value.Equals(Timeout.InfiniteTimeSpan))
            {
                field = value;
            }
            else
            {
                throw new ArgumentNullException(propertyName);
            }
        }
    }
}
