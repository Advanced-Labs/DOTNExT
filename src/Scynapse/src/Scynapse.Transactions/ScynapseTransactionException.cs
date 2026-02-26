using Scynapse.Runtime;
using System;
using System.Runtime.Serialization;

namespace Scynapse.Transactions
{
    /// <summary>
    /// Base class for all transaction exceptions
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public class ScynapseTransactionException : ScynapseException
    {
        public ScynapseTransactionException() : base("Scynapse transaction error.") { }

        public ScynapseTransactionException(string message) : base(message) { }

        public ScynapseTransactionException(string message, Exception innerException) : base(message, innerException) { }

        [Obsolete]
        protected ScynapseTransactionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Scynapse transactions are disabled.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseTransactionsDisabledException : ScynapseTransactionException
    {
        public ScynapseTransactionsDisabledException()
            : base("Scynapse transactions have not been enabled. Transactions are disabled by default and must be configured to be used.")
        {
        }

        [Obsolete]
        private ScynapseTransactionsDisabledException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Signifies that the runtime was unable to start a transaction.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseStartTransactionFailedException : ScynapseTransactionException
    {
        public ScynapseStartTransactionFailedException(Exception innerException)
            : base("Failed to start transaction. Check InnerException for details", innerException)
        {
        }

        [Obsolete]
        private ScynapseStartTransactionFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Signifies that transaction runtime is overloaded
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseTransactionOverloadException : ScynapseTransactionException
    {
        public ScynapseTransactionOverloadException()
            : base("Transaction is overloaded on current silo, please try again later.")
        {
        }
    }

    /// <summary>
    /// Signifies that the runtime is unable to determine whether a transaction
    /// has committed.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseTransactionInDoubtException : ScynapseTransactionException
    {
        [Id(0)]
        public string TransactionId { get; private set; }

        public ScynapseTransactionInDoubtException(string transactionId) : base(string.Format("Transaction {0} is InDoubt", transactionId))
        {
            this.TransactionId = transactionId;
        }

        public ScynapseTransactionInDoubtException(string transactionId, Exception exc) : base(string.Format("Transaction {0} is InDoubt", transactionId), exc)
        {
            this.TransactionId = transactionId;
        }

        public ScynapseTransactionInDoubtException(string transactionId, string msg, Exception innerException) : base(string.Format("Transaction {0} is InDoubt: {1}", transactionId, msg), innerException)
        {
            this.TransactionId = transactionId;
        }

        [Obsolete]
        private ScynapseTransactionInDoubtException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.TransactionId = info.GetString(nameof(this.TransactionId));
        }

        [Obsolete]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(this.TransactionId), this.TransactionId);
        }
    }

    /// <summary>
    /// Signifies that the executing transaction has aborted.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public class ScynapseTransactionAbortedException : ScynapseTransactionException
    {
        /// <summary>
        /// The unique identifier of the aborted transaction.
        /// </summary>
        [Id(0)]
        public string TransactionId { get; private set; }
 
        public ScynapseTransactionAbortedException(string transactionId, string msg, Exception innerException) : base(msg, innerException)
        {
            this.TransactionId = transactionId;
        }

        public ScynapseTransactionAbortedException(string transactionId, string msg) : base(msg)
        {
            this.TransactionId = transactionId;
        }

        public ScynapseTransactionAbortedException(string transactionId, Exception innerException)
            : base($"Transaction {transactionId} Aborted because of an unhandled exception in a grain method call. See InnerException for details.", innerException)
        {
            TransactionId = transactionId;
        }

        [Obsolete]
        protected ScynapseTransactionAbortedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.TransactionId = info.GetString(nameof(this.TransactionId));
        }

