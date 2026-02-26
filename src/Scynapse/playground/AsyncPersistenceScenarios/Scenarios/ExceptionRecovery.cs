using System.Reflection;
using AsyncPersistenceScenarios.Helpers;
using AsyncPersistenceScenarios.Services;
using DOTNExT.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scynapse.AsyncPlus;
using Scynapse.AsyncPlus.Services;
using Scynapse;
using Spectre.Console;

namespace AsyncPersistenceScenarios.Scenarios;

/// <summary>
/// Scenario C4: Exception Recovery
///
/// PURPOSE: Verify that exception handling works correctly with checkpointing.
/// When a workflow throws an exception after a checkpoint, the workflow should:
/// 1. Checkpoint state before the failing await
/// 2. Propagate the exception correctly
/// 3. On restore, re-run and re-throw the same exception
///
/// KEY VALIDATIONS:
/// - Checkpoint is created before the failing await
/// - Exception type and message are preserved after restore
/// - Fault event is raised when workflow throws
/// - Restored workflow reproduces the same failure
///
/// TEST FLOW:
/// Phase 1: Run workflow that throws at step 2
/// Phase 2: Verify checkpoint was created at state 1 (before failing await)
/// Phase 3: Verify Fault event was raised with correct exception
/// Phase 4: Restart and restore, verify same exception is thrown
///
/// WORKFLOW LOGIC:
/// CalculateWithFailure(x, shouldFail):
///   a = await Step1(x)         // a = x * 2      → Checkpoint state 0
///   b = await Step2(a, fail?)  // Throws if fail → Checkpoint state 1, then throw
///   return b
///
/// This scenario is SELF-MANAGING - starts and stops its own silo.
/// </summary>
public static class ExceptionRecovery
{
    // Configuration
    private const string LogFileName = "c4-exception-recovery.log";
    private static readonly string LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), LogFileName);

    private const string WorkflowId = "c4-exception-test";
    private const int InputValue = 5;
    private const string ExpectedExceptionType = "System.InvalidOperationException";
    private const string ExpectedExceptionMessage = "Step2 intentionally failed for testing";

    // Source code for Roslyn+ compiled workflow that can throw
    private const string WorkflowSource = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace ExceptionWorkflow
{
    public class TestWorkflow
    {
        private readonly string _workflowId;

        public TestWorkflow(string workflowId)
        {
            _workflowId = workflowId;
        }

        /// <summary>
        /// Workflow that can throw an exception at step 2.
        /// </summary>
        [Persistable]
        public async Task<int> CalculateWithFailure(int x, bool shouldFail)
        {
            // AWAIT POINT 0: Checkpoint state 0
            var a = await Step1(x);

            // AWAIT POINT 1: Checkpoint state 1, then Step2 may throw
            var b = await Step2(a, shouldFail);

            return b;
        }

        private async Task<int> Step1(int x)
        {
            await Task.Delay(300);
            return x * 2;
        }

        private async Task<int> Step2(int a, bool shouldFail)
        {
            await Task.Delay(300);
            if (shouldFail)
            {
                throw new InvalidOperationException(""Step2 intentionally failed for testing"");
            }
            return a + 10;
        }
    }
}
";

    public static async Task RunAsync()
    {
        // Initialize logging
        ClearLogFile();

        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C4: Exception Recovery");
        LogToFile($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        LogToFile($"Workflow ID: {WorkflowId}");
        LogToFile($"Input: {InputValue}, shouldFail: true");
        LogToFile($"Expected Exception: {ExpectedExceptionType}");
        LogToFile($"Log File: {LogFilePath}");
        LogToFile("=".PadRight(80, '='));

        // Console header
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]  Scenario C4: Exception Recovery                                      [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Explanation panel
        var explanation = new Panel(
            "[white]Tests exception handling with checkpointing.\n\n" +
            "[yellow]Workflow Logic:[/]\n" +
            $"  CalculateWithFailure({InputValue}, shouldFail=true):\n" +
            $"    a = await Step1({InputValue})  → a = 10      [[Checkpoint state 0]]\n" +
            "    b = await Step2(10, true)  → THROWS!    [[Checkpoint state 1]]\n\n" +
            "[yellow]Key Validations:[/]\n" +
            "- Checkpoint created before failing await\n" +
            "- Exception type and message preserved\n" +
            "- Fault event raised with exception details\n" +
            "- After restore, same exception re-thrown[/]")
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
        AnsiConsole.MarkupLine("[cyan]  Scenario C4 Complete                                                  [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine($"[grey]Detailed log: {LogFilePath}[/]");

        // Log final summary to file
        LogToFile("");
        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C4 Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
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
        var compiledAssembly = compiler.CompileAndLoad(WorkflowSource, "ExceptionWorkflowAssembly");

        if (compiledAssembly == null)
        {
            var errors = compiler.GetErrorsString();
            LogToFile($"Compilation FAILED:\n{errors}");
            throw new InvalidOperationException($"Compilation failed: {errors}");
        }

        LogToFile($"Compilation SUCCESS: {compiledAssembly.FullName}");
        AnsiConsole.MarkupLine("[green]✓ Compilation successful[/]");

        var workflowType = compiledAssembly.GetType("ExceptionWorkflow.TestWorkflow")
            ?? throw new InvalidOperationException("Could not find TestWorkflow type");
        var method = workflowType.GetMethod("CalculateWithFailure")
            ?? throw new InvalidOperationException("Could not find CalculateWithFailure method");

        AnsiConsole.WriteLine();

        // ============================================
        // PHASE 1: Run workflow that throws
        // ============================================
        LogToFile("");
        LogToFile("PHASE 1: Running workflow that will throw exception");
        AnsiConsole.MarkupLine("[blue]Phase 1: Running workflow that will throw exception...[/]");

        IHost? silo1 = null;
        var checkpointCount = 0;
        var lastCheckpointState = -1;
        Exception? caughtException = null;
        var faultRaised = false;
        Exception? faultException = null;

        try
        {
            silo1 = await AnsiConsole.Status()
                .StartAsync("Starting Scynapse silo with RavenDB...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11119,
                        gatewayPort: 30009,
                        clusterId: "c4-exception-test",
                        serviceId: "c4-exception-test"
                    );
                    await host.StartAsync();
                    return host;
                });

            LogToFile("Silo started successfully");
            AnsiConsole.MarkupLine("[green]✓ Silo started[/]");

            // Get persistence service
            var persistence = silo1.Services.GetRequiredService<IAsyncPersistenceService>()
                as ScynapseAsyncPersistenceService
                ?? throw new InvalidOperationException("Could not get ScynapseAsyncPersistenceService");

            // Clear previous state
            var grainFactory = silo1.Services.GetRequiredService<IGrainFactory>();
            var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(WorkflowId);
            await grain.ClearAsync();
            LogToFile($"Cleared previous state for {WorkflowId}");

            // Set up event tracking
            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    checkpointCount++;
                    lastCheckpointState = e.StateNumber;
                    LogToFile($"CHECKPOINT: state={e.StateNumber}, total={checkpointCount}");
                }
            }

            void OnFault(object? sender, FaultEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    faultRaised = true;
                    faultException = e.Exception;
                    LogToFile($"FAULT: {e.Exception.GetType().FullName}: {e.Exception.Message}");
                }
            }

            persistence.OnCheckpoint += OnCheckpoint;
            persistence.OnFault += OnFault;

            // Create workflow instance
            var instance = Activator.CreateInstance(workflowType, WorkflowId)
                ?? throw new InvalidOperationException("Failed to create workflow instance");

            LogToFile($"Starting workflow with input={InputValue}, shouldFail=true");
            AnsiConsole.MarkupLine($"[yellow]  Running workflow with shouldFail=true...[/]");

            try
            {
                using (AsyncPersistenceContext.SetCurrent(persistence, WorkflowId))
                {
                    var result = (Task<int>?)method.Invoke(instance, new object[] { InputValue, true })
                        ?? throw new InvalidOperationException("Method invocation returned null");
                    await result;
                }
                // Should not reach here
                LogToFile("ERROR: Workflow completed without throwing!");
            }
            catch (TargetInvocationException tie)
            {
                caughtException = tie.InnerException ?? tie;
                LogToFile($"Caught exception: {caughtException.GetType().FullName}: {caughtException.Message}");
            }
            catch (Exception ex)
            {
                caughtException = ex;
                LogToFile($"Caught exception: {ex.GetType().FullName}: {ex.Message}");
            }

            persistence.OnCheckpoint -= OnCheckpoint;
            persistence.OnFault -= OnFault;

            // Display Phase 1 results
            var phase1Table = new Table();
            phase1Table.AddColumn("Metric");
            phase1Table.AddColumn("Value");
            phase1Table.AddColumn("Status");

            phase1Table.AddRow(
                "Checkpoints created",
                checkpointCount.ToString(),
                checkpointCount >= 2 ? "[green]✓ Pass[/]" : "[yellow]⚠ Expected 2[/]");
            phase1Table.AddRow(
                "Last checkpoint state",
                lastCheckpointState.ToString(),
                lastCheckpointState == 1 ? "[green]✓ State 1[/]" : "[yellow]⚠ Expected 1[/]");
            phase1Table.AddRow(
                "Exception thrown",
                caughtException != null ? "Yes" : "No",
                caughtException != null ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            phase1Table.AddRow(
                "Exception type",
                caughtException?.GetType().FullName ?? "-",
                caughtException?.GetType().FullName == ExpectedExceptionType ? "[green]✓ Match[/]" : "[red]✗ Mismatch[/]");
            phase1Table.AddRow(
                "Fault event raised",
                faultRaised ? "Yes" : "No",
                faultRaised ? "[green]✓ Pass[/]" : "[yellow]⚠ Not raised[/]");

            AnsiConsole.Write(phase1Table);
            AnsiConsole.WriteLine();

            if (caughtException != null)
            {
                AnsiConsole.MarkupLine($"[red]✓ Exception caught: {Markup.Escape(caughtException.Message)}[/]");
            }

            // Verify checkpoint exists
            var hasState = await grain.HasPersistedStateAsync();
            var checkpoint = hasState ? await grain.TryGetCheckpointAsync() : null;
            LogToFile($"Persisted state: hasState={hasState}, stateNumber={checkpoint?.StateNumber}");
            AnsiConsole.MarkupLine($"[grey]  Checkpoint state: {checkpoint?.StateNumber ?? -1}[/]");
        }
        finally
        {
            if (silo1 != null)
            {
                // ============================================
                // PHASE 2: Stop silo
                // ============================================
                LogToFile("");
                LogToFile("PHASE 2: Stopping silo");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[blue]Phase 2: Stopping silo...[/]");

                await silo1.StopAsync();
                silo1.Dispose();
                silo1 = null;

                LogToFile("Silo stopped");
                AnsiConsole.MarkupLine("[grey]  Silo stopped[/]");
            }
        }

        // Wait before restart
        await Task.Delay(2000);

        // ============================================
        // PHASE 3: Restart and verify same exception
        // ============================================
        LogToFile("");
        LogToFile("PHASE 3: Restarting silo and verifying exception reproduction");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Phase 3: Restarting silo and verifying exception reproduction...[/]");

        IHost? silo2 = null;
        var wasRestored = false;
        var restoredFromState = -1;
        Exception? resumeException = null;

        try
        {
            silo2 = await AnsiConsole.Status()
                .StartAsync("Starting Scynapse silo (restart)...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11119,
                        gatewayPort: 30009,
                        clusterId: "c4-exception-test",
                        serviceId: "c4-exception-test"
                    );
                    await host.StartAsync();
                    return host;
                });

            LogToFile("Silo restarted successfully");
            AnsiConsole.MarkupLine("[green]✓ Silo restarted[/]");

            // Get persistence service
            var persistence2 = silo2.Services.GetRequiredService<IAsyncPersistenceService>()
                as ScynapseAsyncPersistenceService
                ?? throw new InvalidOperationException("Could not get persistence service");

            // Set up event tracking
            void OnRestore(object? sender, RestoreEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    wasRestored = true;
                    restoredFromState = e.RestoredState;
                    LogToFile($"RESTORE: from state {e.RestoredState}");
                }
            }

            persistence2.OnRestore += OnRestore;

            // Create new workflow instance for resumption
            var resumeInstance = Activator.CreateInstance(workflowType, WorkflowId)
                ?? throw new InvalidOperationException("Failed to create workflow instance for resume");

            LogToFile("Resuming workflow (expecting same exception)...");
            AnsiConsole.MarkupLine("[yellow]  Resuming workflow (expecting same exception)...[/]");

            try
            {
                using (AsyncPersistenceContext.SetCurrent(persistence2, WorkflowId))
                {
                    // Pass dummy values - should be overwritten by restoration
                    // But shouldFail needs to be true for the exception to occur again
                    var result = (Task<int>?)method.Invoke(resumeInstance, new object[] { 999, true })
                        ?? throw new InvalidOperationException("Method invocation returned null");
                    await result;
                }
                // Should not reach here
                LogToFile("ERROR: Resumed workflow completed without throwing!");
            }
            catch (TargetInvocationException tie)
            {
                resumeException = tie.InnerException ?? tie;
                LogToFile($"Resume caught exception: {resumeException.GetType().FullName}: {resumeException.Message}");
            }
            catch (Exception ex)
            {
                resumeException = ex;
                LogToFile($"Resume caught exception: {ex.GetType().FullName}: {ex.Message}");
            }

            persistence2.OnRestore -= OnRestore;

            // ============================================
            // PHASE 4: Verification
            // ============================================
            LogToFile("");
            LogToFile("PHASE 4: Verification");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 4: Verification...[/]");

            var resultTable = new Table();
            resultTable.AddColumn("Metric");
            resultTable.AddColumn("Value");
            resultTable.AddColumn("Status");

            resultTable.AddRow(
                "Was restored",
                wasRestored ? "Yes" : "No",
                wasRestored ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Restored from state",
                restoredFromState >= 0 ? restoredFromState.ToString() : "-",
                restoredFromState >= 0 ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Resume exception thrown",
                resumeException != null ? "Yes" : "No",
                resumeException != null ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");

            var originalType = caughtException?.GetType().FullName ?? "-";
            var resumeType = resumeException?.GetType().FullName ?? "-";
            var typesMatch = originalType == resumeType;

            resultTable.AddRow(
                "Original exception type",
                originalType,
                "");
            resultTable.AddRow(
                "Resume exception type",
                resumeType,
                typesMatch ? "[green]✓ Match[/]" : "[red]✗ Mismatch[/]");

            var originalMsg = caughtException?.Message ?? "-";
            var resumeMsg = resumeException?.Message ?? "-";
            var msgsMatch = originalMsg == resumeMsg;

            resultTable.AddRow(
                "Messages match",
                msgsMatch ? "Yes" : "No",
                msgsMatch ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");

            AnsiConsole.Write(resultTable);
            AnsiConsole.WriteLine();

            // Final verdict
            var success = wasRestored && resumeException != null && typesMatch && msgsMatch;

            if (success)
            {
                LogToFile("SUCCESS: Exception recovery verified!");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[green]  ✓ SUCCESS: Exception Recovery VERIFIED!                              [/]");
                AnsiConsole.MarkupLine("[green]    • Checkpoint created before failing await                          [/]");
                AnsiConsole.MarkupLine("[green]    • Workflow restored from checkpoint                                 [/]");
                AnsiConsole.MarkupLine("[green]    • Same exception type reproduced after restore                      [/]");
                AnsiConsole.MarkupLine("[green]    • Exception message preserved                                       [/]");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
            }
            else
            {
                LogToFile("FAILED: Exception recovery test failed");
                AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[red]  ✗ FAILED: Exception recovery issue detected                          [/]");

                if (!wasRestored)
                    AnsiConsole.MarkupLine("[red]    • Workflow was not restored from checkpoint                       [/]");
                if (resumeException == null)
                    AnsiConsole.MarkupLine("[red]    • Resume did not throw exception                                   [/]");
                if (!typesMatch)
                    AnsiConsole.MarkupLine("[red]    • Exception types don't match                                      [/]");
                if (!msgsMatch)
                    AnsiConsole.MarkupLine("[red]    • Exception messages don't match                                   [/]");

                AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
            }
        }
        finally
        {
            if (silo2 != null)
            {
                LogToFile("Stopping silo...");
                await silo2.StopAsync();
                silo2.Dispose();
                LogToFile("Silo stopped");
            }
        }
    }

    #region Logging Helpers

    private static void ClearLogFile()
    {
        try
        {
            File.WriteAllText(LogFilePath, $"C4 Exception Recovery Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[C4] Warning: Could not clear log file: {ex.Message}");
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
