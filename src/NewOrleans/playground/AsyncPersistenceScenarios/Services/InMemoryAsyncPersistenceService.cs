using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DOTNExT.Persistence;

namespace AsyncPersistenceScenarios.Services;

/// <summary>
/// In-memory implementation of persistence service.
/// Used for testing and prototyping. Provides full observability via events.
///
/// This implementation:
/// 1. Stores state machine snapshots in memory
/// 2. Uses reflection to extract/restore field values
/// 3. Fires events for all operations (for observability)
/// 4. Can optionally persist to JSON file for process restart testing
///
/// Implements both:
/// - IAsyncPersistenceServiceGeneric (generic, for type-safe test code)
/// - DOTNExT.Persistence.IAsyncPersistenceService (object-based, for Roslyn codegen)
/// </summary>
public class InMemoryAsyncPersistenceService : IAsyncPersistenceServiceGeneric, DOTNExT.Persistence.IAsyncPersistenceService
{
    private readonly ConcurrentDictionary<string, StateMachineSnapshot> _snapshots = new();
    private readonly HashSet<string> _frozenMethods = new();
    private readonly string? _persistenceFilePath;
    private readonly bool _verbose;

    /// <summary>
    /// Fired when a checkpoint is created.
    /// </summary>
    public event EventHandler<CheckpointEventArgs>? OnCheckpoint;

    /// <summary>
    /// Fired when a state machine is restored from a checkpoint.
    /// </summary>
    public event EventHandler<RestoreEventArgs>? OnRestore;

    /// <summary>
    /// Fired when a workflow completes (success or fault).
    /// </summary>
    public event EventHandler<CompleteEventArgs>? OnComplete;

    /// <summary>
    /// Fired when a workflow faults with an exception.
    /// </summary>
    public event EventHandler<FaultEventArgs>? OnFault;

