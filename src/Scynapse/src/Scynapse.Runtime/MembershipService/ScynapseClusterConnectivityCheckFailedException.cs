using System;
using System.Runtime.Serialization;

namespace Scynapse.Runtime.MembershipService
{
    /// <summary>
    /// Exception used to indicate that a cluster connectivity check failed.
    /// </summary>
    /// <seealso cref="Scynapse.Runtime.ScynapseException" />
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseClusterConnectivityCheckFailedException : ScynapseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseClusterConnectivityCheckFailedException"/> class.
        /// </summary>
        public ScynapseClusterConnectivityCheckFailedException() : base("Failed to verify connectivity with active cluster nodes.") { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseClusterConnectivityCheckFailedException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public ScynapseClusterConnectivityCheckFailedException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseClusterConnectivityCheckFailedException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ScynapseClusterConnectivityCheckFailedException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseClusterConnectivityCheckFailedException"/> class.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The context.</param>
        [Obsolete]
        private ScynapseClusterConnectivityCheckFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
