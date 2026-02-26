using System;
using System.Threading.Tasks;

namespace Orleans
{
    /// <summary>
    /// Represents an active subscription to a grain event.
    /// Disposing this object unsubscribes from the remote stream.
    /// </summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <remarks>
    /// <para>
    /// This interface provides a managed handle for grain event subscriptions.
    /// When you subscribe to a grain event using <c>SubscribeTo{EventName}Async()</c>,
    /// you receive an <see cref="IEventSubscription{T}"/> that allows you to:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Check subscription status via <see cref="IsActive"/></description></item>
    /// <item><description>Unsubscribe explicitly via <see cref="UnsubscribeAsync"/></description></item>
    /// <item><description>Use <c>await using</c> for automatic cleanup</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var player = client.GetGrain&lt;IPlayerGrain&gt;("player-1");
    ///
    /// // Create subscription with await using for automatic cleanup
    /// await using var subscription = await player.SubscribeToChatMessageAsync();
    ///
    /// // Attach local handlers
    /// player.ChatMessage += (sender, msg) => Console.WriteLine(msg);
    ///
    /// // Events will flow until subscription is disposed
    /// await player.SendChatAsync("Hello!");
    ///
    /// // Manual unsubscription (alternative to await using)
    /// await subscription.UnsubscribeAsync();
    /// </code>
    /// </example>
    public interface IEventSubscription<T> : IAsyncDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this subscription is currently active.
        /// </summary>
        /// <value>
        /// <c>true</c> if the subscription is active and receiving events;
        /// <c>false</c> if <see cref="UnsubscribeAsync"/> has been called or
        /// the subscription has been disposed.
        /// </value>
        bool IsActive { get; }

        /// <summary>
        /// Unsubscribes from the remote stream.
        /// After calling this method, no more events will be received.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueTask"/> that completes when the unsubscription is complete.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is idempotent - calling it multiple times has no additional effect.
        /// </para>
        /// <para>
        /// After unsubscribing, local event handlers attached via <c>+=</c> will remain
        /// attached to the proxy but will no longer receive events. You may detach them
        /// separately if needed.
        /// </para>
        /// </remarks>
        ValueTask UnsubscribeAsync();
    }
}
