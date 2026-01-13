using System;
using System.Threading.Tasks;
using Orleans.Runtime;
using Orleans.Streams;

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
    /// <item><description>Access the underlying stream via <see cref="StreamId"/></description></item>
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
        /// Gets the underlying Orleans stream subscription handle.
        /// </summary>
        /// <value>
        /// The <see cref="StreamSubscriptionHandle{T}"/> managing this subscription.
        /// </value>
        /// <remarks>
        /// This provides access to the raw Orleans stream handle for advanced scenarios
        /// such as resuming subscriptions after client reconnection.
        /// </remarks>
        StreamSubscriptionHandle<T> Handle { get; }

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
        /// Gets the stream identifier this subscription is listening to.
        /// </summary>
        /// <value>
        /// The <see cref="StreamId"/> uniquely identifying the event stream.
        /// The format is typically "{InterfaceName}.{EventName}" with the grain key.
        /// </value>
        StreamId StreamId { get; }

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
