using Newtonsoft.Json;

namespace Scynapse.AsyncPlus;

/// <summary>
/// DTO representing a persisted async state machine checkpoint.
/// </summary>
[GenerateSerializer]
[Immutable]
public sealed record AsyncStateCheckpoint
{
    /// <summary>
    /// The state machine state number at the checkpoint.
    /// Corresponds to the await point where execution will resume.
    /// </summary>
    [Id(0)]
    public required int StateNumber { get; init; }

    /// <summary>
    /// Serialized state machine fields (hoisted locals, parameters, etc.).
    /// </summary>
    [Id(1)]
    public required byte[] SerializedStateMachine { get; init; }

    /// <summary>
    /// Assembly-qualified type name of the state machine for deserialization.
    /// </summary>
    [Id(2)]
    public required string StateMachineTypeName { get; init; }

    /// <summary>
    /// UTC timestamp when the checkpoint was created.
    /// </summary>
    [Id(3)]
    public required DateTime CheckpointTimeUtc { get; init; }
}

/// <summary>
/// Grain state for async state persistence.
/// Stored by Orleans persistence provider (e.g., RavenDB).
/// </summary>
[GenerateSerializer]
public sealed class AsyncStatePersistenceGrainState
{
    /// <summary>
    /// Current state number (-1 if no checkpoint).
    /// </summary>
    /// <remarks>
    /// JsonProperty with Include is needed because Orleans uses DefaultValueHandling.Ignore
    /// which would skip StateNumber=0 (first await point) since 0 is the int default.
    /// </remarks>
    [Id(0)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public int StateNumber { get; set; } = -1;

    /// <summary>
    /// Serialized state machine data.
    /// </summary>
    [Id(1)]
    public byte[]? SerializedStateMachine { get; set; }

    /// <summary>
    /// Assembly-qualified type name for deserialization.
    /// </summary>
    [Id(2)]
    public string? StateMachineTypeName { get; set; }

    /// <summary>
    /// UTC timestamp of the last checkpoint.
    /// </summary>
    [Id(3)]
    public DateTime? CheckpointTimeUtc { get; set; }

    /// <summary>
    /// Whether the workflow has completed successfully.
    /// </summary>
    [Id(4)]
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Whether the workflow has faulted.
    /// </summary>
    [Id(5)]
    public bool IsFaulted { get; set; }

    /// <summary>
    /// Serialized result if completed successfully.
    /// </summary>
    [Id(6)]
    public byte[]? SerializedResult { get; set; }

    /// <summary>
    /// Exception type name if faulted.
    /// </summary>
    [Id(7)]
    public string? FaultExceptionType { get; set; }

    /// <summary>
    /// Exception message if faulted.
    /// </summary>
    [Id(8)]
    public string? FaultMessage { get; set; }

    /// <summary>
    /// Exception stack trace if faulted.
    /// </summary>
    [Id(9)]
    public string? FaultStackTrace { get; set; }
}
