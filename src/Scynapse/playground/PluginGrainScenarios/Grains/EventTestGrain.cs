using Orleans;

namespace PluginGrainScenarios.Grains;

// ============================================================================
// NEWORLEANS EVENTS TEST GRAINS
// ============================================================================
//
// This file tests the Scynapse Events v1 feature which enables standard C#
// events on grain classes to work transparently across the distributed system.
//
// The code generator detects public events (EventHandler, EventHandler<T>) on
// grain classes and generates:
// 1. Interface event declarations + subscription method signatures
// 2. Grain infrastructure (stream fields, lifecycle hooks, bridge handlers)
// 3. Proxy local event implementation and subscription methods
//
// ============================================================================

/// <summary>
/// Test grain interface for Scynapse Events feature.
///
/// This interface is PARTIAL - the code generator will add:
/// - event EventHandler&lt;string&gt;? ChatMessage;
/// - event EventHandler&lt;int&gt;? ScoreChanged;
/// - Task&lt;IEventSubscription&lt;string&gt;&gt; SubscribeToChatMessageAsync();
/// - Task&lt;IEventSubscription&lt;string&gt;&gt; SubscribeToChatMessageAsync(Func&lt;string, Task&gt; handler);
/// - Task&lt;IEventSubscription&lt;int&gt;&gt; SubscribeToScoreChangedAsync();
/// - Task&lt;IEventSubscription&lt;int&gt;&gt; SubscribeToScoreChangedAsync(Func&lt;int, Task&gt; handler);
/// </summary>
public partial interface IEventTestGrain : IGrainWithStringKey
{
    // ========================================================================
    // CUSTOM METHODS (written by developer, unchanged by codegen)
    // ========================================================================

    /// <summary>
    /// Sends a chat message, which raises the ChatMessage event.
    /// </summary>
    Task SendChatAsync(string message);

    /// <summary>
    /// Adds points to the score, which raises the ScoreChanged event.
    /// </summary>
    Task AddPointsAsync(int points);

    /// <summary>
    /// Gets the current score.
    /// </summary>
    Task<int> GetScoreAsync();

    /// <summary>
    /// Resets the grain state.
    /// </summary>
    Task ResetAsync();
}

/// <summary>
/// Test grain implementation for Scynapse Events feature.
///
/// This class is PARTIAL - the code generator will add:
/// - Stream fields (__chatMessage_stream, __scoreChanged_stream)
/// - Bridge handler fields (__chatMessage_bridge, __scoreChanged_bridge)
/// - ILifecycleParticipant&lt;IGrainLifecycle&gt; implementation
/// - __InitializeScynapseEvents() method
/// - __CleanupScynapseEvents() method
/// - __PublishToStreamAsync&lt;T&gt;() helper
/// - Subscription method implementations (that throw NotSupportedException)
/// </summary>
public partial class EventTestGrain : Grain, IEventTestGrain
{
    private int _score;

    // ========================================================================
    // EVENTS (detected by code generator)
    // ========================================================================

    /// <summary>
    /// Event raised when a chat message is sent.
    /// This will be distributed via Orleans Simple Message Streams (SMS).
    /// </summary>
    public event EventHandler<string>? ChatMessage;

    /// <summary>
    /// Event raised when the score changes.
    /// This will be distributed via Orleans Simple Message Streams (SMS).
    /// </summary>
    public event EventHandler<int>? ScoreChanged;

    // ========================================================================
    // LOCAL-ONLY EVENT (excluded from codegen via [NotEvent])
    // ========================================================================

    /// <summary>
    /// Local diagnostic event - not distributed, stays in-process only.
    /// </summary>
    [NotEvent]
    public event EventHandler? DiagnosticTick;

    // ========================================================================
    // CUSTOM METHODS (written by developer)
    // ========================================================================

    public Task SendChatAsync(string message)
    {
        // Raise the event using standard C# pattern
        // The generated bridge handler will publish this to the SMS stream
        ChatMessage?.Invoke(this, message);

        // Also raise local diagnostic event
        DiagnosticTick?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    public Task AddPointsAsync(int points)
    {
        _score += points;

        // Raise the event using standard C# pattern
        ScoreChanged?.Invoke(this, _score);

        return Task.CompletedTask;
    }

    public Task<int> GetScoreAsync()
    {
        return Task.FromResult(_score);
    }

    public Task ResetAsync()
    {
        _score = 0;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test grain for verifying EventHandler (non-generic) events.
/// </summary>
public partial interface ISimpleEventTestGrain : IGrainWithStringKey
{
    /// <summary>
    /// Triggers the Ping event.
    /// </summary>
    Task PingAsync();
}

/// <summary>
/// Test grain implementation for EventHandler (non-generic) events.
/// </summary>
public partial class SimpleEventTestGrain : Grain, ISimpleEventTestGrain
{
    /// <summary>
    /// Simple event with no payload (uses EventArgs).
    /// </summary>
    public event EventHandler? Ping;

    public Task PingAsync()
    {
        Ping?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
