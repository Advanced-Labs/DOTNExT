using System.Collections.Generic;
using Scynapse.Runtime;
using Scynapse.Runtime.Placement;

#nullable enable
namespace Scynapse.Placement;

public interface IPlacementFilterDirector
{
    IEnumerable<SiloAddress> Filter(PlacementFilterStrategy filterStrategy, PlacementTarget target, IEnumerable<SiloAddress> silos);
}
