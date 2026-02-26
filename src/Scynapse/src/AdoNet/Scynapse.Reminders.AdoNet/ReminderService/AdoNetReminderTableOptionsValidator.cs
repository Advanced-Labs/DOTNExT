using Microsoft.Extensions.Options;
using Scynapse.Runtime;
using Scynapse.Runtime.ReminderService;

namespace Scynapse.Configuration
{
    /// <summary>
    /// Validates <see cref="AdoNetReminderTableOptions"/> configuration.
    /// </summary>
    public class AdoNetReminderTableOptionsValidator : IConfigurationValidator
    {
        private readonly AdoNetReminderTableOptions options;
        
        public AdoNetReminderTableOptionsValidator(IOptions<AdoNetReminderTableOptions> options)
        {
            this.options = options.Value;
        }

        /// <inheritdoc />
        public void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(this.options.Invariant))
            {
                throw new ScynapseConfigurationException($"Invalid {nameof(AdoNetReminderTableOptions)} values for {nameof(AdoNetReminderTable)}. {nameof(options.Invariant)} is required.");
            }

            if (string.IsNullOrWhiteSpace(this.options.ConnectionString))
            {
                throw new ScynapseConfigurationException($"Invalid {nameof(AdoNetReminderTableOptions)} values for {nameof(AdoNetReminderTable)}. {nameof(options.ConnectionString)} is required.");
            }
        }
    }
}