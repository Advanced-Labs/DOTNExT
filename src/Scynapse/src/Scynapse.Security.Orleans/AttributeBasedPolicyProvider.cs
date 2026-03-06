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
    private readonly ConcurrentDictionary<Type, GrainSecurityPolicy> _overrides = new();

    /// <summary>
    /// Register an explicit security policy for a grain interface type.
    /// Overrides any attribute-based policy. Used for system grains that
    /// cannot reference Scynapse.Security.Orleans for attribute annotation.
    /// </summary>
    public void RegisterPolicy(Type grainInterfaceType, GrainSecurityPolicy policy)
    {
        _overrides[grainInterfaceType] = policy;
        _cache.TryRemove(grainInterfaceType, out _); // invalidate cache
    }

    public GrainSecurityPolicy GetPolicy(Type grainInterfaceType)
    {
        return _cache.GetOrAdd(grainInterfaceType, type =>
        {
            if (_overrides.TryGetValue(type, out var overridePolicy))
                return overridePolicy;

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
