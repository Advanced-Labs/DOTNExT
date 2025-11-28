using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Metadata;

#nullable enable

namespace Orleans.DynamicGrains
{
    /// <summary>
    /// Extended client for dynamic grain access with package management.
    /// Works for both external clients AND silos (grain-to-grain calls).
    /// </summary>
    public interface IDynamicGrainClient
    {
        // =============================================
        // Package Management
        // =============================================

        /// <summary>
        /// Loads a grain package from the cluster's package store.
        /// Downloads and caches locally if not already present.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="version">Optional version. If null, loads the latest available.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A handle to the loaded package.</returns>
        Task<GrainPackageHandle> LoadPackageAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unloads a previously loaded package, freeing resources.
        /// </summary>
        /// <param name="handle">The package handle to unload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UnloadPackageAsync(
            GrainPackageHandle handle,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all available packages in the cluster.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available package info.</returns>
        Task<IReadOnlyList<GrainPackageInfo>> ListAvailablePackagesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all currently loaded packages.
        /// </summary>
        IReadOnlyList<GrainPackageHandle> LoadedPackages { get; }

        // =============================================
        // Grain Access
        // =============================================

        /// <summary>
        /// Gets a grain dynamically by type name.
        /// Will auto-load the required package if not already loaded.
        /// </summary>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The string primary key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dynamic grain reference.</returns>
        Task<dynamic> GetGrainDynamicAsync(
            string grainTypeName,
            string primaryKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a grain dynamically by type name.
        /// </summary>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The Guid primary key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dynamic grain reference.</returns>
        Task<dynamic> GetGrainDynamicAsync(
            string grainTypeName,
            Guid primaryKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a grain dynamically by type name.
        /// </summary>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The long primary key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A dynamic grain reference.</returns>
        Task<dynamic> GetGrainDynamicAsync(
            string grainTypeName,
            long primaryKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a grain using metadata from a loaded package.
        /// </summary>
        /// <param name="grainType">The grain type metadata.</param>
        /// <param name="primaryKey">The string primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        dynamic GetGrain(GrainTypeMeta grainType, string primaryKey);

        /// <summary>
        /// Gets a grain using metadata from a loaded package.
        /// </summary>
        /// <param name="grainType">The grain type metadata.</param>
        /// <param name="primaryKey">The Guid primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        dynamic GetGrain(GrainTypeMeta grainType, Guid primaryKey);

        /// <summary>
        /// Gets a grain using metadata from a loaded package.
        /// </summary>
        /// <param name="grainType">The grain type metadata.</param>
        /// <param name="primaryKey">The long primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        dynamic GetGrain(GrainTypeMeta grainType, long primaryKey);

        /// <summary>
        /// Invokes a method on a grain by name (fully dynamic).
        /// Will auto-load the required package if not already loaded.
        /// </summary>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The grain primary key.</param>
        /// <param name="methodName">The method name to invoke.</param>
        /// <param name="args">Method arguments.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The method result.</returns>
        Task<object?> InvokeMethodAsync(
            string grainTypeName,
            string primaryKey,
            string methodName,
            object?[]? args = null,
            CancellationToken cancellationToken = default);

        // =============================================
        // GTD Queries
        // =============================================

        /// <summary>
        /// Queries the Grain Type Directory for available types.
        /// </summary>
        /// <param name="namespaceFilter">Optional namespace filter (exact match).</param>
        /// <param name="namePattern">Optional name pattern (supports wildcards like *Hello*).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of matching grain type metadata.</returns>
        Task<IReadOnlyList<GrainTypeMeta>> QueryGrainTypesAsync(
            string? namespaceFilter = null,
            string? namePattern = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed metadata for a specific grain type.
        /// </summary>
        /// <param name="grainTypeName">Fully qualified grain type name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The grain type metadata, or null if not found.</returns>
        Task<GrainTypeMeta?> GetGrainTypeMetaAsync(
            string grainTypeName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the grain factory used by this client.
        /// </summary>
        IGrainFactory GrainFactory { get; }
    }
}
