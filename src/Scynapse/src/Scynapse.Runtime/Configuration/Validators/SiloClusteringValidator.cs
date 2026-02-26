using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scynapse.Configuration;
using Scynapse.Configuration.Validators;

namespace Scynapse.Runtime.Configuration
{
    /// <summary>
    /// Validates basic cluster membership configuration.
    /// </summary>
    internal class SiloClusteringValidator : IConfigurationValidator
    {
        private readonly IServiceProvider serviceProvider;

        public SiloClusteringValidator(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public void ValidateConfiguration()
        {
            var clusteringTableProvider = this.serviceProvider.GetService<IMembershipTable>();
            if (clusteringTableProvider == null)
            {
                throw new ScynapseConfigurationException(ClientClusteringValidator.ClusteringNotConfigured);
            }

            var clusterMembershipOptions = this.serviceProvider.GetRequiredService<IOptions<ClusterMembershipOptions>>().Value;
            if (clusterMembershipOptions.LivenessEnabled)
            {
                if (clusterMembershipOptions.NumVotesForDeathDeclaration > clusterMembershipOptions.NumProbedSilos)
                {
                    throw new ScynapseConfigurationException($"{nameof(ClusterMembershipOptions)}.{nameof(ClusterMembershipOptions.NumVotesForDeathDeclaration)} ({clusterMembershipOptions.NumVotesForDeathDeclaration}) must be less than or equal to {nameof(ClusterMembershipOptions)}.{nameof(ClusterMembershipOptions.NumProbedSilos)} ({clusterMembershipOptions.NumProbedSilos}).");
                }

                if (clusterMembershipOptions.NumVotesForDeathDeclaration <= 0)
                {
                    throw new ScynapseConfigurationException($"{nameof(ClusterMembershipOptions)}.{nameof(ClusterMembershipOptions.NumVotesForDeathDeclaration)} ({clusterMembershipOptions.NumVotesForDeathDeclaration}) must be greater than 0.");
                }
            }
        }
    }
}