using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime.DynamicGrains;
using Spectre.Console;

namespace PluginGrainScenarios;

/// <summary>
/// Unified scenario runner for testing plugin grain loading.
/// Run this from VS2022 - no command-line arguments needed.
/// </summary>
public static class Program
{
    public static async Task Main()
    {
        AnsiConsole.Write(new FigletText("Plugin Grain Scenarios").Color(Color.Blue));
        AnsiConsole.MarkupLine("[grey]Testing MDCP-based grain loading, unloading, and isolation[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var scenario = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select a scenario to run:[/]")
                    .PageSize(12)
                    .AddChoices(new[]
                    {
                        "1. Single Silo - Basic Load/Unload",
                        "2. Single Silo - MDCP Isolation Verification",
                        "3. Multi-Silo Cluster - Manifest Propagation",
                        "4. Assembly Unload and Memory Reclaim",
                        "5. Split Grain Assemblies",
                        "─── Future Features (Not Yet Implemented) ───",
                        "6. Grain Type Directory (GTD)",
                        "7. Dynamic Grain Client",
                        "Exit"
                    }));

            if (scenario == "Exit")
            {
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                break;
            }

            // Skip separator line
            if (scenario.StartsWith("───"))
            {
                continue;
            }

            try
            {
                await RunScenarioAsync(scenario);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                AnsiConsole.WriteException(ex);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            AnsiConsole.Clear();
        }
    }

    private static async Task RunScenarioAsync(string scenario)
    {
        switch (scenario)
        {
            case "1. Single Silo - Basic Load/Unload":
                await Scenarios.SingleSiloBasicLoadUnload.RunAsync();
                break;
            case "2. Single Silo - MDCP Isolation Verification":
                await Scenarios.MdcpIsolationVerification.RunAsync();
                break;
            case "3. Multi-Silo Cluster - Manifest Propagation":
                await Scenarios.MultiSiloManifestPropagation.RunAsync();
                break;
            case "4. Assembly Unload and Memory Reclaim":
                await Scenarios.AssemblyUnloadMemoryReclaim.RunAsync();
                break;
            case "5. Split Grain Assemblies":
                await Scenarios.SplitGrainAssemblies.RunAsync();
                break;
            case "6. Grain Type Directory (GTD)":
                await Scenarios.GrainTypeDirectory.RunAsync();
                break;
            case "7. Dynamic Grain Client":
                await Scenarios.DynamicGrainClient.RunAsync();
                break;
        }
    }
}

// Helper to find test grain assemblies
public static class TestGrainsFinder
{
    public static string? FindTestGrainsAssembly()
    {
        var baseDir = AppContext.BaseDirectory;
        var searchPaths = new[]
        {
            Path.Combine(baseDir, "DynamicGrainLoading.TestGrains.dll"),
            Path.Combine(baseDir, "..", "DynamicGrainLoading.TestGrains", "bin", "Debug", "net8.0", "DynamicGrainLoading.TestGrains.dll"),
            Path.Combine(baseDir, "..", "DynamicGrainLoading.TestGrains", "bin", "Release", "net8.0", "DynamicGrainLoading.TestGrains.dll"),
        };

        // Also search upward for Orleans root
        var currentDir = new DirectoryInfo(baseDir);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Orleans.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir != null)
        {
            var playgroundPath = Path.Combine(currentDir.FullName, "playground", "DynamicGrainLoading.TestGrains", "bin");
            if (Directory.Exists(playgroundPath))
            {
                foreach (var binDir in Directory.GetDirectories(playgroundPath, "*", SearchOption.AllDirectories))
                {
                    var dllPath = Path.Combine(binDir, "DynamicGrainLoading.TestGrains.dll");
                    if (File.Exists(dllPath))
                    {
                        return dllPath;
                    }
                }
            }
        }

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }
}

// Silo builder helper
public static class SiloHelper
{
    public static IHost BuildSingleSilo(int siloPort = 11111, int gatewayPort = 30000, LogLevel logLevel = LogLevel.Information)
    {
        return Host.CreateDefaultBuilder()
            .UseOrleans(siloBuilder =>
            {
                siloBuilder
                    .UseLocalhostClustering(siloPort, gatewayPort)
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "scenario-test";
                        options.ServiceId = "scenario-test";
                    });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(logLevel);
                logging.AddFilter("Orleans.Runtime.DynamicGrains", LogLevel.Debug);
                logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
            })
            .Build();
    }

    public static IHost BuildClusterSilo(string name, int siloPort, int gatewayPort, int primarySiloPort, LogLevel logLevel = LogLevel.Information)
    {
        return Host.CreateDefaultBuilder()
            .UseOrleans(siloBuilder =>
            {
                siloBuilder
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "scenario-cluster";
                        options.ServiceId = "scenario-cluster";
                    })
                    .ConfigureEndpoints(IPAddress.Loopback, siloPort, gatewayPort)
                    .UseDevelopmentClustering(options =>
                    {
                        options.PrimarySiloEndpoint = new IPEndPoint(IPAddress.Loopback, primarySiloPort);
                    });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(logLevel);
                logging.AddFilter("Orleans.Runtime.DynamicGrains", LogLevel.Debug);
                logging.AddFilter($"Orleans.{name}", LogLevel.Information);
                logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);
            })
            .Build();
    }
}
