using System.Collections.Immutable;
using System.Threading.Tasks;
using Scynapse.Metadata;
using Scynapse.Runtime;

namespace Scynapse.DynamicGrains
{
    /// <summary>
    /// The Grain Type Directory (GTD) - a cluster-wide registry of grain types.
    /// Implemented as a singleton grain with key "gtd".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The GTD provides a central registry for discovering grain types across the cluster,
    /// enabling dynamic grain access without compile-time references. It tracks:
    /// </para>
    /// <list type="bullet">
    /// <item>Registered grain packages and their metadata</item>
    /// <item>Available grain types with full reflection-like information</item>
    /// <item>Which silos have loaded each package</item>
    /// </list>
    /// <para>
    /// Usage: <c>var gtd = grainFactory.GetGrain&lt;IGrainTypeDirectoryGrain&gt;("gtd");</c>
    /// </para>
    /// </remarks>
    public interface IGrainTypeDirectoryGrain : IGrainWithStringKey
    {
        // =============================================
        // Package Registration
        // =============================================

        /// <summary>
        /// Registers a grain package in the directory.
        /// </summary>
        /// <param name="package">The package to register.</param>
        /// <returns>A task that completes when registration is done.</returns>
        /// <remarks>
        /// If a package with the same ID and version already exists, this updates
        /// the registration with the new metadata.
        /// </remarks>
        Task RegisterPackageAsync(GrainPackage package);

        /// <summary>
        /// Unregisters a grain package from the directory.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">The package version.</param>
        /// <returns>True if the package was found and removed; false otherwise.</returns>
        Task<bool> UnregisterPackageAsync(string packageId, string version);

        // =============================================
        // Package Queries
        // =============================================

        /// <summary>
        /// Gets summary information for all registered packages.
        /// </summary>
        /// <returns>List of package summaries.</returns>
        Task<ImmutableList<GrainPackageInfo>> GetPackagesAsync();

        /// <summary>
        /// Gets a specific package by ID and optional version.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">
        /// Optional version. If null, returns the latest version.
        /// </param>
        /// <returns>The package, or null if not found.</returns>
        Task<GrainPackage?> GetPackageAsync(string packageId, string? version = null);

        // =============================================
        // Grain Type Queries
        // =============================================

        /// <summary>
        /// Gets all registered grain types across all packages.
        /// </summary>
        /// <returns>List of all grain type metadata.</returns>
        Task<ImmutableList<GrainTypeMeta>> GetAllGrainTypesAsync();

        /// <summary>
        /// Finds grain types matching optional filters.
        /// </summary>
        /// <param name="namespaceFilter">
        /// Optional namespace prefix to filter by (e.g., "MyApp.Grains").
        /// </param>
        /// <param name="namePattern">
        /// Optional name pattern to match (supports * wildcard).
        /// </param>
        /// <returns>List of matching grain types.</returns>
        Task<ImmutableList<GrainTypeMeta>> FindGrainTypesAsync(
            string? namespaceFilter = null,
            string? namePattern = null);

        /// <summary>
        /// Gets metadata for a specific grain type by full type name.
        /// </summary>
        /// <param name="fullTypeName">The full CLR type name (e.g., "MyApp.Grains.IHelloGrain").</param>
        /// <returns>The grain type metadata, or null if not found.</returns>
        Task<GrainTypeMeta?> GetGrainTypeAsync(string fullTypeName);

        // =============================================
        // Silo Tracking
        // =============================================

        /// <summary>
        /// Reports that a silo has loaded a package.
        /// </summary>
        /// <param name="silo">The silo address.</param>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">The package version.</param>
        /// <returns>A task that completes when the report is recorded.</returns>
        /// <remarks>
        /// This should be called by silos when they load a grain package,
        /// typically from IPluginGrainLoader after successful load.
        /// </remarks>
        Task ReportPackageLoadedAsync(SiloAddress silo, string packageId, string version);

        /// <summary>
        /// Reports that a silo has unloaded a package.
        /// </summary>
        /// <param name="silo">The silo address.</param>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">The package version.</param>
        /// <returns>A task that completes when the report is recorded.</returns>
        Task ReportPackageUnloadedAsync(SiloAddress silo, string packageId, string version);

        /// <summary>
        /// Gets the silos that have a specific grain type loaded.
        /// </summary>
        /// <param name="grainTypeName">The full grain type name.</param>
        /// <returns>List of silos hosting this grain type.</returns>
        Task<ImmutableList<SiloAddress>> GetHostingSilosAsync(string grainTypeName);

        /// <summary>
        /// Reports that a silo has gone down, removing all its package registrations.
        /// </summary>
        /// <param name="silo">The silo that went down.</param>
        /// <returns>A task that completes when cleanup is done.</returns>
        /// <remarks>
        /// This is typically called by the membership service when detecting silo failures.
        /// </remarks>
        Task ReportSiloDownAsync(SiloAddress silo);
    }
}
