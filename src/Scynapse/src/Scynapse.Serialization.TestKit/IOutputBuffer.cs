using System.Buffers;

namespace Scynapse.Serialization.TestKit
{
    public interface IOutputBuffer
    {
        ReadOnlySequence<byte> GetReadOnlySequence(int maxSegmentSize);
    }
}