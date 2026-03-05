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
}
