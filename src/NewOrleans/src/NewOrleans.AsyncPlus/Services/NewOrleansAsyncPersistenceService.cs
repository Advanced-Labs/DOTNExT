using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NewOrleans.AsyncPlus.Services;

/// <summary>
/// Orleans-backed implementation of IAsyncPersistenceService.
/// Bridges the Async+ persistence abstraction to Orleans grains.
///
/// Uses "Tracked Tasks" pattern for async-first handling:
/// - Checkpoint fires async grain call, tracks the task
/// - TryRestore ensures any pending checkpoint completed first
/// </summary>
public class NewOrleansAsyncPersistenceService : DOTNExT.Persistence.IAsyncPersistenceService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<NewOrleansAsyncPersistenceService> _logger;

    // Tracked pending operations per workflow
    private readonly Dictionary<string, Task> _pendingCheckpoints = new();
    private readonly object _pendingLock = new();

    public NewOrleansAsyncPersistenceService(
        IGrainFactory grainFactory,
        ILogger<NewOrleansAsyncPersistenceService> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Checkpoint(object stateMachine, int stateNumber, string methodId)
    {
        // Fire async checkpoint, track the task
        var checkpointTask = CheckpointInternalAsync(stateMachine, stateNumber, methodId);

        lock (_pendingLock)
        {
            _pendingCheckpoints[methodId] = checkpointTask;
        }

        // Don't await - state machine will suspend anyway
        // The task is tracked so TryRestore can ensure completion
    }

    private async Task CheckpointInternalAsync(object stateMachine, int stateNumber, string methodId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
            var serialized = SerializeStateMachine(stateMachine);
            var typeName = stateMachine.GetType().AssemblyQualifiedName!;

            await grain.SaveCheckpointAsync(stateNumber, serialized, typeName);

            _logger.LogDebug(
                "[AsyncPlus] Checkpoint saved: {MethodId} at state {State}, {Bytes} bytes",
                methodId, stateNumber, serialized.Length);

            OnCheckpoint?.Invoke(this, new DOTNExT.Persistence.CheckpointEventArgs(methodId, stateNumber));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Checkpoint failed for {MethodId}", methodId);
            // Don't rethrow - checkpoint failure shouldn't crash the workflow
            // The previous checkpoint is still valid for recovery
        }
    }

    /// <inheritdoc />
    public int TryRestore(object stateMachine, string methodId)
    {
        // Ensure any pending checkpoint for this workflow completed first
        EnsurePendingCheckpointComplete(methodId);

        return TryRestoreInternalAsync(stateMachine, methodId).GetAwaiter().GetResult();
    }

    private void EnsurePendingCheckpointComplete(string methodId)
    {
        Task? pendingTask;
        lock (_pendingLock)
        {
            _pendingCheckpoints.TryGetValue(methodId, out pendingTask);
        }

        if (pendingTask != null && !pendingTask.IsCompleted)
        {
            _logger.LogDebug("[AsyncPlus] Waiting for pending checkpoint: {MethodId}", methodId);
            pendingTask.GetAwaiter().GetResult();
        }
    }

    private async Task<int> TryRestoreInternalAsync(object stateMachine, string methodId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
            var checkpoint = await grain.TryGetCheckpointAsync();

            if (checkpoint == null)
            {
                _logger.LogDebug("[AsyncPlus] No checkpoint found for {MethodId}", methodId);
                return -1;
            }

            DeserializeIntoStateMachine(stateMachine, checkpoint.SerializedStateMachine);

            _logger.LogInformation(
                "[AsyncPlus] Restored {MethodId} to state {State} (checkpoint from {Time})",
                methodId, checkpoint.StateNumber, checkpoint.CheckpointTimeUtc);

            OnRestore?.Invoke(this, new DOTNExT.Persistence.RestoreEventArgs(methodId, checkpoint.StateNumber));

            return checkpoint.StateNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Restore failed for {MethodId}", methodId);
            return -1;
        }
    }

    /// <inheritdoc />
    public void Complete(string methodId, object? result)
    {
        EnsurePendingCheckpointComplete(methodId);
        CompleteInternalAsync(methodId, result).GetAwaiter().GetResult();
    }

    private async Task CompleteInternalAsync(string methodId, object? result)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
            var serializedResult = result != null ? SerializeResult(result) : null;
            await grain.CompleteAsync(serializedResult);

            _logger.LogDebug("[AsyncPlus] Workflow completed: {MethodId}", methodId);

            // Clean up tracked task
            lock (_pendingLock)
            {
                _pendingCheckpoints.Remove(methodId);
            }

            OnComplete?.Invoke(this, new DOTNExT.Persistence.CompleteEventArgs(methodId, result, faulted: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Complete failed for {MethodId}", methodId);
        }
    }

    /// <inheritdoc />
    public void Fault(string methodId, Exception exception)
    {
        EnsurePendingCheckpointComplete(methodId);
        FaultInternalAsync(methodId, exception).GetAwaiter().GetResult();
    }

    private async Task FaultInternalAsync(string methodId, Exception exception)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IAsyncStatePersistenceGrain>(methodId);
            await grain.FaultAsync(
                exception.GetType().FullName ?? "Unknown",
                exception.Message,
                exception.StackTrace);

            _logger.LogWarning(exception, "[AsyncPlus] Workflow faulted: {MethodId}", methodId);

            // Clean up tracked task
            lock (_pendingLock)
            {
                _pendingCheckpoints.Remove(methodId);
            }

            OnFault?.Invoke(this, new DOTNExT.Persistence.FaultEventArgs(methodId, exception));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AsyncPlus] Fault recording failed for {MethodId}", methodId);
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
            // Skip awaiter fields (they're transient)
            if (field.Name.Contains("__awaiter") || field.FieldType.Name.Contains("Awaiter"))
                continue;

            try
            {
                var value = field.GetValue(stateMachine);
                // Only serialize if JSON-serializable
                if (IsJsonSerializable(value))
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

    private static bool IsJsonSerializable(object? value)
    {
        if (value == null) return true;

        var type = value.GetType();

        // Primitive types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
            return true;

        // Enums
        if (type.IsEnum) return true;

        // Arrays and collections of serializable types
        if (type.IsArray) return true;

        // Try to serialize and see if it works
        try
        {
            JsonSerializer.Serialize(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
