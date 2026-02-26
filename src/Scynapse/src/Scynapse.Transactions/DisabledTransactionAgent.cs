using System;
using System.Threading.Tasks;

namespace Scynapse.Transactions
{
    internal class DisabledTransactionAgent : ITransactionAgent
    {
        public Task Abort(TransactionInfo transactionInfo)
        {
            throw new ScynapseTransactionsDisabledException();
        }

        public Task<(TransactionalStatus Status, Exception exception)> Resolve(TransactionInfo transactionInfo)
        {
            throw new ScynapseTransactionsDisabledException();
        }

        public Task<TransactionInfo> StartTransaction(bool readOnly, TimeSpan timeout)
        {
            throw new ScynapseStartTransactionFailedException(new ScynapseTransactionsDisabledException());
        }
    }
}
