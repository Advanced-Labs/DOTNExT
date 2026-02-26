using System.Diagnostics.CodeAnalysis;

//
// Number of #ifs can be reduced (or removed), once we separate test projects by feature/area, otherwise we are ending up with ambigous types and build errors.
//

#if SCYNAPSE_CLUSTERING
namespace Scynapse.Clustering.AzureStorage.Utilities
#elif SCYNAPSE_PERSISTENCE
namespace Scynapse.Persistence.AzureStorage.Utilities
#elif SCYNAPSE_REMINDERS_PROVIDER
namespace Scynapse.Reminders
#elif SCYNAPSE_REMINDERS
namespace Scynapse.Reminders.AzureStorage.Utilities
#elif SCYNAPSE_STREAMING
namespace Scynapse.Streaming.AzureStorage.Utilities
#elif SCYNAPSE_EVENTHUBS
namespace Scynapse.Streaming.EventHubs.Utilities
#elif TESTER_AZUREUTILS
namespace Scynapse.Tests.AzureUtils.Utilities
#elif SCYNAPSE_TRANSACTIONS
namespace Scynapse.Transactions.AzureStorage.Utilities
#elif SCYNAPSE_DIRECTORY
namespace Scynapse.GrainDirectory.AzureStorage.Utilities
#else
// No default namespace intentionally to cause compile errors if something is not defined
#endif
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal enum ErrorCode
    {
        Runtime = 100000,
        AzureTableBase = Runtime + 800,

        AzureTable_01 = AzureTableBase + 1,
        AzureTable_02 = AzureTableBase + 2,
        AzureTable_03 = AzureTableBase + 3,
        AzureTable_04 = AzureTableBase + 4,
        AzureTable_06 = AzureTableBase + 6,
        AzureTable_07 = AzureTableBase + 7,
        AzureTable_08 = AzureTableBase + 8,
        AzureTable_09 = AzureTableBase + 9,
        AzureTable_10 = AzureTableBase + 10,
        AzureTable_11 = AzureTableBase + 11,
        AzureTable_12 = AzureTableBase + 12,
        AzureTable_13 = AzureTableBase + 13,
        AzureTable_14 = AzureTableBase + 14,
        AzureTable_15 = AzureTableBase + 15,
        AzureTable_17 = AzureTableBase + 17,
        AzureTable_18 = AzureTableBase + 18,
        AzureTable_19 = AzureTableBase + 19,
        AzureTable_37 = AzureTableBase + 37,
        // Azure storage provider related
        AzureTable_DataNotFound = AzureTableBase + 50,
        AzureTable_TableNotCreated = AzureTableBase + 51,
    }
}
