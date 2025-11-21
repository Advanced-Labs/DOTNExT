using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Hosting;
using Orleans.Runtime.DynamicGrains;

namespace Orleans.Runtime;

/// <summary>
/// Extension methods for enabling dynamic grain loading.
/// </summary>
public static class DynamicGrainLoadingExtensions
{
    /// <summary>
    /// Adds dynamic grain loading support to the silo.
    /// This enables loading grain assemblies at runtime without requiring application restart.
    /// </summary>
    /// <param name="builder">The silo builder</param>
    /// <returns>The silo builder for method chaining</returns>
    public static ISiloBuilder AddDynamicGrainLoading(this ISiloBuilder builder)
    {
        builder.Services.AddDynamicGrainLoading();
        return builder;
    }

    /// <summary>
    /// Adds dynamic grain loading support to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddDynamicGrainLoading(this IServiceCollection services)
    {
        // Register core services
        services.TryAddSingleton<AssemblyValidator>();
        services.TryAddSingleton<DynamicAssemblyLoader>();
        services.TryAddSingleton<DynamicSerializationManager>();
        services.TryAddSingleton<DynamicGrainLoaderService>();

        // Register the public interface
        services.TryAddSingleton<IDynamicGrainLoader>(sp => sp.GetRequiredService<DynamicGrainLoaderService>());

        // Register lifecycle participant
        services.TryAddSingleton<ILifecycleParticipant<ISiloLifecycle>>(
            sp => sp.GetRequiredService<DynamicGrainLoaderService>());

        return services;
    }
}
