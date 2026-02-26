
namespace Scynapse.Transactions.Abstractions
{
    public interface ITransactionDataCopier<TData>
    {
        TData DeepCopy(TData original);
    }
}
