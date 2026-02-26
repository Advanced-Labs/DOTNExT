using System.Threading.Tasks;

#nullable enable
namespace Scynapse.Runtime.MembershipService.SiloMetadata;

[Alias("Scynapse.Runtime.MembershipService.SiloMetadata.ISiloMetadataSystemTarget")]
internal interface ISiloMetadataSystemTarget : ISystemTarget
{
    [Alias("GetSiloMetadata")]
    Task<SiloMetadata> GetSiloMetadata();
}