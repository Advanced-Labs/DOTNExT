using System;
using Scynapse.Configuration;
using Scynapse.Hosting;
using Scynapse.Providers;
using Scynapse.Transactions.TestKit;

namespace Scynapse.Transactions.TestKit
{
    public static class SiloBuilderExtensions
    {
        /// <summary>
        /// Configure cluster to use the distributed TM algorithm
        /// </summary>
        public static ISiloBuilder UseControlledFaultInjectionTransactionState(this ISiloBuilder builder)
        {
            return builder.ConfigureServices(services => services.UseControlledFaultInjectionTransactionState());
        }

        public static ISiloBuilder AddFaultInjectionAzureTableTransactionalStateStorage(this ISiloBuilder builder, Action<AzureTableTransactionalStateOptions> configureOptions)
        {
            return builder.AddFaultInjectionAzureTableTransactionalStateStorage(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, configureOptions);
        }

        public static ISiloBuilder AddFaultInjectionAzureTableTransactionalStateStorage(this ISiloBuilder builder, string name, Action<AzureTableTransactionalStateOptions> configureOptions)
        {
            return builder.ConfigureServices(services => services.AddFaultInjectionAzureTableTransactionalStateStorage(name, ob => ob.Configure(configureOptions)));
        }
    }
}
