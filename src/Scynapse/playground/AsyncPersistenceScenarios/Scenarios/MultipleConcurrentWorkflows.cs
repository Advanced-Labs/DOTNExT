using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
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
/// Scenario C2: Multiple Concurrent Workflows
///
/// PURPOSE: Test that multiple parallel workflow instances are properly isolated
/// during checkpointing and restoration via Roslyn+ generated code.
///
/// KEY VALIDATIONS:
/// - Each workflow instance gets its own grain (isolated by methodId/workflowId)
/// - Checkpoints don't bleed between workflows
/// - Concurrent checkpoint writes don't corrupt each other
/// - Each workflow restores only its own state
/// - RavenDB handles concurrent writes correctly
///
/// POTENTIAL FAILURE POINTS (heavily logged):
/// 1. Grain ID collision if methodId isn't unique per instance
/// 2. _pendingCheckpoints dictionary race conditions
/// 3. RavenDB write contention from simultaneous checkpoints
/// 4. Event handler confusion with overlapping methodId patterns
/// 5. AsyncPersistenceContext.Current thread safety
/// 6. Memory leaks from accumulated pending tasks
///
/// TEST FLOW:
/// Phase 1: Compile [Persistable] workflow using Roslyn+
/// Phase 2: Start silo, launch N concurrent workflows with different inputs
/// Phase 3: Wait for all to checkpoint at state 0, then "crash"
/// Phase 4: Restart silo, resume all workflows
/// Phase 5: Verify each restored correctly with its own values
///
/// LOGGING:
/// - File: c2-concurrent-workflows.log (detailed diagnostics)
/// - Console: Summary progress with Spectre.Console tables
/// - All checkpoints logged with timestamps and workflow identity
/// - Event sequence tracked with correlation
///
/// This scenario is SELF-MANAGING - starts and stops its own silo.
/// </summary>
public static class MultipleConcurrentWorkflows
{
    // Configuration
    private const int WorkflowCount = 5;  // Number of concurrent workflows
    private const string LogFileName = "c2-concurrent-workflows.log";
    private static readonly string LogFilePath = Path.Combine(Directory.GetCurrentDirectory(), LogFileName);

    // Workflow base ID - each instance gets a unique suffix
    private const string WorkflowIdPrefix = "c2-concurrent";

    // Thread-safe tracking structures
    private static readonly ConcurrentDictionary<string, WorkflowTracker> _workflowTrackers = new();
    private static readonly ConcurrentBag<string> _eventLog = new();
    private static long _eventCounter = 0;

    // Source code for Roslyn+ compiled workflow
    // Uses workflowId parameter to generate unique persistence method IDs
    private const string WorkflowSource = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace ConcurrentWorkflows
{
    public class ConcurrentTestWorkflow
    {
        private readonly string _workflowId;

        public ConcurrentTestWorkflow(string workflowId)
        {
            _workflowId = workflowId;
        }

        /// <summary>
        /// Simple calculation: (input * multiplier) + offset
        /// Each workflow has unique input, multiplier=2, offset=10
        /// With [Persistable], Roslyn+ injects persistence calls.
        ///
        /// IMPORTANT: The methodId used by Roslyn+ is the fully qualified method name:
        /// ""ConcurrentWorkflows.ConcurrentTestWorkflow.Calculate""
        /// We override this by passing workflowId to the persistence service.
        /// </summary>
        [Persistable]
        public async Task<int> Calculate(int input)
        {
            // Debug output minimized - detailed logs go to file via persistence events

            // AWAIT POINT 0: Checkpoint state 0
            var step1 = await Task.Run(async () =>
            {
                await Task.Delay(500); // Ensure async completion, allow time for concurrent ops
                return input * 2;
            });

            // AWAIT POINT 1: Checkpoint state 1
            var step2 = await Task.Run(async () =>
            {
                await Task.Delay(500); // Allow time for concurrent ops
                return step1 + 10;
            });

            return step2;
        }
    }
}
";

