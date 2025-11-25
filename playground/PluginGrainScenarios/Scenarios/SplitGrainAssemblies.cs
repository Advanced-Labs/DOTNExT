using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime.DynamicGrains;
using Orleans.Runtime.Metadata;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 5: Split grain assemblies (interface vs implementation).
/// Tests:
/// - Loading interface-only assembly (contracts)
/// - Loading implementation assembly
/// - Verifying both are tracked correctly
/// - Testing GTD (Grain Type Directory) awareness
/// </summary>
public static class SplitGrainAssemblies
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 5: Split Grain Assemblies[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Look for both contracts and implementation assemblies
        var testGrainsPath = TestGrainsFinder.FindTestGrainsAssembly();
        if (testGrainsPath == null)
        {
            AnsiConsole.MarkupLine("[red]ERROR: Could not find DynamicGrainLoading.TestGrains.dll[/]");
            return;
        }

        // Check for split assembly test projects
        var testGrainsDir = Path.GetDirectoryName(testGrainsPath)!;
        var baseDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(testGrainsDir)));

        // Look for Contracts and Implementation assemblies
        var contractsPath = FindSplitAssembly(baseDir, "Contracts");
        var implementationPath = FindSplitAssembly(baseDir, "Implementation");

        AnsiConsole.MarkupLine("[blue]Assembly Discovery[/]");
        var discoveryTable = new Table();
        discoveryTable.AddColumn("Assembly Type");
        discoveryTable.AddColumn("Path");
        discoveryTable.AddColumn("Status");

        discoveryTable.AddRow("Test Grains", testGrainsPath, "[green]Found[/]");
        discoveryTable.AddRow("Contracts", contractsPath ?? "(not found)", contractsPath != null ? "[green]Found[/]" : "[yellow]Not Found[/]");
        discoveryTable.AddRow("Implementation", implementationPath ?? "(not found)", implementationPath != null ? "[green]Found[/]" : "[yellow]Not Found[/]");

        AnsiConsole.Write(discoveryTable);
        AnsiConsole.WriteLine();

        if (contractsPath == null || implementationPath == null)
        {
            AnsiConsole.MarkupLine("[yellow]Split assembly test requires both Contracts and Implementation assemblies.[/]");
            AnsiConsole.MarkupLine("[yellow]Falling back to single assembly test with type analysis...[/]");
            AnsiConsole.WriteLine();

            await RunSingleAssemblyAnalysis(testGrainsPath);
            return;
        }

        await RunSplitAssemblyTest(contractsPath, implementationPath);
    }

    private static string? FindSplitAssembly(string? baseDir, string assemblyType)
    {
        if (baseDir == null) return null;

        // Common patterns for split assembly projects
        var patterns = new[]
        {
            $"**/DynamicGrainLoading.{assemblyType}/bin/**/DynamicGrainLoading.{assemblyType}.dll",
            $"**/TestGrains.{assemblyType}/bin/**/TestGrains.{assemblyType}.dll",
            $"**/{assemblyType}/bin/**/{assemblyType}.dll",
        };

        foreach (var pattern in patterns)
        {
            try
            {
                var files = Directory.GetFiles(baseDir, $"*{assemblyType}*.dll", SearchOption.AllDirectories)
                    .Where(f => f.Contains("bin") && !f.Contains("obj"))
                    .FirstOrDefault();
                if (files != null) return files;
            }
            catch { }
        }

        return null;
    }

    private static async Task RunSingleAssemblyAnalysis(string assemblyPath)
    {
        AnsiConsole.MarkupLine("[blue]Single Assembly Analysis Mode[/]");
        AnsiConsole.MarkupLine("This analysis shows how grains are split between interfaces and implementations.");
        AnsiConsole.WriteLine();

        var host = SiloHelper.BuildSingleSilo();

        await AnsiConsole.Status()
            .StartAsync("Starting silo...", async ctx =>
            {
                await host.StartAsync();
            });
        AnsiConsole.MarkupLine("[green]Silo started[/]");
        AnsiConsole.WriteLine();

        var grainLoader = host.Services.GetRequiredService<IPluginGrainLoader>();

        // Load the assembly
        AnsiConsole.MarkupLine("[blue]Loading grain assembly...[/]");
        var loadResult = await grainLoader.LoadGrainAssemblyAsync(assemblyPath);

        if (!loadResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]FAILED: {loadResult.ErrorMessage}[/]");
            await host.StopAsync();
            return;
        }

        AnsiConsole.MarkupLine($"[green]Loaded {loadResult.GrainTypes.Count} grain types[/]");
        AnsiConsole.WriteLine();

        // Analyze the grain types
        AnsiConsole.MarkupLine("[blue]Grain Type Analysis[/]");

        var table = new Table();
        table.AddColumn("Grain Implementation");
        table.AddColumn("Interfaces");
        table.AddColumn("Interface Assembly");
        table.AddColumn("Split?");

        foreach (var grainType in loadResult.GrainTypes)
        {
            var grainInterfaces = grainType.GetInterfaces()
                .Where(i => typeof(IGrain).IsAssignableFrom(i) && i != typeof(IGrain) && i != typeof(IGrainObserver))
                .ToList();

            foreach (var iface in grainInterfaces)
            {
                var implAssembly = grainType.Assembly.GetName().Name;
                var ifaceAssembly = iface.Assembly.GetName().Name;
                var isSplit = implAssembly != ifaceAssembly;

                table.AddRow(
                    grainType.Name,
                    iface.Name,
                    ifaceAssembly ?? "unknown",
                    isSplit ? "[green]Yes[/]" : "[yellow]No[/]"
                );
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Check manifest
        AnsiConsole.MarkupLine("[blue]Cluster Manifest Grain Types[/]");
        var manifestProvider = host.Services.GetService<ClusterManifestProvider>();
        if (manifestProvider != null)
        {
            var manifest = manifestProvider.Current;
            var grainCount = manifest.AllGrainManifests.Values.Sum(m => m.Grains.Count);
            AnsiConsole.MarkupLine($"  Total grain types in manifest: {grainCount}");

            // Show sample grain types
            var grains = manifest.AllGrainManifests.Values
                .SelectMany(m => m.Grains)
                .Take(10)
                .ToList();

            if (grains.Any())
            {
                AnsiConsole.MarkupLine("  Sample grain types:");
                foreach (var grain in grains)
                {
                    AnsiConsole.MarkupLine($"    - {grain.Key}");
                }
            }
        }
        AnsiConsole.WriteLine();

        // Explain split grain implications
        AnsiConsole.MarkupLine("[blue]Split Grain Architecture Notes[/]");
        AnsiConsole.MarkupLine("  When grains are split between interface and implementation assemblies:");
        AnsiConsole.MarkupLine("  - Proxy classes are generated in the INTERFACE assembly");
        AnsiConsole.MarkupLine("  - Clients only need the interface assembly to call grains");
        AnsiConsole.MarkupLine("  - Implementations can be updated independently");
        AnsiConsole.MarkupLine("  - GTD (Grain Type Directory) tracks both separately");
        AnsiConsole.WriteLine();

        // Cleanup
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        host.Dispose();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 5 Complete (Single Assembly Analysis)[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }

    private static async Task RunSplitAssemblyTest(string contractsPath, string implementationPath)
    {
        AnsiConsole.MarkupLine("[blue]Full Split Assembly Test Mode[/]");
        AnsiConsole.WriteLine();

        var host = SiloHelper.BuildSingleSilo();

        await AnsiConsole.Status()
            .StartAsync("Starting silo...", async ctx =>
            {
                await host.StartAsync();
            });
        AnsiConsole.MarkupLine("[green]Silo started[/]");
        AnsiConsole.WriteLine();

        var grainLoader = host.Services.GetRequiredService<IPluginGrainLoader>();

        // Phase 1: Load contracts (interfaces) first
        AnsiConsole.MarkupLine("[blue]Phase 1: Loading Contracts Assembly[/]");
        var contractsResult = await grainLoader.LoadGrainAssemblyAsync(contractsPath);

        if (!contractsResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]FAILED to load contracts: {contractsResult.ErrorMessage}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Contracts loaded - {contractsResult.GrainTypes.Count} types[/]");
        }
        AnsiConsole.WriteLine();

        // Check what we have after contracts
        var manifestProvider = host.Services.GetService<ClusterManifestProvider>();
        if (manifestProvider != null)
        {
            var grainCount = manifestProvider.Current.AllGrainManifests.Values.Sum(m => m.Grains.Count);
            AnsiConsole.MarkupLine($"  Grain types in manifest after contracts: {grainCount}");
        }
        AnsiConsole.WriteLine();

        // Phase 2: Load implementation
        AnsiConsole.MarkupLine("[blue]Phase 2: Loading Implementation Assembly[/]");
        var implResult = await grainLoader.LoadGrainAssemblyAsync(implementationPath);

        if (!implResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]FAILED to load implementation: {implResult.ErrorMessage}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Implementation loaded - {implResult.GrainTypes.Count} types[/]");
        }
        AnsiConsole.WriteLine();

        // Check final state
        if (manifestProvider != null)
        {
            var grainCount = manifestProvider.Current.AllGrainManifests.Values.Sum(m => m.Grains.Count);
            AnsiConsole.MarkupLine($"  Grain types in manifest after implementation: {grainCount}");
        }
        AnsiConsole.WriteLine();

        // Phase 3: Try to invoke a grain
        AnsiConsole.MarkupLine("[blue]Phase 3: Testing Grain Invocation[/]");
        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

        if (implResult.Success && implResult.GrainTypes.Any())
        {
            var grainType = implResult.GrainTypes.First();
            var interfaces = grainType.GetInterfaces()
                .Where(i => typeof(IGrain).IsAssignableFrom(i) && i != typeof(IGrain) && i != typeof(IGrainObserver))
                .ToList();

            if (interfaces.Any())
            {
                var grainInterface = interfaces.First();
                AnsiConsole.MarkupLine($"  Attempting to get grain: {grainInterface.Name}");

                try
                {
                    var getGrainMethod = typeof(IGrainFactory).GetMethod(nameof(IGrainFactory.GetGrain), new[] { typeof(string) });
                    if (getGrainMethod != null)
                    {
                        var genericMethod = getGrainMethod.MakeGenericMethod(grainInterface);
                        var grainRef = genericMethod.Invoke(grainFactory, new object[] { "split-test-grain" });
                        AnsiConsole.MarkupLine($"[green]  Successfully got grain reference: {grainRef?.GetType().Name}[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]  Could not get grain: {ex.Message}[/]");
                }
            }
        }
        AnsiConsole.WriteLine();

        // Summary
        AnsiConsole.MarkupLine("[blue]Split Assembly Test Summary[/]");
        var summaryTable = new Table();
        summaryTable.AddColumn("Assembly");
        summaryTable.AddColumn("Load Status");
        summaryTable.AddColumn("Types");

        summaryTable.AddRow(
            "Contracts",
            contractsResult.Success ? "[green]OK[/]" : "[red]Failed[/]",
            contractsResult.GrainTypes.Count.ToString()
        );
        summaryTable.AddRow(
            "Implementation",
            implResult.Success ? "[green]OK[/]" : "[red]Failed[/]",
            implResult.GrainTypes.Count.ToString()
        );

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        // Cleanup
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        host.Dispose();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 5 Complete (Split Assembly Test)[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }
}
