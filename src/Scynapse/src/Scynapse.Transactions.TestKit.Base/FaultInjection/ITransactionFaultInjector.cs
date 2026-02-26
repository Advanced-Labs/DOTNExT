namespace Scynapse.Transactions.TestKit
{
    public interface ITransactionFaultInjector
    {
        void BeforeStore();
        void AfterStore();
    }
}
