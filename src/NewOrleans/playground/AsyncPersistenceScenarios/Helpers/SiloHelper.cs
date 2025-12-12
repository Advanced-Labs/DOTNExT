using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewOrleans.AsyncPlus.Extensions;
using Orleans.Configuration;
using Orleans.Hosting;

namespace AsyncPersistenceScenarios.Helpers;

/// <summary>
/// Helper class for building Orleans silos in Async+ scenarios.
/// Follows the patterns established in PluginGrainScenarios.
/// </summary>
public static class SiloHelper
{
    /// <summary>
    /// Builds a single silo with RavenDB storage for Async+ persistence.
    /// This is the recommended configuration for production-like scenarios.
    /// </summary>
    public static IHost BuildSingleSiloWithRavenDb(
        int siloPort = 11111,
        int gatewayPort = 30000,
        string clusterId = "async-plus-test",
        string serviceId = "async-plus-test",
        string ravenDbUrl = "http://127.0.0.1:38880",
        string databaseName = "AsyncPlusScenarios",
        LogLevel logLevel = LogLevel.Warning)
    {
        return Host.CreateDefaultBuilder()
            .UseOrleans(silo =>
            {
                silo.UseLocalhostClustering(siloPort, gatewayPort)
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = clusterId;
                        options.ServiceId = serviceId;
                    });

                // Always use RavenDB for Async+ scenarios
                silo.UseAsyncPlusPersistenceWithRavenDb(options =>
                {
                    options.Urls = new[] { ravenDbUrl };
                    options.DatabaseName = databaseName;
                    options.CreateDatabaseIfNotExists = true;
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(logLevel);
                // AsyncPlus debug logging goes to file only - console stays clean
                logging.AddFilter("NewOrleans.AsyncPlus", LogLevel.Warning);
                logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
            })
            .Build();
    }

    /// <summary>
    /// Builds a single silo with memory storage for quick testing.
    /// Use this for scenarios that don't require RavenDB.
    /// </summary>
    public static IHost BuildSingleSiloWithMemory(
        int siloPort = 11111,
        int gatewayPort = 30000,
        string clusterId = "async-plus-test",
        string serviceId = "async-plus-test",
        LogLevel logLevel = LogLevel.Warning)
    {
        return Host.CreateDefaultBuilder()
            .UseOrleans(silo =>
            {
                silo.UseLocalhostClustering(siloPort, gatewayPort)
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = clusterId;
                        options.ServiceId = serviceId;
                    })
                    .AddMemoryGrainStorage("AsyncPlusStorage")
                    .UseAsyncPlusPersistence("AsyncPlusStorage");
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(logLevel);
                // AsyncPlus debug logging goes to file only - console stays clean
                logging.AddFilter("NewOrleans.AsyncPlus", LogLevel.Warning);
                logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
            })
            .Build();
    }

    /// <summary>
    /// Builds a silo for a multi-silo cluster with RavenDB storage.
    /// </summary>
    public static IHost BuildClusterSiloWithRavenDb(
        string name,
        int siloPort,
        int gatewayPort,
        int primarySiloPort,
        string ravenDbUrl = "http://127.0.0.1:38880",
        string databaseName = "AsyncPlusScenarios",
        LogLevel logLevel = LogLevel.Warning)
    {
        return Host.CreateDefaultBuilder()
            .UseOrleans(silo =>
            {
                silo.Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "async-plus-cluster";
                        options.ServiceId = "async-plus-cluster";
                    })
                    .ConfigureEndpoints(IPAddress.Loopback, siloPort, gatewayPort)
                    .UseDevelopmentClustering(options =>
                    {
                        options.PrimarySiloEndpoint = new IPEndPoint(IPAddress.Loopback, primarySiloPort);
                    });

                // Always use RavenDB for multi-silo scenarios (shared state)
                silo.UseAsyncPlusPersistenceWithRavenDb(options =>
                {
                    options.Urls = new[] { ravenDbUrl };
                    options.DatabaseName = databaseName;
                    options.CreateDatabaseIfNotExists = true;
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(logLevel);
                logging.AddFilter("NewOrleans.AsyncPlus", LogLevel.Debug);
                logging.AddFilter($"Orleans.{name}", LogLevel.Information);
                logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
            })
            .Build();
    }

    /// <summary>
    /// Safely stops multiple silos, catching any exceptions.
    /// </summary>
    public static async Task StopAllSilosAsync(params IHost[] hosts)
    {
        foreach (var host in hosts)
        {
            try
            {
                await host.StopAsync();
                host.Dispose();
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }
    }
}
