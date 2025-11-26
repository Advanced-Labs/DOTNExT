using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 7: Dynamic Grain Client Loading.
/// Tests the ability for both clients AND silos to access grains without compile-time references.
///
/// STATUS: NOT YET IMPLEMENTED - This scenario outlines the comprehensive design.
///
/// Features to test:
/// - GetGrainDynamic() methods on IGrainFactory
/// - GetGrain(GrainTypeMeta, key) overloads
/// - IDynamicGrainClient for package management
/// - GrainPackage and GrainTypeMeta types
/// - Integration with GTD for type discovery
/// - Works for Orleans Clients AND grain-to-grain calls
/// </summary>
public static class DynamicGrainClient
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 7: Dynamic Grain Access[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]STATUS: NOT YET IMPLEMENTED[/]");
        AnsiConsole.WriteLine();

        // Show what this scenario will test
        AnsiConsole.MarkupLine("[blue]Purpose:[/]");
        AnsiConsole.MarkupLine("  Enable [bold]both clients AND silos[/] (grain-to-grain calls) to access grains");
        AnsiConsole.MarkupLine("  without compile-time references to the grain interfaces.");
        AnsiConsole.MarkupLine("  Uses GrainPackage/GrainTypeMeta for type metadata and routing.");
        AnsiConsole.WriteLine();

        // Show use cases
        ShowUseCases();

        // Show new API design
        ShowApiDesign();

        // Show core types
        ShowCoreTypes();

        // Show implementation components
        ShowImplementationComponents();

        // Show usage examples
        ShowUsageExamples();

        // Show test phases
        ShowTestPhases();

        // Show dependencies
        ShowDependencies();

        // Run implementation status check
        var runCheck = AnsiConsole.Confirm("Run a check to see current implementation status?", defaultValue: true);

        if (runCheck)
        {
            await RunImplementationStatusCheck();
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 7 Complete (Dynamic Access Not Yet Implemented)[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }

    private static void ShowUseCases()
    {
        AnsiConsole.MarkupLine("[blue]Use Cases:[/]");
        var useCasesTable = new Table();
        useCasesTable.AddColumn("Use Case");
        useCasesTable.AddColumn("Description");
        useCasesTable.AddColumn("Caller");

        useCasesTable.AddRow("Plugin System", "Discover and call plugins deployed at runtime", "Client or Silo");
        useCasesTable.AddRow("Orchestrator Grain", "Grain calls other grains by type name from config", "Silo");
        useCasesTable.AddRow("Admin Tools", "Debug tools that call any grain type", "Client");
        useCasesTable.AddRow("Hot Updates", "Update grain contracts without restarting", "Both");
        useCasesTable.AddRow("Multi-tenant", "Different tenants deploy different grain types", "Both");
        useCasesTable.AddRow("Workflow Engine", "Execute grain calls defined in workflow definitions", "Silo");

        AnsiConsole.Write(useCasesTable);
        AnsiConsole.WriteLine();
    }

    private static void ShowApiDesign()
    {
        AnsiConsole.MarkupLine("[blue]New API Design:[/]");
        AnsiConsole.WriteLine();

        // IGrainFactory extensions
        var factoryPanel = new Panel(
            """
            // NEW methods on IGrainFactory
            public interface IGrainFactory
            {
                // ... existing 13 overloads ...

                // Dynamic grain access by type name
                dynamic GetGrainDynamic(string grainTypeName, string primaryKey);
                dynamic GetGrainDynamic(string grainTypeName, Guid primaryKey);
                dynamic GetGrainDynamic(string grainTypeName, long primaryKey);

                // Dynamic access using GTD metadata
                dynamic GetGrain(GrainTypeMeta grainTypeMeta, string primaryKey);
                dynamic GetGrain(GrainTypeMeta grainTypeMeta, Guid primaryKey);
                dynamic GetGrain(GrainTypeMeta grainTypeMeta, long primaryKey);
            }
            """)
        {
            Header = new PanelHeader("IGrainFactory Extensions"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(factoryPanel);
        AnsiConsole.WriteLine();

        // IDynamicGrainClient
        var clientPanel = new Panel(
            """
            // Full dynamic client with package management
            public interface IDynamicGrainClient
            {
                // Package Management
                Task<GrainPackageHandle> LoadPackageAsync(string packageId, string? version = null);
                Task UnloadPackageAsync(GrainPackageHandle handle);
                Task<IReadOnlyList<GrainPackageInfo>> ListAvailablePackagesAsync();

                // Grain Access
                Task<dynamic> GetGrainDynamicAsync(string grainTypeName, string primaryKey);
                dynamic GetGrain(GrainTypeMeta grainType, string primaryKey);

                // Reflection-style invocation
                Task<object?> InvokeMethodAsync(
                    string grainTypeName,
                    string primaryKey,
                    string methodName,
                    object?[]? args = null);

                // GTD Queries
                Task<IReadOnlyList<GrainTypeMeta>> QueryGrainTypesAsync(
                    string? namespaceFilter = null,
                    string? namePattern = null);
                Task<GrainTypeMeta?> GetGrainTypeMetaAsync(string grainTypeName);
            }
            """)
        {
            Header = new PanelHeader("IDynamicGrainClient Interface"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(clientPanel);
        AnsiConsole.WriteLine();
    }

    private static void ShowCoreTypes()
    {
        AnsiConsole.MarkupLine("[blue]Core Types:[/]");
        AnsiConsole.WriteLine();

        // GrainPackage
        var packagePanel = new Panel(
            """
            [GenerateSerializer, Immutable]
            public sealed class GrainPackage
            {
                public string PackageId { get; init; }
                public string Version { get; init; }
                public string ContentHash { get; init; }
                public ImmutableList<GrainTypeMeta> GrainTypes { get; init; }
                public GrainPackageContent ContentType { get; init; }  // InterfacesOnly, Full, ImplementationsOnly
                public ImmutableList<GrainPackageAssembly> Assemblies { get; init; }
                public ImmutableDictionary<string, string> Metadata { get; init; }

                public GrainTypeMeta? GetGrainType(string name, string? version = null);
            }
            """)
        {
            Header = new PanelHeader("GrainPackage Type"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(packagePanel);
        AnsiConsole.WriteLine();

        // GrainTypeMeta
        var metaPanel = new Panel(
            """
            [GenerateSerializer, Immutable]
            public sealed class GrainTypeMeta
            {
                public GrainType GrainType { get; init; }         // Orleans identifier
                public string FullName { get; init; }             // CLR type name
                public string Namespace { get; init; }
                public string TypeName { get; init; }
                public string Version { get; init; }
                public string AssemblyName { get; init; }
                public string AssemblyHash { get; init; }
                public ImmutableList<GrainInterfaceMeta> Interfaces { get; init; }
                public GrainKeyType KeyType { get; init; }        // String, Guid, Int64, etc.
                public GrainPackage? SourcePackage { get; init; } // Reference back to package
                public ImmutableList<SiloAddress> HostingSilos { get; init; }
                public bool IsAvailable { get; init; }
            }
            """)
        {
            Header = new PanelHeader("GrainTypeMeta Type"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(metaPanel);
        AnsiConsole.WriteLine();
    }

    private static void ShowImplementationComponents()
    {
        AnsiConsole.MarkupLine("[blue]Implementation Components:[/]");
        var componentsTable = new Table();
        componentsTable.AddColumn("Component");
        componentsTable.AddColumn("Location");
        componentsTable.AddColumn("Purpose");

        // Core types
        componentsTable.AddRow("[bold]Core Types[/]", "", "");
        componentsTable.AddRow("  GrainPackage", "Orleans.Core.Abstractions/Metadata/", "Package definition");
        componentsTable.AddRow("  GrainTypeMeta", "Orleans.Core.Abstractions/Metadata/", "Type metadata with package ref");
        componentsTable.AddRow("  GrainInterfaceMeta", "Orleans.Core.Abstractions/Metadata/", "Interface methods");

        // Client components
        componentsTable.AddRow("[bold]Client Components[/]", "", "");
        componentsTable.AddRow("  IDynamicGrainClient", "Orleans.Core/DynamicGrains/", "Main client interface");
        componentsTable.AddRow("  DynamicGrainClient", "Orleans.Core/DynamicGrains/", "Implementation");
        componentsTable.AddRow("  DynamicGrainReference", "Orleans.Core/DynamicGrains/", "DLR-based invocation");

        // Runtime components
        componentsTable.AddRow("[bold]Runtime Components[/]", "", "");
        componentsTable.AddRow("  IGrainTypeDirectoryGrain", "Orleans.Runtime/DynamicGrains/", "GTD grain interface");
        componentsTable.AddRow("  GrainTypeDirectoryGrain", "Orleans.Runtime/DynamicGrains/", "GTD implementation");
        componentsTable.AddRow("  IGrainPackageStore", "Orleans.Runtime/DynamicGrains/", "Package storage abstraction");

        // Storage
        componentsTable.AddRow("[bold]Package Storage[/]", "", "");
        componentsTable.AddRow("  FileSystemPackageSource", "Orleans.Runtime/DynamicGrains/", "Load from disk");
        componentsTable.AddRow("  NuGetPackageSource", "Orleans.Runtime/DynamicGrains/", "Load from NuGet feed");
        componentsTable.AddRow("  GrainStoragePackageSource", "Orleans.Runtime/DynamicGrains/", "Load from cluster storage");

        // Cache
        componentsTable.AddRow("[bold]Package Cache[/]", "", "");
        componentsTable.AddRow("  IGrainPackageCache", "Orleans.Core/DynamicGrains/", "Cache interface");
        componentsTable.AddRow("  FileSystemPackageCache", "Orleans.Core/DynamicGrains/", "Disk-based cache");

        AnsiConsole.Write(componentsTable);
        AnsiConsole.WriteLine();
    }

    private static void ShowUsageExamples()
    {
        AnsiConsole.MarkupLine("[blue]Usage Examples:[/]");
        AnsiConsole.WriteLine();

        // Client example
        var clientExample = new Panel(
            """
            // CLIENT CODE: No compile-time reference to IHelloGrain!
            var dynamicClient = serviceProvider.GetRequiredService<IDynamicGrainClient>();

            // Option A: Fully dynamic invocation
            var result = await dynamicClient.InvokeMethodAsync(
                "MyPlugins.IHelloGrain",
                "my-grain-id",
                "SayHello",
                new object[] { "World" }
            );

            // Option B: Dynamic with C# dynamic keyword
            dynamic grain = await dynamicClient.GetGrainDynamicAsync(
                "MyPlugins.IHelloGrain", "my-grain-id");
            string greeting = await grain.SayHello("World");

            // Option C: Load package, then use metadata
            var handle = await dynamicClient.LoadPackageAsync("MyPlugins");
            var grainType = handle.GetGrainType("MyPlugins.IHelloGrain");
            dynamic grain2 = handle.GetGrain("MyPlugins.IHelloGrain", "key");
            await grain2.DoWork();
            """)
        {
            Header = new PanelHeader("Client Usage Example"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(clientExample);
        AnsiConsole.WriteLine();

        // Silo example
        var siloExample = new Panel(
            """
            // SILO CODE: Grain-to-grain dynamic calls
            public class OrchestratorGrain : Grain, IOrchestratorGrain
            {
                private readonly IDynamicGrainClient _dynamicClient;

                public OrchestratorGrain(IDynamicGrainClient dynamicClient)
                {
                    _dynamicClient = dynamicClient;
                }

                public async Task ProcessWorkflow(WorkflowDefinition workflow)
                {
                    foreach (var step in workflow.Steps)
                    {
                        // Call grains dynamically based on workflow config!
                        await _dynamicClient.InvokeMethodAsync(
                            step.GrainTypeName,  // e.g., "Workflow.IValidatorGrain"
                            step.GrainKey,       // e.g., "validator-1"
                            step.MethodName,     // e.g., "Validate"
                            step.Arguments
                        );
                    }
                }
            }
            """)
        {
            Header = new PanelHeader("Silo (Grain-to-Grain) Usage Example"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(siloExample);
        AnsiConsole.WriteLine();

        // Factory extension example
        var factoryExample = new Panel(
            """
            // DIRECT FACTORY USAGE: With GrainTypeMeta from GTD
            var gtd = grainFactory.GetGrain<IGrainTypeDirectoryGrain>("gtd");
            var grainMeta = await gtd.GetGrainTypeAsync("MyPlugins.IHelloGrain");

            if (grainMeta != null)
            {
                // New factory overload accepts GrainTypeMeta
                dynamic grain = grainFactory.GetGrain(grainMeta, "my-key");
                await grain.SayHello("World");

                // Can also check hosting silos
                Console.WriteLine($"Hosted on {grainMeta.HostingSilos.Count} silos");
            }
            """)
        {
            Header = new PanelHeader("IGrainFactory Extension Usage"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(factoryExample);
        AnsiConsole.WriteLine();
    }

    private static void ShowTestPhases()
    {
        AnsiConsole.MarkupLine("[blue]Test Phases (Once Implemented):[/]");
        AnsiConsole.MarkupLine("  Phase 1: Start silo with plugin grains loaded");
        AnsiConsole.MarkupLine("  Phase 2: Create IDynamicGrainClient instance");
        AnsiConsole.MarkupLine("  Phase 3: Query GTD for available grain types");
        AnsiConsole.MarkupLine("  Phase 4: Test GetGrainDynamic() with type name");
        AnsiConsole.MarkupLine("  Phase 5: Test InvokeMethodAsync() for reflection-style calls");
        AnsiConsole.MarkupLine("  Phase 6: Load GrainPackage and test package-based access");
        AnsiConsole.MarkupLine("  Phase 7: Test grain-to-grain dynamic calls (silo scenario)");
        AnsiConsole.MarkupLine("  Phase 8: Unload package and verify memory reclaimed");
        AnsiConsole.WriteLine();
    }

    private static void ShowDependencies()
    {
        AnsiConsole.MarkupLine("[blue]Dependencies:[/]");
        AnsiConsole.MarkupLine("  • Scenario 5: Split Grain Assemblies [green]✓ Complete[/]");
        AnsiConsole.MarkupLine("  • Scenario 6: Grain Type Directory [yellow]Not Yet Implemented[/]");
        AnsiConsole.MarkupLine("  • GrainPackage type [yellow]Not Yet Implemented[/]");
        AnsiConsole.MarkupLine("  • GrainTypeMeta type [yellow]Not Yet Implemented[/]");
        AnsiConsole.MarkupLine("  • Package storage system [yellow]Not Yet Implemented[/]");
        AnsiConsole.WriteLine();

        // Implementation phases from design doc
        AnsiConsole.MarkupLine("[blue]Implementation Phases:[/]");
        var phasesTable = new Table();
        phasesTable.AddColumn("Phase");
        phasesTable.AddColumn("Components");
        phasesTable.AddColumn("Status");

        phasesTable.AddRow("Phase 1: Core Types", "GrainPackage, GrainTypeMeta, GrainInterfaceMeta", "[yellow]Pending[/]");
        phasesTable.AddRow("Phase 2: GTD", "IGrainTypeDirectoryGrain, Implementation", "[yellow]Pending[/]");
        phasesTable.AddRow("Phase 3: Factory Extensions", "GetGrainDynamic(), GetGrain(GrainTypeMeta)", "[yellow]Pending[/]");
        phasesTable.AddRow("Phase 4: Package Storage", "IGrainPackageStore, Sources", "[yellow]Pending[/]");
        phasesTable.AddRow("Phase 5: Package Cache", "IGrainPackageCache, FileSystemCache", "[yellow]Pending[/]");
        phasesTable.AddRow("Phase 6: Client Integration", "IDynamicGrainClient, DynamicGrainClient", "[yellow]Pending[/]");

        AnsiConsole.Write(phasesTable);
        AnsiConsole.WriteLine();
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
        statusTable.AddColumn("Service/Type");
        statusTable.AddColumn("Exists");
        statusTable.AddColumn("Notes");

        // Check for services that would be needed
        var checkItems = new[]
        {
            ("IDynamicGrainClient", "Orleans.Runtime.DynamicGrains.IDynamicGrainClient, Orleans.Core", "Main dynamic client interface"),
            ("IGrainTypeDirectoryGrain", "Orleans.Runtime.DynamicGrains.IGrainTypeDirectoryGrain, Orleans.Runtime", "GTD grain interface"),
            ("GrainPackage", "Orleans.Metadata.GrainPackage, Orleans.Core.Abstractions", "Package type"),
            ("GrainTypeMeta", "Orleans.Metadata.GrainTypeMeta, Orleans.Core.Abstractions", "Type metadata"),
            ("IGrainPackageStore", "Orleans.Runtime.DynamicGrains.IGrainPackageStore, Orleans.Runtime", "Package storage"),
            ("IGrainPackageCache", "Orleans.Runtime.DynamicGrains.IGrainPackageCache, Orleans.Core", "Package cache"),
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
                exists ? $"[green]{notes}[/]" : $"[grey]Not yet implemented - {notes}[/]"
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
                AnsiConsole.MarkupLine("[grey]  The Contracts DLL is what would be distributed to clients via GrainPackage.[/]");
                AnsiConsole.MarkupLine("[grey]  GrainPackageContent.InterfacesOnly would contain only the Contracts DLL.[/]");
            }
        }

        // Show note about dynamic keyword
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Note on C# dynamic keyword:[/]");
        AnsiConsole.MarkupLine("  [grey]GetGrain<dynamic>() is NOT possible - dynamic is a compiler feature, not a type.[/]");
        AnsiConsole.MarkupLine("  [grey]Solution: Use separate GetGrainDynamic() methods that return dynamic.[/]");
        AnsiConsole.MarkupLine("  [grey]The DLR (Dynamic Language Runtime) handles late-bound method dispatch.[/]");

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
