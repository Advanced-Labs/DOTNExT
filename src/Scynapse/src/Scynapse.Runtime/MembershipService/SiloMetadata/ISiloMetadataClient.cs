using System.Threading.Tasks;

#nullable enable
namespace Scynapse.Runtime.MembershipService.SiloMetadata;

internal interface ISiloMetadataClient
{
    Task<SiloMetadata> GetSiloMetadata(SiloAddress siloAddress);
}
