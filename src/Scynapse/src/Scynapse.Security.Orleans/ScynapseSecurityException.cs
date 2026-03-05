namespace Scynapse.Security.Orleans;

/// <summary>
/// Thrown when a grain call fails security verification.
/// </summary>
public sealed class ScynapseSecurityException : Exception
{
    public ScynapseSecurityException(string message) : base(message) { }
    public ScynapseSecurityException(string message, Exception innerException) : base(message, innerException) { }
}
