using Scynapse.Placement;
using Scynapse.Runtime;

namespace UnitTests.GrainInterfaces
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class VersionAwareStrategyAttribute : PlacementAttribute
    {
        public VersionAwareStrategyAttribute()
            : base(VersionAwarePlacementStrategy.Singleton)
        {
        }
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class VersionAwarePlacementStrategy : PlacementStrategy
    {
        internal static VersionAwarePlacementStrategy Singleton { get; } = new VersionAwarePlacementStrategy();
    }
}