        [Obsolete]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(this.TransactionId), this.TransactionId);
        }
    }

    /// <summary>
    /// Signifies that the executing transaction has aborted because a dependent transaction aborted.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseCascadingAbortException : ScynapseTransactionTransientFailureException
    {
        [Id(0)]
        public string DependentTransactionId { get; private set; }

        public ScynapseCascadingAbortException(string transactionId, string dependentId)
            : base(transactionId, string.Format("Transaction {0} aborted because its dependent transaction {1} aborted", transactionId, dependentId))
        {
            this.DependentTransactionId = dependentId;
        }

        public ScynapseCascadingAbortException(string transactionId)
            : base(transactionId, string.Format("Transaction {0} aborted because a dependent transaction aborted", transactionId))
        {
        }

        public ScynapseCascadingAbortException(string transactionId, Exception innerException)
            : base(transactionId, string.Format("Transaction {0} aborted because a dependent transaction aborted", transactionId), innerException)
        {
        }

        [Obsolete]
        private ScynapseCascadingAbortException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.DependentTransactionId = info.GetString(nameof(this.DependentTransactionId));
        }

        [Obsolete]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(this.DependentTransactionId), this.DependentTransactionId);
        }
    }

    /// <summary>
    /// Signifies that the executing transaction has aborted because a method did not await all its pending calls.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseOrphanCallException : ScynapseTransactionAbortedException
    {
        public ScynapseOrphanCallException(string transactionId, int pendingCalls)
            : base(
                transactionId,
                $"Transaction {transactionId} aborted because method did not await all its outstanding calls ({pendingCalls})")
        {
        }

        [Obsolete]
        private ScynapseOrphanCallException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Signifies that the executing read-only transaction has aborted because it attempted to write to a grain.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseReadOnlyViolatedException : ScynapseTransactionAbortedException
    {
        public ScynapseReadOnlyViolatedException(string transactionId)
            : base(transactionId, string.Format("Transaction {0} aborted because it attempted to write a grain", transactionId))
        {
        }

        [Obsolete]
        private ScynapseReadOnlyViolatedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseTransactionServiceNotAvailableException : ScynapseTransactionException
    {
        public ScynapseTransactionServiceNotAvailableException() : base("Transaction service not available")
        {
        }

        [Obsolete]
        private ScynapseTransactionServiceNotAvailableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Signifies that the executing transaction has aborted because its execution lock was broken
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseBrokenTransactionLockException : ScynapseTransactionTransientFailureException
    {
        public ScynapseBrokenTransactionLockException(string transactionId, string situation)
            : base(transactionId, $"Transaction {transactionId} aborted because a broken lock was detected, {situation}")
        {
        }

        public ScynapseBrokenTransactionLockException(string transactionId, string situation, Exception innerException)
            : base(transactionId, $"Transaction {transactionId} aborted because a broken lock was detected, {situation}", innerException)
        {
        }

        [Obsolete]
        private ScynapseBrokenTransactionLockException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Signifies that the executing transaction has aborted because it could not upgrade some lock
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseTransactionLockUpgradeException : ScynapseTransactionTransientFailureException
    {
        public ScynapseTransactionLockUpgradeException(string transactionId) :
            base(transactionId, $"Transaction {transactionId} Aborted because it could not upgrade a lock, because of a higher-priority conflicting transaction")
        {
        }

        [Obsolete]
        private ScynapseTransactionLockUpgradeException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Signifies that the executing transaction has aborted because the TM did not receive all prepared messages in time
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public sealed class ScynapseTransactionPrepareTimeoutException : ScynapseTransactionTransientFailureException
    {
        public ScynapseTransactionPrepareTimeoutException(string transactionId, Exception innerException)
            : base(transactionId, $"Transaction {transactionId} Aborted because the prepare phase did not complete within the timeout limit", innerException)
        {
        }

        [Obsolete]
        private ScynapseTransactionPrepareTimeoutException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Signifies that the executing transaction has aborted because some possibly transient problem, such as internal
    /// timeouts for locks or protocol responses, or speculation failures.
    /// </summary>
    [Serializable]
    [GenerateSerializer]
    public class ScynapseTransactionTransientFailureException : ScynapseTransactionAbortedException
    {
        public ScynapseTransactionTransientFailureException(string transactionId, string msg, Exception innerException)
            : base(transactionId, msg, innerException)
        {
        }

        public ScynapseTransactionTransientFailureException(string transactionId, string msg)
            : base(transactionId, msg)
        {
        }

        [Obsolete]
        protected ScynapseTransactionTransientFailureException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
