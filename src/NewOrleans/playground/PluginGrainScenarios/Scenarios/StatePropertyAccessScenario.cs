using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using PluginGrainScenarios.Grains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 8: State Property Access
///
/// Tests the StatePropertyAccess feature:
/// - StateTask&lt;T&gt; struct (awaitable get, &lt;&lt; operator for set)
/// - Get/Set method pattern on grain interfaces
/// - Property-style access wrapping Get/Set methods
///
/// The StatePropertyAccess feature enables two equivalent styles:
/// - Method style: await grain.SetName("Louis"); var name = await grain.GetName();
/// - Property style: await (grain.Name &lt;&lt; "Louis"); var name = await grain.Name;
/// </summary>
public static class StatePropertyAccessScenario
{
    private static int _passCount;
    private static int _failCount;

   

    public static async Task RunAsync()
    {
        _passCount = 0;
        _failCount = 0;

        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 8: State Property Access[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        ShowFeatureOverview();

        // ════════════════════════════════════════════════════════════════════
        // Phase 1: Test StateTask<T> struct directly (unit test)
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 1: StateTask<T> struct operations[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestStateTaskStruct();
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 2: Test method-style grain access
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 2: Method-style grain access[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        AnsiConsole.MarkupLine("[yellow]Starting Orleans silo...[/]");
        using var host = SiloHelper.BuildSingleSilo(logLevel: LogLevel.Warning);
        await host.StartAsync();
        AnsiConsole.MarkupLine("[green]Silo started[/]");
        AnsiConsole.WriteLine();

        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

        await TestMethodStyleAccess(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 3: Test property-style access using StateTask<T>
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 3: Property-style access using StateTask<T>[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestPropertyStyleAccess(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 4: Both styles on same grain
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 4: Mixed style access[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestMixedStyleAccess(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 5: Direct grain.Name access on IPartialPropertyTestGrain
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 5: Direct grain.Name property access (partial properties)[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestDirectPropertyAccess(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 6: IPersistentState property mapping
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 6: IPersistentState property mapping[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestPersistedPropertyAccess(grainFactory);
        AnsiConsole.WriteLine();

        // Cleanup
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 8 Complete[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        if (_failCount == 0)
            AnsiConsole.MarkupLine($"[green]All tests passed! ({_passCount} passed)[/]");
        else
            AnsiConsole.MarkupLine($"[red]Results: {_passCount} passed, {_failCount} failed[/]");
    }

    private static void ShowFeatureOverview()
    {
        AnsiConsole.MarkupLine("[bold]StatePropertyAccess Feature[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Style");
        table.AddColumn("Syntax");
        table.AddColumn("Description");

        table.AddRow("Method", "[grey]await grain.SetName(\"Louis\")[/]", "Standard Orleans RPC call");
        table.AddRow("Method", "[grey]var name = await grain.GetName()[/]", "Standard Orleans RPC call");
        table.AddRow("Property", "[grey]await (grain.Name << \"Louis\")[/]", "StateTask<T> set via << operator");
        table.AddRow("Property", "[grey]var name = await grain.Name[/]", "StateTask<T> awaitable get");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void Pass(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓ PASS[/] {message}");
        _passCount++;
    }

    private static void Fail(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗ FAIL[/] {message}");
        _failCount++;
    }

    private static async Task TestStateTaskStruct()
    {
        AnsiConsole.MarkupLine("[grey]Testing StateTask<T> struct operations...[/]");

        // Test with in-memory backing store
        string storedValue = "initial";

        var stateTask = new StateTask<string>(
            getter: () => new ValueTask<string>(storedValue),
            setter: v => { storedValue = v; return ValueTask.CompletedTask; }
        );

        // Test 1: GetAsync
        try
        {
            var value = await stateTask.GetAsync();
            if (value == "initial")
                Pass("StateTask<T>.GetAsync() returns value");
            else
                Fail($"GetAsync expected 'initial', got '{value}'");
        }
        catch (Exception ex)
        {
            Fail($"GetAsync threw: {ex.Message}");
        }

        // Test 2: SetAsync
        try
        {
            await stateTask.SetAsync("updated");
            if (storedValue == "updated")
                Pass("StateTask<T>.SetAsync() updates value");
            else
                Fail($"SetAsync didn't update, value is '{storedValue}'");
        }
        catch (Exception ex)
        {
            Fail($"SetAsync threw: {ex.Message}");
        }

        // Test 3: Awaitable (GetAwaiter)
        try
        {
            storedValue = "awaitable-test";
            string value = await stateTask;  // Uses GetAwaiter()
            if (value == "awaitable-test")
                Pass("StateTask<T> is awaitable (await stateTask)");
            else
                Fail($"await expected 'awaitable-test', got '{value}'");
        }
        catch (Exception ex)
        {
            Fail($"await threw: {ex.Message}");
        }

        // Test 4: << operator
        try
        {
            await (stateTask << "operator-test");  // Uses operator <<
            if (storedValue == "operator-test")
                Pass("StateTask<T> << operator works (await (st << value))");
            else
                Fail($"<< didn't update, value is '{storedValue}'");
        }
        catch (Exception ex)
        {
            Fail($"<< operator threw: {ex.Message}");
        }

        // Test 5: Value type (int)
        try
        {
            int storedInt = 0;
            var intStateTask = new StateTask<int>(
                getter: () => new ValueTask<int>(storedInt),
                setter: v => { storedInt = v; return ValueTask.CompletedTask; }
            );

            await (intStateTask << 42);
            int result = await intStateTask;

            if (result == 42)
                Pass("StateTask<int> works for value types");
            else
                Fail($"value type expected 42, got {result}");
        }
        catch (Exception ex)
        {
            Fail($"value type test threw: {ex.Message}");
        }
    }

    private static async Task TestMethodStyleAccess(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing method-style access via Orleans...[/]");

        var grain = grainFactory.GetGrain<IStatePropertyTestGrain>("method-test");

        // Test Get/Set for string
        try
        {
            await grain.SetName("MethodStyleName");
            var name = await grain.GetName();

            if (name == "MethodStyleName")
                Pass("SetName/GetName works via Orleans RPC");
            else
                Fail($"Expected 'MethodStyleName', got '{name}'");
        }
        catch (Exception ex)
        {
            Fail($"String property: {ex.Message}");
        }

        // Test Get/Set for int
        try
        {
            await grain.SetScore(100);
            var score = await grain.GetScore();

            if (score == 100)
                Pass("SetScore/GetScore works via Orleans RPC");
            else
                Fail($"Expected 100, got {score}");
        }
        catch (Exception ex)
        {
            Fail($"Int property: {ex.Message}");
        }

        // Test read-only (no setter)
        try
        {
            var createdAt = await grain.GetCreatedAt();
            Pass($"GetCreatedAt works (read-only): {createdAt:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            Fail($"Read-only property: {ex.Message}");
        }
    }

    private static async Task TestPropertyStyleAccess(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing property-style access using StateTask<T>...[/]");

        var grain = grainFactory.GetGrain<IStatePropertyTestGrain>("property-test");

        // Create StateTask wrappers (this is what the proxy codegen would produce)
        var nameProperty = new StateTask<string>(
            getter: () => new ValueTask<string>(grain.GetName()),
            setter: v => new ValueTask(grain.SetName(v))
        );

        var scoreProperty = new StateTask<int>(
            getter: () => new ValueTask<int>(grain.GetScore()),
            setter: v => new ValueTask(grain.SetScore(v))
        );
        
        // Test property-style set
        try
        {
            string tx = await nameProperty;
            await (nameProperty << "PropertyStyleName");
            string name = await nameProperty;

            if (name == "PropertyStyleName")
                Pass("Property-style: await (name << value) and await name");
            else
                Fail($"Expected 'PropertyStyleName', got '{name}'");
        }
        catch (Exception ex)
        {
            Fail($"Property-style string: {ex.Message}");
        }

        // Test property-style for int
        try
        {
            await (scoreProperty << 999);
            int score = await scoreProperty;

            if (score == 999)
                Pass("Property-style: works for value types (int)");
            else
                Fail($"Expected 999, got {score}");
        }
        catch (Exception ex)
        {
            Fail($"Property-style int: {ex.Message}");
        }
    }

    private static async Task TestMixedStyleAccess(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing mixed method/property style on same grain...[/]");

        var grain = grainFactory.GetGrain<IStatePropertyTestGrain>("mixed-test");

        var nameProperty = new StateTask<string>(
            getter: () => new ValueTask<string>(grain.GetName()),
            setter: v => new ValueTask(grain.SetName(v))
        );

        try
        {
            // Set via method
            await grain.SetName("SetViaMethod");
            
            // Read via property-style
            string name1 = await nameProperty;

            // Set via property-style
            await (nameProperty << "SetViaProperty");

            // Read via method
            string name2 = await grain.GetName();

            if (name1 == "SetViaMethod" && name2 == "SetViaProperty")
                Pass("Mixed styles work: method set → property get, property set → method get");
            else
                Fail($"Mixed: name1='{name1}' (expected 'SetViaMethod'), name2='{name2}' (expected 'SetViaProperty')");
        }
        catch (Exception ex)
        {
            Fail($"Mixed style: {ex.Message}");
        }

        // Test custom method coexistence
        try
        {
            await grain.SetName("Alice");
            await grain.SetScore(50);
            var combined = await grain.GetCombinedInfo();

            if (combined == "Alice: 50 pts")
                Pass("Custom method GetCombinedInfo() coexists with state properties");
            else
                Fail($"Expected 'Alice: 50 pts', got '{combined}'");
        }
        catch (Exception ex)
        {
            Fail($"Custom method: {ex.Message}");
        }
    }

    /// <summary>
    /// Phase 5: Test direct grain.Name property access on IPartialPropertyTestGrain.
    /// This tests the full code generation pipeline where StateTask&lt;T&gt; properties
    /// are generated on the partial interface and implemented by the proxy.
    /// </summary>
    private static async Task TestDirectPropertyAccess(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing direct property access on IPartialPropertyTestGrain...[/]");

        var grain = grainFactory.GetGrain<IPartialPropertyTestGrain>("direct-property-test");

        // Test 1: Direct property set using << operator
        try
        {
            await (grain.Name << "DirectPropertyValue");
            Pass($"grain.Name << value compiles and executes : = {await grain.Name}");
        }
        catch (Exception ex)
        {
            Fail($"grain.Name << value: {ex.Message}");
        }

        // Test 2: Direct property get using await
        try
        {
            string name = await grain.Name;
            if (name == "DirectPropertyValue")
                Pass("await grain.Name returns correct value");
            else
                Fail($"Expected 'DirectPropertyValue', got '{name}'");
        }
        catch (Exception ex)
        {
            Fail($"await grain.Name: {ex.Message}");
        }

        // Test 3: Method style still works alongside property style
        try
        {
            await grain.SetName("MethodSetValue");
            string viaProperty = await grain.Name;
            string viaMethod = await grain.GetName();

            if (viaProperty == "MethodSetValue" && viaMethod == "MethodSetValue")
                Pass("Method set → property get and method get both work");
            else
                Fail($"Mismatch: viaProperty='{viaProperty}', viaMethod='{viaMethod}'");
        }
        catch (Exception ex)
        {
            Fail($"Mixed access: {ex.Message}");
        }

        // Test 4: Property set -> method get
        try
        {
            await (grain.Name << "PropertySetValue");
            string viaMethod = await grain.GetName();

            if (viaMethod == "PropertySetValue")
                Pass("Property set → method get works");
            else
                Fail($"Expected 'PropertySetValue', got '{viaMethod}'");
        }
        catch (Exception ex)
        {
            Fail($"Property → method: {ex.Message}");
        }

        // Test 5: Score (int) property
        try
        {
            await (grain.Score << 42);
            int score = await grain.Score;

            if (score == 42)
                Pass($"grain.Score (int) property works : = {await grain.Score}");
            else
                Fail($"Expected 42, got {score}");
        }
        catch (Exception ex)
        {
            Fail($"grain.Score: {ex.Message}");
        }

        // Test 6: CreatedAt (read-only property)
        try
        {
            DateTime createdAt = await grain.CreatedAt;
            Pass($"grain.CreatedAt (read-only) works: {createdAt:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            Fail($"grain.CreatedAt: {ex.Message}");
        }

        // Test 7: GetCombinedInfo custom method coexistence
        try
        {
            await (grain.Name << "Louis");
            await (grain.Score << 100);
            string combined = await grain.GetCombinedInfo();

            if (combined == "Louis: 100 pts")
                Pass("Custom method coexists with generated StateTask properties");
            else
                Fail($"Expected 'Louis: 100 pts', got '{combined}'");
        }
        catch (Exception ex)
        {
            Fail($"Custom method: {ex.Message}");
        }
    }

    /// <summary>
    /// Phase 6: Test IPersistentState property mapping.
    /// This tests the [State(Persisted = true, StateProperty = "...")] feature
    /// where partial properties are mapped to IPersistentState&lt;T&gt;.State.
    /// </summary>
    private static async Task TestPersistedPropertyAccess(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing IPersistentState property mapping...[/]");

        var grainId = $"persisted-test-{Guid.NewGuid():N}";
        var grain = grainFactory.GetGrain<IPersistedPropertyTestGrain>(grainId);

        // Test 1: Basic persisted property set via method (generated SetName)
        try
        {
            await grain.SetName("PersistedPlayer");
            Pass("SetName() method works on persisted property");
        }
        catch (Exception ex)
        {
            Fail($"SetName on persisted property: {ex.Message}");
        }

        // Test 2: Basic persisted property get via method (generated GetName)
        try
        {
            var name = await grain.GetName();
            if (name == "PersistedPlayer")
                Pass("GetName() returns persisted value");
            else
                Fail($"Expected 'PersistedPlayer', got '{name}'");
        }
        catch (Exception ex)
        {
            Fail($"GetName on persisted property: {ex.Message}");
        }

        // Test 3: Property-style access on persisted property (if proxy has StateTask<T>)
        try
        {
            await (grain.Name << "UpdatedViaProperty");
            string name = await grain.Name;
            if (name == "UpdatedViaProperty")
                Pass("Property-style (grain.Name) works on persisted property");
            else
                Fail($"Expected 'UpdatedViaProperty', got '{name}'");
        }
        catch (Exception ex)
        {
            Fail($"Property-style on persisted: {ex.Message}");
        }

        // Test 4: AutoSave property (Score) - changes should persist automatically
        try
        {
            await grain.SetScore(500);
            // Score has AutoSave = true, so it should call WriteStateAsync automatically

            // Get a fresh grain reference to verify persistence
            var grain2 = grainFactory.GetGrain<IPersistedPropertyTestGrain>(grainId);
            await grain2.RefreshState(); // Force read from storage
            var score = await grain2.GetScore();

            if (score == 500)
                Pass("AutoSave property persists automatically");
            else
                Fail($"AutoSave: Expected 500, got {score} (may not have persisted)");
        }
        catch (Exception ex)
        {
            Fail($"AutoSave property: {ex.Message}");
        }

        // Test 5: Non-AutoSave property requires manual save
        try
        {
            await grain.SetLevel(10);
            await grain.SaveState(); // Manually save

            var grain3 = grainFactory.GetGrain<IPersistedPropertyTestGrain>(grainId);
            await grain3.RefreshState();
            var level = await grain3.GetLevel();

            if (level == 10)
                Pass("Non-AutoSave property persists after manual SaveState()");
            else
                Fail($"Non-AutoSave: Expected 10, got {level}");
        }
        catch (Exception ex)
        {
            Fail($"Non-AutoSave property: {ex.Message}");
        }

        // Test 6: Non-persisted property (SessionId) - should use backing field
        try
        {
            await grain.SetSessionId("session-123");
            var sessionId = await grain.GetSessionId();

            if (sessionId == "session-123")
                Pass("Non-persisted property (SessionId) works with backing field");
            else
                Fail($"Expected 'session-123', got '{sessionId}'");
        }
        catch (Exception ex)
        {
            Fail($"Non-persisted property: {ex.Message}");
        }

        // Test 7: Custom method GetSummary() coexists with persisted properties
        try
        {
            await grain.SetName("TestPlayer");
            await grain.SetScore(999);
            await grain.SetLevel(5);

            var summary = await grain.GetSummary();
            if (summary == "Player 'TestPlayer' - Score: 999, Level: 5")
                Pass("Custom method GetSummary() coexists with persisted properties");
            else
                Fail($"Expected summary format, got '{summary}'");
        }
        catch (Exception ex)
        {
            Fail($"Custom method: {ex.Message}");
        }

        // Test 8: Verify persisted vs non-persisted after "grain death" simulation
        // Note: In-memory storage won't truly persist across silo restarts,
        // but we can verify the state object mapping works correctly
        try
        {
            await grain.SetName("PersistMe");
            await grain.SetSessionId("LoseMe");
            await grain.SaveState();

            // Refresh to simulate re-reading from storage
            await grain.RefreshState();

            var name = await grain.GetName();
            var sessionId = await grain.GetSessionId();

            // Name should come from storage, SessionId from backing field (still in memory)
            if (name == "PersistMe")
                Pass("Persisted property survives RefreshState()");
            else
                Fail($"Persisted name: Expected 'PersistMe', got '{name}'");

            // SessionId may or may not survive depending on implementation
            // (it's non-persisted so uses backing field)
            AnsiConsole.MarkupLine($"[grey]  (Non-persisted SessionId after refresh: '{sessionId}')[/]");
        }
        catch (Exception ex)
        {
            Fail($"Persistence verification: {ex.Message}");
        }
    }
}
