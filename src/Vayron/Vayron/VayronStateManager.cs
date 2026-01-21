// VAYRON - Runtime-Integrated Persistent Storage
// State Machine Manager for handle materialization
//
// Phase 3: Formal state transition management with validation and diagnostics

using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace Vayron;

/// <summary>
/// Manages the materialization state machine for VAYRON handles.
/// Provides state transition validation, diagnostics, and global statistics.
/// </summary>
/// <remarks>
/// <para><b>State Diagram:</b></para>
/// <code>
/// ┌──────────────────┐
/// │  NotMaterialized │ ◄─────────────────────────────────────────────┐
/// └────────┬─────────┘                                               │
///          │                                                         │
///          │ (Begin load)                                            │
///          ▼                                                         │
/// ┌──────────────────┐                                               │
/// │   Materializing  │                                               │
/// └────────┬─────────┘                                               │
///          │                                                         │
///          │ (Load complete)                (Evict/Invalidate)       │
///          ▼                                     │                   │
/// ┌──────────────────┐                           │                   │
/// │   Materialized   │───────────────────────────┼─────────────────► │ Stale
/// └────────┬─────────┘                           │                   │
///          │                                     │                   │
///          │ (Modify field)                      │                   │
///          ▼                                     │                   │
/// ┌──────────────────┐                           │                   │
/// │      Dirty       │───────────────────────────┘                   │
/// └────────┬─────────┘                                               │
///          │                                                         │
///          │ (Persist/Commit)                                        │
///          ▼                                                         │
///          └────────────────────► Materialized ──────────────────────┘
/// </code>
///
/// <para><b>Valid Transitions:</b></para>
/// <list type="bullet">
/// <item><description>NotMaterialized → Materializing (begin load)</description></item>
/// <item><description>NotMaterialized → Dirty (new object created)</description></item>
/// <item><description>Materializing → Materialized (load complete)</description></item>
/// <item><description>Materializing → Stale (load failed)</description></item>
/// <item><description>Materialized → Dirty (field modified)</description></item>
/// <item><description>Materialized → Stale (evicted/invalidated)</description></item>
/// <item><description>Dirty → Materialized (persisted)</description></item>
/// <item><description>Dirty → Stale (evicted with data loss)</description></item>
/// <item><description>Stale → Materializing (reload)</description></item>
/// <item><description>Stale → NotMaterialized (full reset)</description></item>
/// </list>
/// </remarks>
public static class VayronStateManager
{
    // =====================================================================
    // State Transition Graph (Frozen for performance)
    // =====================================================================

    /// <summary>
    /// Valid transitions from each state.
    /// </summary>
    private static readonly FrozenDictionary<MaterializationState, FrozenSet<MaterializationState>> ValidTransitions;

    /// <summary>
    /// Static constructor to initialize the transition graph.
    /// </summary>
    static VayronStateManager()
    {
        ValidTransitions = new Dictionary<MaterializationState, FrozenSet<MaterializationState>>
        {
            [MaterializationState.NotMaterialized] = new HashSet<MaterializationState>
            {
                MaterializationState.Materializing,
                MaterializationState.Dirty,  // New object created directly
                MaterializationState.Materialized, // Direct materialization (Phase 3: hot path)
            }.ToFrozenSet(),

            [MaterializationState.Materializing] = new HashSet<MaterializationState>
            {
                MaterializationState.Materialized,
                MaterializationState.Stale,  // Load failed
            }.ToFrozenSet(),

            [MaterializationState.Materialized] = new HashSet<MaterializationState>
            {
                MaterializationState.Dirty,
                MaterializationState.Stale,
            }.ToFrozenSet(),

            [MaterializationState.Dirty] = new HashSet<MaterializationState>
            {
                MaterializationState.Materialized,  // After persist
                MaterializationState.Stale,  // Evicted with data loss (should be rare)
            }.ToFrozenSet(),

            [MaterializationState.Stale] = new HashSet<MaterializationState>
            {
                MaterializationState.Materializing,
                MaterializationState.NotMaterialized,
                MaterializationState.Materialized, // Direct re-materialization (Phase 3: hot path)
            }.ToFrozenSet(),
        }.ToFrozenDictionary();
    }

