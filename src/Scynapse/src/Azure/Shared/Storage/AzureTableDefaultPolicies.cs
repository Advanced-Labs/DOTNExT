using System;
using System.Diagnostics;
using Microsoft.Azure.Cosmos.Table;

//
// Number of #ifs can be reduced (or removed), once we separate test projects by feature/area, otherwise we are ending up with ambigous types and build errors.
//

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
#elif SCYNAPSE_HOSTING_CLOUDSERVICES // Temporary until azure silo/client is refactored
namespace Scynapse.Hosting.AzureCloudServices
#elif SCYNAPSE_TRANSACTIONS
namespace Scynapse.Transactions.AzureStorage
#elif SCYNAPSE_DIRECTORY
namespace Scynapse.GrainDirectory.AzureStorage
#else
// No default namespace intentionally to cause compile errors if something is not defined
#endif
{
    /// <summary>
    /// Utility class for default retry / timeout settings for Azure storage.
    /// </summary>
    /// <remarks>
    /// These functions are mostly intended for internal usage by Scynapse runtime, but due to certain assembly packaging constraints this class needs to have public visibility.
    /// </remarks>
    public static class AzureTableDefaultPolicies
    {
        public static int MaxTableCreationRetries { get; private set; }
        public static int MaxTableOperationRetries { get; private set; }
        public static int MaxBusyRetries { get; internal set; }

        public static TimeSpan PauseBetweenTableCreationRetries { get; private set; }
        public static TimeSpan PauseBetweenTableOperationRetries { get; private set; }
        public static TimeSpan PauseBetweenBusyRetries { get; private set; }

        public static TimeSpan TableCreationTimeout { get; private set; }
        public static TimeSpan TableOperationTimeout { get; private set; }
        public static TimeSpan BusyRetriesTimeout { get; private set; }

        public static IRetryPolicy TableCreationRetryPolicy { get; private set; }
        public static IRetryPolicy TableOperationRetryPolicy { get; private set; }

        public const int MAX_BULK_UPDATE_ROWS = 100;

        static AzureTableDefaultPolicies()
        {
            MaxTableCreationRetries = 60;
            PauseBetweenTableCreationRetries = (!Debugger.IsAttached)
                ? TimeSpan.FromSeconds(1)
                : TimeSpan.FromSeconds(100);

            MaxTableOperationRetries = 5;
            PauseBetweenTableOperationRetries = (!Debugger.IsAttached)
                ? TimeSpan.FromMilliseconds(100)
                : TimeSpan.FromSeconds(10);

            MaxBusyRetries = 120;
            PauseBetweenBusyRetries = (!Debugger.IsAttached)
                ? TimeSpan.FromMilliseconds(500)
                : TimeSpan.FromSeconds(5);

            TableCreationRetryPolicy = new LinearRetry(PauseBetweenTableCreationRetries, MaxTableCreationRetries); // 60 x 1s
            TableCreationTimeout = TimeSpan.FromMilliseconds(PauseBetweenTableCreationRetries.TotalMilliseconds * MaxTableCreationRetries * 3);    // 3 min

            TableOperationRetryPolicy = new LinearRetry(PauseBetweenTableOperationRetries, MaxTableOperationRetries); // 5 x 100ms
            TableOperationTimeout = TimeSpan.FromMilliseconds(PauseBetweenTableOperationRetries.TotalMilliseconds * MaxTableOperationRetries *6);    // 3 sec

            BusyRetriesTimeout = TimeSpan.FromMilliseconds(PauseBetweenBusyRetries.TotalMilliseconds * MaxBusyRetries);  // 1 minute
        }
    }
}
