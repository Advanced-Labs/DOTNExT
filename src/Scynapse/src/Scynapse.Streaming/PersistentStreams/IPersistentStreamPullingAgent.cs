using System.Threading.Tasks;
using Scynapse.Providers.Streams.Common;

namespace Scynapse.Streams
{
    internal interface IPersistentStreamPullingAgent : ISystemTarget, IStreamProducerExtension
    {
        Task Initialize();
        Task Shutdown();
    }

    internal interface IPersistentStreamPullingManager : ISystemTarget
    {
        Task Initialize();
        Task Stop();
        Task StartAgents();
        Task StopAgents();
        Task<object> ExecuteCommand(PersistentStreamProviderCommand command, object arg);
    }
}
