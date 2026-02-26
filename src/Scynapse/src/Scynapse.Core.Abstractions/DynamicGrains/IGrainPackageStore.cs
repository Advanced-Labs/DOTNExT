using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scynapse.Metadata;

#nullable enable

namespace Scynapse.DynamicGrains
{
    /// <summary>
    /// Central store for grain packages. Orchestrates multiple package sources
    /// and provides a unified interface for package retrieval.
    /// </summary>
    public interface IGrainPackageStore
    {
        /// <summary>
        /// Gets a package by ID, checking sources in priority order.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">Optional version. If null, returns the latest available.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The package with its content, or null if not found.</returns>
        Task<LoadedGrainPackage?> GetPackageAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all available packages across all sources.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available package info.</returns>
        Task<IReadOnlyList<GrainPackageInfo>> ListPackagesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a package to the default writable source.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <param name="content">The package content (assemblies).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if published successfully.</returns>
        Task<bool> PublishPackageAsync(
            GrainPackage package,
            LoadedGrainPackage content,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers a package source.
        /// </summary>
        /// <param name="source">The source to register.</param>
        void RegisterSource(IGrainPackageSource source);

        /// <summary>
        /// Gets all registered sources.
        /// </summary>
        IReadOnlyList<IGrainPackageSource> Sources { get; }
    }

    /// <summary>
    /// A source for grain packages (file system, NuGet, grain storage, etc.).
    /// </summary>
    public interface IGrainPackageSource
    {
        /// <summary>
        /// Gets the name of this source.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the priority of this source. Lower values are checked first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets whether this source supports writing (publishing packages).
        /// </summary>
        bool IsWritable { get; }

        /// <summary>
        /// Attempts to fetch a package from this source.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">Optional version. If null, returns the latest.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The package content, or null if not found.</returns>
        Task<LoadedGrainPackage?> FetchAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all packages available from this source.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available packages.</returns>
        Task<IReadOnlyList<GrainPackageInfo>> ListAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a package to this source.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <param name="content">The package content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if published successfully.</returns>
        /// <exception cref="System.NotSupportedException">Thrown if the source is not writable.</exception>
        Task<bool> PublishAsync(
            GrainPackage package,
            LoadedGrainPackage content,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A loaded grain package with its assembly bytes.
    /// </summary>
    public sealed class LoadedGrainPackage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoadedGrainPackage"/> class.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <param name="assemblies">The assembly files.</param>
        public LoadedGrainPackage(GrainPackage package, IReadOnlyDictionary<string, byte[]> assemblies)
        {
            Package = package ?? throw new System.ArgumentNullException(nameof(package));
            Assemblies = assemblies ?? throw new System.ArgumentNullException(nameof(assemblies));
        }

        /// <summary>
        /// Gets the package metadata.
        /// </summary>
        public GrainPackage Package { get; }

        /// <summary>
        /// Gets the assembly files. Key is the file name, value is the bytes.
        /// </summary>
        public IReadOnlyDictionary<string, byte[]> Assemblies { get; }

        /// <summary>
        /// Gets the total size of all assemblies in bytes.
        /// </summary>
        public long TotalSize
        {
            get
            {
                long total = 0;
                foreach (var asm in Assemblies.Values)
                {
                    total += asm.Length;
                }
                return total;
            }
        }
    }
}
