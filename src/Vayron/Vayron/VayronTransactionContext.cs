// VAYRON - Runtime-Integrated Persistent Storage
// Phase 4: Transaction Context - Transaction-level metadata, events, and participant tracking

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Vayron;

/// <summary>
/// Transaction context that holds metadata, participants, and events for a VAYRON transaction.
/// </summary>
/// <remarks>
/// <para><b>Phase 4: Transaction Integration</b></para>
/// <para>
/// The transaction context provides:
/// <list type="bullet">
/// <item><description>Participant tracking - handles enrolled in the transaction</description></item>
/// <item><description>Event notifications - commit, rollback, dispose events</description></item>
/// <item><description>Transaction metadata - timing, statistics, custom data</description></item>
/// <item><description>Savepoint support - partial rollback within transactions</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class VayronTransactionContext : IDisposable
{
    private readonly ConcurrentDictionary<VayronOid, WeakReference<IVayronHandle>> _participants = new();
    private readonly ConcurrentDictionary<string, object?> _metadata = new();
    private readonly List<Savepoint> _savepoints = new();
    private readonly Stopwatch _stopwatch;
    private readonly object _savepointLock = new();

    private int _operationCount;
    private int _readCount;
    private int _writeCount;
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this transaction context.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The Voron transaction epoch (ID).
    /// </summary>
    public long Epoch { get; }

    /// <summary>
    /// Whether this is a write transaction.
    /// </summary>
    public bool IsWriteTransaction { get; }

    /// <summary>
    /// When the transaction started.
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// Time elapsed since the transaction started.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Number of handles enrolled in this transaction.
    /// </summary>
    public int ParticipantCount => _participants.Count;

    /// <summary>
    /// Total operations performed in this transaction.
    /// </summary>
    public int OperationCount => _operationCount;

    /// <summary>
    /// Number of read operations.
    /// </summary>
    public int ReadCount => _readCount;

    /// <summary>
    /// Number of write operations.
    /// </summary>
    public int WriteCount => _writeCount;

    /// <summary>
    /// Number of active savepoints.
    /// </summary>
    public int SavepointCount
    {
        get { lock (_savepointLock) return _savepoints.Count; }
    }

    /// <summary>
    /// Transaction state.
    /// </summary>
    public TransactionState State { get; private set; }

    /// <summary>
    /// Optional timeout for the transaction.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Whether the transaction has exceeded its timeout.
    /// </summary>
    public bool IsTimedOut => Timeout.HasValue && _stopwatch.Elapsed > Timeout.Value;

    // Events

    /// <summary>
    /// Raised before the transaction commits.
    /// </summary>
    public event EventHandler<TransactionCommittingEventArgs>? Committing;

    /// <summary>
    /// Raised after the transaction commits successfully.
    /// </summary>
    public event EventHandler<TransactionCommittedEventArgs>? Committed;

    /// <summary>
    /// Raised when the transaction rolls back.
    /// </summary>
    public event EventHandler<TransactionRolledBackEventArgs>? RolledBack;

    /// <summary>
    /// Raised when a handle enrolls in the transaction.
    /// </summary>
    public event EventHandler<ParticipantEnrolledEventArgs>? ParticipantEnrolled;

    /// <summary>
    /// Raised when an operation is recorded.
    /// </summary>
    public event EventHandler<OperationRecordedEventArgs>? OperationRecorded;

    /// <summary>
    /// Creates a new transaction context.
    /// </summary>
    internal VayronTransactionContext(long epoch, bool isWriteTransaction)
    {
        Id = Guid.NewGuid();
        Epoch = epoch;
        IsWriteTransaction = isWriteTransaction;
        StartTime = DateTimeOffset.UtcNow;
        State = TransactionState.Active;
        _stopwatch = Stopwatch.StartNew();
    }

    // =====================================================================
    // Participant Management
    // =====================================================================

    /// <summary>
    /// Enrolls a handle as a participant in this transaction.
    /// </summary>
    public void Enroll(IVayronHandle handle)
    {
        if (_disposed || State != TransactionState.Active)
            return;

        if (_participants.TryAdd(handle.Oid, new WeakReference<IVayronHandle>(handle)))
        {
            ParticipantEnrolled?.Invoke(this, new ParticipantEnrolledEventArgs(handle.Oid, this));
        }
    }

    /// <summary>
    /// Checks if a handle is enrolled in this transaction.
    /// </summary>
    public bool IsEnrolled(VayronOid oid)
    {
        return _participants.ContainsKey(oid);
    }

    /// <summary>
    /// Gets all enrolled handles that are still alive.
    /// </summary>
    public IEnumerable<IVayronHandle> GetParticipants()
    {
        foreach (var weakRef in _participants.Values)
        {
            if (weakRef.TryGetTarget(out var handle))
            {
                yield return handle;
            }
        }
    }

    /// <summary>
    /// Gets all dirty handles enrolled in this transaction.
    /// </summary>
    public IEnumerable<IVayronHandle> GetDirtyParticipants()
    {
        foreach (var handle in GetParticipants())
        {
            if (handle is VayronHandle vh && vh.IsDirty)
            {
                yield return vh;
            }
        }
    }

    // =====================================================================
    // Metadata
    // =====================================================================

    /// <summary>
    /// Sets custom metadata on the transaction.
    /// </summary>
    public void SetMetadata(string key, object? value)
    {
        _metadata[key] = value;
    }

    /// <summary>
    /// Gets custom metadata from the transaction.
    /// </summary>
    public T? GetMetadata<T>(string key) where T : class
    {
        if (_metadata.TryGetValue(key, out var value))
        {
            return value as T;
        }
        return null;
    }

    /// <summary>
    /// Tries to get custom metadata from the transaction.
    /// </summary>
    public bool TryGetMetadata<T>(string key, out T? value) where T : class
    {
        if (_metadata.TryGetValue(key, out var obj))
        {
            value = obj as T;
            return value != null;
        }
        value = null;
        return false;
    }

    // =====================================================================
    // Operation Tracking
    // =====================================================================

    /// <summary>
    /// Records a read operation.
    /// </summary>
    public void RecordRead(VayronOid oid)
    {
        Interlocked.Increment(ref _operationCount);
        Interlocked.Increment(ref _readCount);
        OperationRecorded?.Invoke(this, new OperationRecordedEventArgs(OperationType.Read, oid));
    }

    /// <summary>
    /// Records a write operation.
    /// </summary>
    public void RecordWrite(VayronOid oid)
    {
        Interlocked.Increment(ref _operationCount);
        Interlocked.Increment(ref _writeCount);
        OperationRecorded?.Invoke(this, new OperationRecordedEventArgs(OperationType.Write, oid));
    }

    /// <summary>
    /// Records a generic operation.
    /// </summary>
    public void RecordOperation(OperationType type, VayronOid oid = default)
    {
        Interlocked.Increment(ref _operationCount);
        if (type == OperationType.Read)
            Interlocked.Increment(ref _readCount);
        else if (type == OperationType.Write)
            Interlocked.Increment(ref _writeCount);

        OperationRecorded?.Invoke(this, new OperationRecordedEventArgs(type, oid));
    }

    // =====================================================================
    // Savepoints
    // =====================================================================

    /// <summary>
    /// Creates a savepoint at the current position.
    /// </summary>
    public SavepointToken CreateSavepoint(string? name = null)
    {
        if (!IsWriteTransaction)
            throw new InvalidOperationException("Savepoints are only supported in write transactions.");

        if (State != TransactionState.Active)
            throw new InvalidOperationException("Cannot create savepoint: transaction is not active.");

        lock (_savepointLock)
        {
            var savepoint = new Savepoint(
                id: _savepoints.Count,
                name: name ?? $"SP_{_savepoints.Count}",
                operationCountAtSavepoint: _operationCount,
                participantOidsAtSavepoint: _participants.Keys.ToHashSet()
            );

            _savepoints.Add(savepoint);

            return new SavepointToken(savepoint.Id, savepoint.Name);
        }
    }

    /// <summary>
    /// Rolls back to a savepoint.
    /// </summary>
    public void RollbackToSavepoint(SavepointToken token)
    {
        if (!IsWriteTransaction)
            throw new InvalidOperationException("Savepoints are only supported in write transactions.");

        lock (_savepointLock)
        {
            var index = _savepoints.FindIndex(s => s.Id == token.Id);
            if (index < 0)
                throw new InvalidOperationException($"Savepoint {token.Name} not found.");

            var savepoint = _savepoints[index];

            // Remove participants added after the savepoint
            var toRemove = _participants.Keys
                .Where(oid => !savepoint.ParticipantOidsAtSavepoint.Contains(oid))
                .ToList();

            foreach (var oid in toRemove)
            {
                // Notify handle of rollback
                if (_participants.TryRemove(oid, out var weakRef) &&
                    weakRef.TryGetTarget(out var handle) &&
                    handle is VayronHandle vh)
                {
                    vh.Invalidate();
                }
            }

            // Remove savepoints after this one
            if (index < _savepoints.Count - 1)
            {
                _savepoints.RemoveRange(index + 1, _savepoints.Count - index - 1);
            }
        }
    }

    /// <summary>
    /// Releases a savepoint (commits changes up to that point).
    /// </summary>
    public void ReleaseSavepoint(SavepointToken token)
    {
        lock (_savepointLock)
        {
            var index = _savepoints.FindIndex(s => s.Id == token.Id);
            if (index >= 0)
            {
                _savepoints.RemoveAt(index);
            }
        }
    }

    // =====================================================================
    // Lifecycle
    // =====================================================================

    /// <summary>
    /// Called before commit to validate and prepare participants.
    /// </summary>
    internal void OnCommitting()
    {
        if (State != TransactionState.Active)
            return;

        State = TransactionState.Committing;

        var args = new TransactionCommittingEventArgs(this);
        Committing?.Invoke(this, args);

        if (args.Cancel)
        {
            throw new TransactionAbortedException("Transaction commit was cancelled by an event handler.");
        }
    }

    /// <summary>
    /// Called after successful commit.
    /// </summary>
    internal void OnCommitted()
    {
        State = TransactionState.Committed;
        _stopwatch.Stop();

        Committed?.Invoke(this, new TransactionCommittedEventArgs(
            this,
            Elapsed,
            _operationCount,
            _participants.Count
        ));

        // Clear savepoints
        lock (_savepointLock)
        {
            _savepoints.Clear();
        }
    }

    /// <summary>
    /// Called when the transaction rolls back.
    /// </summary>
    internal void OnRolledBack(string? reason = null)
    {
        State = TransactionState.RolledBack;
        _stopwatch.Stop();

        RolledBack?.Invoke(this, new TransactionRolledBackEventArgs(this, reason));

        // Clear savepoints
        lock (_savepointLock)
        {
            _savepoints.Clear();
        }

        // Invalidate all participants
        foreach (var handle in GetParticipants())
        {
            if (handle is VayronHandle vh)
            {
                vh.Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets a summary of this transaction context.
    /// </summary>
    public TransactionSummary GetSummary()
    {
        return new TransactionSummary
        {
            Id = Id,
            Epoch = Epoch,
            IsWriteTransaction = IsWriteTransaction,
            State = State,
            StartTime = StartTime,
            Duration = Elapsed,
            ParticipantCount = _participants.Count,
            OperationCount = _operationCount,
            ReadCount = _readCount,
            WriteCount = _writeCount,
            SavepointCount = SavepointCount,
            IsTimedOut = IsTimedOut,
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stopwatch.Stop();

        if (State == TransactionState.Active)
        {
            OnRolledBack("Transaction disposed without commit");
        }

        _participants.Clear();
        _metadata.Clear();

        lock (_savepointLock)
        {
            _savepoints.Clear();
        }
    }

    // =====================================================================
    // Nested Types
    // =====================================================================

    private sealed class Savepoint
    {
        public int Id { get; }
        public string Name { get; }
        public int OperationCountAtSavepoint { get; }
        public HashSet<VayronOid> ParticipantOidsAtSavepoint { get; }

        public Savepoint(int id, string name, int operationCountAtSavepoint, HashSet<VayronOid> participantOidsAtSavepoint)
        {
            Id = id;
            Name = name;
            OperationCountAtSavepoint = operationCountAtSavepoint;
            ParticipantOidsAtSavepoint = participantOidsAtSavepoint;
        }
    }
}

/// <summary>
/// Token representing a savepoint.
/// </summary>
public readonly struct SavepointToken
{
    internal int Id { get; }
    public string Name { get; }

    internal SavepointToken(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => $"Savepoint[{Name}]";
}

/// <summary>
/// Transaction state.
/// </summary>
public enum TransactionState
{
    /// <summary>Transaction is active and accepting operations.</summary>
    Active,
    /// <summary>Transaction is in the process of committing.</summary>
    Committing,
    /// <summary>Transaction has been committed successfully.</summary>
    Committed,
    /// <summary>Transaction has been rolled back.</summary>
    RolledBack,
    /// <summary>Transaction has been disposed.</summary>
    Disposed,
}

/// <summary>
/// Type of operation.
/// </summary>
public enum OperationType
{
    Read,
    Write,
    Create,
    Delete,
    Materialize,
}

/// <summary>
/// Summary of a transaction.
/// </summary>
public readonly struct TransactionSummary
{
    public Guid Id { get; init; }
    public long Epoch { get; init; }
    public bool IsWriteTransaction { get; init; }
    public TransactionState State { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public TimeSpan Duration { get; init; }
    public int ParticipantCount { get; init; }
    public int OperationCount { get; init; }
    public int ReadCount { get; init; }
    public int WriteCount { get; init; }
    public int SavepointCount { get; init; }
    public bool IsTimedOut { get; init; }

    public override string ToString()
    {
        return $"Transaction[{Id:N8}] Epoch={Epoch} {(IsWriteTransaction ? "Write" : "Read")} " +
               $"State={State} Duration={Duration.TotalMilliseconds:F2}ms " +
               $"Ops={OperationCount} Participants={ParticipantCount}";
    }
}

// =====================================================================
// Event Args
// =====================================================================

/// <summary>
/// Event args for transaction committing event.
/// </summary>
public sealed class TransactionCommittingEventArgs : EventArgs
{
    public VayronTransactionContext Context { get; }
    public bool Cancel { get; set; }

    internal TransactionCommittingEventArgs(VayronTransactionContext context)
    {
        Context = context;
    }
}

/// <summary>
/// Event args for transaction committed event.
/// </summary>
public sealed class TransactionCommittedEventArgs : EventArgs
{
    public VayronTransactionContext Context { get; }
    public TimeSpan Duration { get; }
    public int OperationCount { get; }
    public int ParticipantCount { get; }

    internal TransactionCommittedEventArgs(
        VayronTransactionContext context,
        TimeSpan duration,
        int operationCount,
        int participantCount)
    {
        Context = context;
        Duration = duration;
        OperationCount = operationCount;
        ParticipantCount = participantCount;
    }
}

/// <summary>
/// Event args for transaction rolled back event.
/// </summary>
public sealed class TransactionRolledBackEventArgs : EventArgs
{
    public VayronTransactionContext Context { get; }
    public string? Reason { get; }

    internal TransactionRolledBackEventArgs(VayronTransactionContext context, string? reason)
    {
        Context = context;
        Reason = reason;
    }
}

/// <summary>
/// Event args for participant enrolled event.
/// </summary>
public sealed class ParticipantEnrolledEventArgs : EventArgs
{
    public VayronOid Oid { get; }
    public VayronTransactionContext Context { get; }

    internal ParticipantEnrolledEventArgs(VayronOid oid, VayronTransactionContext context)
    {
        Oid = oid;
        Context = context;
    }
}

/// <summary>
/// Event args for operation recorded event.
/// </summary>
public sealed class OperationRecordedEventArgs : EventArgs
{
    public OperationType Type { get; }
    public VayronOid Oid { get; }

    internal OperationRecordedEventArgs(OperationType type, VayronOid oid)
    {
        Type = type;
        Oid = oid;
    }
}

/// <summary>
/// Exception thrown when a transaction is aborted.
/// </summary>
public class TransactionAbortedException : Exception
{
    public TransactionAbortedException(string message) : base(message) { }
    public TransactionAbortedException(string message, Exception innerException) : base(message, innerException) { }
}
