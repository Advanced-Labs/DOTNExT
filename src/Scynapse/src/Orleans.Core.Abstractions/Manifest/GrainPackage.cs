using System;
using System.Collections.Immutable;
using System.Linq;

namespace Orleans.Metadata
{
    /// <summary>
    /// Represents a distributable package of grain types.
    /// Can contain interfaces only (for clients) or interfaces + implementations (for silos).
    /// </summary>
    [Serializable, GenerateSerializer, Immutable]
    public sealed class GrainPackage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainPackage"/> class.
        /// </summary>
        public GrainPackage(
            string packageId,
            string version,
            string contentHash,
            ImmutableList<GrainTypeMeta> grainTypes,
            GrainPackageContent contentType,
            ImmutableList<GrainPackageAssembly> assemblies,
            ImmutableDictionary<string, string> metadata)
        {
            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
            GrainTypes = grainTypes ?? ImmutableList<GrainTypeMeta>.Empty;
            ContentType = contentType;
            Assemblies = assemblies ?? ImmutableList<GrainPackageAssembly>.Empty;
            Metadata = metadata ?? ImmutableDictionary<string, string>.Empty;
        }

        /// <summary>
        /// Gets the unique identifier for this package.
        /// </summary>
        [Id(0)]
        public string PackageId { get; }

        /// <summary>
        /// Gets the version of the package (SemVer format recommended).
        /// </summary>
        [Id(1)]
        public string Version { get; }

        /// <summary>
        /// Gets the hash of the package contents for integrity verification.
        /// </summary>
        [Id(2)]
        public string ContentHash { get; }

        /// <summary>
        /// Gets the grain types available in this package.
        /// </summary>
        [Id(3)]
        public ImmutableList<GrainTypeMeta> GrainTypes { get; }

        /// <summary>
        /// Gets what this package contains.
        /// </summary>
        [Id(4)]
        public GrainPackageContent ContentType { get; }

        /// <summary>
        /// Gets the assembly files in this package.
        /// </summary>
        [Id(5)]
        public ImmutableList<GrainPackageAssembly> Assemblies { get; }

        /// <summary>
        /// Gets the package metadata (author, description, etc.).
        /// </summary>
        [Id(6)]
        public ImmutableDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Gets a grain type descriptor by name.
        /// </summary>
        /// <param name="grainTypeName">The fully qualified grain type name.</param>
        /// <param name="version">Optional version filter.</param>
        /// <returns>The grain type metadata, or null if not found.</returns>
        public GrainTypeMeta? GetGrainType(string grainTypeName, string? version = null)
        {
            return GrainTypes.FirstOrDefault(t =>
                t.FullName == grainTypeName &&
                (version == null || t.Version == version));
        }

        /// <summary>
        /// Creates summary info for this package.
        /// </summary>
        /// <param name="loadedOnSilos">The silos that have this package loaded.</param>
        /// <returns>A summary of this package.</returns>
        public GrainPackageInfo ToInfo(ImmutableList<Runtime.SiloAddress>? loadedOnSilos = null)
        {
            return new GrainPackageInfo(
                PackageId,
                Version,
                ContentHash,
                GrainTypes.Count,
                ContentType,
                loadedOnSilos ?? ImmutableList<Runtime.SiloAddress>.Empty);
        }
    }

    /// <summary>
    /// What content a grain package contains.
    /// </summary>
    public enum GrainPackageContent
    {
        /// <summary>
        /// Contains only interfaces and generated proxies (for clients).
        /// </summary>
        InterfacesOnly = 0,

        /// <summary>
        /// Contains interfaces, proxies, and implementations (for silos).
        /// </summary>
        Full = 1,

        /// <summary>
        /// Contains only implementations (requires separate interface package).
        /// </summary>
        ImplementationsOnly = 2
    }

    /// <summary>
    /// An assembly file within a grain package.
    /// </summary>
    [Serializable, GenerateSerializer, Immutable]
    public sealed class GrainPackageAssembly
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainPackageAssembly"/> class.
        /// </summary>
        public GrainPackageAssembly(
            string fileName,
            string assemblyName,
            string version,
            string hash,
            GrainAssemblyRole role)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            AssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Role = role;
        }

        /// <summary>
        /// Gets the file name of the assembly.
        /// </summary>
        [Id(0)]
        public string FileName { get; }

        /// <summary>
        /// Gets the assembly name.
        /// </summary>
        [Id(1)]
        public string AssemblyName { get; }

        /// <summary>
        /// Gets the assembly version.
        /// </summary>
        [Id(2)]
        public string Version { get; }

        /// <summary>
        /// Gets the hash of the assembly file.
        /// </summary>
        [Id(3)]
        public string Hash { get; }

        /// <summary>
        /// Gets the role of this assembly in the package.
        /// </summary>
        [Id(4)]
        public GrainAssemblyRole Role { get; }
    }

    /// <summary>
    /// The role of an assembly within a grain package.
    /// </summary>
    public enum GrainAssemblyRole
    {
        /// <summary>
        /// Contains grain interfaces.
        /// </summary>
        Interfaces = 0,

        /// <summary>
        /// Contains grain implementations.
        /// </summary>
        Implementation = 1,

        /// <summary>
        /// Contains generated serialization/proxy code.
        /// </summary>
        Codegen = 2,

        /// <summary>
        /// A dependency required by the grain assemblies.
        /// </summary>
        Dependency = 3
    }

    /// <summary>
    /// Summary info about a grain package (without full assembly content).
    /// </summary>
    [Serializable, GenerateSerializer, Immutable]
    public sealed class GrainPackageInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainPackageInfo"/> class.
        /// </summary>
        public GrainPackageInfo(
            string packageId,
            string version,
            string contentHash,
            int grainTypeCount,
            GrainPackageContent contentType,
            ImmutableList<Runtime.SiloAddress> loadedOnSilos)
        {
            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
            GrainTypeCount = grainTypeCount;
            ContentType = contentType;
            LoadedOnSilos = loadedOnSilos ?? ImmutableList<Runtime.SiloAddress>.Empty;
        }

        /// <summary>
        /// Gets the package identifier.
        /// </summary>
        [Id(0)]
        public string PackageId { get; }

        /// <summary>
        /// Gets the package version.
        /// </summary>
        [Id(1)]
        public string Version { get; }

        /// <summary>
        /// Gets the content hash.
        /// </summary>
        [Id(2)]
        public string ContentHash { get; }

        /// <summary>
        /// Gets the number of grain types in the package.
        /// </summary>
        [Id(3)]
        public int GrainTypeCount { get; }

        /// <summary>
        /// Gets the content type of the package.
        /// </summary>
        [Id(4)]
        public GrainPackageContent ContentType { get; }

        /// <summary>
        /// Gets the silos that have this package loaded.
        /// </summary>
        [Id(5)]
        public ImmutableList<Runtime.SiloAddress> LoadedOnSilos { get; }
    }
}
