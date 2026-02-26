# Silo Orchestration Patterns for Async+ Scenarios

## Overview

This document describes the patterns learned from `PluginGrainScenarios` for self-managing Orleans silos in test scenarios. These patterns should be used for all future Async+ scenarios.

## Key Principles

1. **Self-Managing Silos**: Scenarios start and stop their own silos automatically - no user menu interaction required
2. **Use Real Roslyn+**: Compile [Persistable] methods at runtime using our modified Roslyn compiler
3. **Orleans + RavenDB**: Always use Orleans with RavenDB storage for durable persistence
4. **Phased Execution**: Divide scenarios into clear phases with progress output
5. **Comprehensive Reporting**: Use Spectre.Console tables for structured output

## Architecture

### SiloHelper Pattern

```csharp
public static class SiloHelper
{
    // Single silo for basic scenarios
    public static IHost BuildSingleSilo(int siloPort = 11111, int gatewayPort = 30000)
    {
        return Host.CreateDefaultBuilder()
            .UseOrleans(silo =>
            {
                silo.UseLocalhostClustering(siloPort, gatewayPort)
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "async-plus-test";
                        options.ServiceId = "async-plus-test";
                    });

                // Always use RavenDB for Async+ scenarios
                silo.UseAsyncPlusPersistenceWithRavenDb(options =>
                {
                    options.Urls = new[] { "http://127.0.0.1:38880" };
                    options.DatabaseName = "AsyncPlusScenarios";
                    options.CreateDatabaseIfNotExists = true;
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
                logging.AddFilter("Scynapse.AsyncPlus", LogLevel.Debug);
            })
            .Build();
    }

    // Multi-silo cluster for distributed scenarios
    public static IHost BuildClusterSilo(string name, int siloPort, int gatewayPort, int primarySiloPort)
    {
        // Similar pattern with UseDevelopmentClustering
    }
}
```

### Scenario Structure Pattern

Each scenario is a separate static class with `RunAsync()`:

```csharp
public static class MyAsyncPlusScenario
{
    public static async Task RunAsync()
    {
        // 1. Header
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario X: Description[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");

        // 2. Prerequisites check
        // Check RavenDB, find assemblies, etc.

        // 3. Start silo(s) automatically
        using var host = SiloHelper.BuildSingleSilo();
        await host.StartAsync();

        try
        {
            // 4. Execute phases with clear output
            AnsiConsole.MarkupLine("[yellow]Phase 1: ...[/]");
            // ...

            AnsiConsole.MarkupLine("[yellow]Phase 2: ...[/]");
            // ...

            // 5. Report results with tables
            var table = new Table();
            // ...
            AnsiConsole.Write(table);

            // 6. Conclusions
            AnsiConsole.MarkupLine("[green]✓ SUCCESS: ...[/]");
        }
        finally
        {
            // 7. Always cleanup
            await host.StopAsync();
        }
    }
}
```

### Using Real Roslyn+ Compiled Code

Instead of hand-written state machines, scenarios should:

```csharp
// 1. Define source code with [Persistable] attribute
var source = @"
using DOTNExT.Persistence;

public class Workflow
{
    [Persistable]
    public async Task<int> ProcessAsync(int input)
    {
        var step1 = await Task.FromResult(input * 2);
        var step2 = await Task.FromResult(step1 + 10);
        return step2;
    }
}";

// 2. Compile with modified Roslyn
var compiler = new RoslynAsyncCompiler();  // Uses our modified Roslyn
var assembly = compiler.CompileToAssembly(source);

// 3. Execute compiled code with Orleans persistence context
using (AsyncPersistenceContext.SetCurrent(orleansPersistence))
{
    var workflow = (dynamic)Activator.CreateInstance(assembly.GetType("Workflow"));
    var result = await workflow.ProcessAsync(42);
}
```

## Differences from Legacy Approach

| Aspect | Legacy (Challenges 1-8) | New Approach |
|--------|-------------------------|--------------|
| Silo lifecycle | Manual menu selection | Auto-managed per scenario |
| State machine | Hand-written simulation | Real Roslyn+ generated |
| Storage | Optional, mixed | Always Orleans + RavenDB |
| Test isolation | Shared silo | Fresh silo per scenario |
| Output | Interactive prompts | Phased progress reporting |

## File Organization

```
AsyncPersistenceScenarios/
├── Program.cs                 # Main menu only
├── Scenarios/
│   ├── CrossSessionPersistence.cs
│   ├── MultiSiloCheckpoints.cs
│   ├── NestedPersistableWorkflows.cs
│   └── ...
├── Helpers/
│   ├── SiloHelper.cs          # Silo building utilities
│   ├── RoslynHelper.cs        # Roslyn+ compilation utilities
│   └── ReportingHelper.cs     # Spectre.Console output utilities

AI-Contexts/Claude-Opus/        # Consolidated documentation
├── AsyncPlus-Scenarios.md      # Scenario definitions & analysis
└── AsyncPlus-SiloPatterns.md   # This file
```

## Integration with Roslyn+

The modified Roslyn compiler injects:
1. `AsyncPersistenceContext.Current` reads at state machine initialization
2. `TryRestore()` calls at method entry
3. `Checkpoint()` calls before each await suspension

Scenarios should verify these injections are working correctly by:
1. Compiling with Roslyn+
2. Decompiling to verify injection
3. Running with Orleans persistence to verify checkpoints are saved
4. Simulating process restart to verify restoration works
