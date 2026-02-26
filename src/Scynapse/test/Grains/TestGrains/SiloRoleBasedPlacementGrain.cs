using Scynapse.Placement;
using Scynapse.Runtime;
using UnitTests.GrainInterfaces;


namespace UnitTests.Grains
{
    [SiloRoleBasedPlacement]
    public class SiloRoleBasedPlacementGrain : Grain, ISiloRoleBasedPlacementGrain
    {
        public Task<bool> Ping()
        {
            return Task.FromResult(true);
        }
    }
}
