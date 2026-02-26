using System;
using System.Buffers;
using Scynapse.Serialization;

namespace Scynapse.Storage
{
    /// <summary>
    /// Grain storage serializer that uses the Scynapse <see cref="Serializer"/>.
    /// </summary>
    public class ScynapseGrainStorageSerializer : IGrainStorageSerializer
    {
        private readonly Serializer serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseGrainStorageSerializer"/> class.
        /// </summary>
        /// <param name="serializer">The serializer.</param>
        public ScynapseGrainStorageSerializer(Serializer serializer)
        {
            this.serializer = serializer;
        }

        /// <inheritdoc/>
        public BinaryData Serialize<T>(T value)
        {
            var buffer = new ArrayBufferWriter<byte>();
            this.serializer.Serialize(value, buffer);
            return new BinaryData(buffer.WrittenMemory);
        }

        /// <inheritdoc/>
        public T Deserialize<T>(BinaryData input)
        {
            return this.serializer.Deserialize<T>(input.ToMemory());
        }
    }
}
