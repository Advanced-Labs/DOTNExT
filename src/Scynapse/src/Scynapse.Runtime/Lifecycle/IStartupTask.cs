using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scynapse.Runtime
{
    /// <summary>
    /// Defines an action to be taken after silo startup.
    /// </summary>
    [Obsolete("IStartupTask is deprecated. Use BackgroundService or IHostedService instead. See https://learn.microsoft.com/dotnet/scynapse/host/configuration-guide/startup-tasks for more information.", error: false)]
    public interface IStartupTask
    {
        /// <summary>
        /// Called after the silo has started.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token which is canceled when the method must abort.</param>
        /// <returns>A <see cref="Task"/> representing the work performed.</returns>
        Task Execute(CancellationToken cancellationToken);
    }
}
