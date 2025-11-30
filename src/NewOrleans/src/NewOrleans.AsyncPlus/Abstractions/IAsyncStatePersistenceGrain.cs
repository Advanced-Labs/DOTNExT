namespace NewOrleans.AsyncPlus;

/// <summary>
/// Grain interface for persisting async state machine checkpoints.
/// One grain instance per workflow (keyed by methodId).
/// </summary>
public interface IAsyncStatePersistenceGrain : IGrainWithStringKey
{
    /// <summary>
    /// Save a checkpoint at the given state number.
    /// </summary>
    /// <param name="stateNumber">The state machine state number at the await point</param>
    /// <param name="serializedStateMachine">Serialized state machine fields</param>
    /// <param name="stateMachineTypeName">Assembly-qualified type name for deserialization</param>
    Task SaveCheckpointAsync(int stateNumber, byte[] serializedStateMachine, string stateMachineTypeName);

    /// <summary>
    /// Try to get the latest checkpoint for restoration.
    /// Returns null if no checkpoint exists or workflow is completed.
    /// </summary>
    Task<AsyncStateCheckpoint?> TryGetCheckpointAsync();

    /// <summary>
    /// Mark the workflow as completed successfully.
    /// Clears checkpoint data (no longer needed for recovery).
    /// </summary>
    /// <param name="serializedResult">Optional serialized result value</param>
    Task CompleteAsync(byte[]? serializedResult);

    /// <summary>
    /// Mark the workflow as faulted.
    /// Preserves checkpoint for potential retry/investigation.
    /// </summary>
    Task FaultAsync(string exceptionType, string message, string? stackTrace);

    /// <summary>
    /// Clear all persisted state for this workflow.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Check if this workflow has any persisted state.
    /// </summary>
    Task<bool> HasPersistedStateAsync();
}
