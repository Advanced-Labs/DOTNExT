using System.Reflection;
using AsyncPersistenceScenarios.Helpers;
using AsyncPersistenceScenarios.Services;
using DOTNExT.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scynapse.AsyncPlus;
using Scynapse.AsyncPlus.Services;
using Orleans;
using Spectre.Console;

namespace AsyncPersistenceScenarios.Scenarios;

/// <summary>
/// Scenario R1: Roslyn+ Cross-Session Persistence
///
/// PURPOSE: Test the ACTUAL Roslyn+ generated code (not hand-coded state machines)
/// This verifies the generic TryRestore&lt;T&gt;(ref T) fix for struct boxing.
///
/// DIFFERENCE FROM C1:
/// - C1 uses hand-coded InstrumentedWorkflow (class-based state machine)
/// - R1 uses dynamically compiled [Persistable] workflow via Roslyn+
/// - R1 tests the actual struct state machine that Roslyn generates
///
/// TEST FLOW:
/// 1. Compile [Persistable] workflow using modified Roslyn
/// 2. Start silo, run compiled workflow, checkpoint at state 0
/// 3. Stop silo (simulating crash)
/// 4. Restart silo, workflow should restore from checkpoint via generic TryRestore
/// 5. Verify workflow completes with correct result
///
/// KEY VALIDATIONS:
/// - Modified Roslyn generates TryRestore&lt;T&gt;(ref this, ...) call
/// - Struct state machine is correctly restored (no boxing bug)
/// - Cross-session persistence works with actual Roslyn+ code
///
/// LOGGING:
/// - Roslyn+ logs to: /tmp/dotnext-roslyn-codegen.log (or DOTNEXT_ROSLYN_LOG env var)
/// - Orleans persistence logs to: Console and orleans-grain-storage-debug.log
/// - Scenario logs detailed progress to console
///
/// This scenario is SELF-MANAGING - it starts and stops its own silo.
/// </summary>
public static class RoslynPlusCrossSession
{
    private const string WorkflowId = "roslyn-plus-test-workflow";
    private const string LogFile = "roslyn-plus-scenario.log";

    // The method ID that Roslyn+ uses for persistence (fully qualified method name)
    private const string PersistenceMethodId = "RoslynPlusWorkflows.TestWorkflow.SimpleCalculation";

