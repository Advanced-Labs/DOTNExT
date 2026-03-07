namespace Scynapse.Security.Orleans;

/// <summary>
/// Declares the security policy for a grain interface.
/// Applied to grain interfaces. Grains without this attribute default to RequiresAuthentication = true.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class SecurityPolicyAttribute : Attribute
{
    /// <summary>
    /// Whether callers must present valid identity and credentials. Default: true.
    /// </summary>
    public bool RequiresAuthentication { get; set; } = true;

    /// <summary>
    /// Whether unauthenticated (anonymous) callers are allowed. Default: false.
    /// When true, overrides RequiresAuthentication for this grain.
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Whether to require end-user CCap verification even for silo-originated (node-trusted) calls.
    /// Default: false. When false, calls from trusted nodes are allowed without CCap verification.
    /// When true, forces CCap verification for ALL callers including other silos.
    /// Use for high-security grains that must verify the end-user regardless of call origin.
    /// </summary>
    public bool RequiresCallerCapability { get; set; }
}