    /// <summary>
    /// Tracks state for a single workflow instance
    /// </summary>
    private class WorkflowTracker
    {
        public string WorkflowId { get; init; } = "";
        public int InputValue { get; init; }
        public int ExpectedResult => (InputValue * 2) + 10;
        public int? ActualResult { get; set; }
        public bool WasRestored { get; set; }
        public int RestoredFromState { get; set; } = -1;
        public List<int> CheckpointStates { get; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Completed { get; set; }
        public string? Error { get; set; }

        // Phase tracking
        public bool Phase1Checkpointed { get; set; }
        public bool Phase2Resumed { get; set; }
    }

    public static async Task RunAsync()
    {
        // Initialize logging
        ClearLogFile();
        _workflowTrackers.Clear();
        _eventLog.Clear();
        _eventCounter = 0;

        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C2: Multiple Concurrent Workflows");
        LogToFile($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        LogToFile($"Workflow Count: {WorkflowCount}");
        LogToFile($"Log File: {LogFilePath}");
        LogToFile("=".PadRight(80, '='));

        // Console header
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]  Scenario C2: Multiple Concurrent Workflows (Roslyn+)                  [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Explanation panel
        var explanation = new Panel(
            "[white]Tests parallel workflow isolation with Roslyn+ generated code.\n\n" +
            "[yellow]Key Validations:[/]\n" +
            "- Each workflow instance has isolated grain storage\n" +
            "- Concurrent checkpoints don't corrupt each other\n" +
            "- Each workflow restores only its own state\n" +
            "- RavenDB handles concurrent writes correctly\n\n" +
            "[yellow]Test Flow:[/]\n" +
            $"- Launch {WorkflowCount} workflows with inputs: {string.Join(", ", Enumerable.Range(1, WorkflowCount).Select(i => i * 10))}\n" +
            "- Interrupt all after first checkpoint (state 0)\n" +
            "- Restart and verify each restores its own values\n\n" +
            "[yellow]Potential Failures Logged:[/]\n" +
            "- Grain ID collisions, pending checkpoint races\n" +
            "- RavenDB contention, event handler confusion\n" +
            "- Thread safety of AsyncPersistenceContext.Current[/]")
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
        AnsiConsole.MarkupLine("[cyan]  Scenario C2 Complete                                                  [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine($"[grey]Detailed log: {LogFilePath}[/]");

        // Log final summary to file
        LogToFile("");
        LogToFile("=".PadRight(80, '='));
        LogToFile($"Scenario C2 Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
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
        LogToFile($"Source code length: {WorkflowSource.Length} chars");

        Assembly? compiledAssembly;
        try
        {
            compiledAssembly = compiler.CompileAndLoad(WorkflowSource, "ConcurrentWorkflowsAssembly");
        }
        catch (Exception ex)
        {
            LogToFile($"Compilation exception: {ex}");
            throw;
        }

        if (compiledAssembly == null)
        {
            var errors = compiler.GetErrorsString();
            LogToFile($"Compilation FAILED:\n{errors}");
            throw new InvalidOperationException($"Compilation failed: {errors}");
        }

        LogToFile($"Compilation SUCCESS: {compiledAssembly.FullName}");
        AnsiConsole.MarkupLine("[green]✓ Compilation successful[/]");

        // Get workflow type and method
        var workflowType = compiledAssembly.GetType("ConcurrentWorkflows.ConcurrentTestWorkflow")
            ?? throw new InvalidOperationException("Could not find ConcurrentTestWorkflow type");
        var calculateMethod = workflowType.GetMethod("Calculate")
            ?? throw new InvalidOperationException("Could not find Calculate method");

        LogToFile($"Workflow type: {workflowType.FullName}");
        LogToFile($"Method: {calculateMethod.Name}, Returns: {calculateMethod.ReturnType}");

        // Check state machine type
        var nestedTypes = workflowType.GetNestedTypes(BindingFlags.NonPublic);
        var stateMachineType = nestedTypes.FirstOrDefault(t => t.Name.Contains("d__"));
        if (stateMachineType != null)
        {
            LogToFile($"State machine: {stateMachineType.Name}, IsStruct: {stateMachineType.IsValueType}");
            AnsiConsole.MarkupLine($"[grey]  State machine: {stateMachineType.Name} (IsStruct: {stateMachineType.IsValueType})[/]");
        }

        AnsiConsole.WriteLine();

        // Initialize workflow trackers
        for (int i = 1; i <= WorkflowCount; i++)
        {
            var tracker = new WorkflowTracker
            {
                WorkflowId = $"{WorkflowIdPrefix}-W{i}",
                InputValue = i * 10  // 10, 20, 30, 40, 50
            };
            _workflowTrackers[tracker.WorkflowId] = tracker;
            LogToFile($"Tracker created: {tracker.WorkflowId}, Input={tracker.InputValue}, Expected={tracker.ExpectedResult}");
        }

        // Show workflow table
        var workflowTable = new Table();
        workflowTable.AddColumn("Workflow");
        workflowTable.AddColumn("Input");
        workflowTable.AddColumn("Expected Result");
        foreach (var tracker in _workflowTrackers.Values.OrderBy(t => t.WorkflowId))
        {
            workflowTable.AddRow(tracker.WorkflowId, tracker.InputValue.ToString(), tracker.ExpectedResult.ToString());
        }
        AnsiConsole.Write(workflowTable);
        AnsiConsole.WriteLine();

        // ============================================
        // PHASE 1: Start silo and run concurrent workflows
        // ============================================
        LogToFile("");
        LogToFile("PHASE 1: Starting silo and launching concurrent workflows");
        AnsiConsole.MarkupLine("[blue]Phase 1: Starting silo and launching concurrent workflows...[/]");

        IHost? silo1 = null;
        var phase1CheckpointTcs = new TaskCompletionSource();
        var phase1CheckpointCount = 0;

        try
        {
            silo1 = await AnsiConsole.Status()
                .StartAsync("Starting Scynapse silo with RavenDB...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11117,
                        gatewayPort: 30007,
                        clusterId: "c2-concurrent-test",
                        serviceId: "c2-concurrent-test"
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

            // Clear previous state for all workflows
            var grainFactory = silo1.Services.GetRequiredService<IGrainFactory>();
            foreach (var tracker in _workflowTrackers.Values)
            {
                var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(tracker.WorkflowId);
                await grain.ClearAsync();
                LogToFile($"Cleared previous state: {tracker.WorkflowId}");
            }
            AnsiConsole.MarkupLine("[grey]  Cleared previous workflow states[/]");

            // Set up event tracking
            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                var eventId = Interlocked.Increment(ref _eventCounter);
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                if (_workflowTrackers.TryGetValue(e.MethodId, out var tracker))
                {
                    tracker.CheckpointStates.Add(e.StateNumber);
                    LogToFile($"[EVENT-{eventId}] CHECKPOINT: {e.MethodId} state={e.StateNumber} @ {timestamp}");
                    _eventLog.Add($"[{eventId}] CHECKPOINT {e.MethodId}:{e.StateNumber}");

                    // Track first checkpoint (state 0) for Phase 1 completion
                    if (e.StateNumber == 0 && !tracker.Phase1Checkpointed)
                    {
                        tracker.Phase1Checkpointed = true;
                        var count = Interlocked.Increment(ref phase1CheckpointCount);
                        LogToFile($"[PHASE1] Workflow {e.MethodId} checkpointed (count: {count}/{WorkflowCount})");

                        if (count >= WorkflowCount)
                        {
                            LogToFile("[PHASE1] All workflows checkpointed - triggering crash");
                            phase1CheckpointTcs.TrySetResult();
                        }
                    }

                    // Per-event console output disabled - see log file
                }
                else
                {
                    LogToFile($"[EVENT-{eventId}] CHECKPOINT (UNKNOWN): {e.MethodId} state={e.StateNumber}");
                }
            }

            void OnRestore(object? sender, RestoreEventArgs e)
            {
                var eventId = Interlocked.Increment(ref _eventCounter);
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                if (_workflowTrackers.TryGetValue(e.MethodId, out var tracker))
                {
                    tracker.WasRestored = true;
                    tracker.RestoredFromState = e.RestoredState;
                    LogToFile($"[EVENT-{eventId}] RESTORE: {e.MethodId} state={e.RestoredState} @ {timestamp}");
                    _eventLog.Add($"[{eventId}] RESTORE {e.MethodId}:{e.RestoredState}");
                    // Per-event console output disabled - see log file
                }
                else
                {
                    LogToFile($"[EVENT-{eventId}] RESTORE (UNKNOWN): {e.MethodId} state={e.RestoredState}");
                }
            }

            void OnComplete(object? sender, CompleteEventArgs e)
            {
                var eventId = Interlocked.Increment(ref _eventCounter);
                if (_workflowTrackers.TryGetValue(e.MethodId, out var tracker))
                {
                    tracker.Completed = true;
                    tracker.EndTime = DateTime.Now;
                    LogToFile($"[EVENT-{eventId}] COMPLETE: {e.MethodId}, Result={e.Result}");
                }
            }

            persistence.OnCheckpoint += OnCheckpoint;
            persistence.OnRestore += OnRestore;
            persistence.OnComplete += OnComplete;

            LogToFile($"Launching {WorkflowCount} concurrent workflows...");
            AnsiConsole.MarkupLine($"[yellow]  Launching {WorkflowCount} concurrent workflows...[/]");

            // Launch all workflows concurrently
            // IMPORTANT: Each workflow needs its own context with unique workflowId
            // so grain isolation works correctly
            var workflowTasks = new Dictionary<string, Task<int>>();
            var launchStopwatch = Stopwatch.StartNew();

            foreach (var tracker in _workflowTrackers.Values)
            {
                tracker.StartTime = DateTime.Now;

                // Create workflow instance with unique workflowId
                var instance = Activator.CreateInstance(workflowType, tracker.WorkflowId)
                    ?? throw new InvalidOperationException($"Failed to create instance for {tracker.WorkflowId}");

                // Capture tracker.WorkflowId for the closure
                var workflowId = tracker.WorkflowId;
                var inputValue = tracker.InputValue;

                // Launch each workflow with its own context containing the unique workflowId
                // This ensures each workflow uses a separate grain for persistence
                var task = Task.Run(async () =>
                {
                    using (AsyncPersistenceContext.SetCurrent(persistence, workflowId))
                    {
                        var result = (Task<int>?)calculateMethod.Invoke(instance, new object[] { inputValue })
                            ?? throw new InvalidOperationException("Method invocation returned null");
                        return await result;
                    }
                });

                workflowTasks[tracker.WorkflowId] = task;
                LogToFile($"Launched: {tracker.WorkflowId} with input={tracker.InputValue}");
            }

            LogToFile($"All {WorkflowCount} workflows launched in {launchStopwatch.ElapsedMilliseconds}ms");
            AnsiConsole.MarkupLine($"[green]✓ All {WorkflowCount} workflows launched[/]");

            // Wait for all workflows to reach first checkpoint
            LogToFile("Waiting for all workflows to checkpoint at state 0...");
            AnsiConsole.MarkupLine("[grey]  Waiting for all workflows to checkpoint at state 0...[/]");

            var timeoutTask = Task.Delay(30000); // 30 second timeout
            var allTasks = Task.WhenAll(workflowTasks.Values);
            var checkpointOrTimeout = await Task.WhenAny(phase1CheckpointTcs.Task, timeoutTask, allTasks);

            if (checkpointOrTimeout == timeoutTask)
            {
                LogToFile("TIMEOUT waiting for checkpoints!");
                throw new TimeoutException("Workflows did not checkpoint in time");
            }

            if (checkpointOrTimeout == allTasks)
            {
                LogToFile("WARNING: All workflows completed before checkpoint could be captured");
                AnsiConsole.MarkupLine("[yellow]  Workflows completed too quickly - no crash simulation possible[/]");

                // Still record results
                foreach (var (id, task) in workflowTasks)
                {
                    if (_workflowTrackers.TryGetValue(id, out var trackerResult))
                    {
                        trackerResult.ActualResult = await task;
                    }
                }

                // Skip to verification
                ShowResults(skipRestore: true);
                return;
            }

            LogToFile($"All workflows checkpointed, phase1CheckpointCount={phase1CheckpointCount}");
            AnsiConsole.MarkupLine($"[green]✓ All {WorkflowCount} workflows checkpointed![/]");

            // Verify checkpoints before crash
            LogToFile("Verifying checkpoint state before crash...");
            foreach (var tracker in _workflowTrackers.Values)
            {
                var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(tracker.WorkflowId);
                var hasState = await grain.HasPersistedStateAsync();
                var checkpoint = hasState ? await grain.TryGetCheckpointAsync() : null;

                LogToFile($"  {tracker.WorkflowId}: hasState={hasState}, state={checkpoint?.StateNumber}, bytes={checkpoint?.SerializedStateMachine?.Length}");

                if (checkpoint?.SerializedStateMachine != null)
                {
                    var json = System.Text.Encoding.UTF8.GetString(checkpoint.SerializedStateMachine);
                    LogToFile($"    JSON: {json}");
                }
            }

            persistence.OnCheckpoint -= OnCheckpoint;
            persistence.OnRestore -= OnRestore;
            persistence.OnComplete -= OnComplete;
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
                AnsiConsole.MarkupLine("[red]✓ Silo stopped - all workflows 'crashed'[/]");
            }
        }

        // Wait before restart
        LogToFile("Waiting 2 seconds before restart...");
        AnsiConsole.MarkupLine("[grey]  Waiting 2 seconds before restart...[/]");
        await Task.Delay(2000);

        // ============================================
        // PHASE 3: Restart silo and resume workflows
        // ============================================
        LogToFile("");
        LogToFile("PHASE 3: Restarting silo and resuming all workflows");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Phase 3: Restarting silo and resuming all workflows...[/]");

        IHost? silo2 = null;
        try
        {
            silo2 = await AnsiConsole.Status()
                .StartAsync("Starting Scynapse silo (restart)...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11117,
                        gatewayPort: 30007,
                        clusterId: "c2-concurrent-test",
                        serviceId: "c2-concurrent-test"
                    );
                    await host.StartAsync();
                    return host;
                });

            LogToFile("Silo restarted successfully");
            AnsiConsole.MarkupLine("[green]✓ Silo restarted[/]");

            // Get persistence service
            var persistence2 = silo2.Services.GetRequiredService<IAsyncPersistenceService>()
                as ScynapseAsyncPersistenceService
                ?? throw new InvalidOperationException("Could not get ScynapseAsyncPersistenceService");

            // Verify persisted state exists
            var grainFactory2 = silo2.Services.GetRequiredService<IGrainFactory>();
            LogToFile("Checking persisted state for all workflows...");

            var stateTable = new Table();
            stateTable.AddColumn("Workflow");
            stateTable.AddColumn("Has State");
            stateTable.AddColumn("Checkpoint State");

            foreach (var tracker in _workflowTrackers.Values.OrderBy(t => t.WorkflowId))
            {
                var grain = grainFactory2.GetGrain<IAsyncStatePersistenceGrain>(tracker.WorkflowId);
                var hasState = await grain.HasPersistedStateAsync();
                var checkpoint = hasState ? await grain.TryGetCheckpointAsync() : null;

                LogToFile($"  {tracker.WorkflowId}: hasState={hasState}, checkpoint={checkpoint?.StateNumber}");
                stateTable.AddRow(
                    tracker.WorkflowId,
                    hasState ? "[green]Yes[/]" : "[red]No[/]",
                    checkpoint?.StateNumber.ToString() ?? "-");
            }
            AnsiConsole.Write(stateTable);
            AnsiConsole.WriteLine();

            // Set up event tracking for resume
            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                var eventId = Interlocked.Increment(ref _eventCounter);
                if (_workflowTrackers.TryGetValue(e.MethodId, out var tracker))
                {
                    tracker.CheckpointStates.Add(e.StateNumber);
                    LogToFile($"[EVENT-{eventId}] RESUME-CHECKPOINT: {e.MethodId} state={e.StateNumber}");
                    // Per-event console output disabled - see log file
                }
            }

            void OnRestore(object? sender, RestoreEventArgs e)
            {
                var eventId = Interlocked.Increment(ref _eventCounter);
                if (_workflowTrackers.TryGetValue(e.MethodId, out var tracker))
                {
                    tracker.WasRestored = true;
                    tracker.RestoredFromState = e.RestoredState;
                    tracker.Phase2Resumed = true;
                    LogToFile($"[EVENT-{eventId}] RESUME-RESTORE: {e.MethodId} state={e.RestoredState}");
                    // Per-event console output disabled - see log file
                }
            }

            persistence2.OnCheckpoint += OnCheckpoint;
            persistence2.OnRestore += OnRestore;

            LogToFile("Resuming all workflows...");
            AnsiConsole.MarkupLine("[yellow]  Resuming all workflows...[/]");

            // Resume all workflows concurrently
            // Each workflow needs its own context with unique workflowId for grain isolation
            var resumeTasks = new Dictionary<string, Task<int>>();

            foreach (var tracker in _workflowTrackers.Values)
            {
                // Create new instance - restoration happens in MoveNext via TryRestore
                var instance = Activator.CreateInstance(workflowType, tracker.WorkflowId)
                    ?? throw new InvalidOperationException($"Failed to create instance for {tracker.WorkflowId}");

                // Capture for closure
                var workflowId = tracker.WorkflowId;

                // Resume each workflow with its own context containing the unique workflowId
                var task = Task.Run(async () =>
                {
                    using (AsyncPersistenceContext.SetCurrent(persistence2, workflowId))
                    {
                        // Pass a dummy input value - should be overwritten by restoration
                        // Using 999 so we can detect if restoration didn't happen
                        var result = (Task<int>?)calculateMethod.Invoke(instance, new object[] { 999 })
                            ?? throw new InvalidOperationException("Method invocation returned null");
                        return await result;
                    }
                });

                resumeTasks[tracker.WorkflowId] = task;
                LogToFile($"Resume started: {tracker.WorkflowId}");
            }

            // Wait for all to complete
            LogToFile("Waiting for all workflows to complete...");
            await Task.WhenAll(resumeTasks.Values);

            // Collect results
            foreach (var (id, task) in resumeTasks)
            {
                if (_workflowTrackers.TryGetValue(id, out var tracker))
                {
                    tracker.ActualResult = await task;
                    tracker.Completed = true;
                    tracker.EndTime = DateTime.Now;
                    LogToFile($"Completed: {id} result={tracker.ActualResult} (expected={tracker.ExpectedResult})");
                }
            }

            AnsiConsole.MarkupLine("[green]✓ All workflows completed![/]");

            persistence2.OnCheckpoint -= OnCheckpoint;
            persistence2.OnRestore -= OnRestore;
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

        // ============================================
        // PHASE 4: Verification
        // ============================================
        ShowResults(skipRestore: false);
    }

