using System.Runtime.CompilerServices;

namespace AsyncPersistenceScenarios.Services;

// NOTE: Event args (CheckpointEventArgs, RestoreEventArgs, CompleteEventArgs) are now
// defined in DOTNExT.Persistence namespace in the NewOrleans.AsyncPlus library.
// Use: using DOTNExT.Persistence;

/// <summary>
/// Service interface for async state machine persistence (generic version).
/// This interface uses generic TStateMachine for type-safe access to state machine fields.
///
/// For Roslyn-generated code, use DOTNExT.Persistence.IAsyncPersistenceService instead
/// (which uses object for state machine parameter due to compiler limitations).
/// </summary>
public interface IAsyncPersistenceServiceGeneric
{
    /// <summary>
    /// Called before suspending at an await point.
    /// Captures the current state of the state machine for later restoration.
    /// </summary>
    void Checkpoint<TStateMachine>(
        ref TStateMachine stateMachine,
        int stateNumber,
        string methodId)
        where TStateMachine : IAsyncStateMachine;

    /// <summary>
    /// Called at MoveNext start to check if restoration is needed.
    /// </summary>
    bool TryRestore<TStateMachine>(
        ref TStateMachine stateMachine,
        string methodId,
        out int restoredState)
        where TStateMachine : IAsyncStateMachine;

    /// <summary>
    /// Called when async method completes successfully.
    /// </summary>
    void Complete(string methodId, object? result);

    /// <summary>
    /// Called when async method faults with an exception.
    /// </summary>
    void Fault(string methodId, Exception exception);

    /// <summary>
    /// Checks if there is persisted state for the given method.
    /// </summary>
    bool HasPersistedState(string methodId);

    /// <summary>
    /// Clears persisted state for the given method.
    /// </summary>
    void Clear(string methodId);

    /// <summary>
    /// Gets all persisted method IDs.
    /// </summary>
    IEnumerable<string> GetPersistedMethodIds();
}

/// <summary>
/// Represents a serialized snapshot of an async state machine.
/// </summary>
public class StateMachineSnapshot
{
    /// <summary>
    /// The state number (await point).
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// Assembly-qualified type name of the state machine.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Serialized field values.
    /// Key = field name, Value = serialized value (as JSON or object for in-memory)
    /// </summary>
    public Dictionary<string, object?> Fields { get; set; } = new();

    /// <summary>
    /// When this snapshot was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Optional metadata (e.g., original method name, correlation ID).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// NOTE: PersistableAttribute is now defined in DOTNExT.Persistence namespace
// in the NewOrleans.AsyncPlus library. Use: using DOTNExT.Persistence;