    /// <summary>
    /// Creates a new in-memory persistence service.
    /// </summary>
    /// <param name="persistenceFilePath">Optional path to persist snapshots to disk (for process restart testing)</param>
    /// <param name="verbose">If true, logs all operations to console</param>
    public InMemoryAsyncPersistenceService(string? persistenceFilePath = null, bool verbose = true)
    {
        _persistenceFilePath = persistenceFilePath;
        _verbose = verbose;

        // Load from file if exists
        if (!string.IsNullOrEmpty(_persistenceFilePath) && File.Exists(_persistenceFilePath))
        {
            try
            {
                var json = File.ReadAllText(_persistenceFilePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, StateMachineSnapshot>>(json);
                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                    {
                        _snapshots[kvp.Key] = kvp.Value;
                    }
                    if (_verbose)
                    {
                        Console.WriteLine($"[Persistence] Loaded {_snapshots.Count} snapshots from {_persistenceFilePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                {
                    Console.WriteLine($"[Persistence] Failed to load from file: {ex.Message}");
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Checkpoint<TStateMachine>(
        ref TStateMachine stateMachine,
        int stateNumber,
        string methodId)
        where TStateMachine : IAsyncStateMachine
    {
        // If frozen, ignore this checkpoint (simulating process crash)
        if (IsFrozen(methodId))
        {
            if (_verbose)
            {
                Console.WriteLine($"[Persistence] CHECKPOINT IGNORED (frozen): {methodId}");
            }
            return;
        }

        var snapshot = SerializeStateMachine(ref stateMachine, stateNumber);
        _snapshots[methodId] = snapshot;

        if (_verbose)
        {
            Console.WriteLine($"[Persistence] CHECKPOINT: {methodId} at state {stateNumber}");
            Console.WriteLine($"             Fields captured: {string.Join(", ", snapshot.Fields.Keys)}");
        }

        OnCheckpoint?.Invoke(this, new CheckpointEventArgs(methodId, stateNumber, snapshot));

        // Persist to file if configured
        PersistToFile();
    }

    /// <inheritdoc/>
    public bool TryRestore<TStateMachine>(
        ref TStateMachine stateMachine,
        string methodId,
        out int restoredState)
        where TStateMachine : IAsyncStateMachine
    {
        if (_snapshots.TryGetValue(methodId, out var snapshot))
        {
            DeserializeStateMachine(ref stateMachine, snapshot);
            restoredState = snapshot.State;

            if (_verbose)
            {
                Console.WriteLine($"[Persistence] RESTORE: {methodId} from state {restoredState}");
                Console.WriteLine($"             Fields restored: {string.Join(", ", snapshot.Fields.Keys)}");
            }

            OnRestore?.Invoke(this, new RestoreEventArgs(methodId, restoredState));
            return true;
        }

        restoredState = -1;
        return false;
    }

    /// <inheritdoc/>
    public void Complete(string methodId, object? result)
    {
        // If frozen, ignore (simulating process crash - workflow never completes)
        if (IsFrozen(methodId))
        {
            if (_verbose)
            {
                Console.WriteLine($"[Persistence] COMPLETE IGNORED (frozen): {methodId}");
            }
            return;
        }

        _snapshots.TryRemove(methodId, out _);

        if (_verbose)
        {
            Console.WriteLine($"[Persistence] COMPLETE: {methodId} with result: {result ?? "null"}");
        }

        OnComplete?.Invoke(this, new CompleteEventArgs(methodId, result));

        PersistToFile();
    }

    /// <inheritdoc/>
    public void Fault(string methodId, Exception exception)
    {
        // If frozen, ignore (simulating process crash)
        if (IsFrozen(methodId))
        {
            if (_verbose)
            {
                Console.WriteLine($"[Persistence] FAULT IGNORED (frozen): {methodId}");
            }
            return;
        }

        // Keep the snapshot for potential retry/investigation
        if (_verbose)
        {
            Console.WriteLine($"[Persistence] FAULT: {methodId} with exception: {exception.Message}");
        }

        OnFault?.Invoke(this, new FaultEventArgs(methodId, exception));
        OnComplete?.Invoke(this, new CompleteEventArgs(methodId, null, faulted: true, exception));
    }

    /// <inheritdoc/>
    public bool HasPersistedState(string methodId)
    {
        return _snapshots.ContainsKey(methodId);
    }

    /// <inheritdoc/>
    public void Clear(string methodId)
    {
        _snapshots.TryRemove(methodId, out _);
        if (_verbose)
        {
            Console.WriteLine($"[Persistence] CLEAR: {methodId}");
        }
        PersistToFile();
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetPersistedMethodIds()
    {
        return _snapshots.Keys.ToList();
    }

    /// <summary>
    /// Gets the snapshot for a method (for inspection/debugging).
    /// </summary>
    public StateMachineSnapshot? GetSnapshot(string methodId)
    {
        return _snapshots.TryGetValue(methodId, out var snapshot) ? snapshot : null;
    }

    /// <summary>
    /// Clears all persisted state.
    /// </summary>
    public void ClearAll()
    {
        _snapshots.Clear();
        if (_verbose)
        {
            Console.WriteLine("[Persistence] CLEAR ALL");
        }
        PersistToFile();
    }

    /// <summary>
    /// Gets count of persisted snapshots.
    /// </summary>
    public int Count => _snapshots.Count;

    /// <summary>
    /// Freezes a method ID, preventing any further state changes (Checkpoint, Complete, Fault).
    /// Used to simulate a process crash where the workflow stops mid-execution.
    /// </summary>
    public void Freeze(string methodId)
    {
        lock (_frozenMethods)
        {
            _frozenMethods.Add(methodId);
        }
        if (_verbose)
        {
            Console.WriteLine($"[Persistence] FREEZE: {methodId} - no further state changes allowed");
        }
    }

    /// <summary>
    /// Unfreezes a method ID, allowing state changes again.
    /// Call this before resuming a workflow.
    /// </summary>
    public void Unfreeze(string methodId)
    {
        lock (_frozenMethods)
        {
            _frozenMethods.Remove(methodId);
        }
        if (_verbose)
        {
            Console.WriteLine($"[Persistence] UNFREEZE: {methodId}");
        }
    }

    /// <summary>
    /// Checks if a method ID is frozen.
    /// </summary>
    public bool IsFrozen(string methodId)
    {
        lock (_frozenMethods)
        {
            return _frozenMethods.Contains(methodId);
        }
    }

    private StateMachineSnapshot SerializeStateMachine<T>(ref T sm, int state)
        where T : IAsyncStateMachine
    {
        var type = typeof(T);
        var fields = new Dictionary<string, object?>();

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var fieldName = field.Name;

            // Skip awaiter fields - these are transient
            if (fieldName.Contains("__awaiter") || fieldName.Contains("<>u__"))
                continue;

            // Skip builder field - recreated on resume
            if (fieldName.Contains("__builder") || fieldName.Contains("<>t__builder"))
                continue;

            // Skip captured outer class reference - comes from caller, can't serialize
            if (fieldName.Contains("<>4__this"))
                continue;

            // Skip state field - we track this separately
            if (fieldName.Contains("__state") || fieldName.Contains("<>1__state"))
                continue;

            try
            {
                // Box the struct to get field value
                object boxed = sm!;
                var value = field.GetValue(boxed);
                fields[fieldName] = value;
            }
            catch (Exception ex)
            {
                if (_verbose)
                {
                    Console.WriteLine($"[Persistence] Warning: Could not serialize field {fieldName}: {ex.Message}");
                }
            }
        }

        return new StateMachineSnapshot
        {
            State = state,
            TypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            Fields = fields,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private void DeserializeStateMachine<T>(ref T sm, StateMachineSnapshot snapshot)
        where T : IAsyncStateMachine
    {
        var type = typeof(T);

        // Box the struct so we can modify it
        object boxed = sm!;

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var fieldName = field.Name;

            // Set state field
            if (fieldName.Contains("__state") || fieldName.Contains("<>1__state"))
            {
                field.SetValue(boxed, snapshot.State);
                continue;
            }

            // Restore other fields from snapshot
            if (snapshot.Fields.TryGetValue(fieldName, out var value))
            {
                try
                {
                    // Handle type conversion if needed (e.g., from JsonElement)
                    if (value is JsonElement je)
                    {
                        value = ConvertJsonElement(je, field.FieldType);
                    }

                    field.SetValue(boxed, value);
                }
                catch (Exception ex)
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"[Persistence] Warning: Could not restore field {fieldName}: {ex.Message}");
                    }
                }
            }
        }

        // Unbox back to the ref parameter
        sm = (T)boxed;
    }

    private object? ConvertJsonElement(JsonElement element, Type targetType)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when targetType == typeof(int) => element.GetInt32(),
            JsonValueKind.Number when targetType == typeof(long) => element.GetInt64(),
            JsonValueKind.Number when targetType == typeof(double) => element.GetDouble(),
            JsonValueKind.Number when targetType == typeof(float) => element.GetSingle(),
            JsonValueKind.Number when targetType == typeof(decimal) => element.GetDecimal(),
            JsonValueKind.String when targetType == typeof(string) => element.GetString(),
            JsonValueKind.String when targetType == typeof(Guid) => Guid.Parse(element.GetString()!),
            JsonValueKind.String when targetType == typeof(DateTime) => element.GetDateTime(),
            JsonValueKind.String when targetType == typeof(DateTimeOffset) => element.GetDateTimeOffset(),
            _ => JsonSerializer.Deserialize(element.GetRawText(), targetType)
        };
    }

    private void PersistToFile()
    {
        if (string.IsNullOrEmpty(_persistenceFilePath))
            return;

        try
        {
            var json = JsonSerializer.Serialize(_snapshots, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_persistenceFilePath, json);
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"[Persistence] Failed to persist to file: {ex.Message}");
            }
        }
    }

