using System.Threading.Tasks;
using Scynapse.Concurrency;

namespace Scynapse.Runtime
{
    internal interface IDeploymentLoadPublisher : ISystemTarget
    {
        [OneWay]
        Task UpdateRuntimeStatistics(SiloAddress siloAddress, SiloRuntimeStatistics siloStats);
    }
}
