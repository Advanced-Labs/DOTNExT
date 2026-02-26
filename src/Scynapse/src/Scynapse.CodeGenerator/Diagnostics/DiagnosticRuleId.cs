// Centralized diagnostic rule IDs for Scynapse.CodeGenerator
namespace Scynapse.CodeGenerator.Diagnostics;

internal static class DiagnosticRuleId
{
    public const string InaccessibleSetter = "SCYNAPSE0101";
    public const string InvalidRpcMethodReturnType = "SCYNAPSE0102";
    public const string UnhandledCodeGenerationException = "SCYNAPSE0103";
    public const string IncorrectProxyBaseClassSpecification = "SCYNAPSE0104";
    public const string RpcInterfaceProperty = "SCYNAPSE0105";
    public const string CanNotGenerateImplicitFieldIds = "SCYNAPSE0106";
    public const string InaccessibleSerializableType = "SCYNAPSE0107";
    public const string GenerateCodeForDeclaringAssemblyAttribute_NoDeclaringAssembly = "SCYNAPSE0108";
    public const string MultipleCancellationTokenParameters = "SCYNAPSE0109";
    public const string ReferenceAssemblyWithGenerateSerializer = "SCYNAPSE0110";
}
