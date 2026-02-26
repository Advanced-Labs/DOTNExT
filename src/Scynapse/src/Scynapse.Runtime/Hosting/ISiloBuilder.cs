using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Scynapse.Hosting
{
    /// <summary>
    /// Builder for configuring an Scynapse server.
    /// </summary>
    public interface ISiloBuilder
    {
        /// <summary>
        /// The services shared by the silo and host.
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        IConfiguration Configuration { get; }
    }
}