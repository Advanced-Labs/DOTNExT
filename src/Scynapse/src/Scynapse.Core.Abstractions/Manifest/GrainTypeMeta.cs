using System;
using System.Collections.Immutable;
using Scynapse.Runtime;

namespace Scynapse.Metadata
{
    /// <summary>
    /// Detailed metadata about a grain type, including reflection-like information.
    /// </summary>
    [Serializable, GenerateSerializer, Immutable]
    public sealed class GrainTypeMeta
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainTypeMeta"/> class.
        /// </summary>
        public GrainTypeMeta(
            GrainType grainType,
            string fullName,
            string @namespace,
            string typeName,
            string version,
            string assemblyName,
            string assemblyHash,
            ImmutableList<GrainInterfaceMeta> interfaces,
            GrainKeyType keyType,
            GrainPackage? sourcePackage,
            ImmutableList<SiloAddress> hostingSilos,
            bool isAvailable)
        {
            GrainType = grainType;
            FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
            Namespace = @namespace ?? string.Empty;
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            AssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
            AssemblyHash = assemblyHash ?? throw new ArgumentNullException(nameof(assemblyHash));
            Interfaces = interfaces ?? ImmutableList<GrainInterfaceMeta>.Empty;
            KeyType = keyType;
            SourcePackage = sourcePackage;
            HostingSilos = hostingSilos ?? ImmutableList<SiloAddress>.Empty;
            IsAvailable = isAvailable;
        }

        /// <summary>
        /// Gets the Scynapse GrainType identifier.
        /// </summary>
        [Id(0)]
        public GrainType GrainType { get; }

        /// <summary>
        /// Gets the full CLR type name of the grain interface.
        /// </summary>
        [Id(1)]
        public string FullName { get; }

        /// <summary>
        /// Gets the namespace of the grain interface.
        /// </summary>
        [Id(2)]
        public string Namespace { get; }

        /// <summary>
        /// Gets the simple type name without namespace.
        /// </summary>
        [Id(3)]
        public string TypeName { get; }

        /// <summary>
        /// Gets the version of this grain type.
        /// </summary>
        [Id(4)]
        public string Version { get; }

        /// <summary>
        /// Gets the assembly containing this grain type.
        /// </summary>
        [Id(5)]
        public string AssemblyName { get; }

        /// <summary>
        /// Gets the hash of the assembly for versioning.
        /// </summary>
        [Id(6)]
        public string AssemblyHash { get; }

        /// <summary>
        /// Gets the interface types this grain implements.
        /// </summary>
        [Id(7)]
        public ImmutableList<GrainInterfaceMeta> Interfaces { get; }

        /// <summary>
        /// Gets the key type (String, Guid, Int64, etc.).
        /// </summary>
        [Id(8)]
        public GrainKeyType KeyType { get; }

        /// <summary>
        /// Gets the reference back to the containing package (if loaded from a package).
        /// </summary>
        /// <remarks>
        /// This may be null if the grain type was not loaded from a package,
        /// or if the package reference was not set during construction.
        /// Note: When serializing, circular references are handled by the serializer.
        /// </remarks>
        [Id(9)]
        public GrainPackage? SourcePackage { get; }

        /// <summary>
        /// Gets the silos currently hosting this grain type.
        /// </summary>
        [Id(10)]
        public ImmutableList<SiloAddress> HostingSilos { get; }

        /// <summary>
        /// Gets whether the grain type is currently available for activation.
        /// </summary>
        [Id(11)]
        public bool IsAvailable { get; }

        /// <summary>
        /// Creates a new <see cref="GrainTypeMeta"/> with the specified hosting silos.
        /// </summary>
        /// <param name="hostingSilos">The new hosting silos list.</param>
        /// <returns>A new instance with updated hosting silos.</returns>
        public GrainTypeMeta WithHostingSilos(ImmutableList<SiloAddress> hostingSilos)
        {
            return new GrainTypeMeta(
                GrainType,
                FullName,
                Namespace,
                TypeName,
                Version,
                AssemblyName,
                AssemblyHash,
                Interfaces,
                KeyType,
                SourcePackage,
                hostingSilos,
                IsAvailable);
        }

        /// <summary>
        /// Creates a new <see cref="GrainTypeMeta"/> with the specified availability.
        /// </summary>
        /// <param name="isAvailable">Whether the grain type is available.</param>
        /// <returns>A new instance with updated availability.</returns>
        public GrainTypeMeta WithAvailability(bool isAvailable)
        {
            return new GrainTypeMeta(
                GrainType,
                FullName,
                Namespace,
                TypeName,
                Version,
                AssemblyName,
                AssemblyHash,
                Interfaces,
                KeyType,
                SourcePackage,
                HostingSilos,
                isAvailable);
        }
    }

    /// <summary>
    /// The key type used by a grain.
    /// </summary>
    public enum GrainKeyType
    {
        /// <summary>
        /// String key type.
        /// </summary>
        String = 0,

        /// <summary>
        /// Guid key type.
        /// </summary>
        Guid = 1,

        /// <summary>
        /// Int64 (long) key type.
        /// </summary>
        Int64 = 2,

        /// <summary>
        /// Guid with string extension (compound key).
        /// </summary>
        GuidCompound = 3,

        /// <summary>
        /// Int64 with string extension (compound key).
        /// </summary>
        Int64Compound = 4
    }
}
