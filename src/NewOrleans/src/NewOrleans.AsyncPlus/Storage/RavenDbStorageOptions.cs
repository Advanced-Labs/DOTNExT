using Orleans.Storage;

namespace NewOrleans.AsyncPlus.Storage;

/// <summary>
/// Options for RavenDB grain storage.
/// </summary>
public class RavenDbStorageOptions
{
    /// <summary>
    /// RavenDB server URLs. Default: http://127.0.0.1:38880
    /// </summary>
    public string[] Urls { get; set; } = new[] { "http://127.0.0.1:38880" };

    /// <summary>
    /// Database name for grain storage. Default: "OrleansGrainState"
    /// </summary>
    public string DatabaseName { get; set; } = "OrleansGrainState";

    /// <summary>
    /// Optional certificate path for secured connections.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Optional certificate password.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Whether to delete state on clear (vs marking as deleted). Default: true
    /// </summary>
    public bool DeleteStateOnClear { get; set; } = true;

    /// <summary>
    /// Stage during silo lifecycle when storage should be initialized.
    /// </summary>
    public int InitStage { get; set; } = ServiceLifecycleStage.ApplicationServices;

    /// <summary>
    /// Custom grain storage serializer. If null, uses the default.
    /// </summary>
    public IGrainStorageSerializer? GrainStorageSerializer { get; set; }

    /// <summary>
    /// Whether to create the database if it doesn't exist. Default: true
    /// </summary>
    public bool CreateDatabaseIfNotExists { get; set; } = true;
}

/// <summary>
/// Document structure stored in RavenDB for grain state.
/// </summary>
public class GrainStateDocument
{
    /// <summary>
    /// Document ID (composite of grain type and grain ID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The grain type name.
    /// </summary>
    public string GrainType { get; set; } = string.Empty;

    /// <summary>
    /// The grain ID as string.
    /// </summary>
    public string GrainId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized state data (JSON or binary depending on serializer).
    /// </summary>
    public byte[]? StateData { get; set; }

    /// <summary>
    /// ETag for optimistic concurrency.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Last modified timestamp.
    /// </summary>
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Service ID from Orleans cluster options.
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;
}
