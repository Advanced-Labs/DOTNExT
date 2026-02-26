using System.Collections.Generic;

namespace Scynapse.Streams
{
    /// <summary>
    /// Interface for accessing the deployment configuration.
    /// </summary>
    public interface IDeploymentConfiguration
    {
        /// <summary>
        /// Get the silo instance names for all configured silos.
        /// </summary>
        /// <returns>The list of silo names.</returns>
        IList<string> GetAllSiloNames();
    }
}
