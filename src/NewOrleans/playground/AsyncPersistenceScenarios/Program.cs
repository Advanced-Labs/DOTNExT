using AsyncPersistenceScenarios.Services;
using AsyncPersistenceScenarios.TestWorkflows;
using DOTNExT.Persistence;
using Spectre.Console;

namespace AsyncPersistenceScenarios;

/// <summary>
/// Main program for async persistence scenario testing.
/// Tests the persistence service and workflows before Roslyn modification.
/// </summary>
public static class Program
{
    private static InMemoryAsyncPersistenceService _persistence = null!;
    private static BasicWorkflows _workflows = null!;
    private static readonly string PersistenceFile = Path.Combine(
        AppContext.BaseDirectory, "async-persistence-state.json");

    public static async Task Main()
    {
        // Initialize persistence service with file backing (for process restart tests)
        _persistence = new InMemoryAsyncPersistenceService(PersistenceFile, verbose: true);
        _workflows = new BasicWorkflows(_persistence);

        // Subscribe to events for additional observability
        _persistence.OnCheckpoint += (s, e) =>
            AnsiConsole.MarkupLine($"[blue]EVENT: Checkpoint {e.MethodId} at state {e.StateNumber}[/]");
        _persistence.OnRestore += (s, e) =>
            AnsiConsole.MarkupLine($"[green]EVENT: Restored {e.MethodId} from state {e.RestoredState}[/]");
        _persistence.OnComplete += (s, e) =>
            AnsiConsole.MarkupLine($"[yellow]EVENT: Complete {e.MethodId} {(e.Faulted ? "FAULTED" : "SUCCESS")}[/]");

        AnsiConsole.Write(new FigletText("Async Persistence").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[grey]Testing async state machine persistence[/]");
        AnsiConsole.MarkupLine($"[grey]Persistence file: {PersistenceFile}[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var challenge = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select a challenge:[/]")
                    .PageSize(12)
                    .AddChoices(new[]
                    {
                        "1. Basic Checkpoint (SimpleWorkflow)",
                        "2. Multiple Types (ProcessOrderWorkflow)",
                        "3. Nested Async (OuterWorkflow)",
                        "4. Exception Handling (WorkflowWithExceptionHandling)",
                        "5. Loops (LoopWorkflow)",
                        "───────────────────────────────",
                        "6. ★ Instrumented State Machine (Roslyn Demo)",
                        "───────────────────────────────",
                        "View Persisted State",
                        "Clear All Persisted State",
                        "Exit"
                    }));

            if (challenge == "Exit")
            {
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                break;
            }

            if (challenge.StartsWith("───"))
                continue;

            try
            {
                await RunChallengeAsync(challenge);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                AnsiConsole.WriteException(ex);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            Console.Clear();
        }
    }

    private static async Task RunChallengeAsync(string challenge)
    {
        switch (challenge)
        {
            case "1. Basic Checkpoint (SimpleWorkflow)":
                await RunSimpleWorkflowChallengeAsync();
                break;
            case "2. Multiple Types (ProcessOrderWorkflow)":
                await RunProcessOrderChallengeAsync();
                break;
            case "3. Nested Async (OuterWorkflow)":
                await RunNestedAsyncChallengeAsync();
                break;
            case "4. Exception Handling (WorkflowWithExceptionHandling)":
                await RunExceptionHandlingChallengeAsync();
                break;
            case "5. Loops (LoopWorkflow)":
                await RunLoopChallengeAsync();
                break;
            case "6. ★ Instrumented State Machine (Roslyn Demo)":
                await RunInstrumentedWorkflowChallengeAsync();
                break;
            case "View Persisted State":
                ViewPersistedState();
                break;
            case "Clear All Persisted State":
                ClearAllState();
                break;
        }
    }

    private static async Task RunSimpleWorkflowChallengeAsync()
    {
        const string workflowId = "simple-workflow-1";

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Challenge 1: Basic Checkpoint[/]")
                .AddChoices(new[]
                {
                    "Run Fresh (no persistence)",
                    "Run with Checkpointing",
                    "Run and Simulate Interrupt",
                    "Resume from Checkpoint",
                    "View Checkpoint State",
                    "Clear Checkpoint",
                    "Back"
                }));

