using Scynapse.Runtime;
using Scynapse.Services;

namespace Scynapse.Dashboard.Core;

internal interface ISiloGrainClient : IGrainServiceClient<ISiloGrainService>
{
    ISiloGrainService GrainService(SiloAddress destination);
}
