using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Scynapse.Configuration;
using Scynapse.Runtime;
using Scynapse.Serialization.TypeSystem;

namespace Scynapse.Metadata
{
    /// <summary>
    /// Creates a <see cref="SiloManifest"/> for this silo.
    /// </summary>
    internal class SiloManifestProvider
    {
        private readonly IEnumerable<IGrainPropertiesProvider> _grainPropertiesProviders;
        private readonly IEnumerable<IGrainInterfacePropertiesProvider> _grainInterfacePropertiesProviders;
        private readonly GrainTypeResolver _typeProvider;
        private readonly GrainInterfaceTypeResolver _interfaceIdProvider;
        private readonly TypeConverter _typeConverter;
        private volatile GrainManifest _siloManifest;

        public SiloManifestProvider(
            IEnumerable<IGrainPropertiesProvider> grainPropertiesProviders,
            IEnumerable<IGrainInterfacePropertiesProvider> grainInterfacePropertiesProviders,
            IOptions<GrainTypeOptions> grainTypeOptions,
            GrainTypeResolver typeProvider,
            GrainInterfaceTypeResolver interfaceIdProvider,
            TypeConverter typeConverter)
        {
            _grainPropertiesProviders = grainPropertiesProviders;
            _grainInterfacePropertiesProviders = grainInterfacePropertiesProviders;
            _typeProvider = typeProvider;
            _interfaceIdProvider = interfaceIdProvider;
            _typeConverter = typeConverter;

            var (grainProperties, grainTypes) = CreateGrainManifest(grainPropertiesProviders, grainTypeOptions, typeProvider);
            var interfaces = CreateInterfaceManifest(grainInterfacePropertiesProviders, grainTypeOptions, interfaceIdProvider);
            _siloManifest = new GrainManifest(grainProperties, interfaces);
            this.GrainTypeMap = new GrainClassMap(typeConverter, grainTypes);
        }

        public GrainManifest SiloManifest => _siloManifest;

        public GrainClassMap GrainTypeMap { get; }

        private static ImmutableDictionary<GrainInterfaceType, GrainInterfaceProperties> CreateInterfaceManifest(
            IEnumerable<IGrainInterfacePropertiesProvider> propertyProviders,
            IOptions<GrainTypeOptions> grainTypeOptions,
            GrainInterfaceTypeResolver grainInterfaceIdProvider)
        {
            var builder = ImmutableDictionary.CreateBuilder<GrainInterfaceType, GrainInterfaceProperties>();
            foreach (var grainInterface in grainTypeOptions.Value.Interfaces)
            {
                var interfaceId = grainInterfaceIdProvider.GetGrainInterfaceType(grainInterface);
                var properties = new Dictionary<string, string>();
                foreach (var provider in propertyProviders)
                {
                    provider.Populate(grainInterface, interfaceId, properties);
                }

                var result = new GrainInterfaceProperties(properties.ToImmutableDictionary());
                if (builder.TryGetValue(interfaceId, out var graintInterfaceProperty))
                {
                    throw new InvalidOperationException($"An entry with the key {interfaceId} is already present."
                        + $"\nExisting: {graintInterfaceProperty.ToDetailedString()}\nTrying to add: {result.ToDetailedString()}"
                        + "\nConsider using the [GrainInterfaceType(\"name\")] attribute to give these interfaces unique names.");
                }

                builder.Add(interfaceId, result);
            }

            return builder.ToImmutable();
        }

        private static (ImmutableDictionary<GrainType, GrainProperties>, ImmutableDictionary<GrainType, Type>) CreateGrainManifest(
            IEnumerable<IGrainPropertiesProvider> grainMetadataProviders,
            IOptions<GrainTypeOptions> grainTypeOptions,
            GrainTypeResolver grainTypeProvider)
        {
            var propertiesMap = ImmutableDictionary.CreateBuilder<GrainType, GrainProperties>();
            var typeMap = ImmutableDictionary.CreateBuilder<GrainType, Type>();
            foreach (var grainClass in grainTypeOptions.Value.Classes)
            {
                var grainType = grainTypeProvider.GetGrainType(grainClass);
                var properties = new Dictionary<string, string>();
                foreach (var provider in grainMetadataProviders)
                {
                    provider.Populate(grainClass, grainType, properties);
                }

                var result = new GrainProperties(properties.ToImmutableDictionary());
                if (propertiesMap.TryGetValue(grainType, out var grainProperty))
                {
                    throw new InvalidOperationException($"An entry with the key {grainType} is already present."
                        + $"\nExisting: {grainProperty.ToDetailedString()}\nTrying to add: {result.ToDetailedString()}"
                        + "\nConsider using the [GrainType(\"name\")] attribute to give these classes unique names.");
                }

                propertiesMap.Add(grainType, result);
                typeMap.Add(grainType, grainClass);
            }

            return (propertiesMap.ToImmutable(), typeMap.ToImmutable());
        }

