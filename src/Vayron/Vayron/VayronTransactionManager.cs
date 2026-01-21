// VAYRON - Runtime-Integrated Persistent Storage
// Phase 4: Transaction Manager - Central manager for transaction lifecycle, statistics, and monitoring

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Vayron;

/// <summary>
/// Central manager for VAYRON transaction lifecycle, statistics, and monitoring.
/// </summary>
/// <remarks>
/// <para><b>Phase 4: Transaction Integration</b></para>
/// <para>
/// The transaction manager provides:
/// <list type="bullet">
/// <item><description>Global transaction statistics and monitoring</description></item>
/// <item><description>Transaction timeout enforcement</description></item>
/// <item><description>Active transaction tracking</description></item>
/// <item><description>Auto-transaction support for handle operations</description></item>
/// <item><description>Events for transaction lifecycle monitoring</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class VayronTransactionManager : IDisposable
{
    private static VayronTransactionManager? _instance;
    private static readonly object _initLock = new();

    private readonly ConcurrentDictionary<Guid, WeakReference<VayronTransactionScope>> _activeTransactions = new();
    private readonly Timer? _timeoutTimer;
    private readonly Options _options;

    // Statistics
    private long _totalReadTransactions;
    private long _totalWriteTransactions;
    private long _totalCommits;
    private long _totalRollbacks;
    private long _totalTimeouts;
    private long _totalDurationTicks;
    private long _maxDurationTicks;

    private bool _disposed;

    /// <summary>
    /// Gets the singleton instance of the transaction manager.
    /// </summary>
    public static VayronTransactionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_initLock)
                {
                    _instance ??= new VayronTransactionManager(new Options());
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Initializes the transaction manager with custom options.
    /// </summary>
    public static void Initialize(Options options)
    {
        lock (_initLock)
        {
            if (_instance != null)
            {
                _instance.Dispose();
            }
            _instance = new VayronTransactionManager(options);
        }
    }

    // Events

    /// <summary>
    /// Raised when any transaction starts.
    /// </summary>
    public event EventHandler<TransactionStartedEventArgs>? TransactionStarted;

    /// <summary>
    /// Raised when any transaction commits.
    /// </summary>
    public event EventHandler<TransactionCompletedEventArgs>? TransactionCommitted;

    /// <summary>
    /// Raised when any transaction rolls back.
    /// </summary>
    public event EventHandler<TransactionCompletedEventArgs>? TransactionRolledBack;

    /// <summary>
    /// Raised when a transaction times out.
    /// </summary>
    public event EventHandler<TransactionTimedOutEventArgs>? TransactionTimedOut;

    /// <summary>
    /// Raised when a long-running transaction is detected.
    /// </summary>
    public event EventHandler<LongRunningTransactionEventArgs>? LongRunningTransactionDetected;

    private VayronTransactionManager(Options options)
    {
        _options = options;

        if (options.EnableTimeoutEnforcement && options.TimeoutCheckInterval > TimeSpan.Zero)
        {
            _timeoutTimer = new Timer(
                CheckTimeouts,
                null,
                options.TimeoutCheckInterval,
                options.TimeoutCheckInterval
            );
        }
    }

    // =====================================================================
    // Transaction Registration
    // =====================================================================

    /// <summary>
    /// Registers a transaction with the manager.
    /// </summary>
    internal void RegisterTransaction(VayronTransactionScope scope)
    {
        if (_disposed) return;

        _activeTransactions[scope.Context.Id] = new WeakReference<VayronTransactionScope>(scope);

        if (scope.Context.IsWriteTransaction)
        {
            Interlocked.Increment(ref _totalWriteTransactions);
        }
        else
        {
            Interlocked.Increment(ref _totalReadTransactions);
        }

        TransactionStarted?.Invoke(this, new TransactionStartedEventArgs(
            scope.Context.Id,
            scope.Context.Epoch,
            scope.Context.IsWriteTransaction
        ));
    }

    /// <summary>
    /// Called when a transaction commits.
    /// </summary>
    internal void OnTransactionCommitted(VayronTransactionScope scope)
    {
        if (_disposed) return;

        _activeTransactions.TryRemove(scope.Context.Id, out _);
        Interlocked.Increment(ref _totalCommits);

        var durationTicks = scope.Context.Elapsed.Ticks;
        Interlocked.Add(ref _totalDurationTicks, durationTicks);

        // Update max duration atomically
        long currentMax = Volatile.Read(ref _maxDurationTicks);
        while (durationTicks > currentMax)
        {
            var original = Interlocked.CompareExchange(ref _maxDurationTicks, durationTicks, currentMax);
            if (original == currentMax) break;
            currentMax = original;
        }

        TransactionCommitted?.Invoke(this, new TransactionCompletedEventArgs(
            scope.Context.Id,
            scope.Context.Epoch,
            scope.Context.IsWriteTransaction,
            scope.Context.Elapsed,
            scope.Context.OperationCount,
            scope.Context.ParticipantCount,
            wasCommitted: true
        ));
    }

    /// <summary>
    /// Called when a transaction rolls back.
    /// </summary>
    internal void OnTransactionRolledBack(VayronTransactionScope scope, string? reason)
    {
        if (_disposed) return;

        _activeTransactions.TryRemove(scope.Context.Id, out _);
        Interlocked.Increment(ref _totalRollbacks);

        TransactionRolledBack?.Invoke(this, new TransactionCompletedEventArgs(
            scope.Context.Id,
            scope.Context.Epoch,
            scope.Context.IsWriteTransaction,
            scope.Context.Elapsed,
            scope.Context.OperationCount,
            scope.Context.ParticipantCount,
            wasCommitted: false,
            rollbackReason: reason
        ));
    }

    // =====================================================================
    // Timeout Management
    // =====================================================================

    private void CheckTimeouts(object? state)
    {
        if (_disposed) return;

        var now = DateTimeOffset.UtcNow;
        var toRemove = new List<Guid>();

        foreach (var kvp in _activeTransactions)
        {
            if (!kvp.Value.TryGetTarget(out var scope))
            {
                toRemove.Add(kvp.Key);
                continue;
            }

            // Check for timeout
            if (scope.Context.Timeout.HasValue && scope.Context.IsTimedOut)
            {
                Interlocked.Increment(ref _totalTimeouts);
                TransactionTimedOut?.Invoke(this, new TransactionTimedOutEventArgs(
                    scope.Context.Id,
                    scope.Context.Epoch,
                    scope.Context.Elapsed,
                    scope.Context.Timeout.Value
                ));

                // Force rollback if configured
                if (_options.ForceRollbackOnTimeout)
                {
                    try
                    {
                        scope.ForceRollback("Transaction timeout exceeded");
                    }
                    catch
                    {
                        // Ignore errors during forced rollback
                    }
                }
            }

            // Check for long-running transaction
            if (_options.LongRunningThreshold.HasValue &&
                scope.Context.Elapsed > _options.LongRunningThreshold.Value)
            {
                LongRunningTransactionDetected?.Invoke(this, new LongRunningTransactionEventArgs(
                    scope.Context.Id,
                    scope.Context.Epoch,
                    scope.Context.Elapsed,
                    _options.LongRunningThreshold.Value
                ));
            }
        }

        // Clean up dead references
        foreach (var id in toRemove)
        {
            _activeTransactions.TryRemove(id, out _);
        }
    }

    // =====================================================================
    // Auto-Transaction Support
    // =====================================================================

    /// <summary>
    /// Gets or creates a transaction for the current async context.
    /// </summary>
    /// <remarks>
    /// If a transaction exists, returns it. Otherwise, creates a new read transaction.
    /// </remarks>
    public VayronTransactionScope GetOrCreateReadTransaction(VayronEnvironment environment)
    {
        var current = VayronTransaction.Current;
        if (current != null)
        {
            return current.AddRef();
        }

        return VayronTransaction.BeginRead(environment);
    }

    /// <summary>
    /// Executes an action within a read transaction.
    /// </summary>
    public void ExecuteInReadTransaction(VayronEnvironment environment, Action<VayronTransactionScope> action)
    {
        using var tx = GetOrCreateReadTransaction(environment);
        action(tx);
    }

    /// <summary>
    /// Executes a function within a read transaction.
    /// </summary>
    public T ExecuteInReadTransaction<T>(VayronEnvironment environment, Func<VayronTransactionScope, T> func)
    {
        using var tx = GetOrCreateReadTransaction(environment);
        return func(tx);
    }

    /// <summary>
    /// Executes an action within a write transaction with automatic commit.
    /// </summary>
    public void ExecuteInWriteTransaction(VayronEnvironment environment, Action<VayronTransactionScope> action)
    {
        using var tx = VayronTransaction.BeginWrite(environment);
        action(tx);
        tx.Commit();
    }

    /// <summary>
    /// Executes a function within a write transaction with automatic commit.
    /// </summary>
    public T ExecuteInWriteTransaction<T>(VayronEnvironment environment, Func<VayronTransactionScope, T> func)
    {
        using var tx = VayronTransaction.BeginWrite(environment);
        var result = func(tx);
        tx.Commit();
        return result;
    }

    /// <summary>
    /// Executes an async action within a read transaction.
    /// </summary>
    public async Task ExecuteInReadTransactionAsync(
        VayronEnvironment environment,
        Func<VayronTransactionScope, Task> action)
    {
        using var tx = GetOrCreateReadTransaction(environment);
        await action(tx);
    }

    /// <summary>
    /// Executes an async function within a read transaction.
    /// </summary>
    public async Task<T> ExecuteInReadTransactionAsync<T>(
        VayronEnvironment environment,
        Func<VayronTransactionScope, Task<T>> func)
    {
        using var tx = GetOrCreateReadTransaction(environment);
        return await func(tx);
    }

    /// <summary>
    /// Executes an async action within a write transaction with automatic commit.
    /// </summary>
    public async Task ExecuteInWriteTransactionAsync(
        VayronEnvironment environment,
        Func<VayronTransactionScope, Task> action)
    {
        using var tx = VayronTransaction.BeginWrite(environment);
        await action(tx);
        tx.Commit();
    }

    /// <summary>
    /// Executes an async function within a write transaction with automatic commit.
    /// </summary>
    public async Task<T> ExecuteInWriteTransactionAsync<T>(
        VayronEnvironment environment,
        Func<VayronTransactionScope, Task<T>> func)
    {
        using var tx = VayronTransaction.BeginWrite(environment);
        var result = await func(tx);
        tx.Commit();
        return result;
    }

    // =====================================================================
    // Active Transaction Queries
    // =====================================================================

    /// <summary>
    /// Gets the count of currently active transactions.
    /// </summary>
    public int ActiveTransactionCount
    {
        get
        {
            // Clean up dead references and count
            int count = 0;
            var toRemove = new List<Guid>();

            foreach (var kvp in _activeTransactions)
            {
                if (kvp.Value.TryGetTarget(out _))
                {
                    count++;
                }
                else
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _activeTransactions.TryRemove(id, out _);
            }

            return count;
        }
    }

    /// <summary>
    /// Gets information about all active transactions.
    /// </summary>
    public IEnumerable<TransactionSummary> GetActiveTransactions()
    {
        var toRemove = new List<Guid>();

        foreach (var kvp in _activeTransactions)
        {
            if (kvp.Value.TryGetTarget(out var scope))
            {
                yield return scope.Context.GetSummary();
            }
            else
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var id in toRemove)
        {
            _activeTransactions.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Checks if there are any active write transactions.
    /// </summary>
    public bool HasActiveWriteTransaction
    {
        get
        {
            foreach (var kvp in _activeTransactions)
            {
                if (kvp.Value.TryGetTarget(out var scope) && scope.Context.IsWriteTransaction)
                {
                    return true;
                }
            }
            return false;
        }
    }

    // =====================================================================
    // Statistics
    // =====================================================================

    /// <summary>
    /// Gets comprehensive transaction statistics.
    /// </summary>
    public TransactionStatistics GetStatistics()
    {
        var totalTransactions = _totalReadTransactions + _totalWriteTransactions;
        var avgDurationTicks = totalTransactions > 0
            ? _totalDurationTicks / totalTransactions
            : 0;

        return new TransactionStatistics
        {
            TotalReadTransactions = Volatile.Read(ref _totalReadTransactions),
            TotalWriteTransactions = Volatile.Read(ref _totalWriteTransactions),
            TotalCommits = Volatile.Read(ref _totalCommits),
            TotalRollbacks = Volatile.Read(ref _totalRollbacks),
            TotalTimeouts = Volatile.Read(ref _totalTimeouts),
            ActiveTransactions = ActiveTransactionCount,
            AverageDuration = TimeSpan.FromTicks(avgDurationTicks),
            MaxDuration = TimeSpan.FromTicks(Volatile.Read(ref _maxDurationTicks)),
        };
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalReadTransactions, 0);
        Interlocked.Exchange(ref _totalWriteTransactions, 0);
        Interlocked.Exchange(ref _totalCommits, 0);
        Interlocked.Exchange(ref _totalRollbacks, 0);
        Interlocked.Exchange(ref _totalTimeouts, 0);
        Interlocked.Exchange(ref _totalDurationTicks, 0);
        Interlocked.Exchange(ref _maxDurationTicks, 0);
    }

    // =====================================================================
    // Disposal
    // =====================================================================

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timeoutTimer?.Dispose();
        _activeTransactions.Clear();
    }

    // =====================================================================
    // Options
    // =====================================================================

    /// <summary>
    /// Options for the transaction manager.
    /// </summary>
    public sealed class Options
    {
        /// <summary>
        /// Default timeout for write transactions (null = no timeout).
        /// </summary>
        public TimeSpan? DefaultWriteTimeout { get; init; }

        /// <summary>
        /// Default timeout for read transactions (null = no timeout).
        /// </summary>
        public TimeSpan? DefaultReadTimeout { get; init; }

        /// <summary>
        /// Whether to enforce timeouts by background checks.
        /// </summary>
        public bool EnableTimeoutEnforcement { get; init; } = true;

        /// <summary>
        /// How often to check for timeouts.
        /// </summary>
        public TimeSpan TimeoutCheckInterval { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Whether to force rollback when timeout is exceeded.
        /// </summary>
        public bool ForceRollbackOnTimeout { get; init; } = false;

        /// <summary>
        /// Threshold for detecting long-running transactions.
        /// </summary>
        public TimeSpan? LongRunningThreshold { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Whether to auto-create read transactions when accessing handles without a transaction.
        /// </summary>
        public bool EnableAutoReadTransaction { get; init; } = false;
    }
}

/// <summary>
/// Transaction statistics.
/// </summary>
public readonly struct TransactionStatistics
{
    public long TotalReadTransactions { get; init; }
    public long TotalWriteTransactions { get; init; }
    public long TotalCommits { get; init; }
    public long TotalRollbacks { get; init; }
    public long TotalTimeouts { get; init; }
    public int ActiveTransactions { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public TimeSpan MaxDuration { get; init; }

    public long TotalTransactions => TotalReadTransactions + TotalWriteTransactions;
    public double CommitRate => TotalTransactions > 0
        ? (double)TotalCommits / TotalTransactions * 100
        : 0;
    public double RollbackRate => TotalTransactions > 0
        ? (double)TotalRollbacks / TotalTransactions * 100
        : 0;

    public override string ToString()
    {
        return $"Transactions: {TotalTransactions} (Read={TotalReadTransactions}, Write={TotalWriteTransactions}) " +
               $"Commits={TotalCommits} ({CommitRate:F1}%) Rollbacks={TotalRollbacks} " +
               $"Active={ActiveTransactions} AvgDuration={AverageDuration.TotalMilliseconds:F2}ms";
    }
}

// =====================================================================
// Event Args
// =====================================================================

/// <summary>
/// Event args for transaction started event.
/// </summary>
public sealed class TransactionStartedEventArgs : EventArgs
{
    public Guid TransactionId { get; }
    public long Epoch { get; }
    public bool IsWriteTransaction { get; }

    internal TransactionStartedEventArgs(Guid transactionId, long epoch, bool isWriteTransaction)
    {
        TransactionId = transactionId;
        Epoch = epoch;
        IsWriteTransaction = isWriteTransaction;
    }
}

/// <summary>
/// Event args for transaction completed event.
/// </summary>
public sealed class TransactionCompletedEventArgs : EventArgs
{
    public Guid TransactionId { get; }
    public long Epoch { get; }
    public bool IsWriteTransaction { get; }
    public TimeSpan Duration { get; }
    public int OperationCount { get; }
    public int ParticipantCount { get; }
    public bool WasCommitted { get; }
    public string? RollbackReason { get; }

    internal TransactionCompletedEventArgs(
        Guid transactionId,
        long epoch,
        bool isWriteTransaction,
        TimeSpan duration,
        int operationCount,
        int participantCount,
        bool wasCommitted,
        string? rollbackReason = null)
    {
        TransactionId = transactionId;
        Epoch = epoch;
        IsWriteTransaction = isWriteTransaction;
        Duration = duration;
        OperationCount = operationCount;
        ParticipantCount = participantCount;
        WasCommitted = wasCommitted;
        RollbackReason = rollbackReason;
    }
}

/// <summary>
/// Event args for transaction timed out event.
/// </summary>
public sealed class TransactionTimedOutEventArgs : EventArgs
{
    public Guid TransactionId { get; }
    public long Epoch { get; }
    public TimeSpan Elapsed { get; }
    public TimeSpan Timeout { get; }

    internal TransactionTimedOutEventArgs(Guid transactionId, long epoch, TimeSpan elapsed, TimeSpan timeout)
    {
        TransactionId = transactionId;
        Epoch = epoch;
        Elapsed = elapsed;
        Timeout = timeout;
    }
}

/// <summary>
/// Event args for long-running transaction detection.
/// </summary>
public sealed class LongRunningTransactionEventArgs : EventArgs
{
    public Guid TransactionId { get; }
    public long Epoch { get; }
    public TimeSpan Elapsed { get; }
    public TimeSpan Threshold { get; }

    internal LongRunningTransactionEventArgs(Guid transactionId, long epoch, TimeSpan elapsed, TimeSpan threshold)
    {
        TransactionId = transactionId;
        Epoch = epoch;
        Elapsed = elapsed;
        Threshold = threshold;
    }
}
