using System.Collections.Concurrent;
using System.Reflection;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Reads security policy from [SecurityPolicy] and [RequireCapability] attributes.
/// Caches results per type. Thread-safe.
/// </summary>
public sealed class AttributeBasedPolicyProvider : IGrainSecurityPolicyProvider
{
    private readonly ConcurrentDictionary<Type, GrainSecurityPolicy> _cache = new();

    public GrainSecurityPolicy GetPolicy(Type grainInterfaceType)
    {
        return _cache.GetOrAdd(grainInterfaceType, static type =>
        {
            var attr = type.GetCustomAttribute<SecurityPolicyAttribute>();
            if (attr is null)
                return GrainSecurityPolicy.Default;

            return new GrainSecurityPolicy
            {
                RequiresAuthentication = attr.RequiresAuthentication,
                AllowAnonymous = attr.AllowAnonymous,
                RequiresCallerCapability = attr.RequiresCallerCapability,
            };
        });
    }

    public string? GetRequiredAction(MethodInfo method)
    {
        return method.GetCustomAttribute<RequireCapabilityAttribute>()?.Action;
    }

    public string? GetRequiredResource(MethodInfo method)
    {
        return method.GetCustomAttribute<RequireCapabilityAttribute>()?.Resource;
    }
}
