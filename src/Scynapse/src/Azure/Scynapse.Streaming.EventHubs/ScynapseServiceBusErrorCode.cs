namespace Scynapse.Streaming.EventHubs
{
    /// <summary>
    /// Scynapse ServiceBus error codes
    /// </summary>
    internal enum ScynapseEventHubErrorCode
    {
        /// <summary>
        /// Start of orlean servicebus error codes
        /// </summary>
        ServiceBus = 1<<16,

        FailedPartitionRead = ServiceBus + 1,
        RetryReceiverInit   = ServiceBus + 2,
    }
}
