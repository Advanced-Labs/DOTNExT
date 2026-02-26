using Microsoft.Extensions.Options;
using Scynapse.Runtime;
using Scynapse.Runtime.MembershipService;

namespace Scynapse.Configuration
{
    /// <summary>
    /// Validates <see cref="AdoNetClusteringClientOptions"/> configuration.
    /// </summary>
    public class AdoNetClusteringClientOptionsValidator : IConfigurationValidator
    {
        private readonly AdoNetClusteringClientOptions options;

        public AdoNetClusteringClientOptionsValidator(IOptions<AdoNetClusteringClientOptions> options)
        {
            this.options = options.Value;
        }

        /// <inheritdoc />
        public void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(this.options.Invariant))
            {
                throw new ScynapseConfigurationException($"Invalid {nameof(AdoNetClusteringClientOptions)} values for {nameof(AdoNetClusteringTable)}. {nameof(options.Invariant)} is required.");
            }

            if (string.IsNullOrWhiteSpace(this.options.ConnectionString))
            {
                throw new ScynapseConfigurationException($"Invalid {nameof(AdoNetClusteringClientOptions)} values for {nameof(AdoNetClusteringTable)}. {nameof(options.ConnectionString)} is required.");
            }
        }
    }
}