    // Workflow source code that will be compiled with Roslyn+
    private const string WorkflowSource = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace RoslynPlusWorkflows
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
        /// With [Persistable], Roslyn+ should inject:
        /// - Restoration check at MoveNext start: TryRestore<StateMachine>(ref this, methodId)
        /// - Checkpoint before each await: Checkpoint(this, stateNumber, methodId)
        /// </summary>
        [Persistable]
        public async Task<int> SimpleCalculation(int input)
        {
            Console.WriteLine($""[WORKFLOW] Starting SimpleCalculation with input={input}, workflowId={_workflowId}"");

            // AWAIT POINT 0: State = 0
            Console.WriteLine($""[WORKFLOW] Before await #1 - will checkpoint at state 0"");
            var step1 = await Task.Run(async () =>
            {
                await Task.Delay(200); // Ensure async completion
                var result = input * 2;
                Console.WriteLine($""[WORKFLOW] Step 1 computed: {input} * 2 = {result}"");
                return result;
            });
            Console.WriteLine($""[WORKFLOW] After await #1 - step1={step1}"");

            // AWAIT POINT 1: State = 1
            Console.WriteLine($""[WORKFLOW] Before await #2 - will checkpoint at state 1"");
            var step2 = await Task.Run(async () =>
            {
                await Task.Delay(200); // Ensure async completion
                var result = step1 + 10;
                Console.WriteLine($""[WORKFLOW] Step 2 computed: {step1} + 10 = {result}"");
                return result;
            });
            Console.WriteLine($""[WORKFLOW] After await #2 - step2={step2}"");

            Console.WriteLine($""[WORKFLOW] Completed SimpleCalculation with result={step2}"");
            return step2;
        }
    }
}
";

    public static async Task RunAsync()
    {
        // Initialize logging
        ClearLogFile();
        Log("=".PadRight(70, '='));
        Log("Scenario R1: Roslyn+ Cross-Session Persistence");
        Log("=".PadRight(70, '='));

        AnsiConsole.MarkupLine("[magenta]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[magenta]  Scenario R1: Roslyn+ Cross-Session Persistence                    [/]");
        AnsiConsole.MarkupLine("[magenta]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Scenario explanation
        var explanation = new Panel(
            "[white]This scenario tests ACTUAL Roslyn+ generated code, not hand-coded state machines.\n\n" +
            "[yellow]Why This Matters:[/]\n" +
            "• C1 uses hand-coded class-based state machine (workaround for struct boxing)\n" +
            "• R1 uses Roslyn+-compiled struct state machine (the real deal)\n" +
            "• Tests the generic TryRestore<T>(ref this) fix for struct boxing\n\n" +
            "[yellow]Test Flow:[/]\n" +
            "• Phase 1: Compile workflow with Roslyn+, run to checkpoint\n" +
            "• Phase 2: Stop silo (simulating crash)\n" +
            "• Phase 3: Restart silo, resume from checkpoint\n" +
            "• Phase 4: Verify correct result (struct values restored)\n\n" +
            "[yellow]Logging:[/]\n" +
            "• Roslyn+: /tmp/dotnext-roslyn-codegen.log\n" +
            "• Scenario: roslyn-plus-scenario.log[/]")
            .Header("[green]About This Scenario[/]")
            .BorderColor(Color.Magenta1);
        AnsiConsole.Write(explanation);
        AnsiConsole.WriteLine();

        // Prerequisites
        AnsiConsole.MarkupLine("[yellow]Prerequisites:[/]");
        AnsiConsole.MarkupLine("[grey]  • Modified Roslyn built and referenced[/]");
        AnsiConsole.MarkupLine("[grey]  • RavenDB at: http://127.0.0.1:38880[/]");
        AnsiConsole.MarkupLine("[grey]  • Environment: DOTNEXT_ROSLYN_LOG (optional)[/]");
        AnsiConsole.WriteLine();

        try
        {
            await RunScenarioAsync();
        }
        catch (Exception ex)
        {
            Log($"SCENARIO FAILED: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            AnsiConsole.MarkupLine($"[red]Scenario failed: {Markup.Escape(ex.Message)}[/]");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[red]Inner: {Markup.Escape(ex.InnerException.Message)}[/]");
            }
            AnsiConsole.WriteException(ex);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[magenta]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[magenta]  Scenario R1 Complete                                              [/]");
        AnsiConsole.MarkupLine("[magenta]═══════════════════════════════════════════════════════════════════[/]");

        // Show log file location
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), LogFile);
        AnsiConsole.MarkupLine($"[grey]Scenario log: {logPath}[/]");
        AnsiConsole.MarkupLine($"[grey]Roslyn+ log: {Environment.GetEnvironmentVariable("DOTNEXT_ROSLYN_LOG") ?? Path.Combine(Path.GetTempPath(), "dotnext-roslyn-codegen.log")}[/]");
    }

    private static async Task RunScenarioAsync()
    {
        const int inputValue = 42;
        const int expectedResult = (42 * 2) + 10; // 94

        // ============================================
        // PRE-PHASE: Compile workflow with Roslyn+
        // ============================================
        Log("PRE-PHASE: Compiling workflow with Roslyn+");
        AnsiConsole.MarkupLine("[blue]Pre-Phase: Compiling workflow with Roslyn+...[/]");

        var compiler = new PersistableAsyncCompiler();

        // Show the source being compiled
        Log($"Source code:\n{WorkflowSource}");
        AnsiConsole.MarkupLine("[grey]  Compiling [[Persistable]] workflow...[/]");

        Assembly? compiledAssembly = null;
        try
        {
            compiledAssembly = compiler.CompileAndLoad(WorkflowSource, "RoslynPlusTestWorkflow");
        }
        catch (Exception ex)
        {
            Log($"Compilation exception: {ex.Message}");
            throw;
        }

        if (compiledAssembly == null)
        {
            var errors = compiler.GetErrorsString();
            Log($"Compilation failed: {errors}");
            AnsiConsole.MarkupLine($"[red]Compilation failed:[/]");
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(errors)}[/]");
            throw new InvalidOperationException($"Failed to compile workflow: {errors}");
        }

        Log($"Compilation successful: {compiledAssembly.FullName}");
        AnsiConsole.MarkupLine($"[green]✓ Compilation successful[/]");
        AnsiConsole.MarkupLine($"[grey]  Assembly: {compiledAssembly.GetName().Name}[/]");

        // Log all diagnostics (including warnings)
        var diagnostics = compiler.GetDiagnosticsString();
        if (!string.IsNullOrWhiteSpace(diagnostics))
        {
            Log($"Compilation diagnostics:\n{diagnostics}");
        }

        // Get the workflow type
        var workflowType = compiledAssembly.GetType("RoslynPlusWorkflows.TestWorkflow");
        if (workflowType == null)
        {
            throw new InvalidOperationException("Could not find TestWorkflow type in compiled assembly");
        }

        var simpleCalcMethod = workflowType.GetMethod("SimpleCalculation");
        if (simpleCalcMethod == null)
        {
            throw new InvalidOperationException("Could not find SimpleCalculation method");
        }

        Log($"Found method: {simpleCalcMethod.Name}, ReturnType: {simpleCalcMethod.ReturnType}");
        AnsiConsole.MarkupLine($"[grey]  Found method: {simpleCalcMethod.Name}[/]");

        // Check for state machine type (Roslyn generates nested type ending in 'd__N')
        var nestedTypes = workflowType.GetNestedTypes(BindingFlags.NonPublic);
        var stateMachineType = nestedTypes.FirstOrDefault(t => t.Name.Contains("d__"));
        if (stateMachineType != null)
        {
            Log($"State machine type: {stateMachineType.Name}, IsValueType: {stateMachineType.IsValueType}");
            AnsiConsole.MarkupLine($"[cyan]  State machine: {stateMachineType.Name} (IsStruct: {stateMachineType.IsValueType})[/]");
        }
        else
        {
            Log("WARNING: Could not find state machine nested type");
            AnsiConsole.MarkupLine("[yellow]  WARNING: Could not find state machine nested type[/]");
        }

        AnsiConsole.WriteLine();

        // ============================================
        // PHASE 1: Start silo and run workflow to checkpoint
        // ============================================
        Log("PHASE 1: Starting silo and running workflow to checkpoint");
        AnsiConsole.MarkupLine("[blue]Phase 1: Starting silo and running workflow to checkpoint...[/]");

        IHost? silo1 = null;
        int? checkpointState = null;

        try
        {
            silo1 = await AnsiConsole.Status()
                .StartAsync("Starting Orleans silo with RavenDB...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11116,
                        gatewayPort: 30006,
                        clusterId: "roslyn-plus-test",
                        serviceId: "roslyn-plus-test"
                    );
                    await host.StartAsync();
                    return host;
                });

            Log("Silo started successfully");
            AnsiConsole.MarkupLine("[green]✓ Silo started successfully[/]");

            // Get the persistence service
            var persistence = silo1.Services.GetRequiredService<IAsyncPersistenceService>()
                as ScynapseAsyncPersistenceService;

            if (persistence == null)
            {
                throw new InvalidOperationException("Could not get ScynapseAsyncPersistenceService");
            }

            // Clear any previous state - must clear by the method ID that Roslyn+ uses
            var grainFactory = silo1.Services.GetRequiredService<IGrainFactory>();
            var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(PersistenceMethodId);
            await grain.ClearAsync();
            // Also clear the old workflow ID in case it was used
            var oldGrain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(WorkflowId);
            await oldGrain.ClearAsync();
            Log($"Cleared previous workflow state for {PersistenceMethodId}");
            AnsiConsole.MarkupLine("[grey]  Cleared any previous workflow state[/]");

            // Set up checkpoint tracking
            var checkpointReached = new TaskCompletionSource();
            var checkpointCount = 0;

            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == PersistenceMethodId)
                {
                    checkpointCount++;
                    checkpointState = e.StateNumber;
                    Log($"CHECKPOINT #{checkpointCount}: State {e.StateNumber}");
                    AnsiConsole.MarkupLine($"[cyan]  [[CHECKPOINT]] State {e.StateNumber} - checkpoint #{checkpointCount}[/]");

                    if (checkpointCount == 1)
                    {
                        checkpointReached.TrySetResult();
                    }
                }
            }

            void OnRestore(object? sender, RestoreEventArgs e)
            {
                if (e.MethodId == PersistenceMethodId)
                {
                    Log($"RESTORE: State {e.RestoredState}");
                    AnsiConsole.MarkupLine($"[yellow]  [[RESTORE]] Restored to state {e.RestoredState}[/]");
                }
            }

            persistence.OnCheckpoint += OnCheckpoint;
            persistence.OnRestore += OnRestore;

            Log($"Starting workflow with input={inputValue}");
            AnsiConsole.MarkupLine($"[yellow]  Starting workflow with input={inputValue}...[/]");
            AnsiConsole.MarkupLine("[grey]  Will interrupt after first checkpoint to simulate crash[/]");
            AnsiConsole.WriteLine();

            // Create workflow instance and run
            using (AsyncPersistenceContext.SetCurrent(persistence))
            {
                // Create instance with workflowId
                var workflowInstance = Activator.CreateInstance(workflowType, WorkflowId);
                if (workflowInstance == null)
                {
                    throw new InvalidOperationException("Failed to create workflow instance");
                }

                // Invoke the async method
                var task = (Task<int>?)simpleCalcMethod.Invoke(workflowInstance, new object[] { inputValue });
                if (task == null)
                {
                    throw new InvalidOperationException("Method invocation returned null");
                }

                // Wait for first checkpoint
                var firstCheckpoint = await Task.WhenAny(task, checkpointReached.Task);

                if (checkpointReached.Task.IsCompleted)
                {
                    Log("First checkpoint reached - will 'crash' now");
                    AnsiConsole.MarkupLine("[green]✓ First checkpoint reached![/]");

                    // Read the saved state to verify
                    var hasState = await grain.HasPersistedStateAsync();
                    var checkpoint = hasState ? await grain.TryGetCheckpointAsync() : null;

                    if (checkpoint != null)
                    {
                        Log($"Checkpoint state: {checkpoint.StateNumber}, Time: {checkpoint.CheckpointTimeUtc}");
                        Log($"Serialized state machine size: {checkpoint.SerializedStateMachine?.Length ?? 0} bytes");
                        AnsiConsole.MarkupLine($"[grey]  Checkpoint state: {checkpoint.StateNumber}[/]");
                        AnsiConsole.MarkupLine($"[grey]  Checkpoint timestamp: {checkpoint.CheckpointTimeUtc}[/]");

                        if (checkpoint.SerializedStateMachine != null)
                        {
                            AnsiConsole.MarkupLine($"[grey]  Serialized state machine size: {checkpoint.SerializedStateMachine.Length} bytes[/]");

                            // Log the serialized JSON for debugging
                            var json = System.Text.Encoding.UTF8.GetString(checkpoint.SerializedStateMachine);
                            Log($"Serialized state machine JSON:\n{json}");
                        }
                    }
                }
                else
                {
                    var result = await task;
                    Log($"Workflow completed before checkpoint could be captured: {result}");
                    AnsiConsole.MarkupLine($"[yellow]  Workflow completed too quickly: {result}[/]");
                    AnsiConsole.MarkupLine("[yellow]  (This can happen if awaits complete synchronously)[/]");
                    return;
                }
            }

            persistence.OnCheckpoint -= OnCheckpoint;
            persistence.OnRestore -= OnRestore;
        }
        finally
        {
            if (silo1 != null)
            {
                // ============================================
                // PHASE 2: Stop silo (simulating crash)
                // ============================================
                Log("PHASE 2: Stopping silo (simulating crash)");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[blue]Phase 2: Stopping silo (simulating crash)...[/]");

                await silo1.StopAsync();
                silo1.Dispose();
                silo1 = null;

                Log("Silo stopped - 'crash' complete");
                AnsiConsole.MarkupLine("[red]✓ Silo stopped - process 'crashed'[/]");
            }
        }

        // Wait a moment to simulate restart delay
        Log("Waiting 2 seconds before restart...");
        AnsiConsole.MarkupLine("[grey]  Waiting 2 seconds before restart...[/]");
        await Task.Delay(2000);

        // ============================================
        // PHASE 3: Restart silo and resume workflow
        // ============================================
        Log("PHASE 3: Restarting silo and resuming workflow");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Phase 3: Restarting silo and resuming workflow...[/]");

        IHost? silo2 = null;
        try
        {
            silo2 = await AnsiConsole.Status()
                .StartAsync("Starting Orleans silo (restart)...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11116,
                        gatewayPort: 30006,
                        clusterId: "roslyn-plus-test",
                        serviceId: "roslyn-plus-test"
                    );
                    await host.StartAsync();
                    return host;
                });

            Log("Silo restarted successfully");
            AnsiConsole.MarkupLine("[green]✓ Silo restarted successfully[/]");

            // Get the persistence service
            var persistence2 = silo2.Services.GetRequiredService<IAsyncPersistenceService>()
                as ScynapseAsyncPersistenceService;

            if (persistence2 == null)
            {
                throw new InvalidOperationException("Could not get ScynapseAsyncPersistenceService");
            }

            // Check for saved state - must use PersistenceMethodId (what Roslyn+ uses)
            var grainFactory = silo2.Services.GetRequiredService<IGrainFactory>();
            var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(PersistenceMethodId);
            var hasState = await grain.HasPersistedStateAsync();

            Log($"Persisted state found: {hasState}");
            AnsiConsole.MarkupLine($"[cyan]  Persisted state found: {hasState}[/]");

            if (hasState)
            {
                var checkpoint = await grain.TryGetCheckpointAsync();
                if (checkpoint != null)
                {
                    Log($"Will resume from checkpoint state: {checkpoint.StateNumber}");
                    AnsiConsole.MarkupLine($"[cyan]  Resuming from checkpoint state: {checkpoint.StateNumber}[/]");
                    AnsiConsole.MarkupLine($"[cyan]  Checkpoint timestamp: {checkpoint.CheckpointTimeUtc}[/]");
                }
            }
            else
            {
                Log("No persisted state found - workflow will start from beginning");
                AnsiConsole.MarkupLine("[yellow]  No persisted state found - workflow will start from beginning[/]");
            }

            // Track events during resume
            var resumeCheckpoints = 0;
            var wasRestored = false;
            var restoredState = -1;

            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == PersistenceMethodId)
                {
                    resumeCheckpoints++;
                    Log($"CHECKPOINT during resume: State {e.StateNumber}");
                    AnsiConsole.MarkupLine($"[cyan]  [[CHECKPOINT]] State {e.StateNumber} during resume[/]");
                }
            }

            void OnRestore(object? sender, RestoreEventArgs e)
            {
                if (e.MethodId == PersistenceMethodId)
                {
                    wasRestored = true;
                    restoredState = e.RestoredState;
                    Log($"RESTORE during resume: State {e.RestoredState}");
                    AnsiConsole.MarkupLine($"[yellow]  [[RESTORE]] Restored to state {e.RestoredState}[/]");
                }
            }

            void OnComplete(object? sender, CompleteEventArgs e)
            {
                if (e.MethodId == PersistenceMethodId)
                {
                    Log($"COMPLETE: Result = {e.Result}");
                    AnsiConsole.MarkupLine($"[green]  [[COMPLETE]] Workflow finished with result: {e.Result}[/]");
                }
            }

            persistence2.OnCheckpoint += OnCheckpoint;
            persistence2.OnRestore += OnRestore;
            persistence2.OnComplete += OnComplete;

            Log("Resuming workflow...");
            AnsiConsole.MarkupLine("[yellow]  Resuming workflow...[/]");

            // Resume the workflow
            int actualResult;
            using (AsyncPersistenceContext.SetCurrent(persistence2))
            {
                var workflowInstance = Activator.CreateInstance(workflowType, WorkflowId);
                if (workflowInstance == null)
                {
                    throw new InvalidOperationException("Failed to create workflow instance");
                }

                // Invoke with dummy input - restoration should overwrite it
                var task = (Task<int>?)simpleCalcMethod.Invoke(workflowInstance, new object[] { 999 });
                if (task == null)
                {
                    throw new InvalidOperationException("Method invocation returned null");
                }

                actualResult = await task;
            }

            Log($"Workflow completed with result: {actualResult}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]✓ Workflow completed with result: {actualResult}[/]");

            // ============================================
            // PHASE 4: Verify results
            // ============================================
            Log("PHASE 4: Verification");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Phase 4: Verification...[/]");

            var resultTable = new Table();
            resultTable.AddColumn("Metric");
            resultTable.AddColumn("Value");
            resultTable.AddColumn("Status");

            resultTable.AddRow("Input value", inputValue.ToString(), "[grey]Provided at start[/]");
            resultTable.AddRow("Expected result", expectedResult.ToString(), "[grey](input*2)+10[/]");
            resultTable.AddRow("Actual result", actualResult.ToString(),
                actualResult == expectedResult ? "[green]✓ Match[/]" : "[red]✗ Mismatch[/]");
            resultTable.AddRow("Was restored", wasRestored.ToString(),
                wasRestored ? "[green]✓ Yes[/]" : "[red]✗ No[/]");
            resultTable.AddRow("Restored from state", restoredState.ToString(),
                restoredState >= 0 ? "[green]✓[/]" : "[yellow]N/A[/]");
            resultTable.AddRow("Checkpoints during resume", resumeCheckpoints.ToString(),
                "[grey]Re-run creates checkpoints[/]");
            resultTable.AddRow("State machine type", stateMachineType?.Name ?? "unknown",
                stateMachineType?.IsValueType == true ? "[cyan]struct (Roslyn+ default)[/]" : "[yellow]class[/]");

            AnsiConsole.Write(resultTable);
            AnsiConsole.WriteLine();

            // Log final results
            Log("=".PadRight(50, '='));
            Log($"FINAL RESULTS:");
            Log($"  Input: {inputValue}");
            Log($"  Expected: {expectedResult}");
            Log($"  Actual: {actualResult}");
            Log($"  Was Restored: {wasRestored}");
            Log($"  Restored State: {restoredState}");
            Log($"  Checkpoints During Resume: {resumeCheckpoints}");
            Log($"  State Machine Is Struct: {stateMachineType?.IsValueType}");
            Log("=".PadRight(50, '='));

            // Conclusions
            // Success = correct result + restoration triggered
            // Note: Workflow re-runs from beginning after restoration (awaiters can't be serialized)
            // so checkpoints are created during resume - this is expected behavior
            if (actualResult == expectedResult && wasRestored && restoredState >= 0)
            {
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════[/]");
                AnsiConsole.MarkupLine("[green]  ✓ SUCCESS: Roslyn+ Cross-Session Persistence VERIFIED!           [/]");
                AnsiConsole.MarkupLine("[green]    • Roslyn+ generated code correctly persisted                   [/]");
                AnsiConsole.MarkupLine("[green]    • Field values restored from checkpoint                        [/]");
                AnsiConsole.MarkupLine("[green]    • Workflow re-ran with correct restored values                 [/]");
                AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════[/]");
                Log("SUCCESS: All validations passed!");
            }
            else if (actualResult == expectedResult)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ PARTIAL SUCCESS: Result is correct[/]");
                if (!wasRestored)
                {
                    AnsiConsole.MarkupLine("[yellow]  BUT restoration was not triggered - may have run from beginning[/]");
                    Log("PARTIAL: Result correct but no restoration triggered");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]  Restoration triggered but unexpected state[/]");
                    Log("PARTIAL: Result correct but unexpected restoration state");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ FAILED: Result mismatch or restoration failed[/]");
                if (actualResult == (999 * 2) + 10)
                {
                    AnsiConsole.MarkupLine("[red]  Result matches WRONG input (999) - struct boxing bug![/]");
                    AnsiConsole.MarkupLine("[red]  The generic TryRestore<T>(ref this) may not be working.[/]");
                    Log("FAILED: Struct boxing bug detected - got 2008 instead of 94");
                }
                else
                {
                    Log($"FAILED: Unknown error - got {actualResult} instead of {expectedResult}");
                }
            }

            persistence2.OnCheckpoint -= OnCheckpoint;
            persistence2.OnRestore -= OnRestore;
            persistence2.OnComplete -= OnComplete;
        }
        finally
        {
            if (silo2 != null)
            {
                Log("Stopping silo...");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
                await silo2.StopAsync();
                silo2.Dispose();
                Log("Silo stopped");
                AnsiConsole.MarkupLine("[green]✓ Silo stopped[/]");
            }
        }
    }

    #region Logging Helpers

    private static void ClearLogFile()
    {
        try
        {
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), LogFile);
            File.WriteAllText(logPath, $"Roslyn+ Scenario Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }
        catch
        {
            // Ignore logging errors
        }
    }

    private static void Log(string message)
    {
        try
        {
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), LogFile);
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            File.AppendAllText(logPath, $"[{timestamp}] {message}\n");
        }
        catch
        {
            // Ignore logging errors
        }

        // Also write to console for immediate feedback during debugging
        Console.WriteLine($"[R1] {message}");
    }

    #endregion
}
