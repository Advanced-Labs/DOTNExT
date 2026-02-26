using System.Buffers;

namespace Scynapse.Networking.Shared
{
    internal sealed class SharedMemoryPool
    {
        public MemoryPool<byte> Pool { get; } = KestrelMemoryPool.Create();
    }
}
