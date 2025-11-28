using System.Threading.Tasks;
using Orleans.Metadata;

namespace Orleans.Runtime
{
    internal interface ISiloManifestSystemTarget : ISystemTarget
    {
        ValueTask<GrainManifest> GetSiloManifest();

        /// <summary>
        /// Notifies this silo that a manifest change has occurred somewhere in the cluster.
        /// This triggers a refresh of the cluster manifest to pick up changes from other silos.
        /// </summary>
        ValueTask NotifyManifestChanged();
    }
}