    // ========================================================================
    // DOTNExT.Persistence.IAsyncPersistenceService implementation
    // These methods are called by Roslyn-generated state machines
    // ========================================================================

    /// <summary>
    /// Checkpoint implementation for Roslyn-generated code.
    /// Takes a boxed state machine object.
    /// </summary>
    void DOTNExT.Persistence.IAsyncPersistenceService.Checkpoint(object stateMachine, int stateNumber, string methodId)
    {
        var snapshot = SerializeStateMachineObject(stateMachine, stateNumber);
        _snapshots[methodId] = snapshot;

        if (_verbose)
        {
            Console.WriteLine($"[Persistence] CHECKPOINT: {methodId} at state {stateNumber}");
            Console.WriteLine($"             Fields captured: {string.Join(", ", snapshot.Fields.Keys)}");
        }

        OnCheckpoint?.Invoke(this, new CheckpointEventArgs(methodId, stateNumber, snapshot));
        PersistToFile();
    }

    /// <summary>
    /// TryRestore implementation for Roslyn-generated code.
    /// Returns the state to resume from, or -1 if no restoration needed.
    /// Also restores field values into the state machine.
    /// </summary>
    int DOTNExT.Persistence.IAsyncPersistenceService.TryRestore(object stateMachine, string methodId)
    {
        if (!_snapshots.TryGetValue(methodId, out var snapshot))
        {
            return -1; // No restoration needed
        }

        // Restore fields into the state machine
        RestoreStateMachineObject(stateMachine, snapshot);

        if (_verbose)
        {
            Console.WriteLine($"[Persistence] RESTORE: {methodId} from state {snapshot.State}");
            Console.WriteLine($"             Fields restored: {string.Join(", ", snapshot.Fields.Keys)}");
        }

        OnRestore?.Invoke(this, new RestoreEventArgs(methodId, snapshot.State));

        // Remove the snapshot after restoration (it's been used)
        _snapshots.TryRemove(methodId, out _);
        PersistToFile();

        return snapshot.State;
    }

