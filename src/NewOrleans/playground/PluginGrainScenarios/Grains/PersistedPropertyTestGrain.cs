using Orleans;
using Orleans.Runtime;

namespace PluginGrainScenarios.Grains;

// ============================================================================
// PERSISTED PROPERTY TEST GRAIN
// ============================================================================
//
// This grain tests the IPersistentState property mapping feature:
// - [State(Persisted = true, StateProperty = "...")] attribute
// - Automatic mapping of partial properties to IPersistentState<T>.State
// - AutoSave functionality
// - State persistence across grain deactivation/reactivation
//
// The code generator should:
// 1. Detect the IPersistentState<PlayerState> field
// 2. For properties with [State(Persisted = true)], generate:
//    - get => _playerState.State.PropertyName;
//    - set => _playerState.State.PropertyName = value;
// 3. For properties with AutoSave = true, also call WriteStateAsync()
//
// ============================================================================

/// <summary>
/// State class that holds the persisted data.
/// Properties here must match the partial property names on the grain.
/// </summary>
[GenerateSerializer]
public class PlayerState
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public int Score { get; set; }

    [Id(2)]
    public int Level { get; set; } = 1;

    [Id(3)]
    public DateTime LastPlayed { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Test grain interface for persisted property code generation.
/// The interface is partial to allow the code generator to add GetX/SetX methods.
/// </summary>
public partial interface IPersistedPropertyTestGrain : IGrainWithStringKey
{
    /// <summary>
    /// Custom method to verify the grain is working.
    /// </summary>
    Task<string> GetSummary();

    /// <summary>
    /// Force a read from storage to verify persistence.
    /// </summary>
    Task RefreshState();

    /// <summary>
    /// Manually trigger state save (for testing non-AutoSave properties).
    /// </summary>
    Task SaveState();
}

/// <summary>
/// Test grain implementation using partial properties mapped to IPersistentState.
///
/// The code generator will:
/// - Detect the _playerState field as an IPersistentState&lt;PlayerState&gt;
/// - Generate property implementations that access _playerState.State.X
/// - For AutoSave properties, also call WriteStateAsync()
/// </summary>
public partial class PersistedPropertyTestGrain : Grain, IPersistedPropertyTestGrain
{
    private readonly IPersistentState<PlayerState> _playerState;

    public PersistedPropertyTestGrain(
        [PersistentState("playerState", "Default")] IPersistentState<PlayerState> playerState)
    {
        _playerState = playerState;
    }

    // ========================================================================
    // PERSISTED PARTIAL PROPERTIES
    // These properties map to _playerState.State.X via code generation.
    // ========================================================================

    /// <summary>
    /// Persisted string property WITHOUT AutoSave.
    /// Generated code should be:
    ///   get => _playerState.State.Name;
    ///   set => _playerState.State.Name = value;
    /// </summary>
    [State(Persisted = true, StateProperty = nameof(_playerState))]
    public partial string Name { get; set; }

    /// <summary>
    /// Persisted int property WITH AutoSave.
    /// Generated code should be:
    ///   get => _playerState.State.Score;
    ///   set { _playerState.State.Score = value; _ = _playerState.WriteStateAsync(); }
    /// </summary>
    [State(Persisted = true, StateProperty = nameof(_playerState), AutoSave = true)]
    public partial int Score { get; set; }

    /// <summary>
    /// Persisted int property WITHOUT AutoSave.
    /// </summary>
    [State(Persisted = true, StateProperty = nameof(_playerState))]
    public partial int Level { get; set; }

    // ========================================================================
    // NON-PERSISTED PROPERTY (for comparison)
    // ========================================================================

    /// <summary>
    /// Non-persisted property - uses backing field, NOT IPersistentState.
    /// This property will be lost on grain deactivation.
    /// </summary>
    public partial string SessionId { get; set; }

    // ========================================================================
    // CUSTOM METHODS
    // ========================================================================

    public Task<string> GetSummary()
        => Task.FromResult($"Player '{Name}' - Score: {Score}, Level: {Level}");

    public async Task RefreshState()
        => await _playerState.ReadStateAsync();

    public async Task SaveState()
        => await _playerState.WriteStateAsync();
}
