using BenchmarkDotNet.Attributes;
using Benchmarks.Utilities;
using Scynapse.Serialization;
using Scynapse.Serialization.Buffers;
using Scynapse.Serialization.Session;
using Scynapse.Serialization.WireProtocol;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks
{
    /// <summary>
    /// Benchmarks Scynapse wire protocol field header encoding and writing performance.
    /// </summary>
    [Config(typeof(BenchmarkConfig))]
    public class FieldHeaderBenchmarks
    {
        private static readonly SerializerSession Session;
        private static readonly byte[] ScynapseBuffer = new byte[1000];

        static FieldHeaderBenchmarks()
        {
            var services = new ServiceCollection().AddSerializer();
            var serviceProvider = services.BuildServiceProvider();
            var sessionPool = serviceProvider.GetRequiredService<SerializerSessionPool>();
            Session = sessionPool.GetSession();
        }

        [Benchmark(Baseline = true)]
        public void WritePlainExpectedEmbeddedId()
        {
            var writer = new SingleSegmentBuffer(ScynapseBuffer).CreateWriter(Session);

            // Use an expected type and a field id with a value small enough to be embedded.
            writer.WriteFieldHeader(4, typeof(uint), typeof(uint), WireType.VarInt);
        }

        [Benchmark]
        public void WritePlainExpectedExtendedId()
        {
            var writer = new SingleSegmentBuffer(ScynapseBuffer).CreateWriter(Session);

            // Use a field id delta which is too large to be embedded.
            writer.WriteFieldHeader(Tag.MaxEmbeddedFieldIdDelta + 20, typeof(uint), typeof(uint), WireType.VarInt);
        }

        [Benchmark]
        public void WriteFastEmbedded()
        {
            var writer = new SingleSegmentBuffer(ScynapseBuffer).CreateWriter(Session);

            // Use an expected type and a field id with a value small enough to be embedded.
            writer.WriteFieldHeaderExpected(4, WireType.VarInt);
        }

        [Benchmark]
        public void WriteFastExtended()
        {
            var writer = new SingleSegmentBuffer(ScynapseBuffer).CreateWriter(Session);

            // Use a field id delta which is too large to be embedded.
            writer.WriteFieldHeaderExpected(Tag.MaxEmbeddedFieldIdDelta + 20, WireType.VarInt);
        }

        [Benchmark]
        public void CreateWriter() => _ = new SingleSegmentBuffer(ScynapseBuffer).CreateWriter(Session);

        [Benchmark]
        public void WriteByte()
        {
            var writer = new SingleSegmentBuffer(ScynapseBuffer).CreateWriter(Session);
            writer.WriteByte((byte)4);
        }
    }
}