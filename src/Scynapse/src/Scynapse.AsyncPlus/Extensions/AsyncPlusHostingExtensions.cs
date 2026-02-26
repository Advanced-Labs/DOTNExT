using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scynapse.AsyncPlus.Services;
using Scynapse.AsyncPlus.Storage;
using Scynapse.Configuration;
using Scynapse.Hosting;
using Scynapse.Providers;
using Scynapse.Runtime;
using Scynapse.Storage;

namespace Scynapse.AsyncPlus.Extensions;

/// <summary>
/// Extension methods for configuring Async+ persistence in Scynapse.
/// </summary>
public static class AsyncPlusHostingExtensions
{
    /// <summary>
    /// Adds Async+ persistence support to the Scynapse silo.
    /// Registers ScynapseAsyncPersistenceService as the IAsyncPersistenceService implementation.
    /// </summary>
    /// <param name="siloBuilder">The silo builder</param>
    /// <param name="storageName">
    /// Name of the Scynapse storage provider to use for Async+ state.
    /// Default is "AsyncPlusStorage". Make sure to configure this storage provider.
    /// </param>
    /// <example>
    /// <code>
    /// siloBuilder
    ///     .AddRavenDbGrainStorage("AsyncPlusStorage", options => { ... })
    ///     .UseAsyncPlusPersistence();
    /// </code>
    /// </example>
    public static ISiloBuilder UseAsyncPlusPersistence(
        this ISiloBuilder siloBuilder,
        string storageName = "AsyncPlusStorage")
    {
        siloBuilder.ConfigureServices(services =>
        {
            // Register the Scynapse-backed persistence service
            services.AddSingleton<DOTNExT.Persistence.IAsyncPersistenceService, ScynapseAsyncPersistenceService>();

            // Configure options
            services.Configure<AsyncPlusOptions>(options =>
            {
                options.StorageProviderName = storageName;
            });
        });

        return siloBuilder;
    }

    /// <summary>
    /// Adds Async+ persistence support to an Scynapse client.
    /// Allows client-side code to use the persistence service.
    /// </summary>
    public static IClientBuilder UseAsyncPlusPersistence(this IClientBuilder clientBuilder)
    {
        clientBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<DOTNExT.Persistence.IAsyncPersistenceService, ScynapseAsyncPersistenceService>();
        });

        return clientBuilder;
    }
}

/// <summary>
/// Configuration options for Async+ persistence.
/// </summary>
public class AsyncPlusOptions
{
    /// <summary>
    /// Name of the Scynapse storage provider to use.
    /// Must match a configured grain storage provider name.
    /// </summary>
    public string StorageProviderName { get; set; } = "AsyncPlusStorage";
}

/// <summary>
/// Extension methods for configuring RavenDB grain storage in Scynapse.
/// </summary>
public static class RavenDbSiloBuilderExtensions
{
    /// <summary>
    /// Configures RavenDB as the default grain storage provider.
    /// </summary>
    public static ISiloBuilder AddRavenDbGrainStorageAsDefault(
        this ISiloBuilder builder,
        Action<RavenDbStorageOptions> configureOptions)
    {
        return builder.AddRavenDbGrainStorage(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, configureOptions);
    }

    /// <summary>
    /// Configures RavenDB as a named grain storage provider.
    /// </summary>
    /// <param name="builder">The silo builder.</param>
    /// <param name="name">The name of the storage provider.</param>
    /// <param name="configureOptions">Action to configure RavenDB options.</param>
    public static ISiloBuilder AddRavenDbGrainStorage(
        this ISiloBuilder builder,
        string name,
        Action<RavenDbStorageOptions> configureOptions)
    {
        return builder.ConfigureServices(services =>
        {
            services.AddRavenDbGrainStorage(name, configureOptions);
        });
    }

    /// <summary>
    /// Configures RavenDB as a named grain storage provider with default options.
    /// Uses localhost:8080 and database "ScynapseGrainState".
    /// </summary>
    public static ISiloBuilder AddRavenDbGrainStorage(
        this ISiloBuilder builder,
        string name)
    {
        return builder.AddRavenDbGrainStorage(name, _ => { });
    }

    /// <summary>
    /// Adds Async+ persistence with RavenDB storage in a single call.
    /// This is a convenience method that configures both RavenDB storage
    /// and the Async+ persistence service.
    /// </summary>
    /// <param name="builder">The silo builder.</param>
    /// <param name="configureOptions">Action to configure RavenDB options.</param>
    /// <param name="storageName">Name of the storage provider. Default: "AsyncPlusStorage"</param>
    public static ISiloBuilder UseAsyncPlusPersistenceWithRavenDb(
        this ISiloBuilder builder,
        Action<RavenDbStorageOptions>? configureOptions = null,
        string storageName = "AsyncPlusStorage")
    {
        return builder
            .AddRavenDbGrainStorage(storageName, configureOptions ?? (_ => { }))
            .UseAsyncPlusPersistence(storageName);
    }
}

/// <summary>
/// Service collection extensions for RavenDB grain storage.
/// </summary>
public static class RavenDbServiceCollectionExtensions
{
    /// <summary>
    /// Adds RavenDB grain storage to the service collection.
    /// Uses the proper Scynapse lifecycle pattern - storage participates before silo starts.
    /// </summary>
    public static IServiceCollection AddRavenDbGrainStorage(
        this IServiceCollection services,
        string name,
        Action<RavenDbStorageOptions> configureOptions)
    {
        // Configure named options
        services.AddOptions<RavenDbStorageOptions>(name)
            .Configure(configureOptions);

        // Register the storage provider as a keyed singleton
        services.AddKeyedSingleton<IGrainStorage>(name, (sp, key) =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<RavenDbStorageOptions>>();
            var options = optionsMonitor.Get(name);

            var clusterOptions = sp.GetRequiredService<IOptions<ClusterOptions>>();
            var logger = sp.GetRequiredService<ILogger<RavenDbGrainStorage>>();
            var serializer = sp.GetRequiredService<IGrainStorageSerializer>();

            return new RavenDbGrainStorage(name, options, serializer, clusterOptions, logger);
        });

        // CRITICAL: Also register as lifecycle participant so Participate() is called BEFORE silo starts
        // This is the Scynapse pattern - without this, storage.Participate() would be called lazily
        // when the first grain requests storage, by which time the lifecycle has already started.
        services.AddSingleton<ILifecycleParticipant<ISiloLifecycle>>(sp =>
            (ILifecycleParticipant<ISiloLifecycle>)sp.GetRequiredKeyedService<IGrainStorage>(name));

        return services;
    }
}
