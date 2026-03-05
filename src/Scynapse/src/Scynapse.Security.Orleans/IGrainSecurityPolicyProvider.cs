using System.Reflection;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Reads security policy from grain types.
/// Phase 1: attribute-based. Phase 2: Component Model policies.
/// </summary>
public interface IGrainSecurityPolicyProvider
{
    GrainSecurityPolicy GetPolicy(Type grainInterfaceType);
    string? GetRequiredAction(MethodInfo method);
    string? GetRequiredResource(MethodInfo method);
}
