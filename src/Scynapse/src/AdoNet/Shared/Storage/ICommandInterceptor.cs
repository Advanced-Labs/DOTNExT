using System.Data;

#if CLUSTERING_ADONET
namespace Scynapse.Clustering.AdoNet.Storage
#elif PERSISTENCE_ADONET
namespace Scynapse.Persistence.AdoNet.Storage
#elif REMINDERS_ADONET
namespace Scynapse.Reminders.AdoNet.Storage
#elif STREAMING_ADONET
namespace Scynapse.Streaming.AdoNet.Storage
#elif GRAINDIRECTORY_ADONET
namespace Scynapse.GrainDirectory.AdoNet.Storage
#elif TESTER_SQLUTILS
namespace Scynapse.Tests.SqlUtils
#else
// No default namespace intentionally to cause compile errors if something is not defined
#endif
{
    internal interface ICommandInterceptor
    {
        void Intercept(IDbCommand command);
    }
}
