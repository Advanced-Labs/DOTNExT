namespace UnitTests.GrainInterfaces
{
    using System.Threading.Tasks;

    using Scynapse;
    using Scynapse.Runtime;

    internal interface IDefaultPlacementGrain : IGrainWithIntegerKey
    {
        Task<PlacementStrategy> GetDefaultPlacement();
    }
}
