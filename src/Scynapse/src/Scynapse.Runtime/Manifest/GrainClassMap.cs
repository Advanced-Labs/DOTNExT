using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Scynapse.Runtime;
using Scynapse.Serialization.TypeSystem;

namespace Scynapse.Metadata
{
    /// <summary>
    /// Mapping between <see cref="GrainType"/> and implementing <see cref="Type"/>.
    /// </summary>
    public class GrainClassMap
    {
        private readonly TypeConverter _typeConverter;
        private volatile ImmutableDictionary<GrainType, Type> _types;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrainClassMap"/> class.
        /// </summary>
        /// <param name="typeConverter">The type converter.</param>
        /// <param name="classes">The grain classes.</param>
        public GrainClassMap(TypeConverter typeConverter, ImmutableDictionary<GrainType, Type> classes)
        {
            _typeConverter = typeConverter;
            _types = classes;
        }

        /// <summary>
        /// Returns the grain class type corresponding to the provided grain type.
        /// </summary>
        /// <param name="grainType">Type of the grain.</param>
        /// <param name="grainClass">The grain class.</param>
        /// <returns><see langword="true"/> if a corresponding grain class was found, <see langword="false"/> otherwise.</returns>
        public bool TryGetGrainClass(GrainType grainType, [NotNullWhen(true)] out Type grainClass)
        {
            GrainType lookupType;
            Type[] args;
            if (GenericGrainType.TryParse(grainType, out var genericId))
            {
                lookupType = genericId.GetUnconstructedGrainType().GrainType;
                args = genericId.GetArguments(_typeConverter);
            }
            else
            {
                lookupType = grainType;
                args = default;
            }

            if (!_types.TryGetValue(lookupType, out grainClass))
            {
                return false;
            }

            if (args is not null)
            {
                grainClass = grainClass.MakeGenericType(args);
            }

            return true;
        }

        /// <summary>
        /// Updates the grain type mappings with new types.
        /// This method is thread-safe and uses atomic replacement of the internal dictionary.
        /// </summary>
        /// <param name="updatedTypes">The updated dictionary of grain type mappings</param>
        internal void UpdateTypes(ImmutableDictionary<GrainType, Type> updatedTypes)
        {
            _types = updatedTypes ?? throw new ArgumentNullException(nameof(updatedTypes));
        }

        /// <summary>
        /// Adds new grain types to the existing mappings.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="newTypes">The new grain type mappings to add</param>
        internal void AddTypes(IEnumerable<KeyValuePair<GrainType, Type>> newTypes)
        {
            if (newTypes == null)
            {
                throw new ArgumentNullException(nameof(newTypes));
            }

            // Atomic update using ImmutableDictionary.AddRange
            var current = _types;
            var updated = current.AddRange(newTypes);
            _types = updated;
        }

        /// <summary>
        /// Gets the current count of registered grain types.
        /// </summary>
        internal int Count => _types.Count;

        /// <summary>
        /// Gets all registered grain types.
        /// </summary>
        internal IEnumerable<GrainType> GetGrainTypes() => _types.Keys;

        /// <summary>
        /// Removes grain types from the existing mappings.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="grainTypesToRemove">The grain types to remove</param>
        internal void RemoveTypes(IEnumerable<GrainType> grainTypesToRemove)
        {
            if (grainTypesToRemove == null)
            {
                throw new ArgumentNullException(nameof(grainTypesToRemove));
            }

            // Atomic update using ImmutableDictionary.RemoveRange
            var current = _types;
            var updated = current.RemoveRange(grainTypesToRemove);
            _types = updated;
        }
    }
}
