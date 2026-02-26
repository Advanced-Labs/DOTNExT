using Scynapse.Versions.Compatibility;

namespace Scynapse.Runtime.Versions.Compatibility
{
    internal class AllVersionsCompatibilityDirector : ICompatibilityDirector
    {
        public bool IsCompatible(ushort requestedVersion, ushort currentVersion)
        {
            return true;
        }
    }
}