    /// <summary>
    /// Generic TryRestore implementation for Roslyn+ generated struct state machines.
    /// Avoids boxing by taking the state machine by ref and deserializing directly into it.
    /// </summary>
    int DOTNExT.Persistence.IAsyncPersistenceService.TryRestore<TStateMachine>(ref TStateMachine stateMachine, string methodId)
    {
        if (!_snapshots.TryGetValue(methodId, out var snapshot))
        {
            return -1; // No restoration needed
        }

        // Restore fields into the state machine via ref
        RestoreStateMachineRef(ref stateMachine, snapshot);

        if (_verbose)
        {
            Console.WriteLine($"[Persistence] RESTORE (generic): {methodId} from state {snapshot.State}");
            Console.WriteLine($"             Type: {typeof(TStateMachine).Name}, Fields restored: {string.Join(", ", snapshot.Fields.Keys)}");
        }

        OnRestore?.Invoke(this, new RestoreEventArgs(methodId, snapshot.State));

        // Remove the snapshot after restoration (it's been used)
        _snapshots.TryRemove(methodId, out _);
        PersistToFile();

        return snapshot.State;
    }

    /// <summary>
    /// Complete implementation for Roslyn-generated code.
    /// </summary>
    void DOTNExT.Persistence.IAsyncPersistenceService.Complete(string methodId, object? result)
    {
        _snapshots.TryRemove(methodId, out _);

        if (_verbose)
        {
            Console.WriteLine($"[Persistence] COMPLETE: {methodId} with result: {result ?? "null"}");
        }

        OnComplete?.Invoke(this, new CompleteEventArgs(methodId, result));
        PersistToFile();
    }

