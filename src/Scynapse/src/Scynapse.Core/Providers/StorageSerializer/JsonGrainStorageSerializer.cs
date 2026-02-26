using System;
using Scynapse.Serialization;

namespace Scynapse.Storage
{
    /// <summary>
    /// Grain storage serializer that uses Newtonsoft.Json
    /// </summary>
    public class JsonGrainStorageSerializer : IGrainStorageSerializer
    {
        private readonly ScynapseJsonSerializer _scynapseJsonSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonGrainStorageSerializer"/> class.
        /// </summary>
        public JsonGrainStorageSerializer(ScynapseJsonSerializer scynapseJsonSerializer)
        {
            _scynapseJsonSerializer = scynapseJsonSerializer;
        }

        /// <inheritdoc/>
        public BinaryData Serialize<T>(T value)
        {
            var data = _scynapseJsonSerializer.Serialize(value, typeof(T));
            return new BinaryData(data);
        }

        /// <inheritdoc/>
        public T Deserialize<T>(BinaryData input)
        {
            return (T)_scynapseJsonSerializer.Deserialize(typeof(T), input.ToString());
        }
    }
}
