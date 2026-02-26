using Microsoft.Extensions.DependencyInjection;
using Scynapse;
using Scynapse.Runtime;
using Scynapse.Runtime.DynamicGrains;
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

        // Search from AppContext.BaseDirectory upward to find Scynapse root
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Scynapse.slnx")))
        {
            currentDir = currentDir.Parent;
        }

        if (currentDir != null)
        {
            // Look for DynamicGrainLoading.Contracts or DynamicGrainLoading.Implementation
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

        // Fallback: search in the provided baseDir
        try
        {
            var files = Directory.GetFiles(baseDir, $"DynamicGrainLoading.{assemblyType}.dll", SearchOption.AllDirectories)
                .Where(f => f.Contains("bin") && !f.Contains("obj"))
                .FirstOrDefault();
            if (files != null) return files;
        }
        catch { }

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
            AnsiConsole.MarkupLine($"[red]FAILED: {string.Join(", ", loadResult.Errors)}[/]");
            await host.StopAsync();
            return;
        }

        AnsiConsole.MarkupLine($"[green]Loaded {loadResult.GrainTypes.Count} grain types[/]");
        AnsiConsole.WriteLine();

        // Analyze the grain types - get actual System.Type from the loaded assembly
        AnsiConsole.MarkupLine("[blue]Grain Type Analysis[/]");

        var table = new Table();
        table.AddColumn("Grain Implementation");
        table.AddColumn("Interfaces");
        table.AddColumn("Interface Assembly");
        table.AddColumn("Split?");

        var grainClasses = loadResult.Assembly?.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IGrain).IsAssignableFrom(t))
            .ToList() ?? new List<Type>();

        foreach (var grainType in grainClasses)
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
        var manifestProvider = host.Services.GetService<IClusterManifestProvider>();
        if (manifestProvider != null)
        {
            var manifest = manifestProvider.Current;
            var grainCount = manifest.AllGrainManifests.Sum(m => m.Grains.Count);
            AnsiConsole.MarkupLine($"  Total grain types in manifest: {grainCount}");

            // Show sample grain types
            var grains = manifest.AllGrainManifests
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
        AnsiConsole.MarkupLine("[grey]Testing: Interface and Implementation in separate DLLs[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[blue]Assembly Paths:[/]");
        AnsiConsole.MarkupLine($"  Contracts: {contractsPath}");
        AnsiConsole.MarkupLine($"  Implementation: {implementationPath}");
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
        AnsiConsole.MarkupLine("[blue]Phase 1: Loading Contracts Assembly (interfaces only)[/]");
        AnsiConsole.MarkupLine("[grey]  Contracts contain grain interfaces and shared data types.[/]");
        AnsiConsole.MarkupLine("[grey]  Scynapse generates proxy/stub classes from interfaces.[/]");
        var contractsResult = await grainLoader.LoadGrainAssemblyAsync(contractsPath);

        if (!contractsResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]FAILED to load contracts: {string.Join(", ", contractsResult.Errors)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Contracts loaded - {contractsResult.GrainTypes.Count} grain types registered[/]");

            // Show what types were loaded from contracts
            if (contractsResult.Assembly != null)
            {
                var ifaceTypes = contractsResult.Assembly.GetExportedTypes()
                    .Where(t => t.IsInterface && typeof(IGrain).IsAssignableFrom(t))
                    .ToList();
                AnsiConsole.MarkupLine($"  Interfaces found: {string.Join(", ", ifaceTypes.Select(t => t.Name))}");
            }
        }
        AnsiConsole.WriteLine();

        // Check what we have after contracts
        var manifestProvider = host.Services.GetService<IClusterManifestProvider>();
        if (manifestProvider != null)
        {
            var grainCount = manifestProvider.Current.AllGrainManifests.Sum(m => m.Grains.Count);
            AnsiConsole.MarkupLine($"  Grain types in manifest after contracts: {grainCount}");
        }
        AnsiConsole.WriteLine();

        // Phase 2: Load implementation
        AnsiConsole.MarkupLine("[blue]Phase 2: Loading Implementation Assembly (grain classes)[/]");
        AnsiConsole.MarkupLine("[grey]  Implementation contains grain classes that implement the interfaces.[/]");
        AnsiConsole.MarkupLine("[grey]  Scynapse generates activators and method invokers.[/]");
        var implResult = await grainLoader.LoadGrainAssemblyAsync(implementationPath);

        if (!implResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]FAILED to load implementation: {string.Join(", ", implResult.Errors)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Implementation loaded - {implResult.GrainTypes.Count} grain types registered[/]");

            // Show what types were loaded from implementation
            if (implResult.Assembly != null)
            {
                var grainClasses = implResult.Assembly.GetExportedTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IGrain).IsAssignableFrom(t))
                    .ToList();
                AnsiConsole.MarkupLine($"  Grain classes found: {string.Join(", ", grainClasses.Select(t => t.Name))}");
            }
        }
        AnsiConsole.WriteLine();

        // Check final state
        if (manifestProvider != null)
        {
            var grainCount = manifestProvider.Current.AllGrainManifests.Sum(m => m.Grains.Count);
            AnsiConsole.MarkupLine($"  Grain types in manifest after implementation: {grainCount}");
        }
        AnsiConsole.WriteLine();

        // Phase 3: Analyze the split - show interface vs implementation assemblies
        AnsiConsole.MarkupLine("[blue]Phase 3: Split Assembly Analysis[/]");
        var analysisTable = new Table();
        analysisTable.AddColumn("Grain Class");
        analysisTable.AddColumn("Implementation Assembly");
        analysisTable.AddColumn("Interface");
        analysisTable.AddColumn("Interface Assembly");
        analysisTable.AddColumn("Split?");

        var implGrainClasses = implResult.Assembly?.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IGrain).IsAssignableFrom(t))
            .ToList() ?? new List<Type>();

        foreach (var grainType in implGrainClasses)
        {
            var grainInterfaces = grainType.GetInterfaces()
                .Where(i => typeof(IGrain).IsAssignableFrom(i) && i != typeof(IGrain) && i != typeof(IGrainObserver) && !i.Name.StartsWith("IGrainWith"))
                .ToList();

            foreach (var iface in grainInterfaces)
            {
                var implAssembly = grainType.Assembly.GetName().Name;
                var ifaceAssembly = iface.Assembly.GetName().Name;
                var isSplit = implAssembly != ifaceAssembly;

                analysisTable.AddRow(
                    grainType.Name,
                    implAssembly ?? "unknown",
                    iface.Name,
                    ifaceAssembly ?? "unknown",
                    isSplit ? "[green]Yes - SPLIT[/]" : "[yellow]No - Same Assembly[/]"
                );
            }
        }

        AnsiConsole.Write(analysisTable);
        AnsiConsole.WriteLine();

        // Phase 4: Try to invoke a grain
        AnsiConsole.MarkupLine("[blue]Phase 4: Testing Grain Invocation[/]");
        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

        var invocationSuccess = false;
        string? invocationError = null;

        if (implResult.Success && implGrainClasses.Any())
        {
            // Find IHelloGrain specifically since we know it has SayHello
            var helloGrainClass = implGrainClasses.FirstOrDefault(t =>
                t.GetInterfaces().Any(i => i.Name == "IHelloGrain"));

            if (helloGrainClass != null)
            {
                var helloInterface = helloGrainClass.GetInterfaces()
                    .FirstOrDefault(i => i.Name == "IHelloGrain");

                if (helloInterface != null)
                {
                    AnsiConsole.MarkupLine($"  Testing grain: {helloInterface.Name} (impl: {helloGrainClass.Name})");

                    try
                    {
                        // Find the GetGrain<TGrainInterface>(string) method
                        // IGrainFactory has multiple GetGrain overloads, we need the one with string key
                        var getGrainMethods = typeof(IGrainFactory).GetMethods()
                            .Where(m => m.Name == "GetGrain" && m.IsGenericMethod)
                            .ToList();

                        AnsiConsole.MarkupLine($"  [grey]Found {getGrainMethods.Count} GetGrain overloads[/]");

                        // Find the overload: GetGrain<TGrainInterface>(string primaryKey, string? grainClassNamePrefix = null)
                        var getGrainMethod = getGrainMethods.FirstOrDefault(m =>
                        {
                            var parameters = m.GetParameters();
                            return parameters.Length >= 1 &&
                                   parameters[0].ParameterType == typeof(string) &&
                                   m.GetGenericArguments().Length == 1;
                        });

                        if (getGrainMethod == null)
                        {
                            AnsiConsole.MarkupLine("[yellow]  Could not find GetGrain<T>(string) method[/]");
                            // Show available overloads for debugging
                            foreach (var method in getGrainMethods.Take(5))
                            {
                                var parms = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                AnsiConsole.MarkupLine($"  [grey]  - GetGrain<>({parms})[/]");
                            }
                        }
                        else
                        {
                            var genericMethod = getGrainMethod.MakeGenericMethod(helloInterface);
                            AnsiConsole.MarkupLine($"  [grey]Calling {genericMethod.Name}<{helloInterface.Name}>(\"split-test-grain\")[/]");

                            var grainRef = genericMethod.Invoke(grainFactory, new object?[] { "split-test-grain", null });

                            if (grainRef == null)
                            {
                                AnsiConsole.MarkupLine("[red]  ERROR: GetGrain returned null[/]");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[green]  ✓ Got grain reference: {grainRef.GetType().Name}[/]");

                                // Now invoke SayHello
                                var sayHelloMethod = helloInterface.GetMethod("SayHello");
                                if (sayHelloMethod != null)
                                {
                                    AnsiConsole.MarkupLine("  Invoking SayHello(\"Split Test\")...");
                                    var task = (Task<string>)sayHelloMethod.Invoke(grainRef, new object[] { "Split Test" })!;
                                    var result = await task;
                                    AnsiConsole.MarkupLine($"[green]  ✓ Response: \"{result}\"[/]");
                                    invocationSuccess = true;
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine("[yellow]  SayHello method not found on interface[/]");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var innerEx = ex.InnerException ?? ex;
                        invocationError = innerEx.Message;
                        AnsiConsole.MarkupLine($"[red]  ✗ Invocation failed: {innerEx.GetType().Name}[/]");
                        AnsiConsole.MarkupLine($"[red]    {innerEx.Message}[/]");
                        if (innerEx.InnerException != null)
                        {
                            AnsiConsole.MarkupLine($"[red]    Inner: {innerEx.InnerException.Message}[/]");
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]  Could not find IHelloGrain interface[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]  No HelloGrain found in implementation assembly[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]  Implementation not loaded or no grain classes found[/]");
        }
        AnsiConsole.WriteLine();

        // Summary
        AnsiConsole.MarkupLine("[blue]Split Assembly Test Summary[/]");
        var summaryTable = new Table();
        summaryTable.AddColumn("Component");
        summaryTable.AddColumn("Status");
        summaryTable.AddColumn("Details");

        summaryTable.AddRow(
            "Contracts Assembly",
            contractsResult.Success ? "[green]✓ Loaded[/]" : "[red]✗ Failed[/]",
            $"{contractsResult.GrainTypes.Count} grain types (Interfaces + Proxies)"
        );
        summaryTable.AddRow(
            "Implementation Assembly",
            implResult.Success ? "[green]✓ Loaded[/]" : "[red]✗ Failed[/]",
            $"{implResult.GrainTypes.Count} grain types (Classes + Invokers)"
        );
        summaryTable.AddRow(
            "Grain Invocation",
            invocationSuccess ? "[green]✓ Success[/]" : $"[red]✗ Failed[/]",
            invocationSuccess ? "SayHello() returned valid response" : invocationError ?? "Not attempted"
        );

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        // Explain split grain benefits
        AnsiConsole.MarkupLine("[blue]Benefits of Split Grain Assemblies:[/]");
        AnsiConsole.MarkupLine("  1. Clients only need Contracts DLL (smaller deployment)");
        AnsiConsole.MarkupLine("  2. Implementation can be updated without client changes");
        AnsiConsole.MarkupLine("  3. Better separation of concerns");
        AnsiConsole.MarkupLine("  4. Enables future GTD (Grain Type Directory) features");
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
