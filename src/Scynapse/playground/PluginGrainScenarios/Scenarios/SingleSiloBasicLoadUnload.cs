using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime.DynamicGrains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 1: Basic plugin grain loading and unloading on a single silo.
/// Tests:
/// - Assembly loading via MDCP
/// - Manifest updates
/// - Grain activation
/// - Assembly unloading
/// </summary>
public static class SingleSiloBasicLoadUnload
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 1: Single Silo - Basic Load/Unload[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Find the test grains assembly
        var assemblyPath = TestGrainsFinder.FindTestGrainsAssembly();
        if (assemblyPath == null)
        {
            AnsiConsole.MarkupLine("[red]ERROR: Could not find DynamicGrainLoading.TestGrains.dll[/]");
            AnsiConsole.MarkupLine("[yellow]Please build the TestGrains project first:[/]");
            AnsiConsole.MarkupLine("  cd playground/DynamicGrainLoading.TestGrains");
            AnsiConsole.MarkupLine("  dotnet build");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Found test grains:[/] {assemblyPath}");
        AnsiConsole.WriteLine();

        // Start silo
        AnsiConsole.MarkupLine("[yellow]Starting Orleans silo...[/]");
        using var host = SiloHelper.BuildSingleSilo();
        await host.StartAsync();
        AnsiConsole.MarkupLine("[green]Silo started successfully[/]");
        AnsiConsole.WriteLine();

        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
        var grainLoader = host.Services.GetRequiredService<IPluginGrainLoader>();

        // Phase 1: Load assembly
        AnsiConsole.MarkupLine("[blue]Phase 1: Loading grain assembly...[/]");
        var loadResult = await AnsiConsole.Status()
            .StartAsync("Loading assembly via MDCP...", async ctx =>
            {
                return await grainLoader.LoadGrainAssemblyAsync(assemblyPath);
            });

        if (!loadResult.Success)
        {
            AnsiConsole.MarkupLine("[red]FAILED to load assembly![/]");
            foreach (var error in loadResult.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]{error}[/]");
            }
            await host.StopAsync();
            return;
        }

        AnsiConsole.MarkupLine($"[green]Assembly loaded successfully in {loadResult.LoadDuration.TotalMilliseconds:F0}ms[/]");
        AnsiConsole.MarkupLine($"  Grain types: {loadResult.GrainTypes.Count}");
        foreach (var grainType in loadResult.GrainTypes)
        {
            AnsiConsole.MarkupLine($"    - {grainType}");
        }
        AnsiConsole.WriteLine();

        // Phase 2: Activate and use grains
        AnsiConsole.MarkupLine("[blue]Phase 2: Activating and using grains...[/]");

        // Try to call a grain - we need to use dynamic invocation since we don't have compile-time reference
        try
        {
            // Get grain type by name from the loaded assembly
            var assembly = loadResult.Assembly;
            var helloGrainInterface = assembly?.GetType("DynamicGrainLoading.TestGrains.IHelloGrain");

            if (helloGrainInterface != null)
            {
                // Use reflection to get grain reference
                var getGrainMethod = typeof(IGrainFactory).GetMethod("GetGrain", new[] { typeof(string), typeof(string) });
                var genericMethod = getGrainMethod?.MakeGenericMethod(helloGrainInterface);
                var grain = genericMethod?.Invoke(grainFactory, new object[] { "test-user", null! });

                if (grain != null)
                {
                    // Call the SayHello method
                    var sayHelloMethod = helloGrainInterface.GetMethod("SayHello");
                    if (sayHelloMethod != null)
                    {
                        var result = await (Task<string>)sayHelloMethod.Invoke(grain, new object[] { "World" })!;
                        AnsiConsole.MarkupLine($"[green]HelloGrain response:[/] {result}");
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Could not find IHelloGrain interface - grain activation test skipped[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Grain activation test failed: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[grey]This may be expected if the grain interface assembly isn't properly loaded[/]");
        }
        AnsiConsole.WriteLine();

        // Phase 3: Unload assembly
        AnsiConsole.MarkupLine("[blue]Phase 3: Unloading grain assembly...[/]");

        var grainUnloader = host.Services.GetService<IPluginGrainUnloader>();
        if (grainUnloader != null)
        {
            var unloadResult = await AnsiConsole.Status()
                .StartAsync("Unloading assembly...", async ctx =>
                {
                    return await grainUnloader.UnloadGrainAssemblyAsync(assemblyPath);
                });

            if (unloadResult.Success)
            {
                AnsiConsole.MarkupLine($"[green]Assembly unloaded successfully in {unloadResult.UnloadDuration.TotalMilliseconds:F0}ms[/]");
                AnsiConsole.MarkupLine($"  Grains deactivated: {unloadResult.ActiveGrainsDeactivated}");
                AnsiConsole.MarkupLine($"  Memory reclaimed: {unloadResult.MemoryReclaimed}");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Unload had issues:[/]");
                foreach (var error in unloadResult.Errors)
                {
                    AnsiConsole.MarkupLine($"  [yellow]{error}[/]");
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]IPluginGrainUnloader not available - unload test skipped[/]");
        }
        AnsiConsole.WriteLine();

        // Cleanup
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 1 Complete[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }
}