        /// <summary>
        /// Updates the silo manifest with new grain types and interfaces.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="newGrainClasses">New grain implementation classes to add</param>
        /// <param name="newGrainInterfaces">New grain interfaces to add</param>
        /// <returns>The updated manifest and type mappings</returns>
        internal (GrainManifest Manifest, ImmutableDictionary<GrainType, Type> TypeMap) UpdateManifest(
            IEnumerable<Type> newGrainClasses,
            IEnumerable<Type> newGrainInterfaces)
        {
            if (newGrainClasses == null) throw new ArgumentNullException(nameof(newGrainClasses));
            if (newGrainInterfaces == null) throw new ArgumentNullException(nameof(newGrainInterfaces));

            // Build new grain properties and type mappings
            var grainPropertiesBuilder = _siloManifest.Grains.ToBuilder();
            var grainTypeMapBuilder = ImmutableDictionary.CreateBuilder<GrainType, Type>();

            foreach (var grainClass in newGrainClasses)
            {
                var grainType = _typeProvider.GetGrainType(grainClass);

                // Skip if already registered
                if (grainPropertiesBuilder.ContainsKey(grainType))
                {
                    continue;
                }

                var properties = new Dictionary<string, string>();
                foreach (var provider in _grainPropertiesProviders)
                {
                    provider.Populate(grainClass, grainType, properties);
                }

                var result = new GrainProperties(properties.ToImmutableDictionary());
                grainPropertiesBuilder.Add(grainType, result);
                grainTypeMapBuilder.Add(grainType, grainClass);
            }

            // Build new interface properties
            var interfacePropertiesBuilder = _siloManifest.Interfaces.ToBuilder();

            foreach (var grainInterface in newGrainInterfaces)
            {
                var interfaceId = _interfaceIdProvider.GetGrainInterfaceType(grainInterface);

                // Skip if already registered
                if (interfacePropertiesBuilder.ContainsKey(interfaceId))
                {
                    continue;
                }

                var properties = new Dictionary<string, string>();
                foreach (var provider in _grainInterfacePropertiesProviders)
                {
                    provider.Populate(grainInterface, interfaceId, properties);
                }

                var result = new GrainInterfaceProperties(properties.ToImmutableDictionary());
                interfacePropertiesBuilder.Add(interfaceId, result);
            }

            // Create updated manifest
            var updatedManifest = new GrainManifest(
                grainPropertiesBuilder.ToImmutable(),
                interfacePropertiesBuilder.ToImmutable());

            // Update the silo manifest atomically
            _siloManifest = updatedManifest;

            // Update the grain type map
            if (grainTypeMapBuilder.Count > 0)
            {
                GrainTypeMap.AddTypes(grainTypeMapBuilder);
            }

            return (updatedManifest, grainTypeMapBuilder.ToImmutable());
        }

        /// <summary>
        /// Removes grain types and interfaces from the silo manifest.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="grainClassesToRemove">Grain implementation classes to remove</param>
        /// <param name="grainInterfacesToRemove">Grain interfaces to remove</param>
        /// <returns>The updated manifest and the grain types that were removed</returns>
        internal (GrainManifest Manifest, IEnumerable<GrainType> RemovedGrainTypes) RemoveFromManifest(
            IEnumerable<Type> grainClassesToRemove,
            IEnumerable<Type> grainInterfacesToRemove)
        {
            if (grainClassesToRemove == null) throw new ArgumentNullException(nameof(grainClassesToRemove));
            if (grainInterfacesToRemove == null) throw new ArgumentNullException(nameof(grainInterfacesToRemove));

            var removedGrainTypes = new List<GrainType>();

            // Build new grain properties without the removed types
            var grainPropertiesBuilder = _siloManifest.Grains.ToBuilder();

            foreach (var grainClass in grainClassesToRemove)
            {
                var grainType = _typeProvider.GetGrainType(grainClass);
                if (grainPropertiesBuilder.Remove(grainType))
                {
                    removedGrainTypes.Add(grainType);
                }
            }

            // Build new interface properties without the removed interfaces
            var interfacePropertiesBuilder = _siloManifest.Interfaces.ToBuilder();

            foreach (var grainInterface in grainInterfacesToRemove)
            {
                var interfaceId = _interfaceIdProvider.GetGrainInterfaceType(grainInterface);
                interfacePropertiesBuilder.Remove(interfaceId);
            }

            // Create updated manifest
            var updatedManifest = new GrainManifest(
                grainPropertiesBuilder.ToImmutable(),
                interfacePropertiesBuilder.ToImmutable());

            // Update the silo manifest atomically
            _siloManifest = updatedManifest;

            // Update the grain type map to remove these types
            if (removedGrainTypes.Count > 0)
            {
                GrainTypeMap.RemoveTypes(removedGrainTypes);
            }

            return (updatedManifest, removedGrainTypes);
        }
    }
}
