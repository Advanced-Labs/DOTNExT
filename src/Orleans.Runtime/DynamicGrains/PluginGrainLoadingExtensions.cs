using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Hosting;
using Orleans.Runtime.DynamicGrains;

namespace Orleans.Runtime;

/// <summary>
/// Extension methods for enabling plugin grain loading.
/// </summary>
public static class PluginGrainLoadingExtensions
{
    /// <summary>
    /// Adds plugin grain loading support to the silo.
    /// This enables loading grain assemblies at runtime without requiring application restart.
    /// </summary>
    /// <param name="builder">The silo builder</param>
    /// <returns>The silo builder for method chaining</returns>
    public static ISiloBuilder AddPluginGrainLoading(this ISiloBuilder builder)
    {
        builder.Services.AddPluginGrainLoading();
        return builder;
    }

    /// <summary>
    /// Adds plugin grain loading support to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddPluginGrainLoading(this IServiceCollection services)
    {
        // Register core services
        services.TryAddSingleton<AssemblyValidator>();
        services.TryAddSingleton<PluginAssemblyLoader>();
        services.TryAddSingleton<PluginSerializationManager>();
        services.TryAddSingleton<PluginGrainLoaderService>();

        // Register the public interface
        services.TryAddSingleton<IPluginGrainLoader>(sp => sp.GetRequiredService<PluginGrainLoaderService>());

        // Register lifecycle participant
        services.TryAddSingleton<ILifecycleParticipant<ISiloLifecycle>>(
            sp => sp.GetRequiredService<PluginGrainLoaderService>());

        return services;
    }

    // Legacy aliases for backward compatibility during migration
    [System.Obsolete("Use AddPluginGrainLoading instead. This method will be removed in a future version.")]
    public static ISiloBuilder AddDynamicGrainLoading(this ISiloBuilder builder) => AddPluginGrainLoading(builder);

    [System.Obsolete("Use AddPluginGrainLoading instead. This method will be removed in a future version.")]
    public static IServiceCollection AddDynamicGrainLoading(this IServiceCollection services) => AddPluginGrainLoading(services);
}
