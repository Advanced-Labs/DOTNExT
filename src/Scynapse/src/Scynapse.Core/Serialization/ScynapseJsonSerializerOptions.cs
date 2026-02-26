using System;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Scynapse.Serialization
{
    public class ScynapseJsonSerializerOptions
    {
        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        public ScynapseJsonSerializerOptions()
        {
            JsonSerializerSettings = ScynapseJsonSerializerSettings.GetDefaultSerializerSettings();
        }
    }

    public class ConfigureScynapseJsonSerializerOptions : IPostConfigureOptions<ScynapseJsonSerializerOptions>
    {
        private readonly IServiceProvider _serviceProvider;

        public ConfigureScynapseJsonSerializerOptions(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void PostConfigure(string name, ScynapseJsonSerializerOptions options)
        {
            ScynapseJsonSerializerSettings.Configure(_serviceProvider, options.JsonSerializerSettings);
        }
    }
}