    // =====================================================================
    // State Transition Validation
    // =====================================================================

    /// <summary>
    /// Checks if a state transition is valid.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The desired next state.</param>
    /// <returns>True if the transition is valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidTransition(MaterializationState from, MaterializationState to)
    {
        if (from == to)
            return true;

        return ValidTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    /// <summary>
    /// Gets the valid transitions from a given state.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <returns>The set of valid target states.</returns>
    public static IReadOnlySet<MaterializationState> GetValidTransitions(MaterializationState from)
    {
        return ValidTransitions.TryGetValue(from, out var targets)
            ? targets
            : FrozenSet<MaterializationState>.Empty;
    }

    /// <summary>
    /// Validates and performs a state transition, throwing if invalid.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The desired next state.</param>
    /// <exception cref="InvalidOperationException">Thrown if the transition is invalid.</exception>
    public static void ValidateTransition(MaterializationState from, MaterializationState to)
    {
        if (!IsValidTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid state transition from {from} to {to}. " +
                $"Valid transitions from {from}: {string.Join(", ", GetValidTransitions(from))}");
        }
    }

    // =====================================================================
    // State Query Helpers
    // =====================================================================

    /// <summary>
    /// Checks if the state indicates the body is available in memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBodyAvailable(MaterializationState state)
    {
        return state is MaterializationState.Materialized or MaterializationState.Dirty;
    }

    /// <summary>
    /// Checks if the state indicates the body needs to be loaded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsLoad(MaterializationState state)
    {
        return state is MaterializationState.NotMaterialized or MaterializationState.Stale;
    }

    /// <summary>
    /// Checks if the state indicates the body is being loaded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLoading(MaterializationState state)
    {
        return state == MaterializationState.Materializing;
    }

    /// <summary>
    /// Checks if the state indicates pending writes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasPendingWrites(MaterializationState state)
    {
        return state == MaterializationState.Dirty;
    }

    /// <summary>
    /// Checks if the body can be safely evicted (no pending writes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanEvict(MaterializationState state)
    {
        return state == MaterializationState.Materialized;
    }

    // =====================================================================
    // Global Statistics
    // =====================================================================

    private static long _totalTransitions;
    private static long _invalidTransitionAttempts;
    private static readonly long[] _stateCounters = new long[5]; // One per state

    /// <summary>
    /// Records a state transition for statistics.
    /// </summary>
    internal static void RecordTransition(MaterializationState from, MaterializationState to)
    {
        Interlocked.Increment(ref _totalTransitions);
        Interlocked.Increment(ref _stateCounters[(int)to]);
    }

    /// <summary>
    /// Records an invalid transition attempt for statistics.
    /// </summary>
    internal static void RecordInvalidTransitionAttempt()
    {
        Interlocked.Increment(ref _invalidTransitionAttempts);
    }

    /// <summary>
    /// Gets the total number of state transitions.
    /// </summary>
    public static long TotalTransitions => Volatile.Read(ref _totalTransitions);

    /// <summary>
    /// Gets the number of invalid transition attempts.
    /// </summary>
    public static long InvalidTransitionAttempts => Volatile.Read(ref _invalidTransitionAttempts);

    /// <summary>
    /// Gets the number of transitions to a specific state.
    /// </summary>
    public static long GetStateTransitionCount(MaterializationState state)
    {
        return Volatile.Read(ref _stateCounters[(int)state]);
    }

