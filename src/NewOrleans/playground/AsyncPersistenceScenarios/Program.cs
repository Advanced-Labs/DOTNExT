using System.Net;
using AsyncPersistenceScenarios.Helpers;
using AsyncPersistenceScenarios.Scenarios;
using AsyncPersistenceScenarios.Services;
using AsyncPersistenceScenarios.TestWorkflows;
using DOTNExT.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewOrleans.AsyncPlus.Extensions;
using NewOrleans.AsyncPlus.Services;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
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
        // Clear screen at startup
        Console.Clear();

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

        // Handle CTRL-C gracefully
        Console.CancelKeyPress += async (s, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            AnsiConsole.MarkupLine("\n[yellow]Shutting down gracefully...[/]");
            if (_orleansSiloHost != null)
            {
                try
                {
                    await _orleansSiloHost.StopAsync(TimeSpan.FromSeconds(5));
                    _orleansSiloHost.Dispose();
                }
                catch { }
            }
            Environment.Exit(0);
        };

        AnsiConsole.Write(new FigletText("Async Persistence").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[grey]Testing async state machine persistence[/]");
        AnsiConsole.MarkupLine($"[grey]Persistence file: {PersistenceFile}[/]");
        AnsiConsole.MarkupLine("[grey]Press CTRL+C to exit at any time[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var challenge = ShowMenu("[green]Select a challenge:[/]", new[]
            {
                ("1", "Basic Checkpoint (SimpleWorkflow)"),
                ("2", "Multiple Types (ProcessOrderWorkflow)"),
                ("3", "Nested Async (OuterWorkflow)"),
                ("4", "Exception Handling (WorkflowWithExceptionHandling)"),
                ("5", "Loops (LoopWorkflow)"),
                ("", "───────────────────────────────"),
                ("6", "★ Instrumented State Machine (Roslyn Demo)"),
                ("7", "★★ Dynamic Compilation (Modified Roslyn)"),
                ("8", "★★★ Orleans/RavenDB Persistence (Manual)"),
                ("", "───────────────────────────────"),
                ("S", "★★★★ SELF-MANAGING SCENARIOS →"),
                ("", "───────────────────────────────"),
                ("V", "View Persisted State"),
                ("C", "Clear All Persisted State"),
                ("", "───────────────────────────────"),
                ("R", "★★★ Run All with Report"),
                ("Q", "Exit")
            });

            if (challenge == "Q")
            {
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                break;
            }

            if (string.IsNullOrEmpty(challenge))
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

    private static async Task RunChallengeAsync(string key)
    {
        switch (key.ToUpperInvariant())
        {
            case "1": await RunSimpleWorkflowChallengeAsync(); break;
            case "2": await RunProcessOrderChallengeAsync(); break;
            case "3": await RunNestedAsyncChallengeAsync(); break;
            case "4": await RunExceptionHandlingChallengeAsync(); break;
            case "5": await RunLoopChallengeAsync(); break;
            case "6": await RunInstrumentedWorkflowChallengeAsync(); break;
            case "7": await RunDynamicCompilationChallengeAsync(); break;
            case "8": await RunOrleansRavenDbChallengeAsync(); break;
            case "S": await RunSelfManagingScenariosMenuAsync(); break;
            case "V": ViewPersistedState(); break;
            case "C": ClearAllState(); break;
            case "R": await RunAllScenariosWithReportAsync(); break;
        }
    }

    /// <summary>
    /// Custom menu that supports both arrow navigation AND hotkey selection.
    /// Press the key (1-9, A-Z) to select directly, or use arrows + Enter.
    /// ESC or B returns empty string (back).
    /// </summary>
    private static string ShowMenu(string title, (string Key, string Label)[] items)
    {
        int selectedIndex = 0;
        var selectableItems = items.Select((item, idx) => (item, idx))
            .Where(x => !string.IsNullOrEmpty(x.item.Key) && !x.item.Label.StartsWith("───"))
            .ToList();

        while (true)
        {
            // Render menu
            AnsiConsole.Cursor.SetPosition(0, Console.CursorTop);
            AnsiConsole.MarkupLine(title);
            AnsiConsole.MarkupLine("[grey]Use ↑↓ arrows + Enter, or press the key (1-9, letter) directly. ESC to go back.[/]");
            AnsiConsole.WriteLine();

            for (int i = 0; i < items.Length; i++)
            {
                var (key, label) = items[i];
                bool isSelectable = !string.IsNullOrEmpty(key) && !label.StartsWith("───");
                bool isSelected = selectableItems.Any(s => s.idx == i && selectableItems.IndexOf(s) == selectedIndex);

                if (label.StartsWith("───"))
                {
                    AnsiConsole.MarkupLine($"[grey]{Markup.Escape(label)}[/]");
                }
                else if (isSelected)
                {
                    AnsiConsole.MarkupLine($"[cyan]> [[{Markup.Escape(key)}]] {Markup.Escape(label)}[/]");
                }
                else if (!string.IsNullOrEmpty(key))
                {
                    AnsiConsole.MarkupLine($"  [yellow][[{Markup.Escape(key)}]][/] {Markup.Escape(label)}");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  {Markup.Escape(label)}");
                }
            }

            // Read key
            var keyInfo = Console.ReadKey(true);

            // Handle ESC
            if (keyInfo.Key == ConsoleKey.Escape)
                return "";

            // Handle Enter - return selected item's key
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                if (selectedIndex < selectableItems.Count)
                    return selectableItems[selectedIndex].item.Key;
                continue;
            }

            // Handle arrow keys
            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                selectedIndex = (selectedIndex - 1 + selectableItems.Count) % selectableItems.Count;
                Console.Clear();
                continue;
            }
            if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                selectedIndex = (selectedIndex + 1) % selectableItems.Count;
                Console.Clear();
                continue;
            }

            // Handle direct key press (1-9, A-Z)
            char pressed = char.ToUpperInvariant(keyInfo.KeyChar);
            var match = items.FirstOrDefault(x => x.Key.Equals(pressed.ToString(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key))
            {
                return match.Key;
            }

            // B for Back
            if (pressed == 'B')
                return "";
        }
    }

    private static async Task RunSimpleWorkflowChallengeAsync()
    {
        const string workflowId = "simple-workflow-1";

        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[cyan]    CHALLENGE 1: BASIC CHECKPOINT (SimpleWorkflow)                  [/]");
            AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.WriteLine();

            // Scenario explanation
            var explanation = new Panel(
                "[white]Tests basic async state machine checkpointing with in-memory persistence.\n\n" +
                "[yellow]What it does:[/]\n" +
                "• Runs a simple workflow with 3 steps\n" +
                "• At each await point, state machine fields are serialized and saved\n" +
                "• Can simulate interruption and demonstrate checkpoint contents\n\n" +
                "[yellow]The workflow:[/]\n" +
                "  input(5) → Step1: 5*2=10 → [[CHECKPOINT]] → Step2: 10+3=13 → [[CHECKPOINT]] → Result\n\n" +
                "[yellow]Key concept:[/]\n" +
                "• State machine fields (__state, locals) are captured and can be restored[/]")
                .Header("[green]About This Challenge[/]")
                .BorderColor(Color.Grey);
            AnsiConsole.Write(explanation);
            AnsiConsole.WriteLine();

            // Show checkpoint status
            var hasState = _persistence.HasPersistedState(workflowId);
            var stateStatus = hasState ? "[green]● Has checkpoint[/]" : "[grey]○ No checkpoint[/]";
            AnsiConsole.MarkupLine($"Workflow Status: {stateStatus}");
            AnsiConsole.WriteLine();

            var action = ShowMenu("[cyan]Select an action:[/]", new[]
            {
                ("1", "Run Fresh (no persistence)"),
                ("2", "Run with Checkpointing"),
                ("3", "Run and Simulate Interrupt"),
                ("4", "Resume from Checkpoint"),
                ("5", "View Checkpoint State"),
                ("6", "Clear Checkpoint"),
                ("", "───────────────────────────────"),
                ("B", "Back to Main Menu")
            });

            if (action == "B" || action == "")
                break;

            Console.Clear();
            if (action == "1")
            {
                _persistence.Clear(workflowId);
                await _workflows.SimpleWorkflow(5, workflowId);
            }
            else if (action == "2")
            {
                await _workflows.SimpleWorkflow(5, workflowId);
            }
            else if (action == "3")
            {
                AnsiConsole.MarkupLine("[yellow]Starting workflow... will interrupt after first checkpoint[/]");
                _persistence.Clear(workflowId);

                var runTask = Task.Run(async () =>
                {
                    await _workflows.SimpleWorkflow(5, workflowId);
                });

                await Task.Delay(700);
                AnsiConsole.MarkupLine("[red]INTERRUPT! Simulating crash...[/]");
                AnsiConsole.MarkupLine("[grey]Workflow would be interrupted here. State is persisted.[/]");
                ViewSnapshotDetails(workflowId);
            }
            else if (action == "4")
            {
                if (_persistence.HasPersistedState(workflowId))
                {
                    AnsiConsole.MarkupLine("[green]Found persisted state, resuming...[/]");
                    var snapshot = _persistence.GetSnapshot(workflowId);
                    AnsiConsole.MarkupLine($"[grey]Resuming from state {snapshot?.State}[/]");
                    AnsiConsole.MarkupLine("[yellow]NOTE: Actual resume requires Roslyn modification[/]");
                    ViewSnapshotDetails(workflowId);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]No persisted state found[/]");
                }
            }
            else if (action == "5")
            {
                ViewSnapshotDetails(workflowId);
            }
            else if (action == "6")
            {
                _persistence.Clear(workflowId);
                AnsiConsole.MarkupLine("[green]Cleared[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
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
                    // Unfreeze if it was frozen by a simulated interrupt
                    _persistence.Unfreeze(workflowId);

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
                    // FREEZE the workflow state - prevents Complete() from clearing the checkpoint
                    // This simulates what would happen in a real crash: the process dies,
                    // so no further state changes occur
                    _persistence.Freeze(workflowId);

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[red]Workflow interrupted! State has been persisted.[/]");
                    AnsiConsole.MarkupLine("[grey]In a real scenario, this would be a process crash.[/]");
                    AnsiConsole.MarkupLine("[grey]The state is saved and can be resumed later.[/]");
                    AnsiConsole.MarkupLine("[grey](Workflow frozen - any further state changes are ignored)[/]");
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

    private static async Task RunDynamicCompilationChallengeAsync()
    {
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[cyan]           DYNAMIC COMPILATION WITH MODIFIED ROSLYN                 [/]");
        AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[grey]This compiles [[Persistable]] methods at runtime.[/]");
        AnsiConsole.MarkupLine("[grey]If modified Roslyn is active, it injects checkpoint/restore calls automatically.[/]");
        AnsiConsole.MarkupLine("[grey]Run 'Compare' to verify: [[Persistable]] should show checkpoints, Non-Persistable should not.[/]");
        AnsiConsole.WriteLine();

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Challenge 7: Dynamic Compilation[/]")
                .AddChoices(new[]
                {
                    "Compile Simple [[Persistable]] Workflow",
                    "Compile Non-Persistable (Control)",
                    "Compile Multi-Await Workflow",
                    "Run Compiled Workflow with Persistence",
                    "Compare Persistable vs Non-Persistable",
                    "Save Compiled DLL for Inspection",
                    "Back"
                }));

        var compiler = new PersistableAsyncCompiler();

        switch (action)
        {
            case "Compile Simple [[Persistable]] Workflow":
                await CompileAndShowResultAsync(compiler, PersistableSourceTemplates.SimpleWorkflow, "SimpleWorkflow");
                break;

            case "Compile Non-Persistable (Control)":
                await CompileAndShowResultAsync(compiler, PersistableSourceTemplates.NonPersistableWorkflow, "NonPersistableWorkflow");
                break;

            case "Compile Multi-Await Workflow":
                await CompileAndShowResultAsync(compiler, PersistableSourceTemplates.MultiAwaitWorkflow, "MultiAwaitWorkflow");
                break;

            case "Run Compiled Workflow with Persistence":
                await RunCompiledWorkflowAsync(compiler);
                break;

            case "Compare Persistable vs Non-Persistable":
                await CompareWorkflowsAsync(compiler);
                break;

            case "Save Compiled DLL for Inspection":
                SaveCompiledDll(compiler);
                break;
        }
    }

    private static async Task CompileAndShowResultAsync(PersistableAsyncCompiler compiler, string sourceCode, string name)
    {
        AnsiConsole.MarkupLine($"[yellow]Compiling {name}...[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[grey]Source code:[/]");
        // Escape brackets to prevent Spectre.Console from interpreting [Persistable] as markup
        var escapedSource = sourceCode.Trim().Replace("[", "[[").Replace("]", "]]");
        AnsiConsole.Write(new Panel(escapedSource)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey));
        AnsiConsole.WriteLine();

        var assembly = compiler.CompileAndLoad(sourceCode, name);

        if (assembly == null)
        {
            AnsiConsole.MarkupLine("[red]Compilation failed![/]");
            AnsiConsole.MarkupLine("[red]Errors:[/]");
            AnsiConsole.MarkupLine(compiler.GetErrorsString());
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Compilation succeeded![/]");
            AnsiConsole.MarkupLine($"[grey]Assembly: {assembly.FullName}[/]");

            var types = assembly.GetTypes();
            AnsiConsole.MarkupLine($"[grey]Types: {string.Join(", ", types.Select(t => t.Name))}[/]");

            foreach (var type in types)
            {
                var methods = type.GetMethods()
                    .Where(m => m.DeclaringType == type && !m.IsSpecialName)
                    .ToList();

                if (methods.Any())
                {
                    AnsiConsole.MarkupLine($"[grey]  {type.Name} methods: {string.Join(", ", methods.Select(m => m.Name))}[/]");
                }
            }

            // Note about verification
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]To verify persistence was injected:[/]");
            AnsiConsole.MarkupLine("[grey]1. Use 'Save Compiled DLL' option[/]");
            AnsiConsole.MarkupLine("[grey]2. Decompile with ILSpy/dnSpy/dotPeek[/]");
            AnsiConsole.MarkupLine("[grey]3. Look for AsyncPersistenceContext.Current calls in MoveNext()[/]");
        }

        await Task.CompletedTask; // Satisfy async requirement
    }

    private static async Task RunCompiledWorkflowAsync(PersistableAsyncCompiler compiler)
    {
        const string workflowId = "dynamic-workflow-1";

        AnsiConsole.MarkupLine("[yellow]Compiling and running [[Persistable]] workflow...[/]");
        AnsiConsole.WriteLine();

        var assembly = compiler.CompileAndLoad(PersistableSourceTemplates.SimpleWorkflow, "DynamicTest");

        if (assembly == null)
        {
            AnsiConsole.MarkupLine("[red]Compilation failed![/]");
            AnsiConsole.MarkupLine(compiler.GetErrorsString());
            return;
        }

        var workflowType = assembly.GetType("DynamicWorkflows.TestWorkflow");
        if (workflowType == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find TestWorkflow type[/]");
            return;
        }

        var method = workflowType.GetMethod("SimpleCalculation");
        if (method == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find SimpleCalculation method[/]");
            return;
        }

        var instance = Activator.CreateInstance(workflowType);

        _persistence.Clear(workflowId);

        AnsiConsole.MarkupLine("[green]Running with persistence context...[/]");
        AnsiConsole.WriteLine();

        using (AsyncPersistenceContext.SetCurrent(_persistence))
        {
            var task = (Task<int>?)method.Invoke(instance, new object[] { 5 });
            if (task != null)
            {
                var result = await task;
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]Result: {result}[/]");
            }
        }

        // Show if any checkpoints were created
        AnsiConsole.WriteLine();
        if (_persistence.HasPersistedState(workflowId))
        {
            AnsiConsole.MarkupLine("[green]Checkpoints were created! Persistence injection worked.[/]");
            ViewSnapshotDetails(workflowId);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No checkpoints created.[/]");
            AnsiConsole.MarkupLine("[grey]This could mean:[/]");
            AnsiConsole.MarkupLine("[grey]  - Using stock Roslyn (not modified version)[/]");
            AnsiConsole.MarkupLine("[grey]  - DOTNExT.Persistence types not found during compilation[/]");
            AnsiConsole.MarkupLine("[grey]  - Workflow completed too fast (all awaits were synchronous)[/]");
        }
    }

    private static async Task CompareWorkflowsAsync(PersistableAsyncCompiler compiler)
    {
        AnsiConsole.MarkupLine("[yellow]Comparing [[Persistable]] vs Non-Persistable workflows...[/]");
        AnsiConsole.WriteLine();

        // Compile both
        var persistableAsm = compiler.CompileAndLoad(PersistableSourceTemplates.SimpleWorkflow, "Persistable");
        var nonPersistableAsm = compiler.CompileAndLoad(PersistableSourceTemplates.NonPersistableWorkflow, "NonPersistable");

        if (persistableAsm == null || nonPersistableAsm == null)
        {
            AnsiConsole.MarkupLine("[red]Compilation failed[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Aspect");
        table.AddColumn("[[Persistable]]");
        table.AddColumn("Non-Persistable");

        table.AddRow("Compilation", "[green]Success[/]", "[green]Success[/]");
        // In-memory assemblies have empty Location - use "in-memory" instead
        var persistableSize = string.IsNullOrEmpty(persistableAsm.Location)
            ? "in-memory"
            : $"{new FileInfo(persistableAsm.Location).Length} bytes";
        var nonPersistableSize = string.IsNullOrEmpty(nonPersistableAsm.Location)
            ? "in-memory"
            : $"{new FileInfo(nonPersistableAsm.Location).Length} bytes";
        table.AddRow("Assembly Size", persistableSize, nonPersistableSize);

        // Run both and check for checkpoints
        _persistence.ClearAll();

        var persistableType = persistableAsm.GetType("DynamicWorkflows.TestWorkflow")!;
        var nonPersistableType = nonPersistableAsm.GetType("DynamicWorkflows.NonPersistableWorkflow")!;

        var persistableInstance = Activator.CreateInstance(persistableType);
        var nonPersistableInstance = Activator.CreateInstance(nonPersistableType);

        var persistableMethod = persistableType.GetMethod("SimpleCalculation")!;
        var nonPersistableMethod = nonPersistableType.GetMethod("NormalCalculation")!;

        int persistableCheckpoints = 0;
        int nonPersistableCheckpoints = 0;

        void CountCheckpoints(object? sender, CheckpointEventArgs e)
        {
            if (e.MethodId.Contains("Persistable")) persistableCheckpoints++;
            else nonPersistableCheckpoints++;
        }

        _persistence.OnCheckpoint += CountCheckpoints;

        using (AsyncPersistenceContext.SetCurrent(_persistence))
        {
            await ((Task<int>)persistableMethod.Invoke(persistableInstance, new object[] { 5 })!);
            await ((Task<int>)nonPersistableMethod.Invoke(nonPersistableInstance, new object[] { 5 })!);
        }

        _persistence.OnCheckpoint -= CountCheckpoints;

        table.AddRow("Checkpoints Created",
            persistableCheckpoints > 0 ? $"[green]{persistableCheckpoints}[/]" : "[grey]0[/]",
            nonPersistableCheckpoints > 0 ? $"[green]{nonPersistableCheckpoints}[/]" : "[grey]0[/]");

        AnsiConsole.Write(table);

        if (persistableCheckpoints > 0 && nonPersistableCheckpoints == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]SUCCESS! Modified Roslyn correctly injects persistence only for [[Persistable]] methods.[/]");
        }
        else if (persistableCheckpoints == 0 && nonPersistableCheckpoints == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]No checkpoints created for either workflow.[/]");
            AnsiConsole.MarkupLine("[grey]Ensure you're using the modified Roslyn compiler.[/]");
        }
    }

    private static void SaveCompiledDll(PersistableAsyncCompiler compiler)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "compiled-output");
        Directory.CreateDirectory(outputDir);

        var templates = new Dictionary<string, string>
        {
            ["SimpleWorkflow"] = PersistableSourceTemplates.SimpleWorkflow,
            ["NonPersistableWorkflow"] = PersistableSourceTemplates.NonPersistableWorkflow,
            ["MultiAwaitWorkflow"] = PersistableSourceTemplates.MultiAwaitWorkflow,
            ["ClassLevelPersistable"] = PersistableSourceTemplates.ClassLevelPersistable
        };

        AnsiConsole.MarkupLine($"[yellow]Saving compiled DLLs to: {outputDir}[/]");
        AnsiConsole.WriteLine();

        foreach (var (name, source) in templates)
        {
            var outputPath = Path.Combine(outputDir, $"{name}.dll");
            var success = compiler.CompileToFile(source, outputPath);

            if (success)
            {
                var size = new FileInfo(outputPath).Length;
                AnsiConsole.MarkupLine($"[green]  {name}.dll[/] ({size} bytes)");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]  {name}.dll - FAILED[/]");
                AnsiConsole.MarkupLine($"[grey]    {compiler.GetErrorsString()}[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]To inspect for persistence injection:[/]");
        AnsiConsole.MarkupLine($"[grey]  ilspycmd {outputDir}/SimpleWorkflow.dll -o decompiled/[/]");
        AnsiConsole.MarkupLine("[grey]  OR open in ILSpy/dnSpy/dotPeek[/]");
        AnsiConsole.MarkupLine("[grey]  Look for AsyncPersistenceContext.Current in the state machine[/]");
    }

    /// <summary>
    /// Challenge 8: Orleans/RavenDB persistence using NewOrleans.AsyncPlus.
    /// Starts an Orleans silo with RavenDB-backed grain storage and runs
    /// [Persistable] workflows with real distributed persistence.
    /// </summary>
    private static async Task RunOrleansRavenDbChallengeAsync()
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[cyan]    CHALLENGE 8: ORLEANS/RAVENDB PERSISTENCE (NewOrleans.AsyncPlus)[/]");
            AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.WriteLine();

            // Scenario explanation
            var explanation = new Panel(
                "[white]Tests Orleans grain persistence with the NewOrleans.AsyncPlus library.\n\n" +
                "[yellow]What it does:[/]\n" +
                "• Runs InstrumentedSimpleWorkflow - a hand-written state machine that\n" +
                "  demonstrates the exact pattern Roslyn generates for [[Persistable]] methods\n" +
                "• 2 await points, each triggering a checkpoint to Orleans grain storage\n" +
                "• With RavenDB: checkpoints persist to database, survive process restarts\n\n" +
                "[yellow]The workflow:[/]\n" +
                "  input(42) → Step1: 42*2=84 → [[CHECKPOINT]] → Step2: 84+10=94 → [[CHECKPOINT]] → Result: 94\n\n" +
                "[yellow]Code location:[/]\n" +
                "• TestWorkflows/InstrumentedWorkflow.cs - the state machine being tested\n" +
                "• This mimics what modified Roslyn (Challenge 7) generates automatically[/]")
                .Header("[green]About This Challenge[/]")
                .BorderColor(Color.Grey);
            AnsiConsole.Write(explanation);
            AnsiConsole.WriteLine();

            // Show silo status
            var siloStatus = _orleansSiloHost != null ? "[green]● Running[/]" : "[grey]○ Stopped[/]";
            AnsiConsole.MarkupLine($"Silo Status: {siloStatus}");
            AnsiConsole.WriteLine();

            var action = ShowMenu("[cyan]Select an action:[/]", new[]
            {
                ("1", "Start Silo with MemoryStorage (for testing)"),
                ("2", "Start Silo with RavenDB Storage"),
                ("3", "Run [[Persistable]] Workflow on Orleans"),
                ("4", "View Grain State"),
                ("5", "Stop Silo"),
                ("", "───────────────────────────────"),
                ("S", "View Source Code (InstrumentedWorkflow.cs)"),
                ("", "───────────────────────────────"),
                ("B", "Back to Main Menu")
            });

            if (action == "B" || action == "")
                break;

            Console.Clear();
            if (action == "1") await StartOrleansSiloAsync(useRavenDb: false);
            else if (action == "2") await StartOrleansSiloAsync(useRavenDb: true);
            else if (action == "3") await RunPersistableOnOrleansAsync();
            else if (action == "4") await ViewOrleansGrainStateAsync();
            else if (action == "5") await StopOrleansSiloAsync();
            else if (action == "S") ViewInstrumentedWorkflowSource();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }

    /// <summary>
    /// Self-Managing Scenarios Menu
    /// These scenarios auto-start and auto-stop their own silos.
    /// They follow the patterns from PluginGrainScenarios.
    /// </summary>
    private static async Task RunSelfManagingScenariosMenuAsync()
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[cyan]           SELF-MANAGING ASYNC+ SCENARIOS                          [/]");
            AnsiConsole.MarkupLine("[cyan]═══════════════════════════════════════════════════════════════════[/]");
            AnsiConsole.WriteLine();

            var explanation = new Panel(
                "[white]These scenarios are fully self-contained:\n" +
                "• Auto-start Orleans silos with RavenDB\n" +
                "• Auto-stop silos when done\n" +
                "• Produce structured output with Spectre.Console tables\n\n" +
                "[yellow]Scenario Types:[/]\n" +
                "• [magenta]ROSLYN+ (Active)[/]: Uses real Roslyn+ compiled [[Persistable]] code\n" +
                "• [magenta]ROSLYN+ (Planned)[/]: Future Roslyn+ scenarios (C2-C9)\n" +
                "• [grey]LEGACY[/]: Hand-coded state machines (C1) - kept for reference\n\n" +
                "[yellow]Prerequisites:[/]\n" +
                "• RavenDB running at http://127.0.0.1:38880[/]")
                .Header("[green]About Self-Managing Scenarios[/]")
                .BorderColor(Color.Grey);
            AnsiConsole.Write(explanation);
            AnsiConsole.WriteLine();

            var scenario = ShowMenu("[cyan]Select a scenario:[/]", new[]
            {
                ("", "═══ ROSLYN+ SCENARIOS (Active) ═══"),
                ("R", "R1: Cross-Session Persistence ✓"),
                ("2", "C2: Multiple Concurrent Workflows ✓"),
                ("", ""),
                ("8", "C8: Multi-Silo Visibility ✓"),
                ("", ""),
                ("", "═══ ROSLYN+ SCENARIOS (Planned) ══"),
                ("3", "C3: Nested Async Calls"),
                ("4", "C4: Exception Recovery"),
                ("5", "C5: Large State Serialization"),
                ("6", "C6: Silo Failover"),
                ("7", "C7: Version Migration"),
                ("9", "C9: Grain Mobility"),
                ("", ""),
                ("", "═══ LEGACY (Hand-Coded) ══════════"),
                ("1", "C1: Cross-Session (hand-coded state machine)"),
                ("", "───────────────────────────────"),
                ("B", "Back to Main Menu")
            });

            if (scenario == "B" || string.IsNullOrEmpty(scenario))
                break;

            Console.Clear();
            try
            {
                switch (scenario.ToUpperInvariant())
                {
                    case "R":
                        await RoslynPlusCrossSession.RunAsync();
                        break;
                    case "1":
                        await CrossSessionPersistence.RunAsync();
                        break;
                    case "2":
                        await MultipleConcurrentWorkflows.RunAsync();
                        break;
                    case "8":
                        await MultiSiloCheckpointVisibility.RunAsync();
                        break;
                    case "3":
                    case "4":
                    case "5":
                    case "6":
                    case "7":
                    case "9":
                        AnsiConsole.MarkupLine("[yellow]This scenario is not yet implemented.[/]");
                        AnsiConsole.MarkupLine("[grey]Check AI-Contexts/Claude-Opus/AsyncPlus-Scenarios.md for the design.[/]");
                        break;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Scenario failed: {ex.Message}[/]");
                AnsiConsole.WriteException(ex);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }

    /// <summary>
    /// Display the source code of InstrumentedWorkflow.cs
    /// </summary>
    private static void ViewInstrumentedWorkflowSource()
    {
        var sourceFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestWorkflows", "InstrumentedWorkflow.cs");

        // Try to find the file
        if (!File.Exists(sourceFile))
        {
            // Try alternate paths
            var altPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "TestWorkflows", "InstrumentedWorkflow.cs"),
                Path.Combine(AppContext.BaseDirectory, "TestWorkflows", "InstrumentedWorkflow.cs"),
            };
            sourceFile = altPaths.FirstOrDefault(File.Exists) ?? "";
        }

        if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile))
        {
            AnsiConsole.MarkupLine("[yellow]Could not find InstrumentedWorkflow.cs source file.[/]");
            AnsiConsole.MarkupLine("[grey]File location: TestWorkflows/InstrumentedWorkflow.cs[/]");
            AnsiConsole.WriteLine();

            // Show embedded summary instead
            AnsiConsole.MarkupLine("[cyan]InstrumentedSimpleWorkflow_StateMachine structure:[/]");
            AnsiConsole.WriteLine();
            var code = @"public struct InstrumentedSimpleWorkflow_StateMachine : IAsyncStateMachine
{
    public int __state;                              // Current state (-1=initial, 0, 1, ...)
    public AsyncTaskMethodBuilder<int> __builder;   // .NET async infrastructure
    public int input;                               // Method parameter
    public int _step1;                              // Local variable - persisted
    public int _step2;                              // Local variable - persisted
    private TaskAwaiter<int> __awaiter;             // NOT persisted (transient)
    public string workflowId;                       // For checkpoint identification
    private IAsyncPersistenceService? _persistenceService;  // NOT persisted

    public void MoveNext()
    {
        // 1. Get persistence service from AsyncPersistenceContext.Current
        // 2. If initial state (-1), try to restore from checkpoint
        // 3. Execute workflow steps with checkpoints at each await
        // 4. State machine fields (_step1, _step2) are serialized at checkpoints
    }
}";
            AnsiConsole.Write(new Panel(code).Header("[green]State Machine Structure[/]").BorderColor(Color.Grey));
            return;
        }

        var content = File.ReadAllText(sourceFile);
        AnsiConsole.MarkupLine($"[cyan]Source: {sourceFile}[/]");
        AnsiConsole.WriteLine();

        // Use Spectre.Console's syntax highlighting if available, otherwise raw
        try
        {
            // Display with line numbers
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var lineNum = (i + 1).ToString().PadLeft(4);
                AnsiConsole.MarkupLine($"[grey]{lineNum}[/] {Markup.Escape(line)}");
            }
        }
        catch
        {
            AnsiConsole.WriteLine(content);
        }
    }

    // Static fields for Orleans silo management
    private static IHost? _orleansSiloHost;
    private static NewOrleansAsyncPersistenceService? _orleansPersistence;
    private static EventHandler<DOTNExT.Persistence.CheckpointEventArgs>? _checkpointHandler;
    private static EventHandler<DOTNExT.Persistence.CompleteEventArgs>? _completeHandler;

    private static async Task StartOrleansSiloAsync(bool useRavenDb)
    {
        if (_orleansSiloHost != null)
        {
            AnsiConsole.MarkupLine("[yellow]Silo is already running. Stop it first.[/]");
            return;
        }

        await AnsiConsole.Status()
            .StartAsync($"Starting Orleans silo ({(useRavenDb ? "RavenDB" : "Memory")} storage)...", async ctx =>
            {
                var siloBuilder = Host.CreateDefaultBuilder()
                    .UseOrleans(silo =>
                    {
                        silo.UseLocalhostClustering(siloPort: 11112, gatewayPort: 30001)
                            .Configure<ClusterOptions>(options =>
                            {
                                options.ClusterId = "async-persistence-test";
                                options.ServiceId = "async-persistence-test";
                            });

                        if (useRavenDb)
                        {
                            // RavenDB storage with Async+ persistence
                            silo.UseAsyncPlusPersistenceWithRavenDb(options =>
                            {
                                options.Urls = new[] { "http://127.0.0.1:38880" };
                                options.DatabaseName = "AsyncPersistenceTest";
                                options.CreateDatabaseIfNotExists = true;
                            });
                        }
                        else
                        {
                            // Memory storage with Async+ persistence
                            silo.AddMemoryGrainStorage("AsyncPlusStorage")
                                .UseAsyncPlusPersistence("AsyncPlusStorage");
                        }
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Warning);
                        logging.AddFilter("NewOrleans.AsyncPlus", LogLevel.Information);
                    });

                _orleansSiloHost = siloBuilder.Build();
                await _orleansSiloHost.StartAsync();

                // Get the persistence service from the silo
                _orleansPersistence = _orleansSiloHost.Services.GetRequiredService<IAsyncPersistenceService>()
                    as NewOrleansAsyncPersistenceService;

                ctx.Status("Silo started!");
            });

        AnsiConsole.MarkupLine("[green]Orleans silo started successfully![/]");
        AnsiConsole.MarkupLine("[grey]  Cluster: async-persistence-test[/]");
        AnsiConsole.MarkupLine("[grey]  Storage: AsyncPlusStorage[/]");
        AnsiConsole.MarkupLine("[grey]  Silo Port: 11112[/]");
        AnsiConsole.MarkupLine("[grey]  Gateway: 30001[/]");
    }

    private static async Task RunPersistableOnOrleansAsync()
    {
        if (_orleansSiloHost == null || _orleansPersistence == null)
        {
            AnsiConsole.MarkupLine("[red]Orleans silo not running. Start it first.[/]");
            return;
        }

        const string workflowId = "orleans-persistable-workflow-1";

        AnsiConsole.MarkupLine("[yellow]Running [[Persistable]] workflow with Orleans persistence...[/]");
        AnsiConsole.WriteLine();

        // Track checkpoints
        int checkpointCount = 0;

        // Unsubscribe any previous handlers first to avoid accumulation
        if (_orleansPersistence is IAsyncPersistenceService persistenceWithEvents)
        {
            if (_checkpointHandler != null)
                persistenceWithEvents.OnCheckpoint -= _checkpointHandler;
            if (_completeHandler != null)
                persistenceWithEvents.OnComplete -= _completeHandler;

            // Create and store new handlers
            _checkpointHandler = (s, e) =>
            {
                checkpointCount++;
                AnsiConsole.MarkupLine($"[blue]Orleans Checkpoint: {e.MethodId} at state {e.StateNumber}[/]");
            };
            _completeHandler = (s, e) =>
            {
                AnsiConsole.MarkupLine($"[green]Orleans Complete: {e.MethodId}[/]");
            };

            // Subscribe
            persistenceWithEvents.OnCheckpoint += _checkpointHandler;
            persistenceWithEvents.OnComplete += _completeHandler;
        }

        try
        {
            // Use the Orleans persistence context
            using (AsyncPersistenceContext.SetCurrent(_orleansPersistence))
            {
                // Run the instrumented workflow (which has the Roslyn-compatible state machine)
                var runner = new InstrumentedWorkflowRunner(workflowId);
                var result = await runner.InstrumentedSimpleWorkflow(42);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]Workflow completed with result: {result}[/]");
                AnsiConsole.MarkupLine($"[cyan]Total Orleans checkpoints: {checkpointCount}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Workflow failed: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
        }
    }

    private static async Task ViewOrleansGrainStateAsync()
    {
        if (_orleansSiloHost == null)
        {
            AnsiConsole.MarkupLine("[red]Orleans silo not running.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[yellow]Grain state inspection requires RavenDB management studio[/]");
        AnsiConsole.MarkupLine("[grey]Or querying grain directly...[/]");

        // Get the grain factory and query state
        var grainFactory = _orleansSiloHost.Services.GetRequiredService<IGrainFactory>();

        var workflowIds = new[]
        {
            "orleans-persistable-workflow-1",
            "orleans-dynamic-workflow-1"
        };

        var table = new Table();
        table.AddColumn("Workflow ID");
        table.AddColumn("Has State");
        table.AddColumn("State");

        foreach (var id in workflowIds)
        {
            try
            {
                var grain = grainFactory.GetGrain<NewOrleans.AsyncPlus.IAsyncStatePersistenceGrain>(id);
                var hasState = await grain.HasPersistedStateAsync();
                var checkpoint = hasState ? await grain.TryGetCheckpointAsync() : null;

                table.AddRow(
                    id,
                    hasState ? "[green]Yes[/]" : "[grey]No[/]",
                    checkpoint?.StateNumber.ToString() ?? "-"
                );
            }
            catch (Exception ex)
            {
                table.AddRow(id, "[red]Error[/]", ex.Message);
            }
        }

        AnsiConsole.Write(table);
        await Task.CompletedTask;
    }

    private static async Task StopOrleansSiloAsync()
    {
        if (_orleansSiloHost == null)
        {
            AnsiConsole.MarkupLine("[yellow]No silo running.[/]");
            return;
        }

        await AnsiConsole.Status()
            .StartAsync("Stopping Orleans silo...", async ctx =>
            {
                await _orleansSiloHost.StopAsync();
                _orleansSiloHost.Dispose();
                _orleansSiloHost = null;
                _orleansPersistence = null;
                ctx.Status("Silo stopped!");
            });

        AnsiConsole.MarkupLine("[green]Orleans silo stopped.[/]");
    }

    /// <summary>
    /// Runs all scenarios and generates a comprehensive diagnostic report.
    /// Designed to produce copy-paste friendly output for debugging.
    /// </summary>
    private static async Task RunAllScenariosWithReportAsync()
    {
        var report = new List<string>();
        var scenarioResults = new List<(string Name, bool Success, int Checkpoints, string? Error)>();

        void Log(string message)
        {
            Console.WriteLine(message);
            report.Add(message);
        }

        Log("╔══════════════════════════════════════════════════════════════════════════════╗");
        Log("║             ASYNC PERSISTENCE SCENARIOS - FULL DIAGNOSTIC REPORT             ║");
        Log("╚══════════════════════════════════════════════════════════════════════════════╝");
        Log("");
        Log($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log($"Machine: {Environment.MachineName}");
        Log($"OS: {Environment.OSVersion}");
        Log($".NET Version: {Environment.Version}");
        Log($"Base Directory: {AppContext.BaseDirectory}");
        Log($"Persistence File: {PersistenceFile}");
        Log("");

        // Check for Roslyn compiler info
        Log("=== COMPILER REFERENCE INFO ===");
        var roslynAsm = typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation).Assembly;
        Log($"Microsoft.CodeAnalysis.CSharp Assembly: {roslynAsm.FullName}");
        Log($"  Location: {(string.IsNullOrEmpty(roslynAsm.Location) ? "<in-memory>" : roslynAsm.Location)}");

        var persistenceAsm = typeof(DOTNExT.Persistence.AsyncPersistenceContext).Assembly;
        Log($"DOTNExT.Persistence Assembly: {persistenceAsm.FullName}");
        Log($"  Location: {(string.IsNullOrEmpty(persistenceAsm.Location) ? "<in-memory>" : persistenceAsm.Location)}");
        Log("");

        // Clear all state before running
        _persistence.ClearAll();
        Log("=== STARTING SCENARIO TESTS ===");
        Log("");

        // Track checkpoints globally
        var checkpointCounts = new Dictionary<string, int>();
        void CountCheckpoints(object? sender, CheckpointEventArgs e)
        {
            if (!checkpointCounts.ContainsKey(e.MethodId))
                checkpointCounts[e.MethodId] = 0;
            checkpointCounts[e.MethodId]++;
        }
        _persistence.OnCheckpoint += CountCheckpoints;

        try
        {
            // === Challenge 1: Basic Checkpoint ===
            Log("--- Challenge 1: Basic Checkpoint (SimpleWorkflow) ---");
            try
            {
                const string wf1Id = "report-simple-workflow";
                _persistence.Clear(wf1Id);
                checkpointCounts.Clear();

                var result = await _workflows.SimpleWorkflow(5, wf1Id);
                var checkpoints = checkpointCounts.GetValueOrDefault(wf1Id, 0);

                Log($"  Input: 5");
                Log($"  Result: {result}");
                Log($"  Checkpoints created: {checkpoints}");
                Log($"  Has persisted state: {_persistence.HasPersistedState(wf1Id)}");
                scenarioResults.Add(("Challenge 1: SimpleWorkflow", true, checkpoints, null));
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                scenarioResults.Add(("Challenge 1: SimpleWorkflow", false, 0, ex.Message));
            }
            Log("");

            // === Challenge 2: Multiple Types ===
            Log("--- Challenge 2: Multiple Types (ProcessOrderWorkflow) ---");
            try
            {
                const string wf2Id = "report-order-workflow";
                _persistence.Clear(wf2Id);
                checkpointCounts.Clear();

                var order = new Order
                {
                    OrderId = "ORD-REPORT",
                    CustomerId = "CUST-REPORT",
                    Items = new List<OrderItem>
                    {
                        new() { ProductId = "P1", Name = "Widget", Price = 10m, Quantity = 2 }
                    }
                };
                var result = await _workflows.ProcessOrderWorkflow(order, wf2Id);
                var checkpoints = checkpointCounts.GetValueOrDefault(wf2Id, 0);

                Log($"  Order: {order.OrderId}");
                Log($"  Result: {result.Message}, Total: {result.Total:C}");
                Log($"  Checkpoints created: {checkpoints}");
                scenarioResults.Add(("Challenge 2: ProcessOrderWorkflow", true, checkpoints, null));
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                scenarioResults.Add(("Challenge 2: ProcessOrderWorkflow", false, 0, ex.Message));
            }
            Log("");

            // === Challenge 3: Nested Async ===
            Log("--- Challenge 3: Nested Async (OuterWorkflow) ---");
            try
            {
                const string wf3Id = "report-outer-workflow";
                _persistence.Clear(wf3Id);
                _persistence.Clear($"{wf3Id}-inner1");
                _persistence.Clear($"{wf3Id}-inner2");
                checkpointCounts.Clear();

                var result = await _workflows.OuterWorkflow(5, wf3Id);
                var nestedCheckpoints = checkpointCounts.Values.Sum();

                Log($"  Input: 5");
                Log($"  Result: {result}");
                Log($"  Total checkpoints (all workflows): {nestedCheckpoints}");
                scenarioResults.Add(("Challenge 3: OuterWorkflow", true, nestedCheckpoints, null));
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                scenarioResults.Add(("Challenge 3: OuterWorkflow", false, 0, ex.Message));
            }
            Log("");

            // === Challenge 4: Exception Handling ===
            Log("--- Challenge 4: Exception Handling (success path) ---");
            try
            {
                const string wf4Id = "report-exception-workflow";
                _persistence.Clear(wf4Id);
                checkpointCounts.Clear();

                var result = await _workflows.WorkflowWithExceptionHandling(10, false, wf4Id);
                var checkpoints = checkpointCounts.GetValueOrDefault(wf4Id, 0);

                Log($"  Input: 10, ShouldFail: false");
                Log($"  Result: {result}");
                Log($"  Checkpoints created: {checkpoints}");
                scenarioResults.Add(("Challenge 4: ExceptionHandling", true, checkpoints, null));
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                scenarioResults.Add(("Challenge 4: ExceptionHandling", false, 0, ex.Message));
            }
            Log("");

            // === Challenge 5: Loops ===
            Log("--- Challenge 5: Loops (LoopWorkflow, 5 iterations) ---");
            try
            {
                const string wf5Id = "report-loop-workflow";
                _persistence.Clear(wf5Id);
                checkpointCounts.Clear();

                var result = await _workflows.LoopWorkflow(5, wf5Id);
                var checkpoints = checkpointCounts.GetValueOrDefault(wf5Id, 0);

                Log($"  Iterations: 5");
                Log($"  Result (sum): {result}");
                Log($"  Checkpoints created: {checkpoints}");
                scenarioResults.Add(("Challenge 5: LoopWorkflow", true, checkpoints, null));
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                scenarioResults.Add(("Challenge 5: LoopWorkflow", false, 0, ex.Message));
            }
            Log("");

            // === Challenge 6: Instrumented State Machine ===
            Log("--- Challenge 6: Instrumented State Machine ---");
            try
            {
                const string wf6Id = "report-instrumented-workflow";
                _persistence.Clear(wf6Id);
                checkpointCounts.Clear();

                int checkpoints;
                using (AsyncPersistenceContext.SetCurrent(_persistence))
                {
                    var runner = new InstrumentedWorkflowRunner(wf6Id);
                    var result = await runner.InstrumentedSimpleWorkflow(5);
                    checkpoints = checkpointCounts.GetValueOrDefault(wf6Id, 0);

                    Log($"  Input: 5");
                    Log($"  Result: {result}");
                    Log($"  Checkpoints created: {checkpoints}");
                    Log($"  Has persisted state: {_persistence.HasPersistedState(wf6Id)}");
                }
                scenarioResults.Add(("Challenge 6: InstrumentedStateMachine", true, checkpoints, null));
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                scenarioResults.Add(("Challenge 6: InstrumentedStateMachine", false, 0, ex.Message));
            }
            Log("");

            // === Challenge 7: Dynamic Compilation ===
            Log("--- Challenge 7: Dynamic Compilation (Modified Roslyn) ---");
            try
            {
                var compiler = new PersistableAsyncCompiler();

                // Show compiler diagnostics
                Log("  Compiler initialization output:");

                // Compile persistable workflow
                var persistableAsm = compiler.CompileAndLoad(PersistableSourceTemplates.SimpleWorkflow, "ReportPersistable");
                var nonPersistableAsm = compiler.CompileAndLoad(PersistableSourceTemplates.NonPersistableWorkflow, "ReportNonPersistable");

                if (persistableAsm == null)
                {
                    Log($"  [[Persistable]] compilation FAILED:");
                    Log($"    {compiler.GetErrorsString()}");
                    scenarioResults.Add(("Challenge 7: [[Persistable]] Compilation", false, 0, "Compilation failed"));
                }
                else
                {
                    Log($"  [[Persistable]] compilation: SUCCESS");

                    // Run and count checkpoints
                    const string wf7PersistId = "report-dynamic-persistable";
                    const string wf7NonPersistId = "report-dynamic-nonpersistable";
                    _persistence.Clear(wf7PersistId);
                    _persistence.Clear(wf7NonPersistId);
                    checkpointCounts.Clear();

                    var persistableType = persistableAsm.GetType("DynamicWorkflows.TestWorkflow")!;
                    var persistableMethod = persistableType.GetMethod("SimpleCalculation")!;
                    var persistableInstance = Activator.CreateInstance(persistableType);

                    using (AsyncPersistenceContext.SetCurrent(_persistence))
                    {
                        var task = (Task<int>)persistableMethod.Invoke(persistableInstance, new object[] { 5 })!;
                        var result = await task;
                        Log($"  [[Persistable]] execution result: {result}");
                    }

                    var persistableCheckpoints = checkpointCounts.Values.Sum();
                    Log($"  [[Persistable]] checkpoints created: {persistableCheckpoints}");

                    // Run non-persistable
                    if (nonPersistableAsm != null)
                    {
                        checkpointCounts.Clear();
                        var nonPersistableType = nonPersistableAsm.GetType("DynamicWorkflows.NonPersistableWorkflow")!;
                        var nonPersistableMethod = nonPersistableType.GetMethod("NormalCalculation")!;
                        var nonPersistableInstance = Activator.CreateInstance(nonPersistableType);

                        using (AsyncPersistenceContext.SetCurrent(_persistence))
                        {
                            var task = (Task<int>)nonPersistableMethod.Invoke(nonPersistableInstance, new object[] { 5 })!;
                            var result = await task;
                            Log($"  Non-Persistable execution result: {result}");
                        }

                        var nonPersistableCheckpoints = checkpointCounts.Values.Sum();
                        Log($"  Non-Persistable checkpoints created: {nonPersistableCheckpoints}");

                        // Determine if modified Roslyn is working
                        if (persistableCheckpoints > 0 && nonPersistableCheckpoints == 0)
                        {
                            Log($"  *** MODIFIED ROSLYN VERIFIED: [[Persistable]] has checkpoints, Non-Persistable does not ***");
                        }
                        else if (persistableCheckpoints == 0)
                        {
                            Log($"  NOTE: No checkpoints for [[Persistable]] - likely using STOCK Roslyn");
                        }
                    }

                    scenarioResults.Add(("Challenge 7: DynamicCompilation", true, persistableCheckpoints, null));
                }
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                Log($"  Stack: {ex.StackTrace}");
                scenarioResults.Add(("Challenge 7: DynamicCompilation", false, 0, ex.Message));
            }
            Log("");

            // === Challenge 8: Orleans/RavenDB Persistence ===
            Log("--- Challenge 8: Orleans/RavenDB Persistence ---");
            try
            {
                Log("  Starting Orleans silo with memory storage...");

                // Start silo with memory storage for the automated test
                await StartOrleansSiloAsync(useRavenDb: false);

                if (_orleansSiloHost != null && _orleansPersistence != null)
                {
                    const string wf8Id = "report-orleans-workflow";
                    checkpointCounts.Clear();

                    // Subscribe to Orleans persistence events
                    int orleansCheckpoints = 0;
                    void CountOrleansCheckpoints(object? s, CheckpointEventArgs e)
                    {
                        orleansCheckpoints++;
                    }
                    _orleansPersistence.OnCheckpoint += CountOrleansCheckpoints;

                    using (AsyncPersistenceContext.SetCurrent(_orleansPersistence))
                    {
                        var runner = new InstrumentedWorkflowRunner(wf8Id);
                        var result = await runner.InstrumentedSimpleWorkflow(7);
                        Log($"  Input: 7");
                        Log($"  Result: {result}");
                    }

                    _orleansPersistence.OnCheckpoint -= CountOrleansCheckpoints;

                    Log($"  Orleans checkpoints created: {orleansCheckpoints}");

                    // Query grain state
                    var grainFactory = _orleansSiloHost.Services.GetRequiredService<IGrainFactory>();
                    var grain = grainFactory.GetGrain<NewOrleans.AsyncPlus.IAsyncStatePersistenceGrain>(wf8Id);
                    var hasState = await grain.HasPersistedStateAsync();
                    Log($"  Grain has persisted state: {hasState}");

                    scenarioResults.Add(("Challenge 8: Orleans Persistence", true, orleansCheckpoints, null));

                    // Stop silo
                    await StopOrleansSiloAsync();
                }
                else
                {
                    Log("  ERROR: Failed to start Orleans silo");
                    scenarioResults.Add(("Challenge 8: Orleans Persistence", false, 0, "Silo failed to start"));
                }
            }
            catch (Exception ex)
            {
                Log($"  ERROR: {ex.Message}");
                Log($"  Stack: {ex.StackTrace}");
                scenarioResults.Add(("Challenge 8: Orleans Persistence", false, 0, ex.Message));

                // Try to clean up silo if it was started
                if (_orleansSiloHost != null)
                {
                    try { await StopOrleansSiloAsync(); } catch { }
                }
            }
            Log("");
        }
        finally
        {
            _persistence.OnCheckpoint -= CountCheckpoints;
        }

        // === Summary ===
        Log("╔══════════════════════════════════════════════════════════════════════════════╗");
        Log("║                              SUMMARY                                         ║");
        Log("╚══════════════════════════════════════════════════════════════════════════════╝");
        Log("");
        Log($"{"Scenario",-45} {"Status",-10} {"Checkpoints",-12}");
        Log(new string('-', 70));

        foreach (var (name, success, checkpoints, error) in scenarioResults)
        {
            var status = success ? "PASS" : "FAIL";
            Log($"{name,-45} {status,-10} {checkpoints,-12}");
            if (!success && error != null)
            {
                Log($"  Error: {error}");
            }
        }

        Log("");
        var totalPassed = scenarioResults.Count(r => r.Success);
        var totalCheckpoints = scenarioResults.Sum(r => r.Checkpoints);
        Log($"Total: {totalPassed}/{scenarioResults.Count} passed, {totalCheckpoints} total checkpoints created");
        Log("");

        // Final persisted state
        Log("=== FINAL PERSISTED STATE ===");
        var persistedIds = _persistence.GetPersistedMethodIds().ToList();
        if (persistedIds.Count == 0)
        {
            Log("  (no persisted state)");
        }
        else
        {
            foreach (var id in persistedIds)
            {
                var snapshot = _persistence.GetSnapshot(id);
                if (snapshot != null)
                {
                    Log($"  {id}: state={snapshot.State}, fields={string.Join(",", snapshot.Fields.Keys)}");
                }
            }
        }
        Log("");

        Log("╔══════════════════════════════════════════════════════════════════════════════╗");
        Log("║                           END OF REPORT                                      ║");
        Log("╚══════════════════════════════════════════════════════════════════════════════╝");

        // Option to copy
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]Report complete. The above output can be copied for debugging.[/]");
    }
}
