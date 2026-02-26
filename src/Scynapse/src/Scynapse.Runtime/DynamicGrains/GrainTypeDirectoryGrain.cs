using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scynapse.DynamicGrains;
using Scynapse.Metadata;
using Scynapse.Providers;
using Scynapse.Runtime;

#nullable enable

namespace Scynapse.Runtime.DynamicGrains
{
    /// <summary>
    /// State for the Grain Type Directory grain.
    /// </summary>
    [GenerateSerializer]
    public sealed class GrainTypeDirectoryState
    {
        /// <summary>
        /// Registered packages by (packageId, version) key.
        /// </summary>
        [Id(0)]
        public Dictionary<string, GrainPackage> Packages { get; set; } = new();

        /// <summary>
        /// Tracks which silos have loaded each package.
        /// Key: "packageId:version", Value: list of silo addresses.
        /// </summary>
        [Id(1)]
        public Dictionary<string, HashSet<SiloAddress>> PackageSilos { get; set; } = new();

        /// <summary>
        /// Helper to create a package key.
        /// </summary>
        public static string MakePackageKey(string packageId, string version) => $"{packageId}:{version}";
    }

    /// <summary>
    /// Implementation of the Grain Type Directory (GTD).
    /// A singleton grain that maintains a cluster-wide registry of grain packages and types.
    /// </summary>
    [StorageProvider(ProviderName = ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME)]
    public class GrainTypeDirectoryGrain : Grain<GrainTypeDirectoryState>, IGrainTypeDirectoryGrain
    {
        private readonly ILogger<GrainTypeDirectoryGrain> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrainTypeDirectoryGrain"/> class.
        /// </summary>
        public GrainTypeDirectoryGrain(ILogger<GrainTypeDirectoryGrain> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            _logger.LogInformation("Grain Type Directory activated with {PackageCount} packages",
                State.Packages.Count);
        }

        // =============================================
        // Package Registration
        // =============================================

        /// <inheritdoc />
        public async Task RegisterPackageAsync(GrainPackage package)
        {
            ArgumentNullException.ThrowIfNull(package);

            var key = GrainTypeDirectoryState.MakePackageKey(package.PackageId, package.Version);

            if (State.Packages.TryGetValue(key, out var existing))
            {
                _logger.LogInformation(
                    "Updating existing package {PackageId} v{Version} (had {OldCount} types, now {NewCount})",
                    package.PackageId, package.Version,
                    existing.GrainTypes.Count, package.GrainTypes.Count);
            }
            else
            {
                _logger.LogInformation(
                    "Registering new package {PackageId} v{Version} with {TypeCount} grain types",
                    package.PackageId, package.Version, package.GrainTypes.Count);
            }

            State.Packages[key] = package;

            // Initialize silo tracking if not exists
            if (!State.PackageSilos.ContainsKey(key))
            {
                State.PackageSilos[key] = new HashSet<SiloAddress>();
            }

            await WriteStateAsync();
        }

        /// <inheritdoc />
        public async Task<bool> UnregisterPackageAsync(string packageId, string version)
        {
            var key = GrainTypeDirectoryState.MakePackageKey(packageId, version);

            if (State.Packages.Remove(key))
            {
                State.PackageSilos.Remove(key);
                _logger.LogInformation("Unregistered package {PackageId} v{Version}", packageId, version);
                await WriteStateAsync();
                return true;
            }

            _logger.LogWarning("Package {PackageId} v{Version} not found for unregistration", packageId, version);
            return false;
        }

        // =============================================
        // Package Queries
        // =============================================

