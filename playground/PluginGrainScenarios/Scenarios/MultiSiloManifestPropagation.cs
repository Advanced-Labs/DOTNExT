using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 3: Multi-silo cluster manifest propagation.
/// Tests:
/// - Loading grains on one silo
/// - Manifest propagation to other silos
/// - Cross-silo grain activation
/// </summary>
public static class MultiSiloManifestPropagation
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 3: Multi-Silo Manifest Propagation[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        var assemblyPath = TestGrainsFinder.FindTestGrainsAssembly();
        if (assemblyPath == null)
        {
            AnsiConsole.MarkupLine("[red]ERROR: Could not find DynamicGrainLoading.TestGrains.dll[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Found test grains:[/] {assemblyPath}");
        AnsiConsole.WriteLine();

        const int primaryPort = 11111;

        // Start 3 silos
        AnsiConsole.MarkupLine("[yellow]Starting 3-silo cluster...[/]");

        var silo1 = SiloHelper.BuildClusterSilo("Silo1", 11111, 30000, primaryPort);
        var silo2 = SiloHelper.BuildClusterSilo("Silo2", 11112, 30001, primaryPort);
        var silo3 = SiloHelper.BuildClusterSilo("Silo3", 11113, 30002, primaryPort);

        await AnsiConsole.Status()
            .StartAsync("Starting Silo1 (primary)...", async ctx =>
            {
                await silo1.StartAsync();
            });
        AnsiConsole.MarkupLine("[green]Silo1 started (primary)[/]");

        await AnsiConsole.Status()
            .StartAsync("Starting Silo2...", async ctx =>
            {
                await silo2.StartAsync();
            });
        AnsiConsole.MarkupLine("[green]Silo2 started[/]");

        await AnsiConsole.Status()
            .StartAsync("Starting Silo3...", async ctx =>
            {
                await silo3.StartAsync();
            });
        AnsiConsole.MarkupLine("[green]Silo3 started[/]");

        AnsiConsole.MarkupLine("[yellow]Waiting for cluster to stabilize (3s)...[/]");
        await Task.Delay(3000);
        AnsiConsole.MarkupLine("[green]Cluster ready[/]");
        AnsiConsole.WriteLine();

        // Load assembly on Silo1 only
        AnsiConsole.MarkupLine("[blue]Phase 1: Loading grain assembly on Silo1 only...[/]");
        var grainLoader1 = silo1.Services.GetRequiredService<IPluginGrainLoader>();
        var loadResult = await grainLoader1.LoadGrainAssemblyAsync(assemblyPath);

        if (!loadResult.Success)
        {
            AnsiConsole.MarkupLine("[red]FAILED to load assembly![/]");
            await StopAllSilos(silo1, silo2, silo3);
            return;
        }

        AnsiConsole.MarkupLine($"[green]Assembly loaded on Silo1: {loadResult.GrainTypes.Count} grain types[/]");
        AnsiConsole.WriteLine();

        // Get manifest providers
        var manifest1 = silo1.Services.GetService<IClusterManifestProvider>();
        var manifest2 = silo2.Services.GetService<IClusterManifestProvider>();
        var manifest3 = silo3.Services.GetService<IClusterManifestProvider>();

        if (manifest1 == null || manifest2 == null || manifest3 == null)
        {
            AnsiConsole.MarkupLine("[red]ERROR: Could not get manifest providers[/]");
            await StopAllSilos(silo1, silo2, silo3);
            return;
        }

        // Phase 2: Take multiple snapshots over time to study version convergence
        AnsiConsole.MarkupLine("[blue]Phase 2: Monitoring manifest versions over time...[/]");
        AnsiConsole.MarkupLine("[grey]Taking snapshots every 500ms for 5 seconds to study convergence...[/]");
        AnsiConsole.WriteLine();

        var snapshots = new List<(int Ms, string V1, string V2, string V3, int C1, int C2, int C3)>();

        for (int i = 0; i <= 10; i++)
        {
            var current1 = manifest1.Current;
            var current2 = manifest2.Current;
            var current3 = manifest3.Current;

            var v1 = current1.Version.ToString();
            var v2 = current2.Version.ToString();
            var v3 = current3.Version.ToString();

            var c1 = current1.AllGrainManifests.Sum(m => m.Grains.Count);
            var c2 = current2.AllGrainManifests.Sum(m => m.Grains.Count);
            var c3 = current3.AllGrainManifests.Sum(m => m.Grains.Count);

            snapshots.Add((i * 500, v1, v2, v3, c1, c2, c3));

            if (i < 10)
                await Task.Delay(500);
        }

        // Display snapshot history
        AnsiConsole.MarkupLine("[blue]Phase 3: Manifest Version Timeline[/]");
        var timelineTable = new Table();
        timelineTable.AddColumn("Time (ms)");
        timelineTable.AddColumn("Silo1 Version");
        timelineTable.AddColumn("Silo2 Version");
        timelineTable.AddColumn("Silo3 Version");
        timelineTable.AddColumn("Converged?");

        foreach (var snap in snapshots)
        {
            var converged = (snap.V1 == snap.V2 && snap.V2 == snap.V3) ? "[green]Yes[/]" : "[yellow]No[/]";
            timelineTable.AddRow(
                snap.Ms.ToString(),
                snap.V1,
                snap.V2,
                snap.V3,
                converged
            );
        }
        AnsiConsole.Write(timelineTable);
        AnsiConsole.WriteLine();

        // Check if versions are bouncing (changing after initial stabilization)
        var uniqueVersionSets = snapshots
            .Select(s => $"{s.V1}|{s.V2}|{s.V3}")
            .Distinct()
            .ToList();

        AnsiConsole.MarkupLine($"[blue]Unique version combinations observed:[/] {uniqueVersionSets.Count}");
        foreach (var vs in uniqueVersionSets)
        {
            AnsiConsole.MarkupLine($"  [grey]{vs.Replace("|", " / ")}[/]");
        }
        AnsiConsole.WriteLine();

        // Final state analysis
        var finalSnap = snapshots.Last();
        AnsiConsole.MarkupLine("[blue]Phase 4: Final State Analysis[/]");

        var finalTable = new Table();
        finalTable.AddColumn("Silo");
        finalTable.AddColumn("Manifest Version");
        finalTable.AddColumn("Grain Type Count");
        finalTable.AddRow("Silo1", finalSnap.V1, finalSnap.C1.ToString());
        finalTable.AddRow("Silo2", finalSnap.V2, finalSnap.C2.ToString());
        finalTable.AddRow("Silo3", finalSnap.V3, finalSnap.C3.ToString());
        AnsiConsole.Write(finalTable);
        AnsiConsole.WriteLine();

        // Conclusions
        var allConverged = finalSnap.V1 == finalSnap.V2 && finalSnap.V2 == finalSnap.V3;
        var allSameCount = finalSnap.C1 == finalSnap.C2 && finalSnap.C2 == finalSnap.C3;
        var versionsBouncing = uniqueVersionSets.Count > 2; // More than initial + final

        if (allConverged && allSameCount)
        {
            AnsiConsole.MarkupLine("[green]✓ SUCCESS: All silos converged to same version and grain count[/]");
        }
        else if (allSameCount && !allConverged)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ PARTIAL: All silos have same grain count but different versions[/]");
            AnsiConsole.MarkupLine("[grey]  This means each silo has the grain types but tracks versions independently[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]✗ FAILED: Silos have inconsistent state[/]");
        }

        if (versionsBouncing)
        {
            AnsiConsole.MarkupLine("[red]⚠ WARNING: Versions appear to be bouncing (more than 2 unique combinations)[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]✓ Versions stabilized (no bouncing detected)[/]");
        }
        AnsiConsole.WriteLine();

        // Cleanup
        AnsiConsole.MarkupLine("[yellow]Stopping cluster...[/]");
        await StopAllSilos(silo1, silo2, silo3);
        AnsiConsole.MarkupLine("[green]Cluster stopped[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 3 Complete[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }

    private static async Task StopAllSilos(params Microsoft.Extensions.Hosting.IHost[] hosts)
    {
        foreach (var host in hosts)
        {
            try
            {
                await host.StopAsync();
                host.Dispose();
            }
            catch { }
        }
    }
}
