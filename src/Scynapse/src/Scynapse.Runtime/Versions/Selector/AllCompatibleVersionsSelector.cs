using System.Linq;
using Scynapse.Versions.Compatibility;
using Scynapse.Versions.Selector;

namespace Scynapse.Runtime.Versions.Selector
{
    internal class AllCompatibleVersionsSelector : IVersionSelector
    {
        public ushort[] GetSuitableVersion(ushort requestedVersion, ushort[] availableVersions, ICompatibilityDirector compatibilityDirector)
        {
            return availableVersions.Where(v => compatibilityDirector.IsCompatible(requestedVersion, v)).ToArray();
        }
    }
}
