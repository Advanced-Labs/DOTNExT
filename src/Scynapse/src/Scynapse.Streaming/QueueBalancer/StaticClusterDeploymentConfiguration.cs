using System.Collections.Generic;
using Scynapse.Streams;

namespace Scynapse.Hosting
{
    /// <summary>
    /// Deployment configuration that reads from scynapse cluster configuration
    /// </summary>
    public class StaticClusterDeploymentOptions : IDeploymentConfiguration
    {
        /// <summary>
        /// Gets or sets the silo names.
        /// </summary>
        /// <value>The silo names.</value>
        public IList<string> SiloNames { get; set; } = new List<string>();

        /// <inheritdoc/>
        IList<string> IDeploymentConfiguration.GetAllSiloNames()
        {
            return this.SiloNames;
        }
    }
}