    /// <summary>
    /// Gets all state statistics.
    /// </summary>
    public static StateStatistics GetStatistics()
    {
        return new StateStatistics
        {
            TotalTransitions = TotalTransitions,
            InvalidTransitionAttempts = InvalidTransitionAttempts,
            NotMaterializedCount = GetStateTransitionCount(MaterializationState.NotMaterialized),
            MaterializingCount = GetStateTransitionCount(MaterializationState.Materializing),
            MaterializedCount = GetStateTransitionCount(MaterializationState.Materialized),
            DirtyCount = GetStateTransitionCount(MaterializationState.Dirty),
            StaleCount = GetStateTransitionCount(MaterializationState.Stale),
        };
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public static void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalTransitions, 0);
        Interlocked.Exchange(ref _invalidTransitionAttempts, 0);
        for (int i = 0; i < _stateCounters.Length; i++)
        {
            Interlocked.Exchange(ref _stateCounters[i], 0);
        }
    }

    // =====================================================================
    // State Transition Tracking (Optional, for debugging)
    // =====================================================================

    /// <summary>
    /// Event raised when any state transition occurs.
    /// </summary>
    public static event EventHandler<GlobalStateChangedEventArgs>? GlobalStateChanged;

    /// <summary>
    /// Raises the global state changed event.
    /// </summary>
    internal static void RaiseGlobalStateChanged(VayronOid oid, MaterializationState from, MaterializationState to)
    {
        GlobalStateChanged?.Invoke(null, new GlobalStateChangedEventArgs(oid, from, to));
    }

    /// <summary>
    /// Gets a human-readable description of a state.
    /// </summary>
    public static string GetStateDescription(MaterializationState state) => state switch
    {
        MaterializationState.NotMaterialized => "Body not loaded from storage",
        MaterializationState.Materializing => "Body currently being loaded",
        MaterializationState.Materialized => "Body loaded and cached",
        MaterializationState.Dirty => "Body modified, pending write",
        MaterializationState.Stale => "Cached body is stale, needs reload",
        _ => "Unknown state"
    };

    /// <summary>
    /// Gets a short code for a state (for logging).
    /// </summary>
    public static string GetStateCode(MaterializationState state) => state switch
    {
        MaterializationState.NotMaterialized => "NM",
        MaterializationState.Materializing => "MZ",
        MaterializationState.Materialized => "MT",
        MaterializationState.Dirty => "DY",
        MaterializationState.Stale => "ST",
        _ => "??"
    };
}

/// <summary>
/// Statistics about state transitions.
/// </summary>
public readonly struct StateStatistics
{
    /// <summary>Total number of state transitions.</summary>
    public long TotalTransitions { get; init; }

    /// <summary>Number of invalid transition attempts.</summary>
    public long InvalidTransitionAttempts { get; init; }

    /// <summary>Transitions to NotMaterialized.</summary>
    public long NotMaterializedCount { get; init; }

    /// <summary>Transitions to Materializing.</summary>
    public long MaterializingCount { get; init; }

    /// <summary>Transitions to Materialized.</summary>
    public long MaterializedCount { get; init; }

    /// <summary>Transitions to Dirty.</summary>
    public long DirtyCount { get; init; }

    /// <summary>Transitions to Stale.</summary>
    public long StaleCount { get; init; }

    public override string ToString()
    {
        return $"Transitions: {TotalTransitions} | Invalid: {InvalidTransitionAttempts} | " +
               $"NM={NotMaterializedCount} MZ={MaterializingCount} MT={MaterializedCount} DY={DirtyCount} ST={StaleCount}";
    }
}

/// <summary>
/// Event arguments for global state change tracking.
/// </summary>
public sealed class GlobalStateChangedEventArgs : EventArgs
{
    /// <summary>The OID of the handle that changed.</summary>
    public VayronOid Oid { get; }

    /// <summary>The previous state.</summary>
    public MaterializationState OldState { get; }

    /// <summary>The new state.</summary>
    public MaterializationState NewState { get; }

    /// <summary>When the transition occurred.</summary>
    public DateTimeOffset Timestamp { get; }

    public GlobalStateChangedEventArgs(VayronOid oid, MaterializationState oldState, MaterializationState newState)
    {
        Oid = oid;
        OldState = oldState;
        NewState = newState;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
