// VAYRON - Runtime-Integrated Persistent Storage
// Ambient transaction management

using Voron.Impl;

namespace Vayron;

/// <summary>
/// Provides ambient transaction support for VAYRON operations.
/// Uses AsyncLocal for transaction flow across async boundaries.
/// </summary>
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
    /// Begins a read transaction.
    /// </summary>
    public static VayronTransactionScope BeginRead(VayronEnvironment environment)
    {
        if (_current.Value != null)
        {
            // Nested transaction - just increment reference count
            return _current.Value.AddRef();
        }

        var voronTx = environment.VoronEnvironment.ReadTransaction();
        var scope = new VayronTransactionScope(environment, voronTx, isWriteTransaction: false);
        _current.Value = scope;
        return scope;
    }

    /// <summary>
    /// Begins a write transaction.
    /// </summary>
    public static VayronTransactionScope BeginWrite(VayronEnvironment environment)
    {
        if (_current.Value != null)
        {
            if (!_current.Value.IsWriteTransaction)
            {
                throw new InvalidOperationException(
                    "Cannot start a write transaction while a read transaction is active.");
            }
            // Nested write transaction - just increment reference count
            return _current.Value.AddRef();
        }

        var voronTx = environment.VoronEnvironment.WriteTransaction();
        var scope = new VayronTransactionScope(environment, voronTx, isWriteTransaction: true);
        _current.Value = scope;
        return scope;
    }

    /// <summary>
    /// Clears the current ambient transaction (called when scope is disposed).
    /// </summary>
    internal static void Clear()
    {
        _current.Value = null;
    }
}

/// <summary>
/// A scoped transaction that can be used with 'using' statements.
/// </summary>
public sealed class VayronTransactionScope : IDisposable
{
    private readonly VayronEnvironment _environment;
    private int _refCount;
    private bool _committed;
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

    internal VayronTransactionScope(VayronEnvironment environment, Transaction voronTx, bool isWriteTransaction)
    {
        _environment = environment;
        VoronTransaction = voronTx;
        IsWriteTransaction = isWriteTransaction;
        _refCount = 1;
        _committed = false;
        _disposed = false;
    }

    /// <summary>
    /// Adds a reference (for nested transactions).
    /// </summary>
    internal VayronTransactionScope AddRef()
    {
        Interlocked.Increment(ref _refCount);
        return this;
    }

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

        if (_committed)
        {
            return; // Already committed
        }

        // Persist dirty handles before committing
        _environment.PersistDirtyHandles(this);

        VoronTransaction.Commit();
        _committed = true;
    }

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

        // Clear ambient transaction
        VayronTransaction.Clear();

        // Dispose Voron transaction (rolls back if not committed)
        VoronTransaction.Dispose();
    }
}
