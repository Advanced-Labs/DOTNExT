// VAYRON - Runtime-Integrated Persistent Storage
// Phase 4: Enhanced ambient transaction management with context, savepoints, and async support

using Voron.Impl;

namespace Vayron;

/// <summary>
/// Provides ambient transaction support for VAYRON operations.
/// Uses AsyncLocal for transaction flow across async boundaries.
/// </summary>
/// <remarks>
/// <para><b>Phase 4: Transaction Integration</b></para>
/// <para>
/// Enhanced transaction support includes:
/// <list type="bullet">
/// <item><description>Transaction context with metadata and events</description></item>
/// <item><description>Savepoint support for partial rollbacks</description></item>
/// <item><description>Auto-enrollment of handles as participants</description></item>
/// <item><description>Integration with VayronTransactionManager</description></item>
/// <item><description>Timeout support</description></item>
/// </list>
/// </para>
/// </remarks>
public static class VayronTransaction
{
    /// <summary>
    /// The current ambient transaction, if any.
    /// </summary>
    private static readonly AsyncLocal<VayronTransactionScope?> _current = new();

    /// <summary>
    /// Gets the current ambient transaction, or null if none is active.
    /// </summary>
    public static VayronTransactionScope? Current => _current.Value;

    /// <summary>
    /// Gets the current Voron transaction ID (epoch) for staleness detection.
    /// Returns -1 if no transaction is active.
    /// </summary>
    public static long CurrentEpoch => _current.Value?.VoronTransaction?.LowLevelTransaction.Id ?? -1;

    /// <summary>
    /// Gets the current transaction context, or null if none is active.
    /// </summary>
    public static VayronTransactionContext? CurrentContext => _current.Value?.Context;

    /// <summary>
    /// Checks if there is an active transaction.
    /// </summary>
    public static bool HasActiveTransaction => _current.Value != null;

    /// <summary>
    /// Checks if there is an active write transaction.
    /// </summary>
    public static bool HasActiveWriteTransaction => _current.Value?.IsWriteTransaction == true;

    /// <summary>
    /// Begins a read transaction.
    /// </summary>
    /// <param name="environment">The VAYRON environment.</param>
    /// <returns>A transaction scope that should be disposed when done.</returns>
    public static VayronTransactionScope BeginRead(VayronEnvironment environment)
    {
        return BeginRead(environment, null);
    }

    /// <summary>
    /// Begins a read transaction with optional timeout.
    /// </summary>
    /// <param name="environment">The VAYRON environment.</param>
    /// <param name="timeout">Optional timeout for the transaction.</param>
    /// <returns>A transaction scope that should be disposed when done.</returns>
    public static VayronTransactionScope BeginRead(VayronEnvironment environment, TimeSpan? timeout)
    {
        if (_current.Value != null)
        {
            // Nested transaction - just increment reference count
            return _current.Value.AddRef();
        }

        var voronTx = environment.VoronEnvironment.ReadTransaction();
        var scope = new VayronTransactionScope(environment, voronTx, isWriteTransaction: false, timeout);
        _current.Value = scope;

        // Register with manager
        VayronTransactionManager.Instance.RegisterTransaction(scope);

        return scope;
    }

    /// <summary>
    /// Begins a write transaction.
    /// </summary>
    /// <param name="environment">The VAYRON environment.</param>
    /// <returns>A transaction scope that should be disposed when done.</returns>
    public static VayronTransactionScope BeginWrite(VayronEnvironment environment)
    {
        return BeginWrite(environment, null);
    }

    /// <summary>
    /// Begins a write transaction with optional timeout.
    /// </summary>
    /// <param name="environment">The VAYRON environment.</param>
    /// <param name="timeout">Optional timeout for the transaction.</param>
    /// <returns>A transaction scope that should be disposed when done.</returns>
    public static VayronTransactionScope BeginWrite(VayronEnvironment environment, TimeSpan? timeout)
    {
        if (_current.Value != null)
        {
            if (!_current.Value.IsWriteTransaction)
            {
                throw new InvalidOperationException(
                    "Cannot start a write transaction while a read transaction is active. " +
                    "Dispose the read transaction first, or use the existing read transaction.");
            }
            // Nested write transaction - just increment reference count
            return _current.Value.AddRef();
        }

        var voronTx = environment.VoronEnvironment.WriteTransaction();
        var scope = new VayronTransactionScope(environment, voronTx, isWriteTransaction: true, timeout);
        _current.Value = scope;

        // Register with manager
        VayronTransactionManager.Instance.RegisterTransaction(scope);

        return scope;
    }

    /// <summary>
    /// Requires a transaction to be active, throwing if none exists.
    /// </summary>
    /// <returns>The current transaction scope.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
    public static VayronTransactionScope Require()
    {
        return _current.Value
            ?? throw new InvalidOperationException(
                "No active VAYRON transaction. Use VayronTransaction.BeginRead() or BeginWrite().");
    }

