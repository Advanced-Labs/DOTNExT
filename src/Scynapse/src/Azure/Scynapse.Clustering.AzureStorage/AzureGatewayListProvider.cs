using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scynapse.Clustering.AzureStorage;
using Scynapse.Configuration;
using Scynapse.Messaging;

namespace Scynapse.AzureUtils
{
    internal class AzureGatewayListProvider : IGatewayListProvider
    {
        private ScynapseSiloInstanceManager siloInstanceManager;
        private readonly string clusterId;
        private readonly AzureStorageGatewayOptions options;
        private readonly ILoggerFactory loggerFactory;

        public AzureGatewayListProvider(ILoggerFactory loggerFactory, IOptions<AzureStorageGatewayOptions> options, IOptions<ClusterOptions> clusterOptions, IOptions<GatewayOptions> gatewayOptions)
        {
            this.loggerFactory = loggerFactory;
            this.clusterId = clusterOptions.Value.ClusterId;
            this.MaxStaleness = gatewayOptions.Value.GatewayListRefreshPeriod;
            this.options = options.Value;
        }

        public async Task InitializeGatewayListProvider()
        {
            this.siloInstanceManager = await ScynapseSiloInstanceManager.GetManager(
                this.clusterId,
                this.loggerFactory,
                this.options);
        }
        // no caching
        public Task<IList<Uri>> GetGateways()
        {
            // FindAllGatewayProxyEndpoints already returns a deep copied List<Uri>.
            return this.siloInstanceManager.FindAllGatewayProxyEndpoints();
        }

        public TimeSpan MaxStaleness { get; }

        public bool IsUpdatable => true;
    }
}
