
using Scynapse.Runtime;

namespace Scynapse.Transactions.Abstractions
{
    public interface ITransactionalStateStorageFactory
    {
        ITransactionalStateStorage<TState> Create<TState>(string stateName, IGrainContext context) where TState : class, new();
    }
}
