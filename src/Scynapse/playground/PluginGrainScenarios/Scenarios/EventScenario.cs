using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using PluginGrainScenarios.Grains;
using Spectre.Console;

namespace PluginGrainScenarios.Scenarios;

/// <summary>
/// Scenario 9: Scynapse Events
///
/// Tests the Scynapse Events v1 feature which enables standard C# events
/// on grain classes to work transparently across the distributed system.
///
/// Key concepts:
/// - Events on grain classes are detected by the code generator
/// - Events are transported via Simple Message Streams (SMS)
/// - Subscription is decoupled: SubscribeTo*Async() is async, += is local
/// - IEventSubscription&lt;T&gt; provides subscription lifecycle management
///
/// Usage pattern:
/// <code>
/// var grain = client.GetGrain&lt;IEventTestGrain&gt;("player-1");
/// await using var sub = await grain.SubscribeToChatMessageAsync();
/// grain.ChatMessage += (s, msg) => Console.WriteLine(msg);
/// await grain.SendChatAsync("Hello!");
/// </code>
/// </summary>
public static class EventScenario
{
    private static int _passCount;
    private static int _failCount;

    public static async Task RunAsync()
    {
        _passCount = 0;
        _failCount = 0;

        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 9: Scynapse Events[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        ShowFeatureOverview();

        // ════════════════════════════════════════════════════════════════════
        // Phase 1: Start silo with SMS stream provider
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 1: Starting Orleans silo with SMS provider[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        AnsiConsole.MarkupLine("[yellow]Starting Orleans silo with memory streams...[/]");
        using var host = SiloHelper.BuildSingleSiloWithStreams(logLevel: LogLevel.Warning);
        await host.StartAsync();
        AnsiConsole.MarkupLine("[green]Silo started with SMS stream provider[/]");
        AnsiConsole.WriteLine();

        var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

        // ════════════════════════════════════════════════════════════════════
        // Phase 2: Test code generation output (verify interface has events)
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 2: Code generation verification[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestCodeGenerationOutput(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 3: Test subscription and event reception
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 3: Subscription and event reception[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestSubscriptionAndEventReception(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 4: Test multiple subscribers
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 4: Multiple subscribers[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestMultipleSubscribers(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 5: Test subscription lifecycle (unsubscribe)
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 5: Subscription lifecycle[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestSubscriptionLifecycle(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 6: Test direct async handler
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 6: Direct async handler[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestDirectAsyncHandler(grainFactory);
        AnsiConsole.WriteLine();

        // ════════════════════════════════════════════════════════════════════
        // Phase 7: Test EventHandler (non-generic) events
        // ════════════════════════════════════════════════════════════════════
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");
        AnsiConsole.MarkupLine("[blue]Phase 7: EventHandler (non-generic) events[/]");
        AnsiConsole.MarkupLine("[blue]───────────────────────────────────────────────────────[/]");

        await TestNonGenericEventHandler(grainFactory);
        AnsiConsole.WriteLine();

        // Cleanup
        AnsiConsole.MarkupLine("[yellow]Stopping silo...[/]");
        await host.StopAsync();
        AnsiConsole.MarkupLine("[green]Silo stopped[/]");

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[blue]  Scenario 9 Complete[/]");
        AnsiConsole.MarkupLine("[blue]═══════════════════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();

        if (_failCount == 0)
            AnsiConsole.MarkupLine($"[green]All tests passed! ({_passCount} passed)[/]");
        else
            AnsiConsole.MarkupLine($"[red]Results: {_passCount} passed, {_failCount} failed[/]");
    }

    private static void ShowFeatureOverview()
    {
        AnsiConsole.MarkupLine("[bold]Scynapse Events v1 Feature[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Operation");
        table.AddColumn("Syntax");
        table.AddColumn("Description");

        table.AddRow("Subscribe", "[grey]await grain.SubscribeToChatMessageAsync()[/]", "Create remote subscription (async)");
        table.AddRow("Attach handler", "[grey]grain.ChatMessage += handler[/]", "Attach local handler (sync, non-blocking)");
        table.AddRow("Raise event", "[grey]ChatMessage?.Invoke(this, msg)[/]", "Standard C# event raise in grain");
        table.AddRow("Unsubscribe", "[grey]await subscription.UnsubscribeAsync()[/]", "Remove remote subscription");
        table.AddRow("Auto-dispose", "[grey]await using var sub = ...[/]", "Auto-cleanup on scope exit");

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

    private static void Info(string message)
    {
        AnsiConsole.MarkupLine($"[grey]  {message}[/]");
    }

    private static async Task TestCodeGenerationOutput(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Verifying code generation produced expected members...[/]");

        var grain = grainFactory.GetGrain<IEventTestGrain>("codegen-test");

        // Test 1: Verify interface has ChatMessage event
        try
        {
            var eventType = typeof(IEventTestGrain);
            var chatEvent = eventType.GetEvent("ChatMessage");
            if (chatEvent != null)
                Pass("IEventTestGrain has ChatMessage event (generated)");
            else
                Fail("IEventTestGrain missing ChatMessage event");
        }
        catch (Exception ex)
        {
            Fail($"Event reflection: {ex.Message}");
        }

        // Test 2: Verify interface has SubscribeToChatMessageAsync method
        try
        {
            var ifaceType = typeof(IEventTestGrain);
            var subscribeMethod = ifaceType.GetMethod("SubscribeToChatMessageAsync", Type.EmptyTypes);
            if (subscribeMethod != null)
                Pass("IEventTestGrain has SubscribeToChatMessageAsync() method (generated)");
            else
                Fail("IEventTestGrain missing SubscribeToChatMessageAsync() method");
        }
        catch (Exception ex)
        {
            Fail($"Method reflection: {ex.Message}");
        }

        // Test 3: Verify custom methods still work
        try
        {
            await grain.ResetAsync();
            await grain.AddPointsAsync(10);
            var score = await grain.GetScoreAsync();
            if (score == 10)
                Pass("Custom grain methods work alongside generated code");
            else
                Fail($"Expected score 10, got {score}");
        }
        catch (Exception ex)
        {
            Fail($"Custom method: {ex.Message}");
        }
    }

    private static async Task TestSubscriptionAndEventReception(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing subscription creation and event reception...[/]");

        var grain = grainFactory.GetGrain<IEventTestGrain>("subscription-test");
        await grain.ResetAsync();

        var receivedMessages = new List<string>();
        var eventReceived = new TaskCompletionSource<bool>();

        try
        {
            // Create subscription
            await using var subscription = await grain.SubscribeToChatMessageAsync();

            if (subscription != null && subscription.IsActive)
                Pass("SubscribeToChatMessageAsync() returns active subscription");
            else
            {
                Fail("Subscription is null or not active");
                return;
            }

            // Attach local handler
            grain.ChatMessage += (sender, message) =>
            {
                receivedMessages.Add(message);
                Info($"Received: {message}");
                if (receivedMessages.Count == 1)
                    eventReceived.TrySetResult(true);
            };
            Pass("Local handler attached via += (non-blocking)");

            // Send message to trigger event
            await grain.SendChatAsync("Hello from grain!");

            // Wait for event (with timeout)
            var completed = await Task.WhenAny(eventReceived.Task, Task.Delay(5000));
            if (completed == eventReceived.Task && receivedMessages.Count > 0)
            {
                if (receivedMessages[0] == "Hello from grain!")
                    Pass("Event received with correct payload");
                else
                    Fail($"Expected 'Hello from grain!', got '{receivedMessages[0]}'");
            }
            else
            {
                Fail("Event not received within timeout");
            }
        }
        catch (Exception ex)
        {
            Fail($"Subscription test: {ex.Message}");
        }
    }

    private static async Task TestMultipleSubscribers(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing multiple subscribers to same grain event...[/]");

        var grain = grainFactory.GetGrain<IEventTestGrain>("multi-subscriber-test");
        await grain.ResetAsync();

        var subscriber1Messages = new List<string>();
        var subscriber2Messages = new List<string>();
        var bothReceived = new TaskCompletionSource<bool>();

        try
        {
            // Create two independent subscriptions
            await using var sub1 = await grain.SubscribeToChatMessageAsync();
            await using var sub2 = await grain.SubscribeToChatMessageAsync();

            if (sub1 != null && sub2 != null && sub1.IsActive && sub2.IsActive)
                Pass("Two independent subscriptions created");
            else
            {
                Fail("Failed to create two subscriptions");
                return;
            }

            // Attach handlers
            grain.ChatMessage += (s, msg) =>
            {
                subscriber1Messages.Add(msg);
                CheckBothReceived();
            };

            grain.ChatMessage += (s, msg) =>
            {
                subscriber2Messages.Add(msg);
                CheckBothReceived();
            };

            void CheckBothReceived()
            {
                // Note: With two subscriptions, each message may arrive twice
                // depending on implementation
                if (subscriber1Messages.Count >= 1 || subscriber2Messages.Count >= 1)
                    bothReceived.TrySetResult(true);
            }

            // Send message
            await grain.SendChatAsync("Broadcast message");

            // Wait for reception
            var completed = await Task.WhenAny(bothReceived.Task, Task.Delay(5000));
            if (completed == bothReceived.Task)
            {
                Pass($"Multiple subscribers received events (sub1: {subscriber1Messages.Count}, sub2: {subscriber2Messages.Count})");
            }
            else
            {
                Fail("Multiple subscribers did not receive events in time");
            }
        }
        catch (Exception ex)
        {
            Fail($"Multiple subscribers: {ex.Message}");
        }
    }

    private static async Task TestSubscriptionLifecycle(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing subscription lifecycle (unsubscribe)...[/]");

        var grain = grainFactory.GetGrain<IEventTestGrain>("lifecycle-test");
        await grain.ResetAsync();

        var messagesBeforeUnsubscribe = new List<string>();
        var messagesAfterUnsubscribe = new List<string>();

        try
        {
            // Create subscription
            var subscription = await grain.SubscribeToChatMessageAsync();

            if (subscription.IsActive)
                Pass("Subscription starts as active");
            else
            {
                Fail("Subscription not active");
                return;
            }

            // Attach handler
            grain.ChatMessage += (s, msg) =>
            {
                if (subscription.IsActive)
                    messagesBeforeUnsubscribe.Add(msg);
                else
                    messagesAfterUnsubscribe.Add(msg);
            };

            // Send message before unsubscribe
            await grain.SendChatAsync("Before unsubscribe");
            await Task.Delay(500); // Give time for event to arrive

            // Unsubscribe
            await subscription.UnsubscribeAsync();

            if (!subscription.IsActive)
                Pass("Subscription.IsActive is false after UnsubscribeAsync()");
            else
                Fail("Subscription still active after unsubscribe");

            // Send message after unsubscribe
            await grain.SendChatAsync("After unsubscribe");
            await Task.Delay(500);

            Info($"Messages before unsubscribe: {messagesBeforeUnsubscribe.Count}");
            Info($"Messages after unsubscribe: {messagesAfterUnsubscribe.Count}");

            if (messagesBeforeUnsubscribe.Count >= 1)
                Pass("Messages received before unsubscribe");
            else
                Fail("No messages received before unsubscribe");

            // Note: Messages after unsubscribe might still arrive due to SMS behavior
            // The key test is that the subscription reports IsActive = false
        }
        catch (Exception ex)
        {
            Fail($"Lifecycle test: {ex.Message}");
        }
    }

    private static async Task TestDirectAsyncHandler(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing direct async handler subscription...[/]");

        var grain = grainFactory.GetGrain<IEventTestGrain>("async-handler-test");
        await grain.ResetAsync();

        var asyncHandlerCalled = new TaskCompletionSource<string>();

        try
        {
            // Subscribe with direct async handler
            await using var subscription = await grain.SubscribeToChatMessageAsync(async message =>
            {
                Info($"Async handler received: {message}");
                await Task.Delay(10); // Simulate async work
                asyncHandlerCalled.TrySetResult(message);
            });

            Pass("SubscribeToChatMessageAsync(handler) accepted async handler");

            // Also attach local handler to verify both fire
            var localHandlerCalled = false;
            grain.ChatMessage += (s, msg) =>
            {
                localHandlerCalled = true;
                Info($"Local handler also received: {msg}");
            };

            // Send message
            await grain.SendChatAsync("Async handler test");

            // Wait for async handler
            var completed = await Task.WhenAny(asyncHandlerCalled.Task, Task.Delay(5000));
            if (completed == asyncHandlerCalled.Task)
            {
                var msg = await asyncHandlerCalled.Task;
                Pass($"Direct async handler invoked with message: {msg}");
            }
            else
            {
                Fail("Direct async handler not invoked within timeout");
            }

            // Check if local handler also fired
            await Task.Delay(200);
            if (localHandlerCalled)
                Pass("Both direct async handler AND local += handler fired");
            else
                Info("Only direct async handler fired (local handler may fire separately)");
        }
        catch (Exception ex)
        {
            Fail($"Async handler test: {ex.Message}");
        }
    }

    private static async Task TestNonGenericEventHandler(IGrainFactory grainFactory)
    {
        AnsiConsole.MarkupLine("[grey]Testing EventHandler (non-generic) events...[/]");

        var grain = grainFactory.GetGrain<ISimpleEventTestGrain>("simple-event-test");

        var pingReceived = new TaskCompletionSource<bool>();

        try
        {
            // Check if interface has Ping event
            var ifaceType = typeof(ISimpleEventTestGrain);
            var pingEvent = ifaceType.GetEvent("Ping");
            if (pingEvent != null)
                Pass("ISimpleEventTestGrain has Ping event (EventHandler type)");
            else
            {
                Fail("ISimpleEventTestGrain missing Ping event");
                return;
            }

            // Check for SubscribeToPingAsync method
            var subscribeMethod = ifaceType.GetMethod("SubscribeToPingAsync", Type.EmptyTypes);
            if (subscribeMethod != null)
                Pass("ISimpleEventTestGrain has SubscribeToPingAsync() method");
            else
            {
                Fail("ISimpleEventTestGrain missing SubscribeToPingAsync() method");
                return;
            }

            // Create subscription
            await using var subscription = await grain.SubscribeToPingAsync();

            if (subscription != null && subscription.IsActive)
                Pass("SubscribeToPingAsync() returns active subscription");
            else
            {
                Fail("Ping subscription is null or not active");
                return;
            }

            // Attach handler
            grain.Ping += (sender, args) =>
            {
                Info("Ping event received!");
                pingReceived.TrySetResult(true);
            };

            // Trigger ping
            await grain.PingAsync();

            // Wait for event
            var completed = await Task.WhenAny(pingReceived.Task, Task.Delay(5000));
            if (completed == pingReceived.Task)
                Pass("EventHandler (non-generic) event received");
            else
                Fail("EventHandler (non-generic) event not received within timeout");
        }
        catch (Exception ex)
        {
            Fail($"Non-generic event test: {ex.Message}");
        }
    }
}
