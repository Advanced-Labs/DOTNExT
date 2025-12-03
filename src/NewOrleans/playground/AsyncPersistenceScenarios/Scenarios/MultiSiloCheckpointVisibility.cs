using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using AsyncPersistenceScenarios.Helpers;
using AsyncPersistenceScenarios.Services;
using DOTNExT.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewOrleans.AsyncPlus;
using NewOrleans.AsyncPlus.Services;
using Orleans;
using Spectre.Console;

namespace AsyncPersistenceScenarios.Scenarios;

/// <summary>
/// Scenario C8: Multi-Silo Checkpoint Visibility
///
/// PURPOSE: Verify that checkpoints written by one silo are immediately visible
/// to all other silos in the cluster via RavenDB shared storage.
///
/// KEY VALIDATIONS:
/// - All silos can read checkpoint data written by any silo
/// - No stale reads across silos (RavenDB provides consistency)
/// - Grain state queries work from any silo in the cluster
/// - Cross-silo visibility is immediate (not eventually consistent)
///
/// TEST FLOW:
/// Phase 1: Start 3-silo cluster with shared RavenDB
/// Phase 2: Run workflow on Silo1, checkpoint at state 0
/// Phase 3: Query checkpoint state from Silo2 and Silo3
/// Phase 4: Verify all silos see identical checkpoint data
/// Phase 5: Crash Silo1, resume workflow from Silo2
/// Phase 6: Verify workflow completes correctly on different silo
///
/// LOGGING:
/// - File: c8-multi-silo-visibility.log (detailed diagnostics)
/// - Console: Summary progress with Spectre.Console tables
///
/// This scenario is SELF-MANAGING - starts and stops its own silos.
/// </summary>
public static class MultiSiloCheckpointVisibility
{
    // Configuration
    private const string LogFileName = "c8-multi-silo-visibility.log";
    private static readonly string LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), LogFileName);

    private const string WorkflowId = "c8-multi-silo-test";
    private const int InputValue = 42;
    private static readonly int ExpectedResult = (InputValue * 2) + 10; // 94

    // Silo ports
    private const int Silo1Port = 11121;
    private const int Silo1GatewayPort = 30021;
    private const int Silo2Port = 11122;
    private const int Silo2GatewayPort = 30022;
    private const int Silo3Port = 11123;
    private const int Silo3GatewayPort = 30023;

    // Source code for Roslyn+ compiled workflow
    private const string WorkflowSource = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace MultiSiloWorkflow
{
    public class TestWorkflow
    {
        private readonly string _workflowId;

        public TestWorkflow(string workflowId)
        {
            _workflowId = workflowId;
        }

        /// <summary>
        /// Simple calculation: (input * 2) + 10
        /// With [Persistable], Roslyn+ injects persistence calls.
        /// </summary>
        [Persistable]
        public async Task<int> Calculate(int input)
        {
            // AWAIT POINT 0: Checkpoint state 0
            var step1 = await Task.Run(async () =>
            {
                await Task.Delay(1000); // Give time for cross-silo queries
                return input * 2;
            });

            // AWAIT POINT 1: Checkpoint state 1
            var step2 = await Task.Run(async () =>
            {
                await Task.Delay(500);
                return step1 + 10;
            });

            return step2;
        }
    }
}
";

    public static async Task RunAsync()
    {
        // Initialize logging
        ClearLogFile();

        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C8: Multi-Silo Checkpoint Visibility");
        LogToFile($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        LogToFile($"Workflow ID: {WorkflowId}");
        LogToFile($"Log File: {LogFilePath}");
        LogToFile("=".PadRight(80, '='));

        // Console header
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]  Scenario C8: Multi-Silo Checkpoint Visibility                        [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Explanation panel
        var explanation = new Panel(
            "[white]Tests that checkpoints are visible across all silos in a cluster.\n\n" +
            "[yellow]Key Validations:[/]\n" +
            "- Checkpoint written by Silo1 is readable from Silo2 and Silo3\n" +
            "- No stale reads (RavenDB provides immediate consistency)\n" +
            "- Workflow can resume on a different silo after crash\n\n" +
            "[yellow]Test Flow:[/]\n" +
            "- Start 3-silo cluster with shared RavenDB\n" +
            "- Run workflow on Silo1, checkpoint at state 0\n" +
            "- Query checkpoint from Silo2 and Silo3\n" +
            "- Crash Silo1, resume workflow from Silo2\n" +
            "- Verify correct completion on different silo[/]")
            .Header("[green]About This Scenario[/]")
            .BorderColor(Color.Cyan1);
        AnsiConsole.Write(explanation);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[grey]Log file: {LogFilePath}[/]");
        AnsiConsole.WriteLine();

        try
        {
            await RunScenarioAsync();
        }
        catch (Exception ex)
        {
            LogToFile($"SCENARIO EXCEPTION: {ex}");
            AnsiConsole.MarkupLine($"[red]Scenario failed: {Markup.Escape(ex.Message)}[/]");
            AnsiConsole.WriteException(ex);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]  Scenario C8 Complete                                                  [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine($"[grey]Detailed log: {LogFilePath}[/]");

        // Log final summary to file
        LogToFile("");
        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C8 Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        LogToFile("=".PadRight(80, '='));
    }

    private static async Task RunScenarioAsync()
    {
        // ============================================
        // PRE-PHASE: Compile workflow with Roslyn+
        // ============================================
        LogToFile("");
        LogToFile("PRE-PHASE: Compiling workflow with Roslyn+");
        AnsiConsole.MarkupLine("[blue]Pre-Phase: Compiling workflow with Roslyn+...[/]");

        var compiler = new PersistableAsyncCompiler();
        var compiledAssembly = compiler.CompileAndLoad(WorkflowSource, "MultiSiloWorkflowAssembly");

        if (compiledAssembly == null)
        {
            var errors = compiler.GetErrorsString();
            LogToFile($"Compilation FAILED:\n{errors}");
            throw new InvalidOperationException($"Compilation failed: {errors}");
        }

        LogToFile($"Compilation SUCCESS: {compiledAssembly.FullName}");
        AnsiConsole.MarkupLine("[green]✓ Compilation successful[/]");

        var workflowType = compiledAssembly.GetType("MultiSiloWorkflow.TestWorkflow")
            ?? throw new InvalidOperationException("Could not find TestWorkflow type");
        var calculateMethod = workflowType.GetMethod("Calculate")
            ?? throw new InvalidOperationException("Could not find Calculate method");

        AnsiConsole.WriteLine();

        // ============================================
        // PHASE 1: Start 3-silo cluster
        // ============================================
        LogToFile("");
        LogToFile("PHASE 1: Starting 3-silo cluster with shared RavenDB");
        AnsiConsole.MarkupLine("[blue]Phase 1: Starting 3-silo cluster with shared RavenDB...[/]");

        IHost? silo1 = null;
        IHost? silo2 = null;
        IHost? silo3 = null;

        try
        {
            // Start Silo1 (primary)
            silo1 = await AnsiConsole.Status()
                .StartAsync("Starting Silo1 (primary)...", async ctx =>
                {
                    var host = SiloHelper.BuildClusterSiloWithRavenDb(
                        "Silo1",
                        Silo1Port,
                        Silo1GatewayPort,
                        Silo1Port, // Primary is itself
                        databaseName: "C8MultiSiloTest"
                    );
                    await host.StartAsync();
                    return host;
                });
            LogToFile("Silo1 started (primary)");
            AnsiConsole.MarkupLine("[green]✓ Silo1 started (primary)[/]");

            // Wait for primary to stabilize
            await Task.Delay(2000);

            // Start Silo2
            silo2 = await AnsiConsole.Status()
                .StartAsync("Starting Silo2...", async ctx =>
                {
                    var host = SiloHelper.BuildClusterSiloWithRavenDb(
                        "Silo2",
                        Silo2Port,
                        Silo2GatewayPort,
                        Silo1Port, // Points to primary
                        databaseName: "C8MultiSiloTest"
                    );
                    await host.StartAsync();
                    return host;
                });
            LogToFile("Silo2 started");
            AnsiConsole.MarkupLine("[green]✓ Silo2 started[/]");

            // Start Silo3
            silo3 = await AnsiConsole.Status()
                .StartAsync("Starting Silo3...", async ctx =>
                {
                    var host = SiloHelper.BuildClusterSiloWithRavenDb(
                        "Silo3",
                        Silo3Port,
                        Silo3GatewayPort,
                        Silo1Port, // Points to primary
                        databaseName: "C8MultiSiloTest"
                    );
                    await host.StartAsync();
                    return host;
                });
            LogToFile("Silo3 started");
            AnsiConsole.MarkupLine("[green]✓ Silo3 started[/]");

            // Wait for cluster to form
            await Task.Delay(3000);
            LogToFile("3-silo cluster ready");
            AnsiConsole.MarkupLine("[green]✓ 3-silo cluster formed[/]");
            AnsiConsole.WriteLine();

            // Show cluster configuration
            var clusterTable = new Table();
            clusterTable.AddColumn("Silo");
            clusterTable.AddColumn("Port");
            clusterTable.AddColumn("Gateway");
            clusterTable.AddColumn("Role");
            clusterTable.AddRow("Silo1", Silo1Port.ToString(), Silo1GatewayPort.ToString(), "[yellow]Primary[/]");
            clusterTable.AddRow("Silo2", Silo2Port.ToString(), Silo2GatewayPort.ToString(), "Secondary");
            clusterTable.AddRow("Silo3", Silo3Port.ToString(), Silo3GatewayPort.ToString(), "Secondary");
            AnsiConsole.Write(clusterTable);
            AnsiConsole.WriteLine();

            // Clear previous state
            var grainFactory1 = silo1.Services.GetRequiredService<IGrainFactory>();
            var grain1 = grainFactory1.GetGrain<IAsyncStatePersistenceGrain>(WorkflowId);
            await grain1.ClearAsync();
            LogToFile($"Cleared previous state for {WorkflowId}");
            AnsiConsole.MarkupLine("[grey]  Cleared previous workflow state[/]");

            // ============================================
            // PHASE 2: Run workflow on Silo1, checkpoint
            // ============================================
            LogToFile("");
            LogToFile("PHASE 2: Running workflow on Silo1");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 2: Running workflow on Silo1...[/]");

            var persistence1 = silo1.Services.GetRequiredService<IAsyncPersistenceService>()
                as NewOrleansAsyncPersistenceService
                ?? throw new InvalidOperationException("Could not get persistence service from Silo1");

            var checkpointTcs = new TaskCompletionSource();
            var checkpointState = -1;

            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == WorkflowId && e.StateNumber == 0)
                {
                    checkpointState = e.StateNumber;
                    LogToFile($"Checkpoint received: {e.MethodId} state={e.StateNumber}");
                    checkpointTcs.TrySetResult();
                }
            }

            persistence1.OnCheckpoint += OnCheckpoint;

            // Create workflow instance
            var instance = Activator.CreateInstance(workflowType, WorkflowId)
                ?? throw new InvalidOperationException("Failed to create workflow instance");

            // Launch workflow with context
            var workflowTask = Task.Run(async () =>
            {
                using (AsyncPersistenceContext.SetCurrent(persistence1, WorkflowId))
                {
                    var result = (Task<int>?)calculateMethod.Invoke(instance, new object[] { InputValue })
                        ?? throw new InvalidOperationException("Method invocation returned null");
                    return await result;
                }
            });

            LogToFile($"Workflow started with input={InputValue}");
            AnsiConsole.MarkupLine($"[yellow]  Workflow started with input={InputValue}[/]");

            // Wait for first checkpoint
            var checkpointOrTimeout = await Task.WhenAny(checkpointTcs.Task, Task.Delay(15000));
            if (checkpointOrTimeout != checkpointTcs.Task)
            {
                throw new TimeoutException("Workflow did not checkpoint in time");
            }

            persistence1.OnCheckpoint -= OnCheckpoint;

            LogToFile($"Workflow checkpointed at state {checkpointState}");
            AnsiConsole.MarkupLine($"[green]✓ Workflow checkpointed at state {checkpointState}[/]");

            // ============================================
            // PHASE 3: Query checkpoint from all silos
            // ============================================
            LogToFile("");
            LogToFile("PHASE 3: Querying checkpoint visibility from all silos");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 3: Verifying checkpoint visibility across all silos...[/]");

            // Query from Silo1
            var checkpoint1 = await QueryCheckpointFromSilo(silo1, "Silo1", WorkflowId);

            // Query from Silo2
            var checkpoint2 = await QueryCheckpointFromSilo(silo2, "Silo2", WorkflowId);

            // Query from Silo3
            var checkpoint3 = await QueryCheckpointFromSilo(silo3, "Silo3", WorkflowId);

            // Display visibility results
            var visibilityTable = new Table();
            visibilityTable.AddColumn("Silo");
            visibilityTable.AddColumn("Has State");
            visibilityTable.AddColumn("State #");
            visibilityTable.AddColumn("Data Size");
            visibilityTable.AddColumn("Status");

            void AddVisibilityRow(string siloName, CheckpointInfo? cp)
            {
                var hasState = cp != null;
                var status = hasState ? "[green]✓ Visible[/]" : "[red]✗ Not Visible[/]";
                visibilityTable.AddRow(
                    siloName,
                    hasState ? "[green]Yes[/]" : "[red]No[/]",
                    cp?.StateNumber.ToString() ?? "-",
                    cp?.DataSize.ToString() ?? "-",
                    status);
            }

            AddVisibilityRow("Silo1 (source)", checkpoint1);
            AddVisibilityRow("Silo2", checkpoint2);
            AddVisibilityRow("Silo3", checkpoint3);

            AnsiConsole.Write(visibilityTable);
            AnsiConsole.WriteLine();

            // Verify all silos see the checkpoint
            var allVisible = checkpoint1 != null && checkpoint2 != null && checkpoint3 != null;
            var allMatch = allVisible &&
                checkpoint1.StateNumber == checkpoint2.StateNumber &&
                checkpoint2.StateNumber == checkpoint3.StateNumber &&
                checkpoint1.DataSize == checkpoint2.DataSize &&
                checkpoint2.DataSize == checkpoint3.DataSize;

            if (!allVisible)
            {
                LogToFile("FAILURE: Checkpoint not visible from all silos");
                throw new InvalidOperationException("Checkpoint not visible from all silos");
            }

            if (!allMatch)
            {
                LogToFile("FAILURE: Checkpoint data differs between silos");
                throw new InvalidOperationException("Checkpoint data differs between silos");
            }

            LogToFile("SUCCESS: Checkpoint visible and consistent across all silos");
            AnsiConsole.MarkupLine("[green]✓ Checkpoint visible and consistent across all silos[/]");

            // ============================================
            // PHASE 4: Crash Silo1, resume from Silo2
            // ============================================
            LogToFile("");
            LogToFile("PHASE 4: Crashing Silo1, resuming workflow from Silo2");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 4: Crashing Silo1 and resuming workflow from Silo2...[/]");

            // Stop Silo1 abruptly
            await silo1.StopAsync();
            silo1.Dispose();
            silo1 = null;

            LogToFile("Silo1 stopped (crashed)");
            AnsiConsole.MarkupLine("[red]✓ Silo1 crashed[/]");

            // Wait for cluster to stabilize
            await Task.Delay(2000);

            // Resume workflow from Silo2
            var persistence2 = silo2.Services.GetRequiredService<IAsyncPersistenceService>()
                as NewOrleansAsyncPersistenceService
                ?? throw new InvalidOperationException("Could not get persistence service from Silo2");

            var wasRestored = false;
            var restoredFromState = -1;

            void OnRestore(object? sender, RestoreEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    wasRestored = true;
                    restoredFromState = e.RestoredState;
                    LogToFile($"Restored: {e.MethodId} from state {e.RestoredState}");
                }
            }

            persistence2.OnRestore += OnRestore;

            // Create new workflow instance for resumption
            var resumeInstance = Activator.CreateInstance(workflowType, WorkflowId)
                ?? throw new InvalidOperationException("Failed to create workflow instance for resume");

            LogToFile("Resuming workflow from Silo2...");
            AnsiConsole.MarkupLine("[yellow]  Resuming workflow from Silo2...[/]");

            int actualResult;
            try
            {
                using (AsyncPersistenceContext.SetCurrent(persistence2, WorkflowId))
                {
                    // Pass dummy input - should be overwritten by restoration
                    var result = (Task<int>?)calculateMethod.Invoke(resumeInstance, new object[] { 999 })
                        ?? throw new InvalidOperationException("Method invocation returned null");
                    actualResult = await result;
                }
            }
            finally
            {
                persistence2.OnRestore -= OnRestore;
            }

            LogToFile($"Workflow completed: result={actualResult}, expected={ExpectedResult}");
            AnsiConsole.MarkupLine($"[green]✓ Workflow completed on Silo2[/]");

            // ============================================
            // PHASE 5: Verification
            // ============================================
            LogToFile("");
            LogToFile("PHASE 5: Verification");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 5: Verification...[/]");

            var resultTable = new Table();
            resultTable.AddColumn("Metric");
            resultTable.AddColumn("Value");
            resultTable.AddColumn("Status");

            var resultCorrect = actualResult == ExpectedResult;
            var usedDummyInput = actualResult == (999 * 2) + 10;

            resultTable.AddRow(
                "Cross-silo visibility",
                "All 3 silos",
                allMatch ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Was restored",
                wasRestored ? "Yes" : "No",
                wasRestored ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Restored from state",
                restoredFromState >= 0 ? restoredFromState.ToString() : "-",
                restoredFromState >= 0 ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Expected result",
                ExpectedResult.ToString(),
                "");
            resultTable.AddRow(
                "Actual result",
                actualResult.ToString(),
                resultCorrect ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Resumed on different silo",
                "Silo2 (after Silo1 crash)",
                resultCorrect ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");

            AnsiConsole.Write(resultTable);
            AnsiConsole.WriteLine();

            // Final verdict
            var success = allMatch && wasRestored && resultCorrect;

            if (success)
            {
                LogToFile("SUCCESS: Multi-silo checkpoint visibility verified!");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[green]  ✓ SUCCESS: Multi-Silo Checkpoint Visibility VERIFIED!                [/]");
                AnsiConsole.MarkupLine("[green]    • Checkpoint visible from all silos immediately                    [/]");
                AnsiConsole.MarkupLine("[green]    • Data consistent across all silos                                 [/]");
                AnsiConsole.MarkupLine("[green]    • Workflow successfully resumed on different silo                  [/]");
                AnsiConsole.MarkupLine("[green]    • RavenDB provides correct cross-silo visibility                   [/]");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
            }
            else
            {
                LogToFile("FAILED: Multi-silo checkpoint visibility test failed");
                AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[red]  ✗ FAILED: Multi-silo checkpoint visibility issue detected            [/]");

                if (!allMatch)
                    AnsiConsole.MarkupLine("[red]    • Checkpoint not visible or inconsistent across silos             [/]");
                if (!wasRestored)
                    AnsiConsole.MarkupLine("[red]    • Workflow was not restored from checkpoint                       [/]");
                if (usedDummyInput)
                    AnsiConsole.MarkupLine("[red]    • Used dummy input 999 - restoration failed                       [/]");
                if (!resultCorrect && !usedDummyInput)
                    AnsiConsole.MarkupLine("[red]    • Unexpected result value                                         [/]");

                AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
            }
        }
        finally
        {
            // Cleanup all silos
            LogToFile("Cleaning up silos...");

            if (silo3 != null)
            {
                try { await silo3.StopAsync(); silo3.Dispose(); }
                catch { /* ignore */ }
            }
            if (silo2 != null)
            {
                try { await silo2.StopAsync(); silo2.Dispose(); }
                catch { /* ignore */ }
            }
            if (silo1 != null)
            {
                try { await silo1.StopAsync(); silo1.Dispose(); }
                catch { /* ignore */ }
            }

            LogToFile("All silos stopped");
        }
    }

    private class CheckpointInfo
    {
        public int StateNumber { get; init; }
        public int DataSize { get; init; }
    }

    private static async Task<CheckpointInfo?> QueryCheckpointFromSilo(IHost silo, string siloName, string workflowId)
    {
        try
        {
            var grainFactory = silo.Services.GetRequiredService<IGrainFactory>();
            var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(workflowId);

            var hasState = await grain.HasPersistedStateAsync();
            if (!hasState)
            {
                LogToFile($"  {siloName}: No persisted state found");
                return null;
            }

            var checkpoint = await grain.TryGetCheckpointAsync();
            if (checkpoint == null)
            {
                LogToFile($"  {siloName}: HasPersistedState=true but checkpoint is null");
                return null;
            }

            var dataSize = checkpoint.SerializedStateMachine?.Length ?? 0;
            LogToFile($"  {siloName}: StateNumber={checkpoint.StateNumber}, DataSize={dataSize}");

            return new CheckpointInfo
            {
                StateNumber = checkpoint.StateNumber,
                DataSize = dataSize
            };
        }
        catch (Exception ex)
        {
            LogToFile($"  {siloName}: Error querying checkpoint: {ex.Message}");
            return null;
        }
    }

    #region Logging Helpers

    private static void ClearLogFile()
    {
        try
        {
            File.WriteAllText(LogFilePath, $"C8 Multi-Silo Visibility Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[C8] Warning: Could not clear log file: {ex.Message}");
        }
    }

    private static void LogToFile(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var line = string.IsNullOrEmpty(message) ? "" : $"[{timestamp}] {message}";
            File.AppendAllText(LogFilePath, line + "\n");
        }
        catch
        {
            // Ignore logging errors
        }
    }

    #endregion
}
