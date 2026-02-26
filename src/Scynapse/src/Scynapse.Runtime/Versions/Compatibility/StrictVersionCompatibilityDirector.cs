using Scynapse.Versions.Compatibility;

namespace Scynapse.Runtime.Versions.Compatibility
{
    internal class StrictVersionCompatibilityDirector : ICompatibilityDirector
    {
        public bool IsCompatible(ushort requestedVersion, ushort currentVersion)
        {
            return requestedVersion == currentVersion;
        }
    }
}