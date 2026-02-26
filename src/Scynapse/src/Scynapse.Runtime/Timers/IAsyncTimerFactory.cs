using System;

namespace Scynapse.Runtime
{
    internal interface IAsyncTimerFactory
    {
        IAsyncTimer Create(TimeSpan period, string name);
    }
}
