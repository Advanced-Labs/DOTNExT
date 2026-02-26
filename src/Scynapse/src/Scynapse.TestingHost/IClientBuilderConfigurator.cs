using Microsoft.Extensions.Configuration;
using Scynapse.Hosting;

namespace Scynapse.TestingHost
{
    /// <summary>
    /// Allows implementations to configure the client builder when starting up each silo in the test cluster.
    /// </summary>
    public interface IClientBuilderConfigurator
    {
        /// <summary>
        /// Configures the client builder.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="clientBuilder">The client builder.</param>
        void Configure(IConfiguration configuration, IClientBuilder clientBuilder);
    }
}