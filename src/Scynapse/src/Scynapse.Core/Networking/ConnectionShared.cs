using System;
using Scynapse.Placement.Repartitioning;

namespace Scynapse.Runtime.Messaging
{
    internal sealed class ConnectionCommon(
        IServiceProvider serviceProvider,
        MessageFactory messageFactory,
        MessagingTrace messagingTrace,
        NetworkingTrace networkingTrace,
        IMessageStatisticsSink messageStatisticsSink)
    {
        public MessageFactory MessageFactory { get; } = messageFactory;
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public NetworkingTrace NetworkingTrace { get; } = networkingTrace;
        public IMessageStatisticsSink MessageStatisticsSink { get; } = messageStatisticsSink;
        public MessagingTrace MessagingTrace { get; } = messagingTrace;
    }
}
