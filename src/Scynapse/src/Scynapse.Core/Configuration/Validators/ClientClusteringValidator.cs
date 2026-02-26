using System;

using Microsoft.Extensions.DependencyInjection;
using Scynapse.Messaging;
using Scynapse.Runtime;

namespace Scynapse.Configuration.Validators
{
    /// <summary>
    /// Validator for client-side clustering.
    /// </summary>
    internal class ClientClusteringValidator : IConfigurationValidator
    {
        /// <summary>
        /// The error message displayed when clustering is misconfigured.
        /// </summary>
        internal const string ClusteringNotConfigured =
            "Clustering has not been configured. Configure clustering using one of the clustering packages, such as:"
            + "\n  * Genesa.Scynapse.Clustering.AzureStorage"
            + "\n  * Genesa.Scynapse.Clustering.AdoNet for ADO.NET systems such as SQL Server, MySQL, PostgreSQL, and Oracle"
            + "\n  * Genesa.Scynapse.Clustering.DynamoDB"
            + "\n  * Genesa.Scynapse.Clustering.Consul"
            + "\n  * Genesa.Scynapse.Clustering.ZooKeeper"
            + "\n  * Others, see: https://www.nuget.org/packages?q=Genesa.Scynapse.Clustering.";

        /// <summary>
        /// The service provider.
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientClusteringValidator"/> class.
        /// </summary>
        /// <param name="serviceProvider">
        /// The service provider.
        /// </param>
        public ClientClusteringValidator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public void ValidateConfiguration()
        {
            var gatewayProvider = _serviceProvider.GetService<IGatewayListProvider>();
            if (gatewayProvider == null)
            {
                throw new ScynapseConfigurationException(ClusteringNotConfigured);
            }
        }
    }
}
