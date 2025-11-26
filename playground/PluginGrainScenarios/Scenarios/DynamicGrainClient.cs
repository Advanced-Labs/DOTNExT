using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 7: Dynamic Grain Client Loading.
/// Tests the ability for clients to access grains without compile-time references.
///
/// STATUS: NOT YET IMPLEMENTED - This scenario outlines what needs to be built.
///
/// Features to test:
/// - Download interface/proxy DLL from GTD on demand
/// - Load into isolated AssemblyLoadContext
/// - Create grain references without static typing
/// - Support both split (interface-only) and whole (full assembly) downloads
/// - Client and silo-as-client scenarios
/// </summary>
public static class DynamicGrainClient
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 7: Dynamic Grain Client Loading[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]STATUS: NOT YET IMPLEMENTED[/]");
        AnsiConsole.WriteLine();

        // Show what this scenario will test
        AnsiConsole.MarkupLine("[blue]Purpose:[/]");
        AnsiConsole.MarkupLine("  Enable clients (and silos acting as clients) to access grains");
        AnsiConsole.MarkupLine("  without compile-time references to the grain interfaces.");
        AnsiConsole.MarkupLine("  The client downloads interface DLLs on-demand from the cluster.");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[blue]Use Cases:[/]");
        var useCasesTable = new Table();
        useCasesTable.AddColumn("Use Case");
        useCasesTable.AddColumn("Description");

        useCasesTable.AddRow("Plugin System", "Client discovers and calls plugins deployed at runtime");
        useCasesTable.AddRow("Microservices", "Service mesh where grain types are discovered dynamically");
        useCasesTable.AddRow("Admin Tools", "Tools that need to call any grain type for debugging");
        useCasesTable.AddRow("Hot Updates", "Update grain contracts without restarting clients");
        useCasesTable.AddRow("Multi-tenant", "Different tenants deploy different grain types");

        AnsiConsole.Write(useCasesTable);
        AnsiConsole.WriteLine();

        // Show planned features
        AnsiConsole.MarkupLine("[blue]Planned Features to Test:[/]");
        var featuresTable = new Table();
        featuresTable.AddColumn("Feature");
        featuresTable.AddColumn("Description");
        featuresTable.AddColumn("Status");

        featuresTable.AddRow("Type Discovery", "Query GTD for available grain types", "[yellow]Planned[/]");
        featuresTable.AddRow("Interface Download", "Download interface + proxy DLL from cluster", "[yellow]Planned[/]");
        featuresTable.AddRow("Dynamic Loading", "Load DLLs into isolated AssemblyLoadContext", "[yellow]Planned[/]");
        featuresTable.AddRow("Reflection Invocation", "Create grain refs and invoke methods via reflection", "[yellow]Planned[/]");
        featuresTable.AddRow("Strong-Typed Option", "Get actual Type and use with generic GetGrain<T>", "[yellow]Planned[/]");
        featuresTable.AddRow("Dynamic Option", "Use GetGrainDynamic() for fully dynamic access", "[yellow]Planned[/]");
        featuresTable.AddRow("Unload Support", "Unload client-side types when no longer needed", "[yellow]Planned[/]");
        featuresTable.AddRow("Caching", "Cache downloaded assemblies to avoid re-download", "[yellow]Planned[/]");

        AnsiConsole.Write(featuresTable);
        AnsiConsole.WriteLine();

        // Show the planned API
        AnsiConsole.MarkupLine("[blue]Planned API:[/]");
        AnsiConsole.WriteLine();

        var apiPanel = new Panel(
            """
            // Dynamic grain client interface
            public interface IDynamicGrainClient
            {
                // Get grain without compile-time type reference
                Task<dynamic> GetGrainDynamicAsync(string grainTypeName, string grainKey);

                // Load grain type client (interface + proxy)
                Task<GrainTypeClientHandle> LoadGrainTypeClientAsync(string grainTypeName);

                // Unload grain type client (free memory)
                Task UnloadGrainTypeClientAsync(GrainTypeClientHandle handle);

                // Query available grain types from GTD
                Task<IReadOnlyList<GrainTypeInfo>> GetAvailableGrainTypesAsync();
            }

            // Handle for loaded grain type
            public class GrainTypeClientHandle : IAsyncDisposable
            {
                public string GrainTypeName { get; }
                public Type InterfaceType { get; }  // Actual System.Type
                public Type ProxyType { get; }      // Generated proxy type

                // Create strongly-typed reference (via generic method)
                public TGrainInterface GetGrain<TGrainInterface>(string key)
                    where TGrainInterface : IGrain;

                // Create dynamic reference
                public dynamic GetGrainDynamic(string key);

                // Invoke method by name
                public Task<object?> InvokeMethodAsync(
                    string grainKey,
                    string methodName,
                    params object[] args);
            }
            """)
        {
            Header = new PanelHeader("IDynamicGrainClient Interface"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(apiPanel);
        AnsiConsole.WriteLine();

        // Show implementation components
        AnsiConsole.MarkupLine("[blue]Implementation Components Needed:[/]");
        var componentsTable = new Table();
        componentsTable.AddColumn("Component");
        componentsTable.AddColumn("Location");
        componentsTable.AddColumn("Purpose");

        componentsTable.AddRow("DynamicGrainClient", "Orleans.Core/DynamicGrains/", "Client-side dynamic loading");
        componentsTable.AddRow("AssemblyDownloader", "Orleans.Core/DynamicGrains/", "Downloads DLLs from cluster");
        componentsTable.AddRow("GrainTypeClientHandle", "Orleans.Core/DynamicGrains/", "Handle for loaded types");
        componentsTable.AddRow("ClientAssemblyLoader", "Orleans.Core/DynamicGrains/", "MDCP-based client loader");
        componentsTable.AddRow("IAssemblyStorageGrain", "Orleans.Runtime/DynamicGrains/", "Stores DLL bytes in cluster");

        AnsiConsole.Write(componentsTable);
        AnsiConsole.WriteLine();

        // Show the test phases
        AnsiConsole.MarkupLine("[blue]Test Phases (Once Implemented):[/]");
        AnsiConsole.MarkupLine("  Phase 1: Start silo with plugin grains loaded");
        AnsiConsole.MarkupLine("  Phase 2: Start Orleans client (WITHOUT grain references)");
        AnsiConsole.MarkupLine("  Phase 3: Query available grain types from GTD");
        AnsiConsole.MarkupLine("  Phase 4: Download IHelloGrain interface + proxy");
        AnsiConsole.MarkupLine("  Phase 5: Create grain reference dynamically");
        AnsiConsole.MarkupLine("  Phase 6: Invoke SayHello() method");
        AnsiConsole.MarkupLine("  Phase 7: Unload client-side types");
        AnsiConsole.MarkupLine("  Phase 8: Verify memory reclaimed");
        AnsiConsole.WriteLine();

        // Show code example
        AnsiConsole.MarkupLine("[blue]Usage Example (Once Implemented):[/]");
        AnsiConsole.WriteLine();

        var examplePanel = new Panel(
            """
            // Client code (no compile-time reference to IHelloGrain!)
            var dynamicClient = serviceProvider.GetRequiredService<IDynamicGrainClient>();

            // Option 1: Fully dynamic
            var result = await dynamicClient.GetGrainDynamicAsync(
                "DynamicGrainLoading.Contracts.IHelloGrain",
                "my-grain-key"
            );
            string greeting = await result.SayHello("World");

            // Option 2: Load type first, then use strongly-typed
            using var handle = await dynamicClient.LoadGrainTypeClientAsync(
                "DynamicGrainLoading.Contracts.IHelloGrain"
            );

            // Now we have the actual Type!
            Console.WriteLine($"Interface: {handle.InterfaceType.Name}");

            // Invoke via reflection
            var response = await handle.InvokeMethodAsync(
                "my-grain-key",
                "SayHello",
                "World"
            );
            """)
        {
            Header = new PanelHeader("Client Usage Example"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(examplePanel);
        AnsiConsole.WriteLine();

        // Dependencies
        AnsiConsole.MarkupLine("[blue]Dependencies:[/]");
        AnsiConsole.MarkupLine("  • Scenario 5: Split Grain Assemblies [green]✓ Complete[/]");
        AnsiConsole.MarkupLine("  • Scenario 6: Grain Type Directory [yellow]Not Yet Implemented[/]");
        AnsiConsole.MarkupLine("  • Assembly storage/distribution system [yellow]Not Yet Implemented[/]");
        AnsiConsole.WriteLine();

        // Run a check
        var runCheck = AnsiConsole.Confirm("Run a check to see current implementation status?", defaultValue: true);

        if (runCheck)
        {
            await RunImplementationStatusCheck();
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 7 Complete (Dynamic Client Not Yet Implemented)[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }

    private static async Task RunImplementationStatusCheck()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Checking Current Implementation Status...[/]");
        AnsiConsole.WriteLine();

        var host = SiloHelper.BuildSingleSilo();

        await AnsiConsole.Status()
            .StartAsync("Starting silo...", async ctx =>
            {
                await host.StartAsync();
            });
        AnsiConsole.MarkupLine("[green]Silo started[/]");
        AnsiConsole.WriteLine();

        // Check what services exist
        var statusTable = new Table();
        statusTable.AddColumn("Service/Interface");
        statusTable.AddColumn("Exists");
        statusTable.AddColumn("Notes");

        // Check for services that would be needed
        var checkItems = new[]
        {
            ("IDynamicGrainClient", "Orleans.Core.DynamicGrains.IDynamicGrainClient, Orleans.Core", "Main dynamic client interface"),
            ("IGrainTypeDirectory", "Orleans.Runtime.DynamicGrains.IGrainTypeDirectory, Orleans.Runtime", "Required - provides type discovery"),
            ("IAssemblyStorageGrain", "Orleans.Runtime.DynamicGrains.IAssemblyStorageGrain, Orleans.Runtime", "Required - stores assembly bytes"),
            ("IPluginGrainLoader", "Orleans.Runtime.DynamicGrains.IPluginGrainLoader, Orleans.Runtime", "Available - silo-side loading"),
        };

        foreach (var (name, typeName, notes) in checkItems)
        {
            var type = Type.GetType(typeName);
            var exists = type != null;

            // For IPluginGrainLoader, also check if it's in the DI container
            if (name == "IPluginGrainLoader")
            {
                var service = host.Services.GetService<IPluginGrainLoader>();
                exists = service != null;
            }

            statusTable.AddRow(
                name,
                exists ? "[green]Yes[/]" : "[yellow]No[/]",
                exists ? notes : $"Not yet implemented - {notes}"
            );
        }

        AnsiConsole.Write(statusTable);
        AnsiConsole.WriteLine();

        // Show the split assemblies we created (proof of concept)
        AnsiConsole.MarkupLine("[blue]Split Assembly Foundation (Contracts/Implementation pattern):[/]");

        var testGrainsPath = TestGrainsFinder.FindTestGrainsAssembly();
        if (testGrainsPath != null)
        {
            var baseDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(testGrainsPath))));
            var contractsPath = FindSplitAssembly(baseDir, "Contracts");
            var implPath = FindSplitAssembly(baseDir, "Implementation");

            var assemblyTable = new Table();
            assemblyTable.AddColumn("Assembly");
            assemblyTable.AddColumn("Found");
            assemblyTable.AddColumn("Purpose");

            assemblyTable.AddRow(
                "DynamicGrainLoading.Contracts",
                contractsPath != null ? "[green]Yes[/]" : "[red]No[/]",
                "Interface DLL (what clients would download)"
            );
            assemblyTable.AddRow(
                "DynamicGrainLoading.Implementation",
                implPath != null ? "[green]Yes[/]" : "[red]No[/]",
                "Implementation DLL (stays on silos)"
            );

            AnsiConsole.Write(assemblyTable);

            if (contractsPath != null)
            {
                AnsiConsole.MarkupLine("[grey]  The Contracts DLL is what would be distributed to clients.[/]");
                AnsiConsole.MarkupLine("[grey]  It contains only interfaces + generated proxies - no implementations.[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        host.Dispose();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");
    }

    private static string? FindSplitAssembly(string? baseDir, string assemblyType)
    {
        if (baseDir == null) return null;

        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Orleans.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir != null)
        {
            var playgroundPath = Path.Combine(currentDir.FullName, "playground", $"DynamicGrainLoading.{assemblyType}", "bin");
            if (Directory.Exists(playgroundPath))
            {
                foreach (var binDir in Directory.GetDirectories(playgroundPath, "*", SearchOption.AllDirectories))
                {
                    var dllPath = Path.Combine(binDir, $"DynamicGrainLoading.{assemblyType}.dll");
                    if (File.Exists(dllPath))
                    {
                        return dllPath;
                    }
                }
            }
        }

        return null;
    }
}
