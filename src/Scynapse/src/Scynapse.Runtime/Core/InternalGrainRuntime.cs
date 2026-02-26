using Microsoft.Extensions.Options;
using Scynapse.Configuration;
using Scynapse.Runtime.GrainDirectory;
using Scynapse.Runtime.Messaging;
using Scynapse.Runtime.Versions;
using Scynapse.Runtime.Versions.Compatibility;

namespace Scynapse.Runtime
{
    /// <summary>
    /// Shared runtime services which grains use.
    /// </summary>
    internal class InternalGrainRuntime(
        MessageCenter messageCenter,
        Catalog catalog,
        GrainVersionManifest versionManifest,
        RuntimeMessagingTrace messagingTrace,
        GrainLocator grainLocator,
        CompatibilityDirectorManager compatibilityDirectorManager,
        IOptions<GrainCollectionOptions> collectionOptions,
        ILocalGrainDirectory localGrainDirectory,
        IActivationWorkingSet activationWorkingSet)
    {
        public InsideRuntimeClient RuntimeClient { get; } = catalog.RuntimeClient;
        public MessageCenter MessageCenter { get; } = messageCenter;
        public Catalog Catalog { get; } = catalog;
        public GrainVersionManifest GrainVersionManifest { get; } = versionManifest;
        public RuntimeMessagingTrace MessagingTrace { get; } = messagingTrace;
        public CompatibilityDirectorManager CompatibilityDirectorManager { get; } = compatibilityDirectorManager;
        public GrainLocator GrainLocator { get; } = grainLocator;
        public IOptions<GrainCollectionOptions> CollectionOptions { get; } = collectionOptions;
        public ILocalGrainDirectory LocalGrainDirectory { get; } = localGrainDirectory;
        public IActivationWorkingSet ActivationWorkingSet { get; } = activationWorkingSet;
    }
}
