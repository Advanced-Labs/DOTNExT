#pragma warning disable CS1591, RS0016, RS0041
[assembly: global::Scynapse.ApplicationPartAttribute("TestProject")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Core.Abstractions")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Serialization")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Core")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Runtime")]
[assembly: global::Scynapse.Serialization.Configuration.TypeManifestProviderAttribute(typeof(ScynapseCodeGen.TestProject.Metadata_TestProject))]
namespace ScynapseCodeGen.TestProject
{
    using global::Scynapse.Serialization.Codecs;
    using global::Scynapse.Serialization.GeneratedCodeHelpers;

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Metadata_TestProject : global::Scynapse.Serialization.Configuration.TypeManifestProviderBase
    {
        protected override void ConfigureInner(global::Scynapse.Serialization.Configuration.TypeManifestOptions config)
        {
        }
    }
}
#pragma warning restore CS1591, RS0016, RS0041
