using System;
using System.Runtime.Serialization;

namespace Scynapse.Runtime
{
    /// <summary>
    /// An exception class used by the Scynapse runtime for reporting errors.
    /// </summary>
    /// <remarks>
    /// This is also the base class for any more specific exceptions
    /// raised by the Scynapse runtime.
    /// </remarks>
    [Serializable]
    [GenerateSerializer]
    public class ScynapseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseException"/> class.
        /// </summary>
        public ScynapseException()
            : base("Unexpected error.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        public ScynapseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="innerException">
        /// The inner exception.
        /// </param>
        public ScynapseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseException"/> class.
        /// </summary>
        /// <param name="info">
        /// The serialization info.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <exception cref="SerializationException">The class name is <see langword="null" /> or <see cref="P:System.Exception.HResult" /> is zero (0).</exception>
        /// <exception cref="ArgumentNullException"><paramref name="info" /> is <see langword="null" />.</exception>
        [Obsolete]
        protected ScynapseException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
