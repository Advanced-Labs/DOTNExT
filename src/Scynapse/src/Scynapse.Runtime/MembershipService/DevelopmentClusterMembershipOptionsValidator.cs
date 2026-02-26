using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scynapse.Runtime;
using Scynapse.Runtime.MembershipService;

namespace Scynapse.Configuration
{
    internal class DevelopmentClusterMembershipOptionsValidator : IConfigurationValidator
    {
        private readonly DevelopmentClusterMembershipOptions options;
        private readonly IMembershipTable membershipTable;

        public DevelopmentClusterMembershipOptionsValidator(IOptions<DevelopmentClusterMembershipOptions> options, IServiceProvider serviceProvider)
        {
            this.options = options.Value;
            this.membershipTable = serviceProvider.GetService<IMembershipTable>();
        }

        public void ValidateConfiguration()
        {
            if (this.membershipTable is SystemTargetBasedMembershipTable && this.options.PrimarySiloEndpoint is null)
            {
                throw new ScynapseConfigurationException("Development clustering is enabled but no value is specified ");
            }
        }
    }
}