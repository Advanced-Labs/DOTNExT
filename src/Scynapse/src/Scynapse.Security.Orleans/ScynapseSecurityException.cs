using Scynapse.Runtime;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Structured failure codes for security exceptions.
/// Enables programmatic error handling without string parsing.
/// </summary>
public enum SecurityFailureCode
{
    Unknown = 0,
    MissingAuthentication,
    MissingCapability,
    InvalidCapability,
    BearerVerificationFailed,
    InsufficientCapability,
    ExpiredCapability,
    UntrustedIssuer,
    ChainVerificationFailed,
}

/// <summary>
/// Thrown when a grain call fails security verification.
/// </summary>
[Serializable]
[GenerateSerializer]
public sealed class ScynapseSecurityException : ScynapseException
{
    [Id(0)]
    public SecurityFailureCode FailureCode { get; }

    public ScynapseSecurityException(string message) : base(message)
    {
        FailureCode = SecurityFailureCode.Unknown;
    }

    public ScynapseSecurityException(string message, SecurityFailureCode code) : base(message)
    {
        FailureCode = code;
    }

    public ScynapseSecurityException(string message, Exception innerException) : base(message, innerException)
    {
        FailureCode = SecurityFailureCode.Unknown;
    }
}
