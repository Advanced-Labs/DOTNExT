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
/// STATUS: NOT YET IMPLEMENTED - This scenario outlines the comprehensive design.
///
/// Features to test:
/// - IGrainTypeDirectoryGrain singleton grain
/// - Register grain packages with full metadata
/// - Query available grain types by name/namespace
/// - Track which silos can host which grain types
/// - GrainTypeMeta with package reference
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
        AnsiConsole.MarkupLine("  It is implemented as a [bold]singleton grain[/] for cluster-wide consistency.");
        AnsiConsole.WriteLine();

        // Show features
        ShowPlannedFeatures();

        // Show the API design
        ShowApiDesign();

        // Show package registration
        ShowPackageRegistration();

        // Show implementation components
        ShowImplementationComponents();

        // Show test phases
        ShowTestPhases();

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

    private static void ShowPlannedFeatures()
    {
        AnsiConsole.MarkupLine("[blue]Planned Features to Test:[/]");
        var featuresTable = new Table();
        featuresTable.AddColumn("Feature");
        featuresTable.AddColumn("Description");
        featuresTable.AddColumn("Status");

        featuresTable.AddRow("Package Registration", "Register GrainPackage with full metadata", "[yellow]Planned[/]");
        featuresTable.AddRow("Type Discovery", "Query available grain types by name/namespace", "[yellow]Planned[/]");
        featuresTable.AddRow("Silo Tracking", "Track which silos have loaded which packages", "[yellow]Planned[/]");
        featuresTable.AddRow("Method Metadata", "GrainInterfaceMeta with methods/parameters", "[yellow]Planned[/]");
        featuresTable.AddRow("Version Tracking", "Track package versions and compatibility", "[yellow]Planned[/]");
        featuresTable.AddRow("Cross-Silo Resolution", "Resolve types registered on other silos", "[yellow]Planned[/]");
        featuresTable.AddRow("Package References", "GrainTypeMeta has reference back to package", "[yellow]Planned[/]");

        AnsiConsole.Write(featuresTable);
        AnsiConsole.WriteLine();
    }

    private static void ShowApiDesign()
    {
        AnsiConsole.MarkupLine("[blue]GTD Grain API:[/]");
        AnsiConsole.WriteLine();

        var apiPanel = new Panel(
            """
            // The Grain Type Directory - a cluster-wide singleton grain
            public interface IGrainTypeDirectoryGrain : IGrainWithStringKey
            {
                // =============================================
                // Package Registration
                // =============================================

                Task RegisterPackageAsync(GrainPackage package);
                Task UnregisterPackageAsync(string packageId, string version);

                // =============================================
                // Package Queries
                // =============================================

                Task<ImmutableList<GrainPackageInfo>> GetPackagesAsync();
                Task<GrainPackage?> GetPackageAsync(string packageId, string? version = null);

                // =============================================
                // Grain Type Queries
                // =============================================

                Task<ImmutableList<GrainTypeMeta>> GetAllGrainTypesAsync();
                Task<ImmutableList<GrainTypeMeta>> FindGrainTypesAsync(
                    string? namespaceFilter = null,
                    string? namePattern = null);
                Task<GrainTypeMeta?> GetGrainTypeAsync(string fullTypeName);

                // =============================================
                // Silo Tracking
                // =============================================

                Task ReportPackageLoadedAsync(SiloAddress silo, string packageId, string version);
                Task ReportPackageUnloadedAsync(SiloAddress silo, string packageId, string version);
                Task<ImmutableList<SiloAddress>> GetHostingSilosAsync(string grainTypeName);
            }
            """)
        {
            Header = new PanelHeader("IGrainTypeDirectoryGrain Interface"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(apiPanel);
        AnsiConsole.WriteLine();

        // Package info type
        var infoPanel = new Panel(
            """
            // Summary info about a package (without full assembly content)
            [GenerateSerializer, Immutable]
            public sealed class GrainPackageInfo
            {
                public string PackageId { get; init; }
                public string Version { get; init; }
                public string ContentHash { get; init; }
                public int GrainTypeCount { get; init; }
                public GrainPackageContent ContentType { get; init; }
                public ImmutableList<SiloAddress> LoadedOnSilos { get; init; }
            }
            """)
        {
            Header = new PanelHeader("GrainPackageInfo Type"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();
    }

    private static void ShowPackageRegistration()
    {
        AnsiConsole.MarkupLine("[blue]Package Registration Flow:[/]");
        AnsiConsole.WriteLine();

        var flowPanel = new Panel(
            """
            // When a silo loads a plugin grain assembly:
            1. IPluginGrainLoader.LoadAsync(assemblyPath) is called
            2. MDCP loads assembly into isolated AssemblyLoadContext
            3. Grain types are discovered and added to local manifest
            4. GrainPackage is created from loaded assembly metadata
            5. GTD.RegisterPackageAsync(package) is called
            6. GTD.ReportPackageLoadedAsync(localSilo, packageId, version)
            7. Other silos can now query GTD to discover the new types

            // When a silo unloads a plugin:
            1. IPluginGrainUnloader.UnloadAsync(assemblyName) is called
            2. GTD.ReportPackageUnloadedAsync(localSilo, packageId, version)
            3. If no silos have the package loaded, GTD updates availability
            4. AssemblyLoadContext is unloaded
            """)
        {
            Header = new PanelHeader("Registration Flow"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(flowPanel);
        AnsiConsole.WriteLine();
    }

    private static void ShowImplementationComponents()
    {
        AnsiConsole.MarkupLine("[blue]Implementation Components Needed:[/]");
        var componentsTable = new Table();
        componentsTable.AddColumn("Component");
        componentsTable.AddColumn("Location");
        componentsTable.AddColumn("Purpose");

        componentsTable.AddRow("IGrainTypeDirectoryGrain", "Orleans.Core.Abstractions/DynamicGrains/", "Public grain interface");
        componentsTable.AddRow("GrainTypeDirectoryGrain", "Orleans.Runtime/DynamicGrains/", "Singleton grain implementation");
        componentsTable.AddRow("GrainPackage", "Orleans.Core.Abstractions/Metadata/", "Package definition type");
        componentsTable.AddRow("GrainPackageInfo", "Orleans.Core.Abstractions/Metadata/", "Package summary type");
        componentsTable.AddRow("GrainTypeMeta", "Orleans.Core.Abstractions/Metadata/", "Type metadata with package ref");
        componentsTable.AddRow("GrainInterfaceMeta", "Orleans.Core.Abstractions/Metadata/", "Interface method metadata");
        componentsTable.AddRow("GrainMethodMeta", "Orleans.Core.Abstractions/Metadata/", "Method parameter metadata");

        AnsiConsole.Write(componentsTable);
        AnsiConsole.WriteLine();

        // Show integration points
        AnsiConsole.MarkupLine("[blue]Integration Points:[/]");
        var integrationTable = new Table();
        integrationTable.AddColumn("Existing Component");
        integrationTable.AddColumn("Integration");

        integrationTable.AddRow("IPluginGrainLoader", "Calls GTD.RegisterPackageAsync after loading");
        integrationTable.AddRow("IPluginGrainUnloader", "Calls GTD.ReportPackageUnloadedAsync before unloading");
        integrationTable.AddRow("IClusterManifestProvider", "GTD uses this for initial type discovery");
        integrationTable.AddRow("IMembershipService", "GTD subscribes to detect silo failures");

        AnsiConsole.Write(integrationTable);
        AnsiConsole.WriteLine();
    }

    private static void ShowTestPhases()
    {
        AnsiConsole.MarkupLine("[blue]Test Phases (Once Implemented):[/]");
        AnsiConsole.MarkupLine("  Phase 1: Start silo cluster (2 silos)");
        AnsiConsole.MarkupLine("  Phase 2: Load plugin grain assembly on silo 1");
        AnsiConsole.MarkupLine("  Phase 3: Query GTD from silo 2 for registered types");
        AnsiConsole.MarkupLine("  Phase 4: Verify GrainTypeMeta includes SourcePackage reference");
        AnsiConsole.MarkupLine("  Phase 5: Query GetHostingSilosAsync for HelloGrain");
        AnsiConsole.MarkupLine("  Phase 6: Load same assembly on silo 2");
        AnsiConsole.MarkupLine("  Phase 7: Verify HostingSilos now shows both silos");
        AnsiConsole.MarkupLine("  Phase 8: Unload assembly from silo 1, verify GTD updates");
        AnsiConsole.MarkupLine("  Phase 9: Shutdown silo 2, verify GTD detects via membership");
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
            grainLoader != null ? "[green]Can load grain assemblies[/]" : "Not registered"
        );

        var manifestProvider = host.Services.GetService<IClusterManifestProvider>();
        statusTable.AddRow(
            "IClusterManifestProvider",
            manifestProvider != null ? "[green]Yes[/]" : "[red]No[/]",
            manifestProvider != null ? $"[green]Has {manifestProvider.Current.AllGrainManifests.Sum(m => m.Grains.Count)} grain types[/]" : "Not registered"
        );

        // Check for GTD types (won't exist yet)
        var gtdType = Type.GetType("Orleans.Runtime.DynamicGrains.IGrainTypeDirectoryGrain, Orleans.Runtime");
        statusTable.AddRow(
            "IGrainTypeDirectoryGrain",
            gtdType != null ? "[green]Yes[/]" : "[yellow]No[/]",
            gtdType != null ? "[green]GTD grain interface exists[/]" : "[grey]Not yet implemented[/]"
        );

        var packageType = Type.GetType("Orleans.Metadata.GrainPackage, Orleans.Core.Abstractions");
        statusTable.AddRow(
            "GrainPackage",
            packageType != null ? "[green]Yes[/]" : "[yellow]No[/]",
            packageType != null ? "[green]Package type exists[/]" : "[grey]Not yet implemented[/]"
        );

        var metaType = Type.GetType("Orleans.Metadata.GrainTypeMeta, Orleans.Core.Abstractions");
        statusTable.AddRow(
            "GrainTypeMeta",
            metaType != null ? "[green]Yes[/]" : "[yellow]No[/]",
            metaType != null ? "[green]Type metadata exists[/]" : "[grey]Not yet implemented[/]"
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

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]How GTD Extends This:[/]");
            AnsiConsole.MarkupLine("  • Current manifest is local - GTD is cluster-wide");
            AnsiConsole.MarkupLine("  • Current manifest has GrainType - GTD adds GrainTypeMeta with more detail");
            AnsiConsole.MarkupLine("  • Current manifest doesn't track silos - GTD tracks HostingSilos");
            AnsiConsole.MarkupLine("  • Current manifest has no packaging - GTD has GrainPackage");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        host.Dispose();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");
    }
}
