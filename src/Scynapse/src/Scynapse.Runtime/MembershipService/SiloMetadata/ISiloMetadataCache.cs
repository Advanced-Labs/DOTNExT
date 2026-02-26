#nullable enable

namespace Scynapse.Runtime.MembershipService.SiloMetadata;

public interface ISiloMetadataCache
{
    SiloMetadata GetSiloMetadata(SiloAddress siloAddress);
}