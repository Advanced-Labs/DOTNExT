using System;
using System.Threading;

namespace Scynapse.Transactions.State
{
    internal interface IActivationLifetime
    {
        CancellationToken OnDeactivating { get; }

        IDisposable BlockDeactivation();
    }
}
