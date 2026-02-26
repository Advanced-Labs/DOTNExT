using Scynapse;

namespace PluginGrainScenarios.Grains;

// ============================================================================
// STATE PROPERTY ACCESS TEST GRAINS
// ============================================================================
//
// This file tests the StatePropertyAccess feature with MANUALLY IMPLEMENTED
// methods. The code generator would generate these automatically for partial
// properties, but for this test scenario we implement them manually to test
// the core infrastructure.
//
// NOTE: Full partial property codegen with automatic proxy method generation
// requires additional proxy generator integration work. This manual approach
// tests the core functionality.
//
// ============================================================================

/// <summary>
/// Test grain interface for StatePropertyAccess feature.
///
/// Declares Get/Set methods for state properties plus a custom method.
/// In the full implementation, these would be generated from partial properties
/// on the grain class.
/// </summary>
public interface IStatePropertyTestGrain : IGrainWithStringKey
{
    // ========================================================================
    // STATE PROPERTY METHODS (would be generated for partial properties)
    // ========================================================================

    /// <summary>Gets the Name property value.</summary>
    Task<string> GetName();

    /// <summary>Sets the Name property value.</summary>
    Task SetName(string value);

    /// <summary>Gets the Score property value.</summary>
    Task<int> GetScore();

    /// <summary>Sets the Score property value.</summary>
    Task SetScore(int value);

    /// <summary>
    /// Gets the CreatedAt property value.
    /// Note: No SetCreatedAt - this simulates [State(CanSet = false)]
    /// </summary>
    Task<DateTime> GetCreatedAt();

    // NOTE: No GetInternalNote/SetInternalNote - simulates [NotState]

    // ========================================================================
    // CUSTOM METHODS (written by developer, unchanged by codegen)
    // ========================================================================

    /// <summary>
    /// Custom method that uses the state properties.
    /// This demonstrates that codegen doesn't interfere with user-defined methods.
    /// </summary>
    Task<string> GetCombinedInfo();
}

/// <summary>
/// Test grain implementation for StatePropertyAccess feature.
///
/// Manually implements Get/Set methods for state properties.
/// In the full implementation, these would be generated from partial properties.
/// </summary>
public class StatePropertyTestGrain : Grain, IStatePropertyTestGrain
{
    // ========================================================================
    // BACKING FIELDS (would be generated for partial properties)
    // ========================================================================

    private string _name = string.Empty;
    private int _score;
    private readonly DateTime _createdAt = DateTime.UtcNow;
    
    // [NotState] - excluded from codegen, regular property
    public string InternalNote { get; set; } = "internal";

    // ========================================================================
    // STATE PROPERTY METHODS (would be generated for partial properties)
    // ========================================================================

    public Task<string> GetName() => Task.FromResult(_name);
    public Task SetName(string value)
    {
        _name = value;
        return Task.CompletedTask;
    }

    public Task<int> GetScore() => Task.FromResult(_score);
    public Task SetScore(int value)
    {
        _score = value;
        return Task.CompletedTask;
    }

    // Read-only: only getter, simulates [State(CanSet = false)]
    public Task<DateTime> GetCreatedAt() => Task.FromResult(_createdAt);

    // ========================================================================
    // CUSTOM METHODS (written by developer)
    // ========================================================================

    public Task<string> GetCombinedInfo()
        => Task.FromResult($"{_name}: {_score} pts");
}
