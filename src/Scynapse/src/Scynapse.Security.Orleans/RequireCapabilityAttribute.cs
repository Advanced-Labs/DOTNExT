namespace Scynapse.Security.Orleans;

/// <summary>
/// Declares that a grain method requires a specific capability action.
/// The incoming call filter verifies the CCap grants this action before invoking.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCapabilityAttribute : Attribute
{
    /// <summary>
    /// The required action (e.g., "read", "write", "admin").
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// The required resource URI. Null means infer from grain interface type.
    /// </summary>
    public string? Resource { get; set; }
}
