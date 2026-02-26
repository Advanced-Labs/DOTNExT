
using Scynapse.Transactions.Abstractions;
using System.Threading.Tasks;

namespace Scynapse.Transactions.TestKit
{
    public interface ITransactionCommitterTestGrain : IGrainWithGuidKey
    {
        [Transaction(TransactionOption.Join)]
        Task Commit(ITransactionCommitOperation<IRemoteCommitService> operation);
    }
}
