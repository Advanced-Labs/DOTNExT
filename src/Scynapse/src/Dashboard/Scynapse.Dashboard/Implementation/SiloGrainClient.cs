using System;
using Scynapse.Dashboard.Core;
using Scynapse.Runtime;
using Scynapse.Runtime.Services;

namespace Scynapse.Dashboard.Implementation;

internal sealed class SiloGrainClient(IServiceProvider serviceProvider) : GrainServiceClient<ISiloGrainService>(serviceProvider), ISiloGrainClient
{
    public ISiloGrainService GrainService(SiloAddress destination)
        => GetGrainService(destination);
}