    /// <summary>
    /// Requires a write transaction to be active.
    /// </summary>
    /// <returns>The current write transaction scope.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no write transaction is active.</exception>
    public static VayronTransactionScope RequireWrite()
    {
        var current = _current.Value
            ?? throw new InvalidOperationException("No active VAYRON transaction.");

        if (!current.IsWriteTransaction)
        {
            throw new InvalidOperationException(
                "A write transaction is required but only a read transaction is active.");
        }

        return current;
    }

    /// <summary>
    /// Clears the current ambient transaction (called when scope is disposed).
    /// </summary>
    internal static void Clear()
    {
        _current.Value = null;
    }

    // =====================================================================
    // Convenience Methods
    // =====================================================================

    /// <summary>
    /// Executes an action within a read transaction.
    /// </summary>
    public static void ExecuteRead(VayronEnvironment environment, Action action)
    {
        using var tx = BeginRead(environment);
        action();
    }

    /// <summary>
    /// Executes a function within a read transaction.
    /// </summary>
    public static T ExecuteRead<T>(VayronEnvironment environment, Func<T> func)
    {
        using var tx = BeginRead(environment);
        return func();
    }

    /// <summary>
    /// Executes an action within a write transaction with automatic commit.
    /// </summary>
    public static void ExecuteWrite(VayronEnvironment environment, Action action)
    {
        using var tx = BeginWrite(environment);
        action();
        tx.Commit();
    }

    /// <summary>
    /// Executes a function within a write transaction with automatic commit.
    /// </summary>
    public static T ExecuteWrite<T>(VayronEnvironment environment, Func<T> func)
    {
        using var tx = BeginWrite(environment);
        var result = func();
        tx.Commit();
        return result;
    }

    /// <summary>
    /// Executes an async action within a read transaction.
    /// </summary>
    public static async Task ExecuteReadAsync(VayronEnvironment environment, Func<Task> action)
    {
        using var tx = BeginRead(environment);
        await action();
    }

    /// <summary>
    /// Executes an async function within a read transaction.
    /// </summary>
    public static async Task<T> ExecuteReadAsync<T>(VayronEnvironment environment, Func<Task<T>> func)
    {
        using var tx = BeginRead(environment);
        return await func();
    }

    /// <summary>
    /// Executes an async action within a write transaction with automatic commit.
    /// </summary>
    public static async Task ExecuteWriteAsync(VayronEnvironment environment, Func<Task> action)
    {
        using var tx = BeginWrite(environment);
        await action();
        tx.Commit();
    }

    /// <summary>
    /// Executes an async function within a write transaction with automatic commit.
    /// </summary>
    public static async Task<T> ExecuteWriteAsync<T>(VayronEnvironment environment, Func<Task<T>> func)
    {
        using var tx = BeginWrite(environment);
        var result = await func();
        tx.Commit();
        return result;
    }
}

/// <summary>
/// A scoped transaction that can be used with 'using' statements.
/// </summary>
/// <remarks>
/// <para><b>Phase 4 Enhancements:</b></para>
/// <list type="bullet">
/// <item><description>Transaction context with metadata and events</description></item>
/// <item><description>Savepoint support for partial rollbacks</description></item>
/// <item><description>Timeout support</description></item>
/// <item><description>Force rollback capability</description></item>
/// </list>
/// </remarks>
public sealed class VayronTransactionScope : IDisposable
{
    private readonly VayronEnvironment _environment;
    private int _refCount;
    private bool _committed;
    private bool _rolledBack;
    private bool _disposed;

    /// <summary>
    /// The underlying Voron transaction.
    /// </summary>
    public Transaction VoronTransaction { get; }

    /// <summary>
    /// Whether this is a write transaction.
    /// </summary>
    public bool IsWriteTransaction { get; }

    /// <summary>
    /// The transaction epoch (ID) for staleness detection.
    /// </summary>
    public long Epoch => VoronTransaction.LowLevelTransaction.Id;

    /// <summary>
    /// The transaction context containing metadata, events, and participant tracking.
    /// </summary>
    public VayronTransactionContext Context { get; }

    /// <summary>
    /// Gets the VAYRON environment this transaction belongs to.
    /// </summary>
    public VayronEnvironment Environment => _environment;

    /// <summary>
    /// Whether the transaction has been committed.
    /// </summary>
    public bool IsCommitted => _committed;

    /// <summary>
    /// Whether the transaction has been rolled back.
    /// </summary>
    public bool IsRolledBack => _rolledBack;

    internal VayronTransactionScope(
        VayronEnvironment environment,
        Transaction voronTx,
        bool isWriteTransaction,
        TimeSpan? timeout = null)
    {
        _environment = environment;
        VoronTransaction = voronTx;
        IsWriteTransaction = isWriteTransaction;
        _refCount = 1;
        _committed = false;
        _rolledBack = false;
        _disposed = false;

        // Create transaction context
        Context = new VayronTransactionContext(voronTx.LowLevelTransaction.Id, isWriteTransaction);
        if (timeout.HasValue)
        {
            Context.Timeout = timeout;
        }
    }