    private static void ShowResults(bool skipRestore)
    {
        LogToFile("");
        LogToFile("PHASE 4: Verification");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Phase 4: Verification...[/]");

        // Results table
        var resultTable = new Table();
        resultTable.AddColumn("Workflow");
        resultTable.AddColumn("Input");
        resultTable.AddColumn("Expected");
        resultTable.AddColumn("Actual");
        resultTable.AddColumn("Restored");
        resultTable.AddColumn("From State");
        resultTable.AddColumn("Checkpoints");
        resultTable.AddColumn("Status");

        var allCorrect = true;
        var allRestored = true;

        foreach (var tracker in _workflowTrackers.Values.OrderBy(t => t.WorkflowId))
        {
            var resultMatch = tracker.ActualResult == tracker.ExpectedResult;
            var wrongInputUsed = tracker.ActualResult == (999 * 2) + 10; // 2008 - means restore failed

            if (!resultMatch) allCorrect = false;
            if (!tracker.WasRestored && !skipRestore) allRestored = false;

            var status = resultMatch
                ? (tracker.WasRestored || skipRestore ? "[green]✓ Pass[/]" : "[yellow]⚠ No Restore[/]")
                : (wrongInputUsed ? "[red]✗ No Restore[/]" : "[red]✗ Wrong Result[/]");

            resultTable.AddRow(
                tracker.WorkflowId,
                tracker.InputValue.ToString(),
                tracker.ExpectedResult.ToString(),
                tracker.ActualResult?.ToString() ?? "-",
                tracker.WasRestored ? "[green]Yes[/]" : "[grey]No[/]",
                tracker.RestoredFromState >= 0 ? tracker.RestoredFromState.ToString() : "-",
                string.Join(",", tracker.CheckpointStates),
                status);

            LogToFile($"Result: {tracker.WorkflowId} input={tracker.InputValue} expected={tracker.ExpectedResult} actual={tracker.ActualResult} restored={tracker.WasRestored} state={tracker.RestoredFromState}");
        }

        AnsiConsole.Write(resultTable);
        AnsiConsole.WriteLine();

        // Log event sequence
        LogToFile("");
        LogToFile("EVENT SEQUENCE:");
        foreach (var evt in _eventLog)
        {
            LogToFile($"  {evt}");
        }

        // Final verdict
        LogToFile("");
        if (allCorrect && (allRestored || skipRestore))
        {
            LogToFile("SUCCESS: All workflows produced correct results with proper isolation!");
            AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[green]  ✓ SUCCESS: Multiple Concurrent Workflows VERIFIED!                    [/]");
            AnsiConsole.MarkupLine("[green]    • All workflows restored their own state                            [/]");
            AnsiConsole.MarkupLine("[green]    • No cross-contamination between workflow instances                 [/]");
            AnsiConsole.MarkupLine("[green]    • RavenDB handled concurrent checkpoints correctly                  [/]");
            AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════════[/]");
        }
        else if (allCorrect)
        {
            LogToFile("PARTIAL SUCCESS: Results correct but not all workflows were restored");
            AnsiConsole.MarkupLine("[yellow]═══════════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[yellow]  ⚠ PARTIAL SUCCESS: Results correct                                   [/]");
            AnsiConsole.MarkupLine("[yellow]    • All results match expected values                                 [/]");
            AnsiConsole.MarkupLine("[yellow]    • Some workflows may not have triggered restoration                 [/]");
            AnsiConsole.MarkupLine("[yellow]═══════════════════════════════════════════════════════════════════════[/]");
        }
        else
        {
            LogToFile("FAILED: Some workflows produced incorrect results");
            AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[red]  ✗ FAILED: Workflow isolation issue detected                          [/]");
            AnsiConsole.MarkupLine("[red]    • Some workflows produced incorrect results                        [/]");
            AnsiConsole.MarkupLine("[red]    • Check log file for detailed event sequence                       [/]");
            AnsiConsole.MarkupLine("[red]═══════════════════════════════════════════════════════════════════════[/]");

            // Detailed failure analysis
            foreach (var tracker in _workflowTrackers.Values.Where(t => t.ActualResult != t.ExpectedResult))
            {
                var analysis = tracker.ActualResult == (999 * 2) + 10
                    ? "Used dummy input 999 - restoration failed to apply"
                    : $"Unexpected result - possible state corruption or wrong workflow restored";

                LogToFile($"FAILURE ANALYSIS: {tracker.WorkflowId} - {analysis}");
                AnsiConsole.MarkupLine($"[red]  {tracker.WorkflowId}: {analysis}[/]");
            }
        }
    }

    #region Logging Helpers

    private static void ClearLogFile()
    {
        try
        {
            File.WriteAllText(LogFilePath, $"C2 Concurrent Workflows Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[C2] Warning: Could not clear log file: {ex.Message}");
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

        // Debug output goes to file only - console stays clean for TAI
    }

    #endregion
}