        switch (action)
        {
            case "Run Fresh (no persistence)":
                _persistence.Clear(workflowId);
                await _workflows.SimpleWorkflow(5, workflowId);
                break;

            case "Run with Checkpointing":
                await _workflows.SimpleWorkflow(5, workflowId);
                break;

            case "Run and Simulate Interrupt":
                AnsiConsole.MarkupLine("[yellow]Starting workflow... will interrupt after first checkpoint[/]");
                _persistence.Clear(workflowId);

                // Run until first checkpoint, then cancel
                var cts = new CancellationTokenSource();
                var runTask = Task.Run(async () =>
                {
                    await _workflows.SimpleWorkflow(5, workflowId);
                });

                // Wait for first checkpoint
                await Task.Delay(700);
                AnsiConsole.MarkupLine("[red]INTERRUPT! Simulating crash...[/]");

                // Don't actually cancel - just show the state
                AnsiConsole.MarkupLine("[grey]Workflow would be interrupted here. State is persisted.[/]");
                ViewSnapshotDetails(workflowId);
                break;

            case "Resume from Checkpoint":
                if (_persistence.HasPersistedState(workflowId))
                {
                    AnsiConsole.MarkupLine("[green]Found persisted state, resuming...[/]");
                    var snapshot = _persistence.GetSnapshot(workflowId);
                    AnsiConsole.MarkupLine($"[grey]Resuming from state {snapshot?.State}[/]");

                    // In real impl, we'd restore the state machine and call MoveNext
                    // For now, just show what would happen
                    AnsiConsole.MarkupLine("[yellow]NOTE: Actual resume requires Roslyn modification[/]");
                    AnsiConsole.MarkupLine("[grey]The persisted state contains:[/]");
                    ViewSnapshotDetails(workflowId);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]No persisted state found[/]");
                }
                break;

            case "View Checkpoint State":
                ViewSnapshotDetails(workflowId);
                break;

