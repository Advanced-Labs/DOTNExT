// DOTNExT.Persistence namespace - Core Async+ abstractions
// This is the canonical location for Async+ persistence interfaces
// Used by Roslyn-generated code and all persistence implementations

namespace DOTNExT.Persistence;

/// <summary>
/// Ambient context for async persistence.
/// Allows Roslyn-generated state machine code to access the persistence service
/// without modifying method signatures.
///
/// Usage:
/// <code>
/// using (AsyncPersistenceContext.SetCurrent(myService))
/// {
///     await MyPersistableWorkflowAsync();
/// }
/// </code>
/// </summary>
public static class AsyncPersistenceContext
{
    private static readonly AsyncLocal<IAsyncPersistenceService?> _current = new();

    /// <summary>
    /// Gets the current persistence service for this async flow.
    /// Returns null if no persistence is configured.
    /// </summary>
    public static IAsyncPersistenceService? Current => _current.Value;

    /// <summary>
    /// Sets the persistence service for the current async flow.
    /// Returns a disposable that restores the previous value.
    /// </summary>
    public static IDisposable SetCurrent(IAsyncPersistenceService? service)
    {
        var previous = _current.Value;
        _current.Value = service;
        return new ContextScope(previous);
    }

    /// <summary>
    /// Indicates if persistence is enabled for the current context.
    /// </summary>
    public static bool IsEnabled => _current.Value != null;

    private sealed class ContextScope : IDisposable
    {
        private readonly IAsyncPersistenceService? _previous;
        private bool _disposed;

        public ContextScope(IAsyncPersistenceService? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _current.Value = _previous;
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Interface that persistence services must implement.
/// This is the contract between Roslyn-generated code and persistence implementations.
///
/// NOTE: Methods are sync because Roslyn-generated MoveNext() is sync.
/// Implementations should handle async internally (e.g., fire-and-forget, tracked tasks).
/// </summary>
public interface IAsyncPersistenceService
{
    /// <summary>
    /// Called before suspending at an await point.
    /// The state machine should be serializable at this point.
    /// </summary>
    /// <param name="stateMachine">The state machine instance (boxed for struct state machines)</param>
    /// <param name="stateNumber">The await point state number</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    void Checkpoint(object stateMachine, int stateNumber, string methodId);

    /// <summary>
    /// [DEPRECATED] Use TryRestore&lt;TStateMachine&gt;(ref TStateMachine, string) instead.
    /// This method has a struct boxing bug - modifications are lost for value types.
    /// Kept for backwards compatibility with hand-coded test state machines.
    /// </summary>
    /// <param name="stateMachine">The state machine instance to potentially restore into</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    /// <returns>The state to resume from, or -1 if no restoration</returns>
    [Obsolete("Use TryRestore<TStateMachine>(ref TStateMachine, string) instead - this method has struct boxing issues")]
    int TryRestore(object stateMachine, string methodId);

    /// <summary>
    /// Checks if there's persisted state to restore and applies it if so.
    /// Uses ref parameter to properly handle struct state machines without boxing issues.
    ///
    /// This is the preferred method for Roslyn+ generated code.
    /// </summary>
    /// <typeparam name="TStateMachine">The state machine type (struct or class)</typeparam>
    /// <param name="stateMachine">Ref to the state machine instance - will be replaced with restored state</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    /// <returns>The state to resume from, or -1 if no restoration</returns>
    int TryRestore<TStateMachine>(ref TStateMachine stateMachine, string methodId);

    /// <summary>
    /// Called when async method completes successfully.
    /// </summary>
    void Complete(string methodId, object? result);

    /// <summary>
    /// Called when async method faults.
    /// </summary>
    void Fault(string methodId, Exception exception);

    // Events for observability
    event EventHandler<CheckpointEventArgs>? OnCheckpoint;
    event EventHandler<RestoreEventArgs>? OnRestore;
    event EventHandler<CompleteEventArgs>? OnComplete;
    event EventHandler<FaultEventArgs>? OnFault;
}

/// <summary>
/// Event args for checkpoint events.
/// </summary>
public class CheckpointEventArgs : EventArgs
{
    public string MethodId { get; }
    public int StateNumber { get; }
    public object? Snapshot { get; }

    public CheckpointEventArgs(string methodId, int stateNumber, object? snapshot = null)
    {
        MethodId = methodId;
        StateNumber = stateNumber;
        Snapshot = snapshot;
    }
}

/// <summary>
/// Event args for restore events.
/// </summary>
public class RestoreEventArgs : EventArgs
{
    public string MethodId { get; }
    public int RestoredState { get; }

    public RestoreEventArgs(string methodId, int restoredState)
    {
        MethodId = methodId;
        RestoredState = restoredState;
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

    public CompleteEventArgs(string methodId, object? result, bool faulted = false, Exception? exception = null)
    {
        MethodId = methodId;
        Result = result;
        Faulted = faulted;
        Exception = exception;
    }
}

/// <summary>
/// Event args for fault events.
/// </summary>
public class FaultEventArgs : EventArgs
{
    public string MethodId { get; }
    public Exception Exception { get; }

    public FaultEventArgs(string methodId, Exception exception)
    {
        MethodId = methodId;
        Exception = exception;
    }
}

/// <summary>
/// Attribute to mark async methods as persistable.
/// Roslyn (modified) looks for this attribute to enable persistence codegen.
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
