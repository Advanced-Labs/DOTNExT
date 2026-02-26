using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scynapse.Clustering.AdoNet.Storage;
using Scynapse.Messaging;
using Scynapse.Configuration;

namespace Scynapse.Runtime.Membership
{
    public partial class AdoNetGatewayListProvider : IGatewayListProvider
    {
        private readonly ILogger _logger;
        private readonly string _clusterId;
        private readonly AdoNetClusteringClientOptions _options;
        private RelationalScynapseQueries _scynapseQueries;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _maxStaleness;

        public AdoNetGatewayListProvider(
            ILogger<AdoNetGatewayListProvider> logger,
            IServiceProvider serviceProvider,
            IOptions<AdoNetClusteringClientOptions> options,
            IOptions<GatewayOptions> gatewayOptions,
            IOptions<ClusterOptions> clusterOptions)
        {
            this._logger = logger;
            this._serviceProvider = serviceProvider;
            this._options = options.Value;
            this._clusterId = clusterOptions.Value.ClusterId;
            this._maxStaleness = gatewayOptions.Value.GatewayListRefreshPeriod;
        }

        public TimeSpan MaxStaleness
        {
            get { return this._maxStaleness; }
        }

        public bool IsUpdatable
        {
            get { return true; }
        }

        public async Task InitializeGatewayListProvider()
        {
            LogTraceInitializeGatewayListProvider();
            _scynapseQueries = await RelationalScynapseQueries.CreateInstance(_options.Invariant, _options.ConnectionString);
        }

        public async Task<IList<Uri>> GetGateways()
        {
            LogTraceGetGateways();
            try
            {
                return await _scynapseQueries.ActiveGatewaysAsync(this._clusterId);
            }
            catch (Exception ex)
            {
                LogDebugGatewaysFailed(ex);
                throw;
            }
        }

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = $"{nameof(AdoNetGatewayListProvider)}.{nameof(InitializeGatewayListProvider)} called."
        )]
        private partial void LogTraceInitializeGatewayListProvider();

        [LoggerMessage(
            Level = LogLevel.Trace,
            Message = $"{nameof(AdoNetGatewayListProvider)}.{nameof(GetGateways)} called."
        )]
        private partial void LogTraceGetGateways();

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = $"{nameof(AdoNetGatewayListProvider)}.{nameof(GetGateways)} failed"
        )]
        private partial void LogDebugGatewaysFailed(Exception exception);
    }
}
