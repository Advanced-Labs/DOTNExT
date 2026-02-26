using Microsoft.Extensions.Options;
using Scynapse.Runtime;
using Scynapse.Runtime.MembershipService;

namespace Scynapse.Configuration
{
    /// <summary>
    /// Validates <see cref="AdoNetClusteringSiloOptions"/> configuration.
    /// </summary>
    public class AdoNetClusteringSiloOptionsValidator : IConfigurationValidator
    {
        private readonly AdoNetClusteringSiloOptions options;

        public AdoNetClusteringSiloOptionsValidator(IOptions<AdoNetClusteringSiloOptions> options)
        {
            this.options = options.Value;
        }

        /// <inheritdoc />
        public void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(this.options.Invariant))
            {
                throw new ScynapseConfigurationException($"Invalid {nameof(AdoNetClusteringSiloOptions)} values for {nameof(AdoNetClusteringTable)}. {nameof(options.Invariant)} is required.");
            }

            if (string.IsNullOrWhiteSpace(this.options.ConnectionString))
            {
                throw new ScynapseConfigurationException($"Invalid {nameof(AdoNetClusteringSiloOptions)} values for {nameof(AdoNetClusteringTable)}. {nameof(options.ConnectionString)} is required.");
            }
        }
    }
}