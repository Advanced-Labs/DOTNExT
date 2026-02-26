using Newtonsoft.Json;

#if SCYNAPSE_CLUSTERING
namespace Scynapse.Clustering.Cosmos;
#elif SCYNAPSE_PERSISTENCE
namespace Scynapse.Persistence.Cosmos;
#elif SCYNAPSE_REMINDERS
namespace Scynapse.Reminders.Cosmos;
#elif SCYNAPSE_STREAMING
namespace Scynapse.Streaming.Cosmos;
#elif SCYNAPSE_DIRECTORY
namespace Scynapse.GrainDirectory.Cosmos;
#else
// No default namespace intentionally to cause compile errors if something is not defined
#endif

internal abstract class BaseEntity
{
    internal const string ID_FIELD = "id";
    internal const string ETAG_FIELD = "_etag";    

    [JsonProperty(ID_FIELD)]
    [JsonPropertyName(ID_FIELD)]
    public string Id { get; set; } = default!;

    [JsonProperty(ETAG_FIELD)]
    [JsonPropertyName(ETAG_FIELD)]
    public string ETag { get; set; } = default!;
}