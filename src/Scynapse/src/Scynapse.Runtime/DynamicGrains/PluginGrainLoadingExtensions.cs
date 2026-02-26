using Microsoft.Extensions.DependencyInjection;
using Scynapse.Hosting;

namespace Scynapse.Runtime;

/// <summary>
/// Extension methods for plugin grain loading.
/// NOTE: Plugin grain loading is now enabled by default. These methods are retained for backward compatibility only.
/// </summary>
public static class PluginGrainLoadingExtensions
{
    /// <summary>
    /// Plugin grain loading is now enabled by default. This method is a no-op retained for backward compatibility.
    /// </summary>
    /// <param name="builder">The silo builder</param>
    /// <returns>The silo builder for method chaining</returns>
    [System.Obsolete("Plugin grain loading is now enabled by default. This method is no longer needed and will be removed in a future version.")]
    public static ISiloBuilder AddPluginGrainLoading(this ISiloBuilder builder)
    {
        // No-op: services are now registered by default in DefaultSiloServices
        return builder;
    }

    /// <summary>
    /// Plugin grain loading is now enabled by default. This method is a no-op retained for backward compatibility.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    [System.Obsolete("Plugin grain loading is now enabled by default. This method is no longer needed and will be removed in a future version.")]
    public static IServiceCollection AddPluginGrainLoading(this IServiceCollection services)
    {
        // No-op: services are now registered by default in DefaultSiloServices
        return services;
    }

    /// <summary>
    /// Legacy alias. Plugin grain loading is now enabled by default.
    /// </summary>
    [System.Obsolete("Plugin grain loading is now enabled by default. This method is no longer needed and will be removed in a future version.")]
    public static ISiloBuilder AddDynamicGrainLoading(this ISiloBuilder builder) => builder;

    /// <summary>
    /// Legacy alias. Plugin grain loading is now enabled by default.
    /// </summary>
    [System.Obsolete("Plugin grain loading is now enabled by default. This method is no longer needed and will be removed in a future version.")]
    public static IServiceCollection AddDynamicGrainLoading(this IServiceCollection services) => services;
}
