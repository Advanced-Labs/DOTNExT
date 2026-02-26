using System.Diagnostics.Tracing;

namespace Scynapse.Runtime
{
    /// <summary>
    /// Event source for <see cref="CallbackData"/>.
    /// </summary>
    [EventSource(Name = "Microsoft-Scynapse-CallBackData")]
    internal sealed class ScynapseCallBackDataEvent : EventSource
    {
        public static readonly ScynapseCallBackDataEvent Log = new ScynapseCallBackDataEvent();

        /// <summary>
        /// Indicates that a request timeout occurred.
        /// </summary>
        /// <param name="message">The message.</param>
        [NonEvent]
        public void OnTimeout(Message message)
        {
            if (this.IsEnabled())
            {
                this.OnTimeout();
            }
        }

        /// <summary>
        /// Indicates that a request timeout occurred.
        /// </summary>
        [Event(1, Level = EventLevel.Warning)]
        private void OnTimeout() => this.WriteEvent(1);

        /// <summary>
        /// Indicates that a target silo failed.
        /// </summary>
        /// <param name="message">A message addressed to the target silo.</param>
        [NonEvent]
        public void OnTargetSiloFail(Message message)
        {
            if (this.IsEnabled())
            {
                this.OnTargetSiloFail();
            }
        }

        /// <summary>
        /// Indicates that a target silo failed.
        /// </summary>
        [Event(2, Level = EventLevel.Warning)]
        private void OnTargetSiloFail() => this.WriteEvent(2);

        /// <summary>
        /// Indicates that a request completed.
        /// </summary>
        [NonEvent]
        public void DoCallback(Message message)
        {
            if (this.IsEnabled())
            {
                this.DoCallback();
            }
        }

        /// <summary>
        /// Indicates that a request completed.
        /// </summary>
        [Event(3, Level = EventLevel.Verbose)]
        private void DoCallback() => this.WriteEvent(3);

        /// <summary>
        /// Indicates that a request was canceled.
        /// </summary>
        [NonEvent]
        public void OnCanceled(Message message)
        {
            if (this.IsEnabled())
            {
                this.OnCanceled();
            }
        }

        /// <summary>
        /// Indicates that a request was canceled.
        /// </summary>
        [Event(4, Level = EventLevel.Verbose)]
        private void OnCanceled() => this.WriteEvent(4);
    }

    [EventSource(Name = "Microsoft-Scynapse-OutsideRuntimeClient")]
    internal sealed class ScynapseOutsideRuntimeClientEvent : EventSource
    {
        public static readonly ScynapseOutsideRuntimeClientEvent Log = new ScynapseOutsideRuntimeClientEvent();

        [NonEvent]
        public void SendRequest(Message message)
        {
            if (this.IsEnabled())
            {
                this.SendRequest();
            }
        }

        [Event(1, Level = EventLevel.Verbose)]
        private void SendRequest() => this.WriteEvent(1);

        [NonEvent]
        public void ReceiveResponse(Message message)
        {
            if (this.IsEnabled())
            {
                this.ReceiveResponse();
            }
        }

        [Event(2, Level = EventLevel.Verbose)]
        private void ReceiveResponse() => this.WriteEvent(2);

        [NonEvent]
        public void SendResponse(Message message)
        {
            if (this.IsEnabled())
            {
                this.SendResponse();
            }
        }

        [Event(3, Level = EventLevel.Verbose)]
        private void SendResponse() => this.WriteEvent(3);
    }

    [EventSource(Name = "Microsoft-Scynapse-Dispatcher")]
    internal sealed class ScynapseDispatcherEvent : EventSource
    {
        public static readonly ScynapseDispatcherEvent Log = new ScynapseDispatcherEvent();

        [NonEvent]
        public void ReceiveMessage(Message message)
        {
            if (this.IsEnabled())
            {
                this.ReceiveMessage();
            }
        }

        [Event(1, Level = EventLevel.Verbose)]
        private void ReceiveMessage() => WriteEvent(1);
    }

    [EventSource(Name = "Microsoft-Scynapse-InsideRuntimeClient")]
    internal sealed class ScynapseInsideRuntimeClientEvent : EventSource
    {
        public static readonly ScynapseInsideRuntimeClientEvent Log = new ScynapseInsideRuntimeClientEvent();

        [NonEvent]
        public void SendRequest(Message message)
        {
            if (this.IsEnabled())
            {
                this.SendRequest();
            }
        }

        [Event(1, Level = EventLevel.Verbose)]
        private void SendRequest() => WriteEvent(1);

        [NonEvent]
        public void ReceiveResponse(Message message)
        {
            if (this.IsEnabled())
            {
                this.ReceiveResponse();
            }
        }

        [Event(2, Level = EventLevel.Verbose)]
        private void ReceiveResponse() => WriteEvent(2);

        [NonEvent]
        public void SendResponse(Message message)
        {
            if (this.IsEnabled())
            {
                this.SendResponse();
            }
        }

        [Event(3, Level = EventLevel.Verbose)]
        private void SendResponse() => WriteEvent(3);
    }

    [EventSource(Name = "Microsoft-Scynapse-IncomingMessageAgent")]
    internal sealed class ScynapseIncomingMessageAgentEvent : EventSource
    {
        public static readonly ScynapseIncomingMessageAgentEvent Log = new ScynapseIncomingMessageAgentEvent();

        [NonEvent]
        public void ReceiveMessage(Message message)
        {
            if (this.IsEnabled())
            {
                this.ReceiveMessage();
            }
        }

        [Event(1, Level = EventLevel.Verbose)]
        private void ReceiveMessage() => WriteEvent(1);
    }
}