    /// <summary>
    /// Fault implementation for Roslyn-generated code.
    /// </summary>
    void DOTNExT.Persistence.IAsyncPersistenceService.Fault(string methodId, Exception exception)
    {
        // Keep the snapshot for potential retry/investigation
        if (_verbose)
        {
            Console.WriteLine($"[Persistence] FAULT: {methodId} with exception: {exception.Message}");
        }

        OnFault?.Invoke(this, new FaultEventArgs(methodId, exception));
        OnComplete?.Invoke(this, new CompleteEventArgs(methodId, null, faulted: true, exception));
    }

    /// <summary>
    /// Serialize a boxed state machine object using reflection.
    /// </summary>
    private StateMachineSnapshot SerializeStateMachineObject(object stateMachine, int state)
    {
        var type = stateMachine.GetType();
        var fields = new Dictionary<string, object?>();

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var fieldName = field.Name;

            // Skip awaiter fields - these are transient
            if (fieldName.Contains("__awaiter") || fieldName.Contains("<>u__"))
                continue;

            // Skip builder field - recreated on resume
            if (fieldName.Contains("__builder") || fieldName.Contains("<>t__builder"))
                continue;

            // Skip captured outer class reference - comes from caller, can't serialize
            if (fieldName.Contains("<>4__this"))
                continue;

            // Skip state field - we track this separately
            if (fieldName.Contains("__state") || fieldName.Contains("<>1__state"))
                continue;

            // Skip persistence service field
            if (fieldName.Contains("_persistenceService"))
                continue;

            try
            {
                var value = field.GetValue(stateMachine);
                fields[fieldName] = value;
            }
            catch (Exception ex)
            {
                if (_verbose)
                {
                    Console.WriteLine($"[Persistence] Warning: Could not serialize field {fieldName}: {ex.Message}");
                }
            }
        }

        return new StateMachineSnapshot
        {
            State = state,
            TypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            Fields = fields,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Restore field values into a boxed state machine object.
    /// </summary>
    private void RestoreStateMachineObject(object stateMachine, StateMachineSnapshot snapshot)
    {
        var type = stateMachine.GetType();

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var fieldName = field.Name;

            // Set state field
            if (fieldName.Contains("__state") || fieldName.Contains("<>1__state"))
            {
                field.SetValue(stateMachine, snapshot.State);
                continue;
            }

            // Restore other fields from snapshot
            if (snapshot.Fields.TryGetValue(fieldName, out var value))
            {
                try
                {
                    // Handle type conversion if needed (e.g., from JsonElement)
                    if (value is JsonElement je)
                    {
                        value = ConvertJsonElement(je, field.FieldType);
                    }

                    field.SetValue(stateMachine, value);
                }
                catch (Exception ex)
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"[Persistence] Warning: Could not restore field {fieldName}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Restore field values into a state machine via ref parameter.
    /// This properly handles struct state machines without boxing issues.
    /// </summary>
    private void RestoreStateMachineRef<TStateMachine>(ref TStateMachine stateMachine, StateMachineSnapshot snapshot)
    {
        var type = typeof(TStateMachine);

        // Box the struct temporarily for reflection (this is unavoidable for field access)
        object boxed = stateMachine!;

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var fieldName = field.Name;

            // Set state field
            if (fieldName.Contains("__state") || fieldName.Contains("<>1__state"))
            {
                field.SetValue(boxed, snapshot.State);
                continue;
            }

            // Restore other fields from snapshot
            if (snapshot.Fields.TryGetValue(fieldName, out var value))
            {
                try
                {
                    // Handle type conversion if needed (e.g., from JsonElement)
                    if (value is JsonElement je)
                    {
                        value = ConvertJsonElement(je, field.FieldType);
                    }

                    field.SetValue(boxed, value);
                }
                catch (Exception ex)
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"[Persistence] Warning: Could not restore field {fieldName}: {ex.Message}");
                    }
                }
            }
        }

        // Unbox back to the ref parameter - this is the key difference from RestoreStateMachineObject
        // The ref parameter ensures the unboxed value is written back to the caller's variable
        stateMachine = (TStateMachine)boxed;
    }
}
