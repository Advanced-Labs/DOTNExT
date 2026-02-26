using System.Reflection;
using System.Text.Json;
using DOTNExT.Persistence;
using Microsoft.Extensions.Logging;

namespace Scynapse.AsyncPlus.Services;

/// <summary>
/// Orleans-backed implementation of IAsyncPersistenceService.
/// Bridges the Async+ persistence abstraction to Orleans grains.
///
/// Uses "Tracked Tasks" pattern for async-first handling:
/// - Checkpoint fires async grain call, tracks the task
/// - TryRestore ensures any pending checkpoint completed first
///
/// IMPORTANT: Grain ID Resolution
/// - If AsyncPersistenceContext.WorkflowId is set, it's used as the grain ID
/// - Otherwise, the methodId (from Roslyn codegen) is used
/// - This allows concurrent workflow instances to have isolated storage
/// </summary>
public class ScynapseAsyncPersistenceService : DOTNExT.Persistence.IAsyncPersistenceService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ScynapseAsyncPersistenceService> _logger;

    // Tracked pending operations per workflow (keyed by resolved grain ID)
    private readonly Dictionary<string, Task> _pendingCheckpoints = new();
    private readonly object _pendingLock = new();

    public ScynapseAsyncPersistenceService(
        IGrainFactory grainFactory,
        ILogger<ScynapseAsyncPersistenceService> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the grain ID to use for persistence operations.
    /// Uses WorkflowId from context if set, otherwise falls back to methodId.
    /// </summary>
    private string ResolveGrainId(string methodId)
    {
        var workflowId = AsyncPersistenceContext.WorkflowId;
        return workflowId ?? methodId;
    }

    /// <inheritdoc />
    public void Checkpoint(object stateMachine, int stateNumber, string methodId)
    {
        // Resolve grain ID using context workflow ID if available
        var grainId = ResolveGrainId(methodId);

        // Fire async checkpoint, track the task
        var checkpointTask = CheckpointInternalAsync(stateMachine, stateNumber, grainId);

        lock (_pendingLock)
        {
            _pendingCheckpoints[grainId] = checkpointTask;
        }

        // Don't await - state machine will suspend anyway
        // The task is tracked so TryRestore can ensure completion
    }

    private async Task CheckpointInternalAsync(object stateMachine, int stateNumber, string grainId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(grainId);
            var serialized = SerializeStateMachine(stateMachine);
            var typeName = stateMachine.GetType().AssemblyQualifiedName!;

            await grain.SaveCheckpointAsync(stateNumber, serialized, typeName);

            _logger.LogDebug(
                "[AsyncPlus] Checkpoint saved: {GrainId} at state {State}, {Bytes} bytes",
                grainId, stateNumber, serialized.Length);

            OnCheckpoint?.Invoke(this, new DOTNExT.Persistence.CheckpointEventArgs(grainId, stateNumber));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Checkpoint failed for {GrainId}", grainId);
            // Don't rethrow - checkpoint failure shouldn't crash the workflow
            // The previous checkpoint is still valid for recovery
        }
    }

    /// <inheritdoc />
    [Obsolete("Use TryRestore<TStateMachine>(ref TStateMachine, string) instead")]
    public int TryRestore(object stateMachine, string methodId)
    {
        // Resolve grain ID using context workflow ID if available
        var grainId = ResolveGrainId(methodId);

        // Ensure any pending checkpoint for this workflow completed first
        EnsurePendingCheckpointComplete(grainId);

        return TryRestoreInternalAsync(stateMachine, grainId).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    /// <summary>
    /// Type-safe restoration that properly handles struct state machines.
    /// Deserializes checkpoint directly into the ref parameter, avoiding boxing issues.
    /// </summary>
    public int TryRestore<TStateMachine>(ref TStateMachine stateMachine, string methodId)
    {
        // Resolve grain ID using context workflow ID if available
        var grainId = ResolveGrainId(methodId);

        // Ensure any pending checkpoint for this workflow completed first
        EnsurePendingCheckpointComplete(grainId);

        return TryRestoreGenericInternal(ref stateMachine, grainId);
    }

    private int TryRestoreGenericInternal<TStateMachine>(ref TStateMachine stateMachine, string grainId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(grainId);
            var checkpoint = grain.TryGetCheckpointAsync().GetAwaiter().GetResult();

            if (checkpoint == null)
            {
                _logger.LogDebug("[AsyncPlus] No checkpoint found for {GrainId}", grainId);
                return -1;
            }

            // Deserialize into a new instance of the actual type
            var restored = DeserializeStateMachine<TStateMachine>(checkpoint.SerializedStateMachine);

            // Assign restored state to the ref parameter - this works for both structs and classes
            stateMachine = restored;

            _logger.LogInformation(
                "[AsyncPlus] Restored {GrainId} to state {State} (checkpoint from {Time}) via generic TryRestore<{Type}>",
                grainId, checkpoint.StateNumber, checkpoint.CheckpointTimeUtc, typeof(TStateMachine).Name);

            OnRestore?.Invoke(this, new DOTNExT.Persistence.RestoreEventArgs(grainId, checkpoint.StateNumber));

            return checkpoint.StateNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Restore failed for {GrainId}", grainId);
            return -1;
        }
    }

    private void EnsurePendingCheckpointComplete(string grainId)
    {
        Task? pendingTask;
        lock (_pendingLock)
        {
            _pendingCheckpoints.TryGetValue(grainId, out pendingTask);
        }

        if (pendingTask != null && !pendingTask.IsCompleted)
        {
            _logger.LogDebug("[AsyncPlus] Waiting for pending checkpoint: {GrainId}", grainId);
            pendingTask.GetAwaiter().GetResult();
        }
    }

    private async Task<int> TryRestoreInternalAsync(object stateMachine, string grainId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(grainId);
            var checkpoint = await grain.TryGetCheckpointAsync();

            if (checkpoint == null)
            {
                _logger.LogDebug("[AsyncPlus] No checkpoint found for {GrainId}", grainId);
                return -1;
            }

            DeserializeIntoStateMachine(stateMachine, checkpoint.SerializedStateMachine);

            _logger.LogInformation(
                "[AsyncPlus] Restored {GrainId} to state {State} (checkpoint from {Time})",
                grainId, checkpoint.StateNumber, checkpoint.CheckpointTimeUtc);

            OnRestore?.Invoke(this, new DOTNExT.Persistence.RestoreEventArgs(grainId, checkpoint.StateNumber));

            return checkpoint.StateNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Restore failed for {GrainId}", grainId);
            return -1;
        }
    }

    /// <inheritdoc />
    public void Complete(string methodId, object? result)
    {
        // Resolve grain ID using context workflow ID if available
        var grainId = ResolveGrainId(methodId);

        EnsurePendingCheckpointComplete(grainId);
        CompleteInternalAsync(grainId, result).GetAwaiter().GetResult();
    }

    private async Task CompleteInternalAsync(string grainId, object? result)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(grainId);
            var serializedResult = result != null ? SerializeResult(result) : null;
            await grain.CompleteAsync(serializedResult);

            _logger.LogDebug("[AsyncPlus] Workflow completed: {GrainId}", grainId);

            // Clean up tracked task
            lock (_pendingLock)
            {
                _pendingCheckpoints.Remove(grainId);
            }

            OnComplete?.Invoke(this, new DOTNExT.Persistence.CompleteEventArgs(grainId, result, faulted: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Complete failed for {GrainId}", grainId);
        }
    }

    /// <inheritdoc />
    public void Fault(string methodId, Exception exception)
    {
        // Resolve grain ID using context workflow ID if available
        var grainId = ResolveGrainId(methodId);

        EnsurePendingCheckpointComplete(grainId);
        FaultInternalAsync(grainId, exception).GetAwaiter().GetResult();
    }

    private async Task FaultInternalAsync(string grainId, Exception exception)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(grainId);
            await grain.FaultAsync(
                exception.GetType().FullName ?? "Unknown",
                exception.Message,
                exception.StackTrace);

            _logger.LogWarning(exception, "[AsyncPlus] Workflow faulted: {GrainId}", grainId);

            // Clean up tracked task
            lock (_pendingLock)
            {
                _pendingCheckpoints.Remove(grainId);
            }

            OnFault?.Invoke(this, new DOTNExT.Persistence.FaultEventArgs(grainId, exception));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Fault recording failed for {GrainId}", grainId);
        }
    }

    #region Events

    public event EventHandler<DOTNExT.Persistence.CheckpointEventArgs>? OnCheckpoint;
    public event EventHandler<DOTNExT.Persistence.RestoreEventArgs>? OnRestore;
    public event EventHandler<DOTNExT.Persistence.CompleteEventArgs>? OnComplete;
    public event EventHandler<DOTNExT.Persistence.FaultEventArgs>? OnFault;

    #endregion

    #region Serialization Helpers

    private static byte[] SerializeStateMachine(object stateMachine)
    {
        // Extract fields from state machine using reflection
        var type = stateMachine.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var fieldData = new Dictionary<string, object?>();
        foreach (var field in fields)
        {
            // Skip transient/infrastructure fields that can't or shouldn't be serialized:
            // - Awaiter fields (TaskAwaiter, etc.)
            // - Builder fields (AsyncTaskMethodBuilder, etc.) - these contain Tasks
            // - Captured outer class reference (<>4__this) - can't serialize, comes from caller
            // - Persistence service references
            var fieldName = field.Name;
            var typeName = field.FieldType.Name;

            if (fieldName.Contains("__awaiter") || typeName.Contains("Awaiter"))
                continue;
            if (fieldName.Contains("__builder") || typeName.Contains("MethodBuilder"))
                continue;
            if (fieldName.Contains("<>4__this"))  // Captured 'this' reference to outer class
                continue;
            if (fieldName.Contains("persistenceService") || fieldName.Contains("PersistenceService"))
                continue;
            if (typeName.Contains("IAsyncPersistenceService"))
                continue;

            try
            {
                var value = field.GetValue(stateMachine);
                // Only serialize if JSON-serializable (quick check, no test serialization)
                if (IsSerializableType(field.FieldType, value))
                {
                    fieldData[field.Name] = value;
                }
            }
            catch
            {
                // Skip fields that can't be read
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(fieldData, new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = true
        });
    }

    /// <summary>
    /// Fast check if a type is serializable. Avoids test serialization which can hang.
    /// </summary>
    private static bool IsSerializableType(Type type, object? value)
    {
        if (value == null) return true;

        // Primitive types - always safe
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
            return true;

        // Enums
        if (type.IsEnum) return true;

        // Nullable value types
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return IsSerializableType(underlying, value);

        // Skip interfaces, delegates, Task types
        if (type.IsInterface) return false;
        if (typeof(Delegate).IsAssignableFrom(type)) return false;
        if (typeof(Task).IsAssignableFrom(type)) return false;

        // Arrays of primitives
        if (type.IsArray && type.GetElementType()?.IsPrimitive == true)
            return true;

        // For other types, be conservative - only allow known safe types
        // This avoids hanging on complex object graphs
        return false;
    }

    private static void DeserializeIntoStateMachine(object stateMachine, byte[] data)
    {
        var fieldData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data);
        if (fieldData == null) return;

        var type = stateMachine.GetType();

        foreach (var (fieldName, jsonValue) in fieldData)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) continue;

            try
            {
                var value = JsonSerializer.Deserialize(jsonValue.GetRawText(), field.FieldType);
                field.SetValue(stateMachine, value);
            }
            catch
            {
                // Skip fields that can't be deserialized
            }
        }
    }

    /// <summary>
    /// Type-safe deserialization that creates a new instance of the state machine type.
    /// This is the preferred method for struct state machines (no boxing).
    /// </summary>
    private static TStateMachine DeserializeStateMachine<TStateMachine>(byte[] data)
    {
        var fieldData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data);
        if (fieldData == null)
        {
            return default!;
        }

        var type = typeof(TStateMachine);

        // Create a new instance of the state machine
        // For structs, this creates a default-initialized struct
        // For classes, this uses the parameterless constructor
        TStateMachine instance;
        if (type.IsValueType)
        {
            instance = default!;
        }
        else
        {
            instance = (TStateMachine)Activator.CreateInstance(type)!;
        }

        // Box the instance for field setting (needed for structs)
        // The boxing is local to this method and we return the unboxed value
        object boxed = instance!;

        foreach (var (fieldName, jsonValue) in fieldData)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) continue;

            try
            {
                var value = JsonSerializer.Deserialize(jsonValue.GetRawText(), field.FieldType);
                field.SetValue(boxed, value);
            }
            catch
            {
                // Skip fields that can't be deserialized
            }
        }

        // Unbox the modified instance (for structs)
        return (TStateMachine)boxed;
    }

    private static byte[]? SerializeResult(object result)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(result);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
