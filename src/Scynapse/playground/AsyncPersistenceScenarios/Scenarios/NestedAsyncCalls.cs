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
/// Scenario C3: Nested Async Calls
///
/// PURPOSE: Verify checkpointing with nested awaits - an outer [Persistable] method
/// that calls multiple inner async methods, with intermediate values checkpointed.
///
/// KEY VALIDATIONS:
/// - Each await point in the outer method generates a checkpoint
/// - Nested return values (intermediate results) are preserved across checkpoints
/// - After crash, restored workflow uses checkpointed intermediate values
/// - Inner methods are NOT [Persistable] - only outer method checkpoints
///
/// TEST FLOW:
/// Phase 1: Compile [Persistable] workflow with nested async calls using Roslyn+
/// Phase 2: Start silo, run workflow, checkpoint after first inner call
/// Phase 3: Crash silo after checkpoint
/// Phase 4: Restart silo, resume workflow
/// Phase 5: Verify intermediate values were restored correctly
///
/// WORKFLOW LOGIC:
/// Outer(x):
///   a = await Inner1(x)      // a = x * 2        → Checkpoint state 0
///   b = await Inner2(a)      // b = a + 10       → Checkpoint state 1
///   c = await Combine(a, b)  // c = a + b        → Checkpoint state 2
///   return c
///
/// With input=5: a=10, b=20, c=30 (result=30)
///
/// LOGGING:
/// - File: c3-nested-async.log (detailed diagnostics)
/// - Console: Summary progress with Spectre.Console tables
///
/// This scenario is SELF-MANAGING - starts and stops its own silo.
/// </summary>
public static class NestedAsyncCalls
{
    // Configuration
    private const string LogFileName = "c3-nested-async.log";
    private static readonly string LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), LogFileName);

    private const string WorkflowId = "c3-nested-async-test";
    private const int InputValue = 5;

    // Expected calculation: a=10, b=20, c=30
    private static readonly int ExpectedA = InputValue * 2;           // 10
    private static readonly int ExpectedB = ExpectedA + 10;           // 20
    private static readonly int ExpectedResult = ExpectedA + ExpectedB; // 30

    // Source code for Roslyn+ compiled workflow with nested async calls
    private const string WorkflowSource = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace NestedAsyncWorkflow
{
    public class TestWorkflow
    {
        private readonly string _workflowId;

        public TestWorkflow(string workflowId)
        {
            _workflowId = workflowId;
        }

        /// <summary>
        /// Outer method with nested async calls.
        /// Only this method is [Persistable] - inner methods are regular async.
        /// Each await generates a checkpoint capturing intermediate values.
        /// </summary>
        [Persistable]
        public async Task<int> OuterCalculation(int x)
        {
            // AWAIT POINT 0: Checkpoint captures 'x', will checkpoint 'a' after return
            var a = await Inner1(x);

            // AWAIT POINT 1: Checkpoint captures 'x', 'a', will checkpoint 'b' after return
            var b = await Inner2(a);

            // AWAIT POINT 2: Checkpoint captures 'x', 'a', 'b', will checkpoint 'c' after return
            var c = await Combine(a, b);

            return c;
        }

        /// <summary>
        /// Inner1: Doubles the input
        /// NOT [Persistable] - just a regular async method
        /// </summary>
        private async Task<int> Inner1(int x)
        {
            await Task.Delay(500); // Simulate async work
            return x * 2;
        }

        /// <summary>
        /// Inner2: Adds 10
        /// NOT [Persistable] - just a regular async method
        /// </summary>
        private async Task<int> Inner2(int a)
        {
            await Task.Delay(500); // Simulate async work
            return a + 10;
        }

        /// <summary>
        /// Combine: Adds two values
        /// NOT [Persistable] - just a regular async method
        /// </summary>
        private async Task<int> Combine(int a, int b)
        {
            await Task.Delay(500); // Simulate async work
            return a + b;
        }
    }
}
";

    public static async Task RunAsync()
    {
        // Initialize logging
        ClearLogFile();

        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C3: Nested Async Calls");
        LogToFile($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        LogToFile($"Workflow ID: {WorkflowId}");
        LogToFile($"Input: {InputValue}");
        LogToFile($"Expected: a={ExpectedA}, b={ExpectedB}, result={ExpectedResult}");
        LogToFile($"Log File: {LogFilePath}");
        LogToFile("=".PadRight(80, '='));

        // Console header
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]  Scenario C3: Nested Async Calls                                      [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Explanation panel
        var explanation = new Panel(
            "[white]Tests checkpointing with nested awaits in a single [[Persistable]] method.\n\n" +
            "[yellow]Workflow Logic:[/]\n" +
            $"  Outer({InputValue}):\n" +
            $"    a = await Inner1({InputValue})  → a = {InputValue} * 2 = {ExpectedA}\n" +
            $"    b = await Inner2({ExpectedA})  → b = {ExpectedA} + 10 = {ExpectedB}\n" +
            $"    c = await Combine({ExpectedA}, {ExpectedB}) → c = {ExpectedResult}\n\n" +
            "[yellow]Key Validations:[/]\n" +
            "- Each await in Outer creates a checkpoint\n" +
            "- Intermediate values (a, b) are hoisted and checkpointed\n" +
            "- After crash/restore, workflow uses restored intermediate values\n" +
            "- Inner methods are NOT [[Persistable]] - only Outer checkpoints[/]")
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
        AnsiConsole.MarkupLine("[cyan]  Scenario C3 Complete                                                  [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine($"[grey]Detailed log: {LogFilePath}[/]");

        // Log final summary to file
        LogToFile("");
        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C3 Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
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
        var compiledAssembly = compiler.CompileAndLoad(WorkflowSource, "NestedAsyncWorkflowAssembly");

        if (compiledAssembly == null)
        {
            var errors = compiler.GetErrorsString();
            LogToFile($"Compilation FAILED:\n{errors}");
            throw new InvalidOperationException($"Compilation failed: {errors}");
        }

        LogToFile($"Compilation SUCCESS: {compiledAssembly.FullName}");
        AnsiConsole.MarkupLine("[green]✓ Compilation successful[/]");

        var workflowType = compiledAssembly.GetType("NestedAsyncWorkflow.TestWorkflow")
            ?? throw new InvalidOperationException("Could not find TestWorkflow type");
        var outerMethod = workflowType.GetMethod("OuterCalculation")
            ?? throw new InvalidOperationException("Could not find OuterCalculation method");

        // Show state machine info
        var nestedTypes = workflowType.GetNestedTypes(BindingFlags.NonPublic);
        var stateMachineType = nestedTypes.FirstOrDefault(t => t.Name.Contains("d__"));
        if (stateMachineType != null)
        {
            LogToFile($"State machine: {stateMachineType.Name}");
            AnsiConsole.MarkupLine($"[grey]  State machine: {stateMachineType.Name}[/]");

            // Show hoisted locals (the intermediate values we're testing)
            var fields = stateMachineType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var hoistedLocals = fields.Where(f => f.Name.Contains("__") && !f.Name.Contains("awaiter")).ToList();
            if (hoistedLocals.Any())
            {
                LogToFile($"  Hoisted locals: {string.Join(", ", hoistedLocals.Select(f => f.Name))}");
                AnsiConsole.MarkupLine($"[grey]  Hoisted locals: {string.Join(", ", hoistedLocals.Select(f => f.Name))}[/]");
            }
        }

        AnsiConsole.WriteLine();

        // ============================================
        // PHASE 1: Start silo and run workflow
        // ============================================
        LogToFile("");
        LogToFile("PHASE 1: Starting silo and running workflow");
        AnsiConsole.MarkupLine("[blue]Phase 1: Starting silo and running workflow...[/]");

        IHost? silo1 = null;
        var checkpointTcs = new TaskCompletionSource();
        var checkpointCount = 0;
        var lastCheckpointState = -1;

        try
        {
            silo1 = await AnsiConsole.Status()
                .StartAsync("Starting Scynapse silo with RavenDB...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11118,
                        gatewayPort: 30008,
                        clusterId: "c3-nested-test",
                        serviceId: "c3-nested-test"
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
            AnsiConsole.MarkupLine("[grey]  Cleared previous workflow state[/]");

            // Set up event tracking - trigger interrupt after first checkpoint
            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    checkpointCount++;
                    lastCheckpointState = e.StateNumber;
                    LogToFile($"CHECKPOINT: state={e.StateNumber}, total={checkpointCount}");

                    // Interrupt after first checkpoint (state 0) to test intermediate value preservation
                    if (e.StateNumber == 0)
                    {
                        checkpointTcs.TrySetResult();
                    }
                }
            }

            persistence.OnCheckpoint += OnCheckpoint;

            // Create workflow instance
            var instance = Activator.CreateInstance(workflowType, WorkflowId)
                ?? throw new InvalidOperationException("Failed to create workflow instance");

            LogToFile($"Starting workflow with input={InputValue}");
            AnsiConsole.MarkupLine($"[yellow]  Starting workflow with input={InputValue}...[/]");

            // Launch workflow with context
            var workflowTask = Task.Run(async () =>
            {
                using (AsyncPersistenceContext.SetCurrent(persistence, WorkflowId))
                {
                    var result = (Task<int>?)outerMethod.Invoke(instance, new object[] { InputValue })
                        ?? throw new InvalidOperationException("Method invocation returned null");
                    return await result;
                }
            });

            // Wait for first checkpoint
            var checkpointOrTimeout = await Task.WhenAny(checkpointTcs.Task, Task.Delay(15000));
            if (checkpointOrTimeout != checkpointTcs.Task)
            {
                throw new TimeoutException("Workflow did not checkpoint in time");
            }

            persistence.OnCheckpoint -= OnCheckpoint;

            LogToFile($"Checkpointed at state {lastCheckpointState}, interrupting...");
            AnsiConsole.MarkupLine($"[green]✓ Checkpointed at state {lastCheckpointState}[/]");

            // Verify checkpoint contains intermediate value 'a'
            var checkpoint = await grain.TryGetCheckpointAsync();
            if (checkpoint?.SerializedStateMachine != null)
            {
                var json = System.Text.Encoding.UTF8.GetString(checkpoint.SerializedStateMachine);
                LogToFile($"Checkpoint JSON: {json}");

                // Check if 'a' value is in checkpoint
                if (json.Contains($"\"<a>5__1\":{ExpectedA}") || json.Contains($"\"<a>5__2\":{ExpectedA}"))
                {
                    AnsiConsole.MarkupLine($"[green]✓ Intermediate value 'a'={ExpectedA} found in checkpoint[/]");
                    LogToFile($"Verified: 'a'={ExpectedA} in checkpoint");
                }
            }
        }
        finally
        {
            if (silo1 != null)
            {
                // ============================================
                // PHASE 2: Stop silo (simulating crash)
                // ============================================
                LogToFile("");
                LogToFile("PHASE 2: Stopping silo (simulating crash)");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[blue]Phase 2: Stopping silo (simulating crash)...[/]");

                await silo1.StopAsync();
                silo1.Dispose();
                silo1 = null;

                LogToFile("Silo stopped - 'crash' complete");
                AnsiConsole.MarkupLine("[red]✓ Silo stopped - workflow 'crashed' after Inner1 completed[/]");
            }
        }

        // Wait before restart
        LogToFile("Waiting 2 seconds before restart...");
        AnsiConsole.MarkupLine("[grey]  Waiting 2 seconds before restart...[/]");
        await Task.Delay(2000);

        // ============================================
        // PHASE 3: Restart silo and resume workflow
        // ============================================
        LogToFile("");
        LogToFile("PHASE 3: Restarting silo and resuming workflow");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Phase 3: Restarting silo and resuming workflow...[/]");

        IHost? silo2 = null;
        var wasRestored = false;
        var restoredFromState = -1;
        var resumeCheckpointCount = 0;

        try
        {
            silo2 = await AnsiConsole.Status()
                .StartAsync("Starting Scynapse silo (restart)...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11118,
                        gatewayPort: 30008,
                        clusterId: "c3-nested-test",
                        serviceId: "c3-nested-test"
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

            // Verify persisted state exists
            var grainFactory2 = silo2.Services.GetRequiredService<IGrainFactory>();
            var grain2 = grainFactory2.GetGrain<IAsyncStatePersistenceGrain>(WorkflowId);
            var hasState = await grain2.HasPersistedStateAsync();
            var checkpoint = hasState ? await grain2.TryGetCheckpointAsync() : null;

            LogToFile($"Persisted state: hasState={hasState}, stateNumber={checkpoint?.StateNumber}");
            AnsiConsole.MarkupLine($"[grey]  Persisted state found at checkpoint {checkpoint?.StateNumber}[/]");

            // Set up event tracking for resume
            void OnRestore(object? sender, RestoreEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    wasRestored = true;
                    restoredFromState = e.RestoredState;
                    LogToFile($"RESTORE: from state {e.RestoredState}");
                }
            }

            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    resumeCheckpointCount++;
                    LogToFile($"RESUME-CHECKPOINT: state={e.StateNumber}");
                }
            }

            persistence2.OnRestore += OnRestore;
            persistence2.OnCheckpoint += OnCheckpoint;

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
                    var result = (Task<int>?)outerMethod.Invoke(resumeInstance, new object[] { 999 })
                        ?? throw new InvalidOperationException("Method invocation returned null");
                    actualResult = await result;
                }
            }
            finally
            {
                persistence2.OnRestore -= OnRestore;
                persistence2.OnCheckpoint -= OnCheckpoint;
            }

            LogToFile($"Workflow completed: result={actualResult}, expected={ExpectedResult}");
            AnsiConsole.MarkupLine($"[green]✓ Workflow completed[/]");

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

            var resultCorrect = actualResult == ExpectedResult;
            var usedDummyInput = actualResult == (999 * 2 + 10 + 999 * 2); // If restore failed completely

            resultTable.AddRow(
                "Was restored",
                wasRestored ? "Yes" : "No",
                wasRestored ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Restored from state",
                restoredFromState >= 0 ? restoredFromState.ToString() : "-",
                restoredFromState >= 0 ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Resume checkpoints",
                resumeCheckpointCount.ToString(),
                "[grey]Info[/]");
            resultTable.AddRow(
                "Expected result",
                ExpectedResult.ToString(),
                "");
            resultTable.AddRow(
                "Actual result",
                actualResult.ToString(),
                resultCorrect ? "[green]✓ Pass[/]" : "[red]✗ Fail[/]");
            resultTable.AddRow(
                "Intermediate 'a'",
                ExpectedA.ToString(),
                resultCorrect ? "[green]✓ Preserved[/]" : "[red]✗ Lost[/]");
            resultTable.AddRow(
                "Intermediate 'b'",
                ExpectedB.ToString(),
                resultCorrect ? "[green]✓ Computed[/]" : "[red]✗ Wrong[/]");

            AnsiConsole.Write(resultTable);
            AnsiConsole.WriteLine();

            // Final verdict
            var success = wasRestored && resultCorrect;

            if (success)
            {
                LogToFile("SUCCESS: Nested async calls with intermediate values preserved!");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[green]  ✓ SUCCESS: Nested Async Calls VERIFIED!                              [/]");
                AnsiConsole.MarkupLine("[green]    • Outer method checkpointed intermediate values                    [/]");
                AnsiConsole.MarkupLine("[green]    • After crash, restored with correct 'a' value                     [/]");
                AnsiConsole.MarkupLine("[green]    • Inner methods re-ran with restored state                         [/]");
                AnsiConsole.MarkupLine("[green]    • Final result matches expected calculation                        [/]");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
            }
            else
            {
                LogToFile("FAILED: Nested async calls test failed");
                AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[red]  ✗ FAILED: Nested async calls issue detected                          [/]");

                if (!wasRestored)
                    AnsiConsole.MarkupLine("[red]    • Workflow was not restored from checkpoint                       [/]");
                if (usedDummyInput)
                    AnsiConsole.MarkupLine("[red]    • Used dummy input 999 - restoration failed completely            [/]");
                if (!resultCorrect && !usedDummyInput)
                    AnsiConsole.MarkupLine("[red]    • Intermediate values not preserved correctly                     [/]");

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
            File.WriteAllText(LogFilePath, $"C3 Nested Async Calls Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[C3] Warning: Could not clear log file: {ex.Message}");
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
