using Microsoft.Extensions.DependencyInjection;
using Scynapse;
using Scynapse.Runtime.DynamicGrains;
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

        // Summary of first cycle
        var memoryRecovered = afterLoadMemory - afterGCMemory;
        var loadedMemory = afterLoadMemory - baselineMemory;
        var retainedMemory = afterGCMemory - baselineMemory;

        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Memory Analysis Summary (First Load/Unload Cycle)[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");

        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddColumn("Explanation");

        table.AddRow("Baseline", FormatBytes(baselineMemory), "Memory before loading any plugin");
        table.AddRow("After Load", FormatBytes(afterLoadMemory), "Memory with plugin loaded");
        table.AddRow("Memory Added by Load", FormatBytes(loadedMemory), "What loading the plugin added");
        table.AddRow("After Unload+GC", FormatBytes(afterGCMemory), "Memory after unload and GC");
        table.AddRow("Memory Recovered", FormatBytes(memoryRecovered), "Freed by unloading");
        table.AddRow("Memory Still Retained", FormatBytes(retainedMemory), "Still above baseline");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (loadedMemory > 0)
        {
            var recoveryPercent = (memoryRecovered * 100.0) / loadedMemory;
            var retainedPercent = (retainedMemory * 100.0) / loadedMemory;

            AnsiConsole.MarkupLine($"[yellow]Recovery analysis:[/]");
            AnsiConsole.MarkupLine($"  Loaded: {FormatBytes(loadedMemory)} (100%)");
            AnsiConsole.MarkupLine($"  Recovered: {FormatBytes(memoryRecovered)} ({recoveryPercent:F1}%)");
            AnsiConsole.MarkupLine($"  Retained: {FormatBytes(retainedMemory)} ({retainedPercent:F1}%)");
            AnsiConsole.WriteLine();

            if (retainedPercent > 10)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ Significant memory retained after unload. Possible causes:[/]");
                AnsiConsole.MarkupLine("  - Scynapse caches (serializers, type metadata, etc.)");
                AnsiConsole.MarkupLine("  - Weak references not yet collected");
                AnsiConsole.MarkupLine("  - JIT-compiled code retained in memory");
                AnsiConsole.MarkupLine("  - Actual memory leak");
            }
        }
        AnsiConsole.WriteLine();

        // Phase 5: Multiple load/unload cycles to detect leaks
        AnsiConsole.MarkupLine("[blue]Phase 5: Testing for memory leaks (3 additional load/unload cycles)...[/]");
        AnsiConsole.MarkupLine("[grey]If memory grows with each cycle, there's likely a leak.[/]");
        AnsiConsole.WriteLine();

        var cycleMemory = new List<(int Cycle, long AfterLoad, long AfterUnload)>();
        cycleMemory.Add((1, afterLoadMemory, afterGCMemory)); // First cycle already done

        for (int cycle = 2; cycle <= 4; cycle++)
        {
            // Load
            var cycleLoadResult = await grainLoader.LoadGrainAssemblyAsync(assemblyPath);
            if (!cycleLoadResult.Success)
            {
                AnsiConsole.MarkupLine($"[red]Cycle {cycle}: Load failed[/]");
                break;
            }
            var cycleAfterLoad = GC.GetTotalMemory(true);

            // Unload
            var cycleUnloadResult = await grainUnloader.UnloadGrainAssemblyAsync(assemblyPath);

            // GC
            for (int i = 0; i < 5; i++)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                await Task.Delay(100);
            }
            var cycleAfterUnload = GC.GetTotalMemory(true);

            cycleMemory.Add((cycle, cycleAfterLoad, cycleAfterUnload));
            AnsiConsole.MarkupLine($"  Cycle {cycle}: Load={FormatBytes(cycleAfterLoad)}, After Unload+GC={FormatBytes(cycleAfterUnload)}");
        }
        AnsiConsole.WriteLine();

        // Analyze leak pattern
        var cycleTable = new Table();
        cycleTable.AddColumn("Cycle");
        cycleTable.AddColumn("After Load");
        cycleTable.AddColumn("After Unload+GC");
        cycleTable.AddColumn("Delta from Cycle 1");

        foreach (var cm in cycleMemory)
        {
            var delta = cm.AfterUnload - cycleMemory[0].AfterUnload;
            cycleTable.AddRow(
                cm.Cycle.ToString(),
                FormatBytes(cm.AfterLoad),
                FormatBytes(cm.AfterUnload),
                FormatDelta(delta)
            );
        }
        AnsiConsole.Write(cycleTable);
        AnsiConsole.WriteLine();

        // Check for growing memory (leak indicator)
        var firstUnloadMem = cycleMemory[0].AfterUnload;
        var lastUnloadMem = cycleMemory.Last().AfterUnload;
        var growth = lastUnloadMem - firstUnloadMem;
        var growthPerCycle = growth / (cycleMemory.Count - 1);

        if (growth > loadedMemory * 0.5) // Growing by more than 50% of loaded size
        {
            AnsiConsole.MarkupLine($"[red]⚠ POTENTIAL LEAK: Memory grew by {FormatBytes(growth)} over {cycleMemory.Count - 1} cycles[/]");
            AnsiConsole.MarkupLine($"[red]  Average growth per cycle: {FormatBytes(growthPerCycle)}[/]");
        }
        else if (growth > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Minor memory growth: {FormatBytes(growth)} over {cycleMemory.Count - 1} cycles[/]");
            AnsiConsole.MarkupLine($"[grey]  This may be normal runtime behavior or small caches[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓ No memory leak detected - memory stable across cycles[/]");
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