        /// <inheritdoc />
        public Task<ImmutableList<GrainPackageInfo>> GetPackagesAsync()
        {
            var result = State.Packages.Values
                .Select(p =>
                {
                    var key = GrainTypeDirectoryState.MakePackageKey(p.PackageId, p.Version);
                    var silos = State.PackageSilos.TryGetValue(key, out var s)
                        ? s.ToImmutableList()
                        : ImmutableList<SiloAddress>.Empty;
                    return p.ToInfo(silos);
                })
                .ToImmutableList();

            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task<GrainPackage?> GetPackageAsync(string packageId, string? version = null)
        {
            if (version != null)
            {
                var key = GrainTypeDirectoryState.MakePackageKey(packageId, version);
                State.Packages.TryGetValue(key, out var package);
                return Task.FromResult(package);
            }

            // Find latest version by comparing version strings
            var latest = State.Packages.Values
                .Where(p => p.PackageId == packageId)
                .OrderByDescending(p => p.Version)
                .FirstOrDefault();

            return Task.FromResult(latest);
        }

        // =============================================
        // Grain Type Queries
        // =============================================

        /// <inheritdoc />
        public Task<ImmutableList<GrainTypeMeta>> GetAllGrainTypesAsync()
        {
            var result = State.Packages.Values
                .SelectMany(p => p.GrainTypes.Select(t => UpdateTypeWithSilos(t, p)))
                .ToImmutableList();

            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task<ImmutableList<GrainTypeMeta>> FindGrainTypesAsync(
            string? namespaceFilter = null,
            string? namePattern = null)
        {
            var query = State.Packages.Values.SelectMany(p =>
                p.GrainTypes.Select(t => UpdateTypeWithSilos(t, p)));

            // Apply namespace filter
            if (!string.IsNullOrEmpty(namespaceFilter))
            {
                query = query.Where(t =>
                    t.Namespace.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply name pattern (supports * wildcard)
            if (!string.IsNullOrEmpty(namePattern))
            {
                var regex = new Regex(
                    "^" + Regex.Escape(namePattern).Replace("\\*", ".*") + "$",
                    RegexOptions.IgnoreCase);
                query = query.Where(t => regex.IsMatch(t.TypeName) || regex.IsMatch(t.FullName));
            }

            return Task.FromResult(query.ToImmutableList());
        }

        /// <inheritdoc />
        public Task<GrainTypeMeta?> GetGrainTypeAsync(string fullTypeName)
        {
            foreach (var package in State.Packages.Values)
            {
                var grainType = package.GrainTypes.FirstOrDefault(t =>
                    t.FullName.Equals(fullTypeName, StringComparison.OrdinalIgnoreCase));

                if (grainType != null)
                {
                    return Task.FromResult<GrainTypeMeta?>(UpdateTypeWithSilos(grainType, package));
                }
            }

            return Task.FromResult<GrainTypeMeta?>(null);
        }

        // =============================================
        // Silo Tracking
        // =============================================

        /// <inheritdoc />
        public async Task ReportPackageLoadedAsync(SiloAddress silo, string packageId, string version)
        {
            var key = GrainTypeDirectoryState.MakePackageKey(packageId, version);

            if (!State.PackageSilos.TryGetValue(key, out var silos))
            {
                silos = new HashSet<SiloAddress>();
                State.PackageSilos[key] = silos;
            }

            if (silos.Add(silo))
            {
                _logger.LogInformation(
                    "Silo {Silo} loaded package {PackageId} v{Version}. Now on {SiloCount} silos.",
                    silo, packageId, version, silos.Count);
                await WriteStateAsync();
            }
        }

        /// <inheritdoc />
        public async Task ReportPackageUnloadedAsync(SiloAddress silo, string packageId, string version)
        {
            var key = GrainTypeDirectoryState.MakePackageKey(packageId, version);

            if (State.PackageSilos.TryGetValue(key, out var silos) && silos.Remove(silo))
            {
                _logger.LogInformation(
                    "Silo {Silo} unloaded package {PackageId} v{Version}. Now on {SiloCount} silos.",
                    silo, packageId, version, silos.Count);
                await WriteStateAsync();
            }
        }

        /// <inheritdoc />
        public Task<ImmutableList<SiloAddress>> GetHostingSilosAsync(string grainTypeName)
        {
            // Find the package containing this grain type
            foreach (var package in State.Packages.Values)
            {
                var hasType = package.GrainTypes.Any(t =>
                    t.FullName.Equals(grainTypeName, StringComparison.OrdinalIgnoreCase));

                if (hasType)
                {
                    var key = GrainTypeDirectoryState.MakePackageKey(package.PackageId, package.Version);
                    if (State.PackageSilos.TryGetValue(key, out var silos))
                    {
                        return Task.FromResult(silos.ToImmutableList());
                    }
                }
            }

            return Task.FromResult(ImmutableList<SiloAddress>.Empty);
        }

        /// <inheritdoc />
        public async Task ReportSiloDownAsync(SiloAddress silo)
        {
            var modified = false;

            foreach (var kvp in State.PackageSilos)
            {
                if (kvp.Value.Remove(silo))
                {
                    _logger.LogInformation(
                        "Removed silo {Silo} from package {Package} due to silo down event",
                        silo, kvp.Key);
                    modified = true;
                }
            }

            if (modified)
            {
                await WriteStateAsync();
            }
        }

        // =============================================
        // Private Helpers
        // =============================================

        /// <summary>
        /// Updates a grain type with current hosting silos and availability.
        /// </summary>
        private GrainTypeMeta UpdateTypeWithSilos(GrainTypeMeta type, GrainPackage package)
        {
            var key = GrainTypeDirectoryState.MakePackageKey(package.PackageId, package.Version);
            var silos = State.PackageSilos.TryGetValue(key, out var s)
                ? s.ToImmutableList()
                : ImmutableList<SiloAddress>.Empty;

            return type
                .WithHostingSilos(silos)
                .WithAvailability(silos.Count > 0);
        }
    }
}
