using Orleans;

namespace PluginGrainScenarios.Grains;

// ============================================================================
// PARTIAL PROPERTY TEST GRAIN
// ============================================================================
//
// This grain tests the FULL StatePropertyAccess feature using partial properties.
// The code generator should:
// 1. Generate GetX/SetX method signatures on the partial interface
// 2. Generate method implementations on the partial grain class
// 3. Generate backing fields for partial properties
// 4. Generate proxy methods (GetX/SetX) on the proxy class
// 5. Generate StateTask<T> properties on the proxy class
//
// ============================================================================

/// <summary>
/// Test grain interface for partial property code generation.
/// The interface is partial to allow the code generator to add GetX/SetX methods.
/// </summary>
public partial interface IPartialPropertyTestGrain : IGrainWithStringKey
{
    // Custom method - this is user-defined, not generated
    Task<string> GetCombinedInfo();
}

/// <summary>
/// Test grain implementation using partial properties.
/// The code generator will:
/// - Generate backing fields for Name and Score
/// - Generate GetName/SetName, GetScore/SetScore method implementations
/// - Add these methods to the partial interface
/// </summary>
public partial class PartialPropertyTestGrain : Grain, IPartialPropertyTestGrain
{
    // ========================================================================
    // PARTIAL PROPERTIES
    // These are the properties that trigger code generation.
    // The code generator will:
    // - Generate backing fields (e.g., _name_backing)
    // - Generate property implementations (get => _name_backing; set => _name_backing = value;)
    // - Generate interface methods (GetName() => Task.FromResult(Name), SetName(v) => { Name = v; return Task.CompletedTask; })
    // - Generate proxy methods on the proxy class
    // - Generate StateTask<T> properties on the proxy class
    // ========================================================================

    /// <summary>
    /// A partial string property. Code generator will generate:
    /// - Interface: Task&lt;string&gt; GetName(); Task SetName(string value);
    /// - Grain: explicit interface implementations delegating to property
    /// - Proxy: GetName/SetName methods + StateTask&lt;string&gt; Name property
    /// </summary>
    public partial string Name { get; set; }

    public partial int Age { get; set; }

    /// <summary>
    /// A partial int property.
    /// </summary>
    public partial int Score { get; set; }

    /// <summary>
    /// A read-only property (not partial - no code generation needed for this).
    /// For truly immutable properties, use regular properties instead of partial.
    /// The [State] attribute is only meaningful for properties that can be remotely accessed.
    /// </summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    // ========================================================================
    // NON-STATE PROPERTIES
    // These are excluded from code generation.
    // ========================================================================

    /// <summary>
    /// Property marked with [NotState] - excluded from code generation.
    /// No GetInternalNote/SetInternalNote will be generated.
    /// </summary>
    [NotState]
    public string InternalNote { get; set; } = "internal";

    // ========================================================================
    // CUSTOM METHODS
    // These are user-defined methods, not affected by code generation.
    // ========================================================================

    /// <summary>
    /// A custom method that uses the state properties.
    /// </summary>
    public Task<string> GetCombinedInfo()
        => Task.FromResult($"{Name}: {Score} pts");
}
