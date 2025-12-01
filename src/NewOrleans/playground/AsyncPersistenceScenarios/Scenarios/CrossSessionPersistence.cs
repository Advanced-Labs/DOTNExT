using AsyncPersistenceScenarios.Helpers;
using AsyncPersistenceScenarios.TestWorkflows;
using DOTNExT.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewOrleans.AsyncPlus;
using NewOrleans.AsyncPlus.Services;
using Orleans;
using Spectre.Console;

namespace AsyncPersistenceScenarios.Scenarios;

/// <summary>
/// Scenario C1: Basic Cross-Session Persistence
///
/// Purpose: Verify checkpoints survive process restarts
///
/// Test Flow:
/// 1. Start silo, run [Persistable] workflow, checkpoint at state 1
/// 2. Kill process abruptly (simulating crash)
/// 3. Restart silo
/// 4. Workflow should restore from checkpoint and complete
///
/// Key Validations:
/// - RavenDB contains checkpoint data
/// - State machine fields are correctly restored
/// - Workflow resumes from correct state (not from beginning)
///
/// This scenario is SELF-MANAGING - it starts and stops its own silo.
/// </summary>
public static class CrossSessionPersistence
{
    private const string WorkflowId = "cross-session-test-workflow";

    public static async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario C1: Cross-Session Persistence                           [/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        // Scenario explanation
        var explanation = new Panel(
            "[white]This scenario demonstrates that workflow checkpoints survive process restarts.\n\n" +
            "[yellow]Test Flow:[/]\n" +
            "• Phase 1: Start silo, run workflow, interrupt at checkpoint\n" +
            "• Phase 2: Stop silo (simulating crash)\n" +
            "• Phase 3: Restart silo, resume workflow from checkpoint\n" +
            "• Phase 4: Verify workflow completes with correct result\n\n" +
            "[yellow]Key Validation:[/]\n" +
            "• RavenDB contains checkpoint data between silo restarts\n" +
            "• State machine fields (_step1, input) are correctly restored\n" +
            "• Workflow resumes from checkpoint state, not from beginning[/]")
            .Header("[green]About This Scenario[/]")
            .BorderColor(Color.Grey);
        AnsiConsole.Write(explanation);
        AnsiConsole.WriteLine();

        // Check RavenDB prerequisite
        AnsiConsole.MarkupLine("[yellow]Prerequisites check...[/]");
        AnsiConsole.MarkupLine("[grey]  RavenDB expected at: http://127.0.0.1:38880[/]");
        AnsiConsole.MarkupLine("[grey]  Database: AsyncPlusScenarios[/]");
        AnsiConsole.WriteLine();

        try
        {
            await RunScenarioAsync();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Scenario failed: {ex.Message}[/]");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[red]Inner: {ex.InnerException.Message}[/]");
            }
            AnsiConsole.WriteException(ex);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[green]  Scenario C1 Complete                                              [/]");
        AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════[/]");
    }

    private static async Task RunScenarioAsync()
    {
        // ============================================
        // PHASE 1: Start silo and run workflow to checkpoint
        // ============================================
        AnsiConsole.MarkupLine("[blue]Phase 1: Starting silo and running workflow to checkpoint...[/]");
        AnsiConsole.WriteLine();

        IHost? silo1 = null;
        int? checkpointState = null;
        const int inputValue = 42;

        try
        {
            silo1 = await AnsiConsole.Status()
                .StartAsync("Starting Orleans silo with RavenDB...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11115,
                        gatewayPort: 30005,
                        clusterId: "cross-session-test",
                        serviceId: "cross-session-test"
                    );
                    await host.StartAsync();
                    return host;
                });

            AnsiConsole.MarkupLine("[green]✓ Silo started successfully[/]");

            // Get the persistence service
            var persistence = silo1.Services.GetRequiredService<IAsyncPersistenceService>()
                as NewOrleansAsyncPersistenceService;

            if (persistence == null)
            {
                throw new InvalidOperationException("Could not get NewOrleansAsyncPersistenceService");
            }

            // Clear any previous state for this workflow
            var grainFactory = silo1.Services.GetRequiredService<IGrainFactory>();
            var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(WorkflowId);
            await grain.ClearAsync();
            AnsiConsole.MarkupLine("[grey]  Cleared any previous workflow state[/]");

            // Set up checkpoint tracking
            var checkpointReached = new TaskCompletionSource();
            var checkpointCount = 0;

            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    checkpointCount++;
                    checkpointState = e.StateNumber;
                    AnsiConsole.MarkupLine($"[cyan]  [[CHECKPOINT]] State {e.StateNumber} - checkpoint #{checkpointCount}[/]");

                    // After first checkpoint, we'll "crash"
                    if (checkpointCount == 1)
                    {
                        checkpointReached.TrySetResult();
                    }
                }
            }

            persistence.OnCheckpoint += OnCheckpoint;

            AnsiConsole.MarkupLine($"[yellow]  Starting workflow with input={inputValue}...[/]");
            AnsiConsole.MarkupLine("[grey]  Will interrupt after first checkpoint to simulate crash[/]");
            AnsiConsole.WriteLine();

            // Run workflow and wait for first checkpoint
            using (AsyncPersistenceContext.SetCurrent(persistence))
            {
                var runner = new InstrumentedWorkflowRunner(WorkflowId);
                var workflowTask = runner.InstrumentedSimpleWorkflow(inputValue);

                // Wait for first checkpoint
                var firstCheckpoint = await Task.WhenAny(workflowTask, checkpointReached.Task);

                if (checkpointReached.Task.IsCompleted)
                {
                    AnsiConsole.MarkupLine("[green]✓ First checkpoint reached![/]");

                    // Read the saved state to verify
                    var hasState = await grain.HasPersistedStateAsync();
                    var checkpoint = hasState ? await grain.TryGetCheckpointAsync() : null;

                    if (checkpoint != null)
                    {
                        AnsiConsole.MarkupLine($"[grey]  Checkpoint state: {checkpoint.StateNumber}[/]");
                        AnsiConsole.MarkupLine($"[grey]  Checkpoint timestamp: {checkpoint.CheckpointTimeUtc}[/]");

                        // Try to extract _step1 from the serialized data
                        if (checkpoint.SerializedStateMachine != null)
                        {
                            AnsiConsole.MarkupLine($"[grey]  Serialized state machine size: {checkpoint.SerializedStateMachine.Length} bytes[/]");
                        }
                    }
                }
                else
                {
                    // Workflow completed before we could interrupt
                    var result = await workflowTask;
                    AnsiConsole.MarkupLine($"[yellow]  Workflow completed too quickly: {result}[/]");
                    AnsiConsole.MarkupLine("[yellow]  (This can happen if awaits complete synchronously)[/]");
                    return;
                }
            }

            persistence.OnCheckpoint -= OnCheckpoint;
        }
        finally
        {
            if (silo1 != null)
            {
                // ============================================
                // PHASE 2: Stop silo (simulating crash)
                // ============================================
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[blue]Phase 2: Stopping silo (simulating crash)...[/]");

                await silo1.StopAsync();
                silo1.Dispose();
                silo1 = null;

                AnsiConsole.MarkupLine("[red]✓ Silo stopped - process 'crashed'[/]");
                AnsiConsole.MarkupLine("[grey]  In a real scenario, this would be an unexpected termination[/]");
            }
        }

        // Wait a moment to simulate restart delay
        AnsiConsole.MarkupLine("[grey]  Waiting 2 seconds before restart...[/]");
        await Task.Delay(2000);

        // ============================================
        // PHASE 3: Restart silo and resume workflow
        // ============================================
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Phase 3: Restarting silo and resuming workflow...[/]");
        AnsiConsole.WriteLine();

        IHost? silo2 = null;
        try
        {
            silo2 = await AnsiConsole.Status()
                .StartAsync("Starting Orleans silo (restart)...", async ctx =>
                {
                    var host = SiloHelper.BuildSingleSiloWithRavenDb(
                        siloPort: 11115,
                        gatewayPort: 30005,
                        clusterId: "cross-session-test",
                        serviceId: "cross-session-test"
                    );
                    await host.StartAsync();
                    return host;
                });

            AnsiConsole.MarkupLine("[green]✓ Silo restarted successfully[/]");

            // Get the persistence service
            var persistence2 = silo2.Services.GetRequiredService<IAsyncPersistenceService>()
                as NewOrleansAsyncPersistenceService;

            if (persistence2 == null)
            {
                throw new InvalidOperationException("Could not get NewOrleansAsyncPersistenceService");
            }

            // Check for saved state
            var grainFactory = silo2.Services.GetRequiredService<IGrainFactory>();
            var grain = grainFactory.GetGrain<IAsyncStatePersistenceGrain>(WorkflowId);
            var hasState = await grain.HasPersistedStateAsync();

            AnsiConsole.MarkupLine($"[cyan]  Persisted state found: {hasState}[/]");

            if (hasState)
            {
                var checkpoint = await grain.TryGetCheckpointAsync();
                if (checkpoint != null)
                {
                    AnsiConsole.MarkupLine($"[cyan]  Resuming from checkpoint state: {checkpoint.StateNumber}[/]");
                    AnsiConsole.MarkupLine($"[cyan]  Checkpoint timestamp: {checkpoint.CheckpointTimeUtc}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]  No persisted state found - workflow will start from beginning[/]");
            }

            // Track events during resume
            var resumeCheckpoints = 0;
            var completeResult = 0;

            void OnCheckpoint(object? sender, CheckpointEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    resumeCheckpoints++;
                    AnsiConsole.MarkupLine($"[cyan]  [[CHECKPOINT]] State {e.StateNumber} during resume[/]");
                }
            }

            void OnComplete(object? sender, CompleteEventArgs e)
            {
                if (e.MethodId == WorkflowId)
                {
                    completeResult = (int)(e.Result ?? 0);
                    AnsiConsole.MarkupLine($"[green]  [[COMPLETE]] Workflow finished with result: {completeResult}[/]");
                }
            }

            persistence2.OnCheckpoint += OnCheckpoint;
            persistence2.OnComplete += OnComplete;

            AnsiConsole.MarkupLine("[yellow]  Resuming workflow...[/]");

            // Resume the workflow
            using (AsyncPersistenceContext.SetCurrent(persistence2))
            {
                var runner = new InstrumentedWorkflowRunner(WorkflowId);
                // Input is ignored if restoring - the saved state will be used
                var result = await runner.InstrumentedSimpleWorkflow(999);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]✓ Workflow completed with result: {result}[/]");

                // ============================================
                // PHASE 4: Verify results
                // ============================================
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[blue]Phase 4: Verification...[/]");

                // Expected: input(42) -> step1(84) -> step2(94)
                var expectedResult = (inputValue * 2) + 10;

                var resultTable = new Table();
                resultTable.AddColumn("Metric");
                resultTable.AddColumn("Value");
                resultTable.AddColumn("Status");

                resultTable.AddRow(
                    "Input value",
                    inputValue.ToString(),
                    "[grey]Provided at start[/]"
                );

                resultTable.AddRow(
                    "Expected result",
                    expectedResult.ToString(),
                    "[grey](input*2)+10[/]"
                );

                resultTable.AddRow(
                    "Actual result",
                    result.ToString(),
                    result == expectedResult ? "[green]✓ Match[/]" : "[red]✗ Mismatch[/]"
                );

                resultTable.AddRow(
                    "Checkpoints during resume",
                    resumeCheckpoints.ToString(),
                    resumeCheckpoints < 2 ? "[green]✓ Skipped restored steps[/]" : "[yellow]All checkpoints[/]"
                );

                resultTable.AddRow(
                    "Restoration worked",
                    hasState ? "Yes" : "No",
                    hasState ? "[green]✓[/]" : "[red]✗[/]"
                );

                AnsiConsole.Write(resultTable);
                AnsiConsole.WriteLine();

                // Conclusions
                if (result == expectedResult && hasState && resumeCheckpoints < 2)
                {
                    AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════[/]");
                    AnsiConsole.MarkupLine("[green]  ✓ SUCCESS: Cross-session persistence verified!                    [/]");
                    AnsiConsole.MarkupLine("[green]    • Checkpoint survived silo restart                              [/]");
                    AnsiConsole.MarkupLine("[green]    • Workflow resumed from saved state                             [/]");
                    AnsiConsole.MarkupLine("[green]    • Correct result computed                                       [/]");
                    AnsiConsole.MarkupLine("[green]═══════════════════════════════════════════════════════════════════[/]");
                }
                else if (result == expectedResult)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ PARTIAL SUCCESS: Result is correct but restoration may not have worked[/]");
                    AnsiConsole.MarkupLine("[grey]  The workflow may have run from the beginning[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ FAILED: Result mismatch or restoration failed[/]");
                }
            }

            persistence2.OnCheckpoint -= OnCheckpoint;
            persistence2.OnComplete -= OnComplete;
        }
        finally
        {
            if (silo2 != null)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
                await silo2.StopAsync();
                silo2.Dispose();
                AnsiConsole.MarkupLine("[green]✓ Silo stopped[/]");
            }
        }
    }
}
