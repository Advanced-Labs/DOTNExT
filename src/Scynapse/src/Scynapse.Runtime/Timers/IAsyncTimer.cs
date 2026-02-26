using System;
using System.Threading.Tasks;

namespace Scynapse.Runtime
{
    internal interface IAsyncTimer : IDisposable, IHealthCheckable
    {
        Task<bool> NextTick(TimeSpan? overrideDelay = default);
    }
}
