#nullable enable

using Microsoft.Extensions.Options;
using Scynapse.Runtime;
using Scynapse.Serialization;

namespace Scynapse.Streaming.JsonConverters
{
    internal class StreamingConverterConfigurator : IPostConfigureOptions<ScynapseJsonSerializerOptions>
    {
        private readonly IRuntimeClient _runtimeClient;

        public StreamingConverterConfigurator(IRuntimeClient runtimeClient)
        {
            _runtimeClient = runtimeClient;
        }

        public void PostConfigure(string? name, ScynapseJsonSerializerOptions options)
        {
            options.JsonSerializerSettings.Converters.Add(new StreamImplConverter(_runtimeClient));
        }
    }
}
