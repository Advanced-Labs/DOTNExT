using Scynapse.Runtime;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Thrown when a grain call fails security verification.
/// </summary>
[Serializable]
[GenerateSerializer]
public sealed class ScynapseSecurityException : ScynapseException
{
    public ScynapseSecurityException(string message) : base(message) { }
    public ScynapseSecurityException(string message, Exception innerException) : base(message, innerException) { }
}
