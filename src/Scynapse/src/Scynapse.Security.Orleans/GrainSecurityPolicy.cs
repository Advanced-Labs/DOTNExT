namespace Scynapse.Security.Orleans;

/// <summary>
/// Computed security policy for a grain type.
/// Immutable after creation, cached by the policy provider.
/// </summary>
public sealed class GrainSecurityPolicy
{
    public static readonly GrainSecurityPolicy Default = new() { RequiresAuthentication = true };

    public bool RequiresAuthentication { get; init; }
    public bool AllowAnonymous { get; init; }
}
