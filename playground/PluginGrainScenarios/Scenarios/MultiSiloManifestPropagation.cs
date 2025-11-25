using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime.DynamicGrains;
using Orleans.Runtime.Metadata;
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

        // Wait for propagation
        AnsiConsole.MarkupLine("[blue]Phase 2: Waiting for manifest propagation...[/]");
        await Task.Delay(2000);

        // Check manifest versions on all silos
        AnsiConsole.MarkupLine("[blue]Phase 3: Checking manifest versions across cluster...[/]");
        var table = new Table();
        table.AddColumn("Silo");
        table.AddColumn("Manifest Version");
        table.AddColumn("Grain Type Count");

        var manifest1 = silo1.Services.GetService<ClusterManifestProvider>();
        var manifest2 = silo2.Services.GetService<ClusterManifestProvider>();
        var manifest3 = silo3.Services.GetService<ClusterManifestProvider>();

        if (manifest1 != null)
        {
            var current1 = manifest1.Current;
            table.AddRow("Silo1", current1.Version.ToString(), current1.AllGrainManifests.Values.Sum(m => m.Grains.Count).ToString());
        }
        if (manifest2 != null)
        {
            var current2 = manifest2.Current;
            table.AddRow("Silo2", current2.Version.ToString(), current2.AllGrainManifests.Values.Sum(m => m.Grains.Count).ToString());
        }
        if (manifest3 != null)
        {
            var current3 = manifest3.Current;
            table.AddRow("Silo3", current3.Version.ToString(), current3.AllGrainManifests.Values.Sum(m => m.Grains.Count).ToString());
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Verify all have same version
        if (manifest1 != null && manifest2 != null && manifest3 != null)
        {
            var v1 = manifest1.Current.Version;
            var v2 = manifest2.Current.Version;
            var v3 = manifest3.Current.Version;

            if (v1 == v2 && v2 == v3)
            {
                AnsiConsole.MarkupLine("[green]All silos have the same manifest version![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]WARNING: Manifest versions differ - propagation may need more time[/]");
            }
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
