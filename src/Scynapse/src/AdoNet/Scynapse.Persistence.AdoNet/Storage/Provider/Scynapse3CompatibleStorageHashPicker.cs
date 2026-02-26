using System.Collections.Generic;
using Scynapse.Runtime;

namespace Scynapse.Storage
{
    /// <summary>
    /// Scynapse v3-compatible hash picker implementation for Scynapse v3 -> v7+ migration scenarios.
    /// </summary>
    public class Scynapse3CompatibleStorageHashPicker : IStorageHasherPicker
    {
        private readonly Scynapse3CompatibleHasher _nonStringHasher;

        /// <summary>
        /// <see cref="IStorageHasherPicker.HashProviders"/>.
        /// </summary>
        public ICollection<IHasher> HashProviders { get; }

        /// <summary>
        /// A constructor.
        /// </summary>
        public Scynapse3CompatibleStorageHashPicker()
        {
            _nonStringHasher = new();
            HashProviders = [_nonStringHasher];
        }

        /// <summary>
        /// <see cref="IStorageHasherPicker.PickHasher{T}"/>.
        /// </summary>
        public IHasher PickHasher<T>(
            string serviceId,
            string storageProviderInstanceName,
            string grainType,
            GrainId grainId,
            IGrainState<T> grainState,
            string tag = null)
        {
            // string-only grain keys had special behaviour in Scynapse v3
            if (grainId.TryGetIntegerKey(out _, out _) || grainId.TryGetGuidKey(out _, out _))
                return _nonStringHasher;

            // unable to cache hasher instances: content-aware behaviour, see hasher implementation for details
            return new Scynapse3CompatibleStringKeyHasher(_nonStringHasher, grainType);
        }
    }
}
