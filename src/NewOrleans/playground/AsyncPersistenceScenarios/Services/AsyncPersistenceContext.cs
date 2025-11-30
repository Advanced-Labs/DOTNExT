namespace DOTNExT.Persistence;

/// <summary>
/// Ambient context for async persistence.
/// This allows the Roslyn-generated state machine code to access
/// the persistence service without modifying method signatures.
///
/// Usage:
/// using (AsyncPersistenceContext.SetCurrent(myService))
/// {
///     await MyPersistableWorkflowAsync();
/// }
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

    private class ContextScope : IDisposable
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
/// This is the contract between Roslyn-generated code and the persistence implementation.
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
    /// Checks if there's persisted state to restore and applies it if so.
    /// </summary>
    /// <param name="stateMachine">The state machine instance to potentially restore into</param>
    /// <param name="methodId">Unique identifier for this workflow instance</param>
    /// <returns>The state to resume from, or -1 if no restoration</returns>
    int TryRestore(object stateMachine, string methodId);

    /// <summary>
    /// Called when async method completes successfully.
    /// </summary>
    void Complete(string methodId, object? result);

    /// <summary>
    /// Called when async method faults.
    /// </summary>
    void Fault(string methodId, Exception exception);
}
