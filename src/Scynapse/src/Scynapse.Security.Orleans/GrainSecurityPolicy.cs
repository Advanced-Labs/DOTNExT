namespace Scynapse.Security.Orleans;

/// <summary>
/// Computed security policy for a grain type.
/// Immutable after creation, cached by the policy provider.
/// </summary>
public sealed class GrainSecurityPolicy
{
    // Unannotated grains (including internal Orleans system grains) are allowed through.
    // Only grains explicitly marked [SecurityPolicy(RequiresAuthentication = true)] require auth.
    public static readonly GrainSecurityPolicy Default = new() { RequiresAuthentication = false, AllowAnonymous = true };

    public bool RequiresAuthentication { get; init; }
    public bool AllowAnonymous { get; init; }
    public bool RequiresCallerCapability { get; init; }
}
