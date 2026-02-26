namespace Scynapse.Messaging
{
    internal enum ConnectionDirection : byte
    {
        SiloToSilo,
        ClientToGateway,
        GatewayToClient
    }
}
