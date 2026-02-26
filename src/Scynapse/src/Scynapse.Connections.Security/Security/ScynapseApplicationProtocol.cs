using System.Net.Security;

namespace Scynapse.Connections.Security
{
    internal static class ScynapseApplicationProtocol
    {
        public static readonly SslApplicationProtocol Scynapse1 = new SslApplicationProtocol("Scynapse1");
    }
}
