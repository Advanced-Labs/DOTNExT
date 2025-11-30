using System.Runtime.CompilerServices;

namespace AsyncPersistenceScenarios.Services;

/// <summary>
/// Service interface for async state machine persistence.
/// Injected via DI - if null/not registered, no persistence occurs.
///
/// This is the agnostic interface that:
/// 1. Roslyn-generated code will call (when we modify Roslyn)
/// 2. Can be implemented by different backends (memory, Orleans, file, etc.)
/// </summary>
public interface IAsyncPersistenceService
{
    /// <summary>
    /// Called before suspending at an await point.
    /// Captures the current state of the state machine for later restoration.
    /// </summary>
    /// <typeparam name="TStateMachine">The compiler-generated state machine type</typeparam>
    /// <param name="stateMachine">Reference to the state machine instance</param>
    /// <param name="stateNumber">The await point state number (0, 1, 2, ...)</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    void Checkpoint<TStateMachine>(
        ref TStateMachine stateMachine,
        int stateNumber,
        string methodId)
        where TStateMachine : IAsyncStateMachine;

    /// <summary>
    /// Called at MoveNext start to check if restoration is needed.
    /// If returns true, the state machine fields have been populated from the snapshot.
    /// </summary>
    /// <typeparam name="TStateMachine">The compiler-generated state machine type</typeparam>
    /// <param name="stateMachine">Reference to the state machine instance to restore into</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    /// <param name="restoredState">The state number to resume from</param>
    /// <returns>True if restoration occurred, false if starting fresh</returns>
    bool TryRestore<TStateMachine>(
        ref TStateMachine stateMachine,
        string methodId,
        out int restoredState)
        where TStateMachine : IAsyncStateMachine;

    /// <summary>
    /// Called when async method completes successfully.
    /// Clears persisted state (or marks as complete, depending on implementation).
    /// </summary>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    /// <param name="result">The result value (null for void/Task methods)</param>
    void Complete(string methodId, object? result);

    /// <summary>
    /// Called when async method faults with an exception.
    /// </summary>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    /// <param name="exception">The exception that caused the fault</param>
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
/// Event args for checkpoint events.
/// </summary>
public class CheckpointEventArgs : EventArgs
{
    public string MethodId { get; }
    public int StateNumber { get; }
    public StateMachineSnapshot Snapshot { get; }
    public DateTimeOffset Timestamp { get; }

    public CheckpointEventArgs(string methodId, int stateNumber, StateMachineSnapshot snapshot)
    {
        MethodId = methodId;
        StateNumber = stateNumber;
        Snapshot = snapshot;
        Timestamp = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Event args for restore events.
/// </summary>
public class RestoreEventArgs : EventArgs
{
    public string MethodId { get; }
    public int RestoredState { get; }
    public DateTimeOffset Timestamp { get; }

    public RestoreEventArgs(string methodId, int restoredState)
    {
        MethodId = methodId;
        RestoredState = restoredState;
        Timestamp = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Event args for completion events.
/// </summary>
public class CompleteEventArgs : EventArgs
{
    public string MethodId { get; }
    public object? Result { get; }
    public bool Faulted { get; }
    public Exception? Exception { get; }
    public DateTimeOffset Timestamp { get; }

    public CompleteEventArgs(string methodId, object? result, bool faulted = false, Exception? exception = null)
    {
        MethodId = methodId;
        Result = result;
        Faulted = faulted;
        Exception = exception;
        Timestamp = DateTimeOffset.UtcNow;
    }
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

/// <summary>
/// Attribute to mark async methods as persistable.
/// When Roslyn is modified, it will look for this attribute to enable persistence codegen.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class PersistableAttribute : Attribute
{
    /// <summary>
    /// Optional custom ID prefix for this workflow type.
    /// </summary>
    public string? IdPrefix { get; set; }

    /// <summary>
    /// If true, checkpoints at every await. If false, only at marked points.
    /// Default is true.
    /// </summary>
    public bool AutoCheckpoint { get; set; } = true;
}
