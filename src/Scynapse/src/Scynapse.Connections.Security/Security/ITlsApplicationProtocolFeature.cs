using System;

namespace Scynapse.Connections.Security
{
    public interface ITlsApplicationProtocolFeature
    {
        ReadOnlyMemory<byte> ApplicationProtocol { get; }
    }
}
