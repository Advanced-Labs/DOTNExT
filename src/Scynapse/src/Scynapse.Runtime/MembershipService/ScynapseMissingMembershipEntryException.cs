using System;
using System.Runtime.Serialization;

namespace Scynapse.Runtime.MembershipService
{
    /// <summary>
    /// Exception used to indicate that a cluster membership entry which was expected to be present.
    /// </summary>
    /// <seealso cref="Scynapse.Runtime.ScynapseException" />
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseMissingMembershipEntryException : ScynapseException
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseMissingMembershipEntryException"/> class.
        /// </summary>
        public ScynapseMissingMembershipEntryException() : base("Membership table does not contain information an entry for this silo.") { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseMissingMembershipEntryException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public ScynapseMissingMembershipEntryException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseMissingMembershipEntryException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ScynapseMissingMembershipEntryException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseMissingMembershipEntryException"/> class.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The context.</param>
        [Obsolete]
        private ScynapseMissingMembershipEntryException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
