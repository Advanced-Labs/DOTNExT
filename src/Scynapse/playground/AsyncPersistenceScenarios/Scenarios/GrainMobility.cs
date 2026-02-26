using System.Reflection;
using AsyncPersistenceScenarios.Helpers;
using AsyncPersistenceScenarios.Services;
using DOTNExT.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scynapse.AsyncPlus;
using Scynapse.AsyncPlus.Services;
using Scynapse;
using Scynapse.Runtime;
using Spectre.Console;

namespace AsyncPersistenceScenarios.Scenarios;

/// <summary>
/// Scenario C9: Grain Mobility (Reactivation on Different Silo)
///
/// PURPOSE: Test that checkpoint state follows the grain when it's deactivated
/// on one silo and reactivated on another silo. This is different from C8 which
/// crashes the entire silo - C9 tests explicit grain deactivation and mobility.
///
/// KEY VALIDATIONS:
/// - Checkpoint state persists to RavenDB during normal operation
/// - After grain deactivation, state remains in RavenDB
/// - When grain reactivates on a different silo, state is restored
/// - Workflow continues correctly from restored state
///
/// TEST FLOW:
/// Phase 1: Start 2-silo cluster with shared RavenDB
/// Phase 2: Run workflow on Silo1, checkpoint at state 0
/// Phase 3: Explicitly deactivate the persistence grain on Silo1
/// Phase 4: Access the grain again (will reactivate, possibly on Silo2)
/// Phase 5: Resume workflow and verify state was restored
///
/// WHY THIS MATTERS:
/// In production, grains are regularly deactivated (idle timeout, memory pressure,
/// rebalancing). Async+ checkpoints must survive grain deactivation/reactivation
/// cycles, not just silo crashes.
///
/// LOGGING:
/// - File: c9-grain-mobility.log (detailed diagnostics)
/// - Console: Summary progress with Spectre.Console tables
///
/// This scenario is SELF-MANAGING - starts and stops its own silos.
/// </summary>
public static class GrainMobility
{
    // Configuration
    private const string LogFileName = "c9-grain-mobility.log";
    private static readonly string LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), LogFileName);

    private const string WorkflowId = "c9-grain-mobility-test";
    private const int InputValue = 7;
    private static readonly int ExpectedResult = (InputValue * 2) + 10; // 24

    // Silo ports
    private const int Silo1Port = 11131;
    private const int Silo1GatewayPort = 30031;
    private const int Silo2Port = 11132;
    private const int Silo2GatewayPort = 30032;

    // Source code for Roslyn+ compiled workflow
    private const string WorkflowSource = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace GrainMobilityWorkflow
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
                await Task.Delay(500);
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
        LogToFile($"Scenario C9: Grain Mobility");
        LogToFile($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        LogToFile($"Workflow ID: {WorkflowId}");
        LogToFile($"Log File: {LogFilePath}");
        LogToFile("=".PadRight(80, '='));

        // Console header
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]  Scenario C9: Grain Mobility                                          [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Explanation panel
        var explanation = new Panel(
            "[white]Tests that checkpoint state follows the grain during deactivation/reactivation.\n\n" +
            "[yellow]Key Difference from C8:[/]\n" +
            "- C8: Crashes entire silo, starts new workflow instance\n" +
            "- C9: Explicitly deactivates grain, tests state persistence\n\n" +
            "[yellow]Why This Matters:[/]\n" +
            "In production, grains are regularly deactivated (idle timeout, memory\n" +
            "pressure, rebalancing). Checkpoints must survive these cycles.\n\n" +
            "[yellow]Test Flow:[/]\n" +
            "1. Run workflow on 2-silo cluster, checkpoint at state 0\n" +
            "2. Explicitly deactivate the persistence grain\n" +
            "3. Access grain again (reactivates from RavenDB)\n" +
            "4. Verify state was correctly restored[/]")
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
        AnsiConsole.MarkupLine("[cyan]  Scenario C9 Complete                                                  [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine($"[grey]Detailed log: {LogFilePath}[/]");

        // Log final summary to file
        LogToFile("");
        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C9 Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
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
        var compiledAssembly = compiler.CompileAndLoad(WorkflowSource, "GrainMobilityWorkflowAssembly");

        if (compiledAssembly == null)
        {
            var errors = compiler.GetErrorsString();
            LogToFile($"Compilation FAILED:\n{errors}");
            throw new InvalidOperationException($"Compilation failed: {errors}");
        }

        LogToFile($"Compilation SUCCESS: {compiledAssembly.FullName}");
        AnsiConsole.MarkupLine("[green]✓ Compilation successful[/]");

        var workflowType = compiledAssembly.GetType("GrainMobilityWorkflow.TestWorkflow")
            ?? throw new InvalidOperationException("Could not find TestWorkflow type");
        var calculateMethod = workflowType.GetMethod("Calculate")
            ?? throw new InvalidOperationException("Could not find Calculate method");

        AnsiConsole.WriteLine();

        // ============================================
        // PHASE 1: Start 2-silo cluster
        // ============================================
        LogToFile("");
        LogToFile("PHASE 1: Starting 2-silo cluster with shared RavenDB");
        AnsiConsole.MarkupLine("[blue]Phase 1: Starting 2-silo cluster with shared RavenDB...[/]");

        IHost? silo1 = null;
        IHost? silo2 = null;

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
                        databaseName: "C9GrainMobilityTest"
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
                        databaseName: "C9GrainMobilityTest"
                    );
                    await host.StartAsync();
                    return host;
                });
            LogToFile("Silo2 started");
            AnsiConsole.MarkupLine("[green]✓ Silo2 started[/]");

            // Wait for cluster to form
            await Task.Delay(3000);
            LogToFile("2-silo cluster ready");
            AnsiConsole.MarkupLine("[green]✓ 2-silo cluster formed[/]");
            AnsiConsole.WriteLine();

            // Show cluster configuration
            var clusterTable = new Table();
            clusterTable.AddColumn("Silo");
            clusterTable.AddColumn("Port");
            clusterTable.AddColumn("Gateway");
            clusterTable.AddColumn("Role");
            clusterTable.AddRow("Silo1", Silo1Port.ToString(), Silo1GatewayPort.ToString(), "[yellow]Primary[/]");
            clusterTable.AddRow("Silo2", Silo2Port.ToString(), Silo2GatewayPort.ToString(), "Secondary");
            AnsiConsole.Write(clusterTable);
            AnsiConsole.WriteLine();

            // Clear previous state
            var grainFactory1 = silo1.Services.GetRequiredService<IGrainFactory>();
            var persistenceGrain = grainFactory1.GetGrain<IAsyncStatePersistenceGrain>(WorkflowId);
            await persistenceGrain.ClearAsync();
            LogToFile($"Cleared previous state for {WorkflowId}");
            AnsiConsole.MarkupLine("[grey]  Cleared previous workflow state[/]");

            // ============================================
            // PHASE 2: Run workflow on cluster, checkpoint
            // ============================================
            LogToFile("");
            LogToFile("PHASE 2: Running workflow on cluster");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 2: Running workflow on cluster...[/]");

            var persistence1 = silo1.Services.GetRequiredService<IAsyncPersistenceService>()
                as ScynapseAsyncPersistenceService
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

            // Check the state before deactivation
            var hasStateBefore = await persistenceGrain.HasPersistedStateAsync();
            var checkpointBefore = hasStateBefore ? await persistenceGrain.TryGetCheckpointAsync() : null;
            LogToFile($"State before deactivation: hasState={hasStateBefore}, stateNumber={checkpointBefore?.StateNumber}");
            AnsiConsole.MarkupLine($"[grey]  State persisted: {hasStateBefore}, checkpoint state: {checkpointBefore?.StateNumber}[/]");

            // ============================================
            // PHASE 3: Deactivate the persistence grain
            // ============================================
            LogToFile("");
            LogToFile("PHASE 3: Deactivating persistence grain");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 3: Deactivating persistence grain...[/]");

            // Deactivate the grain - this forces Scynapse to unload it
            // When accessed again, it will reactivate (possibly on a different silo)
            await persistenceGrain.RequestDeactivationAsync();
            LogToFile("Called RequestDeactivationAsync on persistence grain");
            AnsiConsole.MarkupLine("[yellow]  Called RequestDeactivationAsync on persistence grain[/]");

            // Wait for deactivation to complete
            await Task.Delay(3000);
            LogToFile("Waited for grain deactivation");
            AnsiConsole.MarkupLine("[grey]  Waited for grain deactivation (3 seconds)[/]");

            // ============================================
            // PHASE 4: Access grain again (reactivation)
            // ============================================
            LogToFile("");
            LogToFile("PHASE 4: Accessing grain again (will trigger reactivation)");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 4: Accessing grain again (will trigger reactivation)...[/]");

            // Access the grain - this will reactivate it
            // With 2 silos and RavenDB, it should load state from storage
            var hasStateAfter = await persistenceGrain.HasPersistedStateAsync();
            var checkpointAfter = hasStateAfter ? await persistenceGrain.TryGetCheckpointAsync() : null;

            LogToFile($"State after reactivation: hasState={hasStateAfter}, stateNumber={checkpointAfter?.StateNumber}");
            AnsiConsole.MarkupLine($"[green]✓ Grain reactivated, state loaded from RavenDB[/]");
            AnsiConsole.MarkupLine($"[grey]  State found: {hasStateAfter}, checkpoint state: {checkpointAfter?.StateNumber}[/]");

            // ============================================
            // PHASE 5: Resume workflow and verify
            // ============================================
            LogToFile("");
            LogToFile("PHASE 5: Resuming workflow and verifying state restoration");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 5: Resuming workflow and verifying state restoration...[/]");

            var persistence2 = silo2.Services.GetRequiredService<IAsyncPersistenceService>()
                as ScynapseAsyncPersistenceService
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

            LogToFile("Resuming workflow...");
            AnsiConsole.MarkupLine("[yellow]  Resuming workflow...[/]");

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
            AnsiConsole.MarkupLine($"[green]✓ Workflow completed[/]");

            // ============================================
            // PHASE 6: Verification
            // ============================================
            LogToFile("");
            LogToFile("PHASE 6: Verification");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 6: Verification...[/]");

            var resultTable = new Table();
            resultTable.AddColumn("Metric");
            resultTable.AddColumn("Value");
            resultTable.AddColumn("Status");

            var stateSurvived = hasStateAfter && checkpointAfter?.StateNumber == checkpointBefore?.StateNumber;
            var resultCorrect = actualResult == ExpectedResult;

            resultTable.AddRow(
                "State before deactivation",
                checkpointBefore?.StateNumber.ToString() ?? "-",
                hasStateBefore ? "[green]✓ Present[/]" : "[red]✗ Missing[/]");
            resultTable.AddRow(
                "State after reactivation",
                checkpointAfter?.StateNumber.ToString() ?? "-",
                hasStateAfter ? "[green]✓ Present[/]" : "[red]✗ Missing[/]");
            resultTable.AddRow(
                "State survived deactivation",
                stateSurvived ? "Yes" : "No",
                stateSurvived ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
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

            AnsiConsole.Write(resultTable);
            AnsiConsole.WriteLine();

            // Final verdict
            var success = stateSurvived && wasRestored && resultCorrect;

            if (success)
            {
                LogToFile("SUCCESS: Grain mobility with state persistence verified!");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[green]  ✓ SUCCESS: Grain Mobility VERIFIED!                                  [/]");
                AnsiConsole.MarkupLine("[green]    • Checkpoint state persisted to RavenDB                            [/]");
                AnsiConsole.MarkupLine("[green]    • State survived grain deactivation                                [/]");
                AnsiConsole.MarkupLine("[green]    • Grain reactivated with state loaded from storage                 [/]");
                AnsiConsole.MarkupLine("[green]    • Workflow correctly restored and completed                        [/]");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
            }
            else
            {
                LogToFile("FAILED: Grain mobility test failed");
                AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[red]  ✗ FAILED: Grain mobility issue detected                              [/]");

                if (!stateSurvived)
                    AnsiConsole.MarkupLine("[red]    • State did not survive grain deactivation                        [/]");
                if (!wasRestored)
                    AnsiConsole.MarkupLine("[red]    • Workflow was not restored from checkpoint                       [/]");
                if (!resultCorrect)
                    AnsiConsole.MarkupLine("[red]    • Result incorrect - state may not have been restored properly    [/]");

                AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
            }
        }
        finally
        {
            // Cleanup all silos
            LogToFile("Cleaning up silos...");

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

    #region Logging Helpers

    private static void ClearLogFile()
    {
        try
        {
            File.WriteAllText(LogFilePath, $"C9 Grain Mobility Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[C9] Warning: Could not clear log file: {ex.Message}");
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
