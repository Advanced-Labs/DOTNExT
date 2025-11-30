using Microsoft.Extensions.DependencyInjection;
using NewOrleans.AsyncPlus.Services;
using Orleans.Hosting;

namespace NewOrleans.AsyncPlus;

/// <summary>
/// Extension methods for configuring Async+ persistence in Orleans.
/// </summary>
public static class AsyncPlusHostingExtensions
{
    /// <summary>
    /// Adds Async+ persistence support to the Orleans silo.
    /// Registers NewOrleansAsyncPersistenceService as the IAsyncPersistenceService implementation.
    /// </summary>
    /// <param name="siloBuilder">The silo builder</param>
    /// <param name="storageName">
    /// Name of the Orleans storage provider to use for Async+ state.
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
            // Register the Orleans-backed persistence service
            services.AddSingleton<DOTNExT.Persistence.IAsyncPersistenceService, NewOrleansAsyncPersistenceService>();

            // Configure options
            services.Configure<AsyncPlusOptions>(options =>
            {
                options.StorageProviderName = storageName;
            });
        });

        return siloBuilder;
    }

    /// <summary>
    /// Adds Async+ persistence support to an Orleans client.
    /// Allows client-side code to use the persistence service.
    /// </summary>
    public static IClientBuilder UseAsyncPlusPersistence(this IClientBuilder clientBuilder)
    {
        clientBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<DOTNExT.Persistence.IAsyncPersistenceService, NewOrleansAsyncPersistenceService>();
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
    /// Name of the Orleans storage provider to use.
    /// Must match a configured grain storage provider name.
    /// </summary>
    public string StorageProviderName { get; set; } = "AsyncPlusStorage";
}
