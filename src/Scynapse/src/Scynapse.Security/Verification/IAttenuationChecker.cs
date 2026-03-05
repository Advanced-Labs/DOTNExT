using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// Checks that a child assertion's claims are within the scope of its parent.
/// Extensible: different claim types have different attenuation rules.
/// </summary>
public interface IAttenuationChecker
{
    /// <summary>
    /// Returns true if the child's claims are within the parent's authorized scope.
    /// </summary>
    bool Check(SignedAssertion parent, SignedAssertion child);
}
