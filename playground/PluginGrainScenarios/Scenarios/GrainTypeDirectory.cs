using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 6: Grain Type Directory (GTD).
/// Tests the cluster-wide registry of all available grain types with metadata.
///
/// STATUS: NOT YET IMPLEMENTED - This scenario outlines what needs to be built.
///
/// Features to test:
/// - Register grain types with metadata in the directory
/// - Query available grain types without compile-time references
/// - Track which silos can host which grain types
/// - Metadata includes: methods, properties, version info
/// - Cross-silo type resolution
/// </summary>
public static class GrainTypeDirectory
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 6: Grain Type Directory (GTD)[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]STATUS: NOT YET IMPLEMENTED[/]");
        AnsiConsole.WriteLine();

        // Show what this scenario will test
        AnsiConsole.MarkupLine("[blue]Purpose:[/]");
        AnsiConsole.MarkupLine("  The Grain Type Directory (GTD) is a cluster-wide registry that enables");
        AnsiConsole.MarkupLine("  discovery of grain types without compile-time references.");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[blue]Planned Features to Test:[/]");
        var featuresTable = new Table();
        featuresTable.AddColumn("Feature");
        featuresTable.AddColumn("Description");
        featuresTable.AddColumn("Status");

        featuresTable.AddRow("Type Registration", "Register grain types with full metadata", "[yellow]Planned[/]");
        featuresTable.AddRow("Type Discovery", "Query available grain types by name/namespace", "[yellow]Planned[/]");
        featuresTable.AddRow("Silo Mapping", "Track which silos can host which grain types", "[yellow]Planned[/]");
        featuresTable.AddRow("Method Metadata", "Expose grain interface methods/parameters", "[yellow]Planned[/]");
        featuresTable.AddRow("Version Tracking", "Track assembly versions and compatibility", "[yellow]Planned[/]");
        featuresTable.AddRow("Cross-Silo Resolution", "Resolve types registered on other silos", "[yellow]Planned[/]");

        AnsiConsole.Write(featuresTable);
        AnsiConsole.WriteLine();

        // Show the planned API
        AnsiConsole.MarkupLine("[blue]Planned API:[/]");
        AnsiConsole.WriteLine();

        var apiPanel = new Panel(
            """
            // Query the directory
            public interface IGrainTypeDirectory
            {
                // List all registered grain types
                Task<IReadOnlyList<GrainTypeRegistration>> GetAllTypesAsync();

                // Find types by name pattern
                Task<IReadOnlyList<GrainTypeRegistration>> FindTypesAsync(string pattern);

                // Get detailed metadata for a type
                Task<GrainTypeMetadata?> GetTypeMetadataAsync(string fullTypeName);

                // Find silos that host a grain type
                Task<IReadOnlyList<SiloAddress>> GetHostingSilosAsync(string fullTypeName);

                // Check if a grain type is available
                Task<bool> IsTypeAvailableAsync(string fullTypeName);
            }

            // Registration data
            public class GrainTypeRegistration
            {
                public string FullName { get; set; }
                public string Namespace { get; set; }
                public string AssemblyName { get; set; }
                public string AssemblyHash { get; set; }  // For versioning
                public GrainTypeKind Kind { get; set; }   // Interface or Class
                public IReadOnlyList<SiloAddress> AvailableOn { get; set; }
            }
            """)
        {
            Header = new PanelHeader("IGrainTypeDirectory Interface"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(apiPanel);
        AnsiConsole.WriteLine();

        // Show what implementation looks like
        AnsiConsole.MarkupLine("[blue]Implementation Components Needed:[/]");
        var componentsTable = new Table();
        componentsTable.AddColumn("Component");
        componentsTable.AddColumn("Location");
        componentsTable.AddColumn("Purpose");

        componentsTable.AddRow("GrainTypeDirectory", "Orleans.Runtime/DynamicGrains/", "Core directory implementation");
        componentsTable.AddRow("GrainTypeRegistryGrain", "Orleans.Runtime/DynamicGrains/", "Singleton grain storing registry");
        componentsTable.AddRow("GrainTypeMetadataProvider", "Orleans.Runtime/DynamicGrains/", "Service to query/cache metadata");
        componentsTable.AddRow("IGrainTypeDirectory", "Orleans.Core.Abstractions/", "Public interface for clients");

        AnsiConsole.Write(componentsTable);
        AnsiConsole.WriteLine();

        // Demo what the test will do once implemented
        AnsiConsole.MarkupLine("[blue]Test Phases (Once Implemented):[/]");
        AnsiConsole.MarkupLine("  Phase 1: Start silo cluster");
        AnsiConsole.MarkupLine("  Phase 2: Load plugin grain assemblies");
        AnsiConsole.MarkupLine("  Phase 3: Query GTD for registered types");
        AnsiConsole.MarkupLine("  Phase 4: Get detailed metadata for IHelloGrain");
        AnsiConsole.MarkupLine("  Phase 5: Find which silos host HelloGrain");
        AnsiConsole.MarkupLine("  Phase 6: Unload assembly and verify GTD updates");
        AnsiConsole.WriteLine();

        // Option to run current implementation status check
        var runCheck = AnsiConsole.Confirm("Run a check to see current implementation status?", defaultValue: true);

        if (runCheck)
        {
            await RunImplementationStatusCheck();
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 6 Complete (GTD Not Yet Implemented)[/]");
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

        // Check what services are available
        var statusTable = new Table();
        statusTable.AddColumn("Service");
        statusTable.AddColumn("Available");
        statusTable.AddColumn("Notes");

        // Check for existing services that could be part of GTD
        var grainLoader = host.Services.GetService<IPluginGrainLoader>();
        statusTable.AddRow(
            "IPluginGrainLoader",
            grainLoader != null ? "[green]Yes[/]" : "[red]No[/]",
            grainLoader != null ? "Can load grain assemblies" : "Not registered"
        );

        var manifestProvider = host.Services.GetService<IClusterManifestProvider>();
        statusTable.AddRow(
            "IClusterManifestProvider",
            manifestProvider != null ? "[green]Yes[/]" : "[red]No[/]",
            manifestProvider != null ? $"Has {manifestProvider.Current.AllGrainManifests.Sum(m => m.Grains.Count)} grain types" : "Not registered"
        );

        // Check for GTD interface (won't exist yet)
        var gtdType = Type.GetType("Orleans.Runtime.DynamicGrains.IGrainTypeDirectory, Orleans.Runtime");
        statusTable.AddRow(
            "IGrainTypeDirectory",
            gtdType != null ? "[green]Yes[/]" : "[yellow]No[/]",
            gtdType != null ? "GTD interface exists" : "Not yet implemented"
        );

        AnsiConsole.Write(statusTable);
        AnsiConsole.WriteLine();

        // Show what manifest data we have today (this is the foundation for GTD)
        if (manifestProvider != null)
        {
            AnsiConsole.MarkupLine("[blue]Current Manifest Data (Foundation for GTD):[/]");

            var manifest = manifestProvider.Current;
            var grainTypes = manifest.AllGrainManifests
                .SelectMany(m => m.Grains)
                .Take(10)
                .ToList();

            if (grainTypes.Any())
            {
                var grainTable = new Table();
                grainTable.AddColumn("Grain Type");
                grainTable.AddColumn("Properties");

                foreach (var grain in grainTypes)
                {
                    var propCount = grain.Value.Properties.Count;
                    grainTable.AddRow(grain.Key.ToString(), $"{propCount} properties");
                }

                AnsiConsole.Write(grainTable);
                AnsiConsole.MarkupLine($"[grey]  (showing first 10 of {manifest.AllGrainManifests.Sum(m => m.Grains.Count)} grain types)[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        host.Dispose();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");
    }
}
