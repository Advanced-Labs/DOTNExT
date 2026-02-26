using System;
using System.Threading.Tasks;
using Scynapse.Runtime;
using Scynapse.Streams;

namespace Scynapse.Streaming
{
    /// <summary>
    /// Default implementation of <see cref="IEventSubscription{T}"/>.
    /// </summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class wraps an Scynapse <see cref="StreamSubscriptionHandle{T}"/> and provides
    /// a clean API for managing grain event subscriptions. It implements
    /// <see cref="IAsyncDisposable"/> for use with <c>await using</c> syntax.
    /// </para>
    /// <para>
    /// Instances are created by the generated <c>SubscribeTo{EventName}Async()</c> methods
    /// on grain proxies. You should not typically need to create instances directly.
    /// </para>
    /// </remarks>
    public sealed class EventSubscription<T> : IEventSubscription<T>
    {
        private readonly StreamSubscriptionHandle<T> _handle;
        private readonly StreamId _streamId;
        private volatile bool _isActive;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSubscription{T}"/> class.
        /// </summary>
        /// <param name="handle">The underlying stream subscription handle.</param>
        /// <param name="streamId">The stream identifier.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="handle"/> is <c>null</c>.
        /// </exception>
        public EventSubscription(StreamSubscriptionHandle<T> handle, StreamId streamId)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _streamId = streamId;
            _isActive = true;
        }

        /// <summary>
        /// Gets the underlying Scynapse stream subscription handle.
        /// </summary>
        /// <value>
        /// The <see cref="StreamSubscriptionHandle{T}"/> managing this subscription.
        /// </value>
        /// <remarks>
        /// This provides access to the raw Scynapse stream handle for advanced scenarios
        /// such as resuming subscriptions after client reconnection.
        /// </remarks>
        public StreamSubscriptionHandle<T> Handle => _handle;

        /// <inheritdoc/>
        public bool IsActive => _isActive;

        /// <summary>
        /// Gets the stream identifier this subscription is listening to.
        /// </summary>
        /// <value>
        /// The <see cref="StreamId"/> uniquely identifying the event stream.
        /// The format is typically "{InterfaceName}.{EventName}" with the grain key.
        /// </value>
        public StreamId StreamId => _streamId;

        /// <inheritdoc/>
        public async ValueTask UnsubscribeAsync()
        {
            if (_isActive)
            {
                _isActive = false;
                await _handle.UnsubscribeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => UnsubscribeAsync();
    }
}