    /// <summary>
    /// Adds a reference (for nested transactions).
    /// </summary>
    internal VayronTransactionScope AddRef()
    {
        Interlocked.Increment(ref _refCount);
        return this;
    }

    // =====================================================================
    // Commit/Rollback
    // =====================================================================

    /// <summary>
    /// Commits the write transaction.
    /// Only valid for write transactions; throws for read transactions.
    /// </summary>
    public void Commit()
    {
        if (!IsWriteTransaction)
        {
            throw new InvalidOperationException("Cannot commit a read transaction.");
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VayronTransactionScope));
        }

        if (_rolledBack)
        {
            throw new InvalidOperationException("Cannot commit: transaction has been rolled back.");
        }

        if (_committed)
        {
            return; // Already committed
        }

        // Check timeout
        if (Context.IsTimedOut)
        {
            throw new TransactionAbortedException($"Transaction timeout exceeded ({Context.Timeout}).");
        }

        // Notify context of impending commit
        Context.OnCommitting();

        // Persist dirty handles before committing
        _environment.PersistDirtyHandles(this);

        // Commit Voron transaction
        VoronTransaction.Commit();
        _committed = true;

        // Notify context and manager
        Context.OnCommitted();
        VayronTransactionManager.Instance.OnTransactionCommitted(this);
    }

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    public void Rollback()
    {
        Rollback(null);
    }

    /// <summary>
    /// Rolls back the transaction with a reason.
    /// </summary>
    public void Rollback(string? reason)
    {
        if (_committed || _rolledBack || _disposed)
            return;

        _rolledBack = true;

        // Notify context
        Context.OnRolledBack(reason);

        // Dispose Voron transaction (implicit rollback)
        VoronTransaction.Dispose();

        // Notify manager
        VayronTransactionManager.Instance.OnTransactionRolledBack(this, reason);
    }

    /// <summary>
    /// Forces a rollback (used by timeout enforcement).
    /// </summary>
    internal void ForceRollback(string reason)
    {
        Rollback(reason);
    }

    // =====================================================================
    // Savepoint Support
    // =====================================================================

    /// <summary>
    /// Creates a savepoint at the current position.
    /// </summary>
    /// <param name="name">Optional name for the savepoint.</param>
    /// <returns>A token representing the savepoint.</returns>
    public SavepointToken CreateSavepoint(string? name = null)
    {
        return Context.CreateSavepoint(name);
    }

    /// <summary>
    /// Rolls back to a savepoint, undoing changes made after it was created.
    /// </summary>
    /// <param name="token">The savepoint token.</param>
    public void RollbackToSavepoint(SavepointToken token)
    {
        Context.RollbackToSavepoint(token);
    }

    /// <summary>
    /// Releases a savepoint, keeping the changes made after it was created.
    /// </summary>
    /// <param name="token">The savepoint token.</param>
    public void ReleaseSavepoint(SavepointToken token)
    {
        Context.ReleaseSavepoint(token);
    }

    // =====================================================================
    // Participant Management
    // =====================================================================

    /// <summary>
    /// Enrolls a handle as a participant in this transaction.
    /// </summary>
    /// <param name="handle">The handle to enroll.</param>
    public void Enroll(IVayronHandle handle)
    {
        Context.Enroll(handle);
    }

    /// <summary>
    /// Records a read operation.
    /// </summary>
    public void RecordRead(VayronOid oid)
    {
        Context.RecordRead(oid);
    }

    /// <summary>
    /// Records a write operation.
    /// </summary>
    public void RecordWrite(VayronOid oid)
    {
        Context.RecordWrite(oid);
    }

    // =====================================================================
    // Metadata
    // =====================================================================

    /// <summary>
    /// Sets custom metadata on the transaction.
    /// </summary>
    public void SetMetadata(string key, object? value)
    {
        Context.SetMetadata(key, value);
    }

    /// <summary>
    /// Gets custom metadata from the transaction.
    /// </summary>
    public T? GetMetadata<T>(string key) where T : class
    {
        return Context.GetMetadata<T>(key);
    }

    // =====================================================================
    // Disposal
    // =====================================================================

    /// <summary>
    /// Disposes the transaction scope.
    /// For nested transactions, decrements reference count.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (Interlocked.Decrement(ref _refCount) > 0)
        {
            return; // Still has references
        }

        _disposed = true;

        // Rollback if not committed
        if (!_committed && !_rolledBack)
        {
            Rollback("Transaction disposed without commit");
        }

        // Dispose context
        Context.Dispose();

        // Clear ambient transaction
        VayronTransaction.Clear();

        // Dispose Voron transaction (no-op if already disposed from rollback)
        if (!_rolledBack)
        {
            VoronTransaction.Dispose();
        }
    }
}
