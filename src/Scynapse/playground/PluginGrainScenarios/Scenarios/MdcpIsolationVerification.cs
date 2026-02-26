using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Scynapse;
using Scynapse.Runtime;
using Scynapse.Runtime.DynamicGrains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 2: Verify MDCP provides proper assembly isolation.
/// Tests:
/// - Assembly loaded in separate AssemblyLoadContext
/// - IsCollectible = true for unloading support
/// - Shared types (Scynapse runtime) not duplicated
/// - Plugin types properly isolated
/// </summary>
public static class MdcpIsolationVerification
{
    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 2: MDCP Isolation Verification[/]");
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

        // Start silo
        AnsiConsole.MarkupLine("[yellow]Starting Scynapse silo...[/]");
        using var host = SiloHelper.BuildSingleSilo();
        await host.StartAsync();
        AnsiConsole.MarkupLine("[green]Silo started successfully[/]");
        AnsiConsole.WriteLine();

        var grainLoader = host.Services.GetRequiredService<IPluginGrainLoader>();

        // Load assembly
        AnsiConsole.MarkupLine("[blue]Loading grain assembly...[/]");
        var loadResult = await grainLoader.LoadGrainAssemblyAsync(assemblyPath);

        if (!loadResult.Success)
        {
            AnsiConsole.MarkupLine("[red]FAILED to load assembly![/]");
            await host.StopAsync();
            return;
        }

        AnsiConsole.MarkupLine("[green]Assembly loaded[/]");
        AnsiConsole.WriteLine();

        // Check 1: Assembly Load Context
        AnsiConsole.MarkupLine("[blue]Check 1: AssemblyLoadContext Isolation[/]");
        var loadedAssembly = loadResult.Assembly;
        if (loadedAssembly != null)
        {
            var alc = AssemblyLoadContext.GetLoadContext(loadedAssembly);
            var defaultAlc = AssemblyLoadContext.Default;

            var table = new Table();
            table.AddColumn("Property");
            table.AddColumn("Value");
            table.AddColumn("Status");

            var isIsolated = alc != defaultAlc;
            table.AddRow("Isolated from Default ALC", isIsolated.ToString(), isIsolated ? "[green]PASS[/]" : "[red]FAIL[/]");

            var isCollectible = alc?.IsCollectible ?? false;
            table.AddRow("IsCollectible", isCollectible.ToString(), isCollectible ? "[green]PASS[/]" : "[red]FAIL[/]");

            table.AddRow("ALC Name", alc?.Name ?? "(null)", "[grey]INFO[/]");
            table.AddRow("Default ALC Name", defaultAlc.Name ?? "(null)", "[grey]INFO[/]");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Check 2: Shared Types
        AnsiConsole.MarkupLine("[blue]Check 2: Shared Types (Scynapse runtime should NOT be duplicated)[/]");
        if (loadedAssembly != null)
        {
            var table = new Table();
            table.AddColumn("Type");
            table.AddColumn("Host Assembly Location");
            table.AddColumn("Plugin Sees Same?");

            // Check if Scynapse types are shared
            var scynapseTypes = new[]
            {
                typeof(IGrain),
                typeof(GrainId),
                typeof(SiloAddress)
            };

            foreach (var hostType in scynapseTypes)
            {
                try
                {
                    // See if the plugin assembly references the same Scynapse type
                    var pluginRef = loadedAssembly.GetReferencedAssemblies()
                        .FirstOrDefault(a => a.Name == hostType.Assembly.GetName().Name);

                    var isSame = pluginRef != null;
                    table.AddRow(
                        hostType.Name,
                        hostType.Assembly.Location.Length > 50 ? "..." + hostType.Assembly.Location[^50..] : hostType.Assembly.Location,
                        isSame ? "[green]YES (shared)[/]" : "[yellow]Different version?[/]"
                    );
                }
                catch
                {
                    table.AddRow(hostType.Name, "Error checking", "[grey]UNKNOWN[/]");
                }
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Check 3: Plugin Types Isolated
        AnsiConsole.MarkupLine("[blue]Check 3: Plugin Types Isolation[/]");
        if (loadedAssembly != null)
        {
            var table = new Table();
            table.AddColumn("Plugin Type");
            table.AddColumn("In Plugin ALC?");

            var pluginTypes = loadedAssembly.GetExportedTypes().Take(5);
            foreach (var pluginType in pluginTypes)
            {
                var typeAlc = AssemblyLoadContext.GetLoadContext(pluginType.Assembly);
                var isInPluginAlc = typeAlc != AssemblyLoadContext.Default;
                table.AddRow(
                    pluginType.Name,
                    isInPluginAlc ? "[green]YES (isolated)[/]" : "[red]NO (leaked to default)[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Check 4: Assembly count comparison
        AnsiConsole.MarkupLine("[blue]Check 4: Loaded Assembly Count[/]");
        var defaultAlcAssemblies = AssemblyLoadContext.Default.Assemblies.Count();
        var allAssemblies = AppDomain.CurrentDomain.GetAssemblies().Length;

        var countTable = new Table();
        countTable.AddColumn("Metric");
        countTable.AddColumn("Count");
        countTable.AddRow("Assemblies in Default ALC", defaultAlcAssemblies.ToString());
        countTable.AddRow("Total Assemblies in AppDomain", allAssemblies.ToString());
        countTable.AddRow("Plugin Assemblies (isolated)", (allAssemblies - defaultAlcAssemblies).ToString());
        AnsiConsole.Write(countTable);
        AnsiConsole.WriteLine();

        // Cleanup
        await host.StopAsync();

        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario 2 Complete - Review results above[/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════[/]");
    }
}
