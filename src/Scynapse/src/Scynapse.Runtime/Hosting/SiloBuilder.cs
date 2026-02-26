using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Scynapse.Hosting
{
    /// <summary>
    /// Builder for configuring an Scynapse server.
    /// </summary>
    internal class SiloBuilder : ISiloBuilder
    {
        public SiloBuilder(IServiceCollection services, IConfiguration configuration)
        {
            Services = services;
            Configuration = configuration;
            DefaultSiloServices.AddDefaultServices(this);
        }

        public IServiceCollection Services { get; }
        public IConfiguration Configuration { get; }
    }
}
