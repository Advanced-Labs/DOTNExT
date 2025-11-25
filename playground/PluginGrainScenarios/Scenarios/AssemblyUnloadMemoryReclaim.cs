using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Runtime.DynamicGrains;
using Spectre.Console;
using System.Runtime;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 4: Assembly unload and memory reclamation.
/// Tests:
/// - Loading an assembly
/// - Using grains from it
/// - Unloading the assembly
/// - Verifying memory is reclaimed (via GC)
/// </summary>
public static class AssemblyUnloadMemoryReclaim
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 4: Assembly Unload & Memory Reclaim[/]");
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

        // Build a single silo
        var host = SiloHelper.BuildSingleSilo();

        await AnsiConsole.Status()
            .StartAsync("Starting silo...", async ctx =>
            {
                await host.StartAsync();
            });
        AnsiConsole.MarkupLine("[green]Silo started[/]");
        AnsiConsole.WriteLine();

        var grainLoader = host.Services.GetRequiredService<IPluginGrainLoader>();
        var grainUnloader = host.Services.GetRequiredService<IPluginGrainUnloader>();

        // Capture baseline memory
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        var baselineMemory = GC.GetTotalMemory(true);
        AnsiConsole.MarkupLine($"[yellow]Baseline memory:[/] {FormatBytes(baselineMemory)}");
        AnsiConsole.WriteLine();

        // Phase 1: Load assembly
        AnsiConsole.MarkupLine("[blue]Phase 1: Loading grain assembly...[/]");
        var loadResult = await grainLoader.LoadGrainAssemblyAsync(assemblyPath);

        if (!loadResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]FAILED to load assembly: {string.Join(", ", loadResult.Errors)}[/]");
            await host.StopAsync();
            return;
        }

        AnsiConsole.MarkupLine($"[green]Loaded {loadResult.GrainTypes.Count} grain types[/]");

        // Measure memory after load
        var afterLoadMemory = GC.GetTotalMemory(true);
        AnsiConsole.MarkupLine($"[yellow]Memory after load:[/] {FormatBytes(afterLoadMemory)} (+{FormatBytes(afterLoadMemory - baselineMemory)})");
        AnsiConsole.WriteLine();

        // Phase 2: Create grain references and use them
        AnsiConsole.MarkupLine("[blue]Phase 2: Creating and using grain instances...[/]");
        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

        // Get actual types from the loaded assembly
        var grainClasses = loadResult.Assembly?.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IGrain).IsAssignableFrom(t))
            .ToList() ?? new List<Type>();

        // Create multiple grain references to ensure we're allocating
        var grainRefs = new List<object>();
        foreach (var grainType in grainClasses.Take(3))
        {
            AnsiConsole.MarkupLine($"  Creating reference for: {grainType.Name}");

            // Find the interface type
            var interfaces = grainType.GetInterfaces()
                .Where(i => typeof(IGrain).IsAssignableFrom(i) && i != typeof(IGrain) && i != typeof(IGrainObserver))
                .ToList();

            if (interfaces.Any())
            {
                var grainInterface = interfaces.First();
                try
                {
                    // Use reflection to call GetGrain<T>(string key)
                    var getGrainMethod = typeof(IGrainFactory).GetMethod(nameof(IGrainFactory.GetGrain), new[] { typeof(string) });
                    if (getGrainMethod != null)
                    {
                        var genericMethod = getGrainMethod.MakeGenericMethod(grainInterface);
                        var grainRef = genericMethod.Invoke(grainFactory, new object[] { $"test-{grainType.Name}" });
                        grainRefs.Add(grainRef!);
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"    [yellow]Could not create reference: {ex.Message}[/]");
                }
            }
        }

        AnsiConsole.MarkupLine($"[green]Created {grainRefs.Count} grain references[/]");

        var afterUseMemory = GC.GetTotalMemory(true);
        AnsiConsole.MarkupLine($"[yellow]Memory after use:[/] {FormatBytes(afterUseMemory)} (+{FormatBytes(afterUseMemory - baselineMemory)})");
        AnsiConsole.WriteLine();

        // Phase 3: Unload assembly
        AnsiConsole.MarkupLine("[blue]Phase 3: Unloading grain assembly...[/]");

        // Clear references
        grainRefs.Clear();

        var unloadResult = await grainUnloader.UnloadGrainAssemblyAsync(assemblyPath);
        if (unloadResult.Success)
        {
            AnsiConsole.MarkupLine("[green]Assembly unloaded successfully[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Unload failed: {string.Join(", ", unloadResult.Errors)}[/]");
        }
        AnsiConsole.WriteLine();

        // Phase 4: Force GC and measure memory
        AnsiConsole.MarkupLine("[blue]Phase 4: Forcing garbage collection...[/]");

        // Multiple GC passes to ensure collectible ALC is reclaimed
        for (int i = 0; i < 5; i++)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            await Task.Delay(100);
        }
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);

        var afterGCMemory = GC.GetTotalMemory(true);
        AnsiConsole.MarkupLine($"[yellow]Memory after GC:[/] {FormatBytes(afterGCMemory)}");
        AnsiConsole.WriteLine();

        // Summary
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Memory Analysis Summary[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");

        var table = new Table();
        table.AddColumn("Phase");
        table.AddColumn("Memory");
        table.AddColumn("Delta from Baseline");

        table.AddRow("Baseline", FormatBytes(baselineMemory), "-");
        table.AddRow("After Load", FormatBytes(afterLoadMemory), $"+{FormatBytes(afterLoadMemory - baselineMemory)}");
        table.AddRow("After Use", FormatBytes(afterUseMemory), $"+{FormatBytes(afterUseMemory - baselineMemory)}");
        table.AddRow("After Unload+GC", FormatBytes(afterGCMemory), FormatDelta(afterGCMemory - baselineMemory));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var memoryRecovered = afterLoadMemory - afterGCMemory;
        var loadedMemory = afterLoadMemory - baselineMemory;

        if (memoryRecovered > 0 && loadedMemory > 0)
        {
            var recoveryPercent = (memoryRecovered * 100.0) / loadedMemory;
            AnsiConsole.MarkupLine($"[green]Memory recovered: {FormatBytes(memoryRecovered)} ({recoveryPercent:F1}% of loaded)[/]");

            if (recoveryPercent > 50)
            {
                AnsiConsole.MarkupLine("[green]MDCP unloading appears to be working - significant memory recovered![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Some memory recovered, but less than expected. This could be normal variance.[/]");
            }
        }
        else if (afterGCMemory <= baselineMemory * 1.1) // Within 10% of baseline
        {
            AnsiConsole.MarkupLine("[green]Memory returned to near baseline - unloading successful![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Memory not fully reclaimed. This may indicate:");
            AnsiConsole.MarkupLine("  - Cached references still holding assembly");
            AnsiConsole.MarkupLine("  - GC not yet collected all objects");
            AnsiConsole.MarkupLine("  - Normal runtime memory growth[/]");
        }
        AnsiConsole.WriteLine();

        // Cleanup
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        host.Dispose();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 4 Complete[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private static string FormatDelta(long delta)
    {
        var sign = delta >= 0 ? "+" : "";
        return $"{sign}{FormatBytes(Math.Abs(delta))}";
    }
}