            case "Clear Checkpoint":
                _persistence.Clear(workflowId);
                AnsiConsole.MarkupLine("[green]Cleared[/]");
                break;
        }
    }

    private static async Task RunProcessOrderChallengeAsync()
    {
        const string workflowId = "order-workflow-1";

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Challenge 2: Multiple Types[/]")
                .AddChoices(new[]
                {
                    "Run Order Processing",
                    "View Checkpoint State",
                    "Clear Checkpoint",
                    "Back"
                }));

        switch (action)
        {
            case "Run Order Processing":
                var order = new Order
                {
                    OrderId = "ORD-001",
                    CustomerId = "CUST-123",
                    Items = new List<OrderItem>
                    {
                        new() { ProductId = "PROD-1", Name = "Widget", Price = 29.99m, Quantity = 2 },
                        new() { ProductId = "PROD-2", Name = "Gadget", Price = 49.99m, Quantity = 1 }
                    }
                };
                _persistence.Clear(workflowId);
                var result = await _workflows.ProcessOrderWorkflow(order, workflowId);
                AnsiConsole.MarkupLine($"[green]Order result: {result.Message}, Total: {result.Total:C}[/]");
                break;

            case "View Checkpoint State":
                ViewSnapshotDetails(workflowId);
                break;

            case "Clear Checkpoint":
                _persistence.Clear(workflowId);
                AnsiConsole.MarkupLine("[green]Cleared[/]");
                break;
        }
    }

    private static async Task RunNestedAsyncChallengeAsync()
    {
        const string workflowId = "outer-workflow-1";

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Challenge 3: Nested Async[/]")
                .AddChoices(new[]
                {
                    "Run Nested Workflow",
                    "View All Related States",
                    "Clear All",
                    "Back"
                }));

        switch (action)
        {
            case "Run Nested Workflow":
                _persistence.Clear(workflowId);
                _persistence.Clear($"{workflowId}-inner1");
                _persistence.Clear($"{workflowId}-inner2");
                var result = await _workflows.OuterWorkflow(5, workflowId);
                AnsiConsole.MarkupLine($"[green]Final result: {result}[/]");
                break;

            case "View All Related States":
                ViewSnapshotDetails(workflowId);
                ViewSnapshotDetails($"{workflowId}-inner1");
                ViewSnapshotDetails($"{workflowId}-inner2");
                break;

            case "Clear All":
                _persistence.Clear(workflowId);
                _persistence.Clear($"{workflowId}-inner1");
                _persistence.Clear($"{workflowId}-inner2");
                AnsiConsole.MarkupLine("[green]Cleared all[/]");
                break;
        }
    }

    private static async Task RunExceptionHandlingChallengeAsync()
    {
        const string workflowId = "exception-workflow-1";

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Challenge 4: Exception Handling[/]")
                .AddChoices(new[]
                {
                    "Run (Success Path)",
                    "Run (Failure Path - triggers catch)",
                    "View Checkpoint State",
                    "Clear Checkpoint",
                    "Back"
                }));

        switch (action)
        {
            case "Run (Success Path)":
                _persistence.Clear(workflowId);
                var successResult = await _workflows.WorkflowWithExceptionHandling(10, false, workflowId);
                AnsiConsole.MarkupLine($"[green]Result: {successResult}[/]");
                break;

            case "Run (Failure Path - triggers catch)":
                _persistence.Clear(workflowId);
                var failResult = await _workflows.WorkflowWithExceptionHandling(10, true, workflowId);
                AnsiConsole.MarkupLine($"[yellow]Result (from fallback): {failResult}[/]");
                break;

            case "View Checkpoint State":
                ViewSnapshotDetails(workflowId);
                break;

            case "Clear Checkpoint":
                _persistence.Clear(workflowId);
                AnsiConsole.MarkupLine("[green]Cleared[/]");
                break;
        }
    }

    private static async Task RunLoopChallengeAsync()
    {
        const string workflowId = "loop-workflow-1";

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Challenge 5: Loops[/]")
                .AddChoices(new[]
                {
                    "Run Loop (5 iterations)",
                    "Run Loop (10 iterations)",
                    "View Checkpoint State",
                    "Clear Checkpoint",
                    "Back"
                }));

        switch (action)
        {
            case "Run Loop (5 iterations)":
                _persistence.Clear(workflowId);
                var result5 = await _workflows.LoopWorkflow(5, workflowId);
                AnsiConsole.MarkupLine($"[green]Sum: {result5}[/]");
                break;

            case "Run Loop (10 iterations)":
                _persistence.Clear(workflowId);
                var result10 = await _workflows.LoopWorkflow(10, workflowId);
                AnsiConsole.MarkupLine($"[green]Sum: {result10}[/]");
                break;

            case "View Checkpoint State":
                ViewSnapshotDetails(workflowId);
                break;

            case "Clear Checkpoint":
                _persistence.Clear(workflowId);
                AnsiConsole.MarkupLine("[green]Cleared[/]");
                break;
        }
    }

    private static async Task RunInstrumentedWorkflowChallengeAsync()
    {
        const string workflowId = "instrumented-workflow-1";

        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]              INSTRUMENTED STATE MACHINE DEMO                       [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[grey]This demonstrates what Roslyn-generated code will look like.[/]");
        AnsiConsole.MarkupLine("[grey]The state machine is manually written to match compiler output.[/]");
        AnsiConsole.MarkupLine("[grey]Unlike other challenges, this one supports ACTUAL RESTORE.[/]");
        AnsiConsole.WriteLine();

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Challenge 6: Instrumented State Machine[/]")
                .AddChoices(new[]
                {
                    "Run Fresh (no persistence context)",
                    "Run with Persistence Context",
                    "Simulate Interrupt at Checkpoint 0",
                    "Simulate Interrupt at Checkpoint 1",
                    "Resume from Checkpoint (REAL RESTORE)",
                    "View Checkpoint State",
                    "Clear Checkpoint",
                    "Back"
                }));

        switch (action)
        {
            case "Run Fresh (no persistence context)":
                AnsiConsole.MarkupLine("[yellow]Running WITHOUT persistence context...[/]");
                AnsiConsole.MarkupLine("[grey]No checkpoints will be created.[/]");
                AnsiConsole.WriteLine();

                _persistence.Clear(workflowId);
                var runner1 = new InstrumentedWorkflowRunner(workflowId);
                var result1 = await runner1.InstrumentedSimpleWorkflow(5);
                AnsiConsole.MarkupLine($"[green]Result: {result1}[/]");
                break;

            case "Run with Persistence Context":
                AnsiConsole.MarkupLine("[yellow]Running WITH persistence context...[/]");
                AnsiConsole.MarkupLine("[grey]Checkpoints will be created at each await point.[/]");
                AnsiConsole.WriteLine();

                _persistence.Clear(workflowId);
                using (AsyncPersistenceContext.SetCurrent(_persistence))
                {
                    var runner2 = new InstrumentedWorkflowRunner(workflowId);
                    var result2 = await runner2.InstrumentedSimpleWorkflow(5);
                    AnsiConsole.MarkupLine($"[green]Result: {result2}[/]");
                }
                break;

            case "Simulate Interrupt at Checkpoint 0":
                await SimulateInterruptAsync(workflowId, 0, 5);
                break;

            case "Simulate Interrupt at Checkpoint 1":
                await SimulateInterruptAsync(workflowId, 1, 5);
                break;

            case "Resume from Checkpoint (REAL RESTORE)":
                if (_persistence.HasPersistedState(workflowId))
                {
                    var snapshot = _persistence.GetSnapshot(workflowId);
                    AnsiConsole.MarkupLine($"[green]Found persisted state at checkpoint {snapshot?.State}[/]");
                    AnsiConsole.MarkupLine("[yellow]Resuming workflow from checkpoint...[/]");
                    AnsiConsole.WriteLine();

                    using (AsyncPersistenceContext.SetCurrent(_persistence))
                    {
                        // Create a new workflow runner - it will restore from checkpoint
                        var runner = new InstrumentedWorkflowRunner(workflowId);
                        var result = await runner.InstrumentedSimpleWorkflow(999); // Input ignored if restoring
                        AnsiConsole.MarkupLine($"[green]Resumed workflow completed with result: {result}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]No persisted state found. Run 'Simulate Interrupt' first.[/]");
                }
                break;

            case "View Checkpoint State":
                ViewSnapshotDetails(workflowId);
                break;

            case "Clear Checkpoint":
                _persistence.Clear(workflowId);
                AnsiConsole.MarkupLine("[green]Cleared[/]");
                break;
        }
    }

    private static async Task SimulateInterruptAsync(string workflowId, int interruptAtState, int input)
    {
        _persistence.Clear(workflowId);

        AnsiConsole.MarkupLine($"[yellow]Starting workflow with input={input}[/]");
        AnsiConsole.MarkupLine($"[yellow]Will interrupt at checkpoint {interruptAtState}[/]");
        AnsiConsole.WriteLine();

        var checkpointReached = false;
        var tcs = new TaskCompletionSource();

        // Subscribe to checkpoint events to know when to interrupt
        void OnCheckpoint(object? sender, CheckpointEventArgs e)
        {
            if (e.StateNumber == interruptAtState && e.MethodId == workflowId)
            {
                checkpointReached = true;
                AnsiConsole.MarkupLine($"[red]>>> INTERRUPT! Checkpoint {interruptAtState} reached <<<[/]");
                tcs.TrySetResult();
            }
        }

        _persistence.OnCheckpoint += OnCheckpoint;

        try
        {
            using (AsyncPersistenceContext.SetCurrent(_persistence))
            {
                var runner = new InstrumentedWorkflowRunner(workflowId);
                var workflowTask = runner.InstrumentedSimpleWorkflow(input);

                // Wait for either checkpoint or completion
                var completed = await Task.WhenAny(workflowTask, tcs.Task);

                if (checkpointReached)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[red]Workflow interrupted! State has been persisted.[/]");
                    AnsiConsole.MarkupLine("[grey]In a real scenario, this would be a process crash.[/]");
                    AnsiConsole.MarkupLine("[grey]The state is saved and can be resumed later.[/]");
                    AnsiConsole.WriteLine();
                    ViewSnapshotDetails(workflowId);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[cyan]Select 'Resume from Checkpoint' to continue the workflow.[/]");
                }
                else
                {
                    var result = await workflowTask;
                    AnsiConsole.MarkupLine($"[yellow]Workflow completed before interrupt: {result}[/]");
                }
            }
        }
        finally
        {
            _persistence.OnCheckpoint -= OnCheckpoint;
        }
    }

    private static void ViewPersistedState()
    {
        var ids = _persistence.GetPersistedMethodIds().ToList();

        if (ids.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No persisted state[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Method ID");
        table.AddColumn("State");
        table.AddColumn("Timestamp");
        table.AddColumn("Fields");

        foreach (var id in ids)
        {
            var snapshot = _persistence.GetSnapshot(id);
            if (snapshot != null)
            {
                table.AddRow(
                    id,
                    snapshot.State.ToString(),
                    snapshot.Timestamp.ToString("HH:mm:ss"),
                    string.Join(", ", snapshot.Fields.Keys));
            }
        }

        AnsiConsole.Write(table);
    }

    private static void ViewSnapshotDetails(string methodId)
    {
        var snapshot = _persistence.GetSnapshot(methodId);
        if (snapshot == null)
        {
            AnsiConsole.MarkupLine($"[grey]No snapshot for {methodId}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]Snapshot: {methodId}[/]");
        AnsiConsole.MarkupLine($"  State: {snapshot.State}");
        AnsiConsole.MarkupLine($"  Timestamp: {snapshot.Timestamp}");
        AnsiConsole.MarkupLine($"  Fields:");

        foreach (var field in snapshot.Fields)
        {
            var valueStr = field.Value?.ToString() ?? "null";
            if (valueStr.Length > 50)
                valueStr = valueStr[..50] + "...";
            AnsiConsole.MarkupLine($"    {field.Key} = {valueStr}");
        }
    }

    private static void ClearAllState()
    {
        _persistence.ClearAll();
        AnsiConsole.MarkupLine("[green]All persisted state cleared[/]");
    }
}
