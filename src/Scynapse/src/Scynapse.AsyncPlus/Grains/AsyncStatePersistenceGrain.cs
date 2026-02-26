using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace Scynapse.AsyncPlus.Grains;

/// <summary>
/// Grain implementation for persisting async state machine checkpoints.
/// Uses Orleans grain storage (configured via silo builder) for durability.
/// </summary>
public class AsyncStatePersistenceGrain : Grain, IAsyncStatePersistenceGrain
{
    private readonly IPersistentState<AsyncStatePersistenceGrainState> _state;
    private readonly ILogger<AsyncStatePersistenceGrain> _logger;

    public AsyncStatePersistenceGrain(
        [PersistentState("asyncState", "AsyncPlusStorage")]
        IPersistentState<AsyncStatePersistenceGrainState> state,
        ILogger<AsyncStatePersistenceGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveCheckpointAsync(int stateNumber, byte[] serializedStateMachine, string stateMachineTypeName)
    {
        var grainId = this.GetPrimaryKeyString();

        _state.State.StateNumber = stateNumber;
        _state.State.SerializedStateMachine = serializedStateMachine;
        _state.State.StateMachineTypeName = stateMachineTypeName;
        _state.State.CheckpointTimeUtc = DateTime.UtcNow;
        _state.State.IsCompleted = false;
        _state.State.IsFaulted = false;

        await _state.WriteStateAsync();

        _logger.LogDebug(
            "Checkpoint saved for {GrainId} at state {StateNumber}, {ByteCount} bytes",
            grainId, stateNumber, serializedStateMachine.Length);
    }

    /// <inheritdoc />
    public Task<AsyncStateCheckpoint?> TryGetCheckpointAsync()
    {
        // No checkpoint if never set, completed, or faulted
        if (_state.State.StateNumber < 0 ||
            _state.State.SerializedStateMachine == null ||
            _state.State.IsCompleted)
        {
            return Task.FromResult<AsyncStateCheckpoint?>(null);
        }

        var checkpoint = new AsyncStateCheckpoint
        {
            StateNumber = _state.State.StateNumber,
            SerializedStateMachine = _state.State.SerializedStateMachine,
            StateMachineTypeName = _state.State.StateMachineTypeName!,
            CheckpointTimeUtc = _state.State.CheckpointTimeUtc ?? DateTime.UtcNow
        };

        _logger.LogDebug(
            "Returning checkpoint for {GrainId} at state {StateNumber}",
            this.GetPrimaryKeyString(), checkpoint.StateNumber);

        return Task.FromResult<AsyncStateCheckpoint?>(checkpoint);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(byte[]? serializedResult)
    {
        var grainId = this.GetPrimaryKeyString();

        _state.State.IsCompleted = true;
        _state.State.SerializedResult = serializedResult;
        // Clear checkpoint data - no longer needed for recovery
        _state.State.SerializedStateMachine = null;

        await _state.WriteStateAsync();

        _logger.LogDebug("Workflow {GrainId} marked as completed", grainId);
    }

    /// <inheritdoc />
    public async Task FaultAsync(string exceptionType, string message, string? stackTrace)
    {
        var grainId = this.GetPrimaryKeyString();

        _state.State.IsFaulted = true;
        _state.State.FaultExceptionType = exceptionType;
        _state.State.FaultMessage = message;
        _state.State.FaultStackTrace = stackTrace;
        // Keep checkpoint data for potential investigation/retry

        await _state.WriteStateAsync();

        _logger.LogWarning(
            "Workflow {GrainId} faulted: {ExceptionType}: {Message}",
            grainId, exceptionType, message);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        var grainId = this.GetPrimaryKeyString();

        await _state.ClearStateAsync();

        // CRITICAL: Also reset in-memory state to defaults!
        // ClearStateAsync only clears storage and sets RecordExists=false,
        // but the State object retains its old values which would cause
        // TryGetCheckpointAsync to return stale data.
        _state.State.StateNumber = -1;
        _state.State.SerializedStateMachine = null;
        _state.State.StateMachineTypeName = null;
        _state.State.CheckpointTimeUtc = null;
        _state.State.IsCompleted = false;
        _state.State.IsFaulted = false;
        _state.State.SerializedResult = null;
        _state.State.FaultExceptionType = null;
        _state.State.FaultMessage = null;
        _state.State.FaultStackTrace = null;

        _logger.LogDebug("Cleared all state for {GrainId}", grainId);
    }

    /// <inheritdoc />
    public Task<bool> HasPersistedStateAsync()
    {
        var grainId = this.GetPrimaryKeyString();
        var stateNumber = _state.State.StateNumber;
        var isCompleted = _state.State.IsCompleted;
        var isFaulted = _state.State.IsFaulted;
        var hasSerializedData = _state.State.SerializedStateMachine != null;

        var hasState = stateNumber >= 0 || isCompleted || isFaulted;

        _logger.LogDebug(
            "[DEBUG] HasPersistedStateAsync for {GrainId}: StateNumber={StateNumber}, IsCompleted={IsCompleted}, IsFaulted={IsFaulted}, HasSerializedData={HasData}, RecordExists={RecordExists} => Result={HasState}",
            grainId, stateNumber, isCompleted, isFaulted, hasSerializedData, _state.RecordExists, hasState);

        return Task.FromResult(hasState);
    }

    /// <inheritdoc />
    public Task RequestDeactivationAsync()
    {
        var grainId = this.GetPrimaryKeyString();
        _logger.LogDebug("Deactivation requested for {GrainId}", grainId);

        // DeactivateOnIdle marks the grain for deactivation when the current
        // request completes and no other requests are pending
        this.DeactivateOnIdle();

        return Task.CompletedTask;
    }
}
