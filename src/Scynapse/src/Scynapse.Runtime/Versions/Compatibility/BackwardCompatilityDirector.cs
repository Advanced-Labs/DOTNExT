using Scynapse.Versions.Compatibility;

namespace Scynapse.Runtime.Versions.Compatibility
{
    internal class BackwardCompatilityDirector : ICompatibilityDirector
    {
        public bool IsCompatible(ushort requestedVersion, ushort currentVersion)
        {
            return requestedVersion <= currentVersion;
        }
    }
}
