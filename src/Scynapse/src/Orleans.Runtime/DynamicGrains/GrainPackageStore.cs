using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.DynamicGrains;
using Orleans.Metadata;

#nullable enable

namespace Orleans.Runtime.DynamicGrains
{
    /// <summary>
    /// Default implementation of <see cref="IGrainPackageStore"/>.
    /// Orchestrates multiple package sources and provides a unified interface.
    /// </summary>
    public class GrainPackageStore : IGrainPackageStore
    {
        private readonly List<IGrainPackageSource> _sources = new();
        private readonly ILogger<GrainPackageStore> _logger;
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="GrainPackageStore"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public GrainPackageStore(ILogger<GrainPackageStore> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GrainPackageStore"/> class with initial sources.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="sources">Initial sources to register.</param>
        public GrainPackageStore(ILogger<GrainPackageStore> logger, IEnumerable<IGrainPackageSource> sources)
            : this(logger)
        {
            foreach (var source in sources)
            {
                RegisterSource(source);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<IGrainPackageSource> Sources
        {
            get
            {
                lock (_lock)
                {
                    return _sources.OrderBy(s => s.Priority).ToList();
                }
            }
        }

        /// <inheritdoc />
        public void RegisterSource(IGrainPackageSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            lock (_lock)
            {
                // Check for duplicate
                if (_sources.Any(s => s.Name == source.Name))
                {
                    _logger.LogWarning("Source {Name} already registered, skipping", source.Name);
                    return;
                }

                _sources.Add(source);
                _logger.LogInformation(
                    "Registered package source: {Name} (priority: {Priority}, writable: {Writable})",
                    source.Name, source.Priority, source.IsWritable);
            }
        }

        /// <inheritdoc />
        public async Task<LoadedGrainPackage?> GetPackageAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            var sources = Sources; // Get ordered snapshot

            foreach (var source in sources)
            {
                try
                {
                    _logger.LogDebug(
                        "Checking source {Name} for package {PackageId} v{Version}",
                        source.Name, packageId, version ?? "latest");

                    var content = await source.FetchAsync(packageId, version, cancellationToken);
                    if (content != null)
                    {
                        _logger.LogInformation(
                            "Found package {PackageId} v{Version} in source {Name}",
                            packageId, content.Package.Version, source.Name);
                        return content;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Error fetching package {PackageId} from source {Name}",
                        packageId, source.Name);
                }
            }

            _logger.LogWarning(
                "Package {PackageId} v{Version} not found in any source",
                packageId, version ?? "latest");
            return null;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<GrainPackageInfo>> ListPackagesAsync(
            CancellationToken cancellationToken = default)
        {
            var sources = Sources;
            var allPackages = new Dictionary<string, GrainPackageInfo>();

            foreach (var source in sources)
            {
                try
                {
                    var packages = await source.ListAsync(cancellationToken);
                    foreach (var pkg in packages)
                    {
                        var key = $"{pkg.PackageId}:{pkg.Version}";
                        // First source wins (highest priority)
                        if (!allPackages.ContainsKey(key))
                        {
                            allPackages[key] = pkg;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error listing packages from source {Name}", source.Name);
                }
            }

            return allPackages.Values.ToList();
        }

        /// <inheritdoc />
        public async Task<bool> PublishPackageAsync(
            GrainPackage package,
            LoadedGrainPackage content,
            CancellationToken cancellationToken = default)
        {
            var sources = Sources;
            var writableSource = sources.FirstOrDefault(s => s.IsWritable);

            if (writableSource == null)
            {
                _logger.LogError("No writable source available for publishing");
                return false;
            }

            try
            {
                _logger.LogInformation(
                    "Publishing package {PackageId} v{Version} to source {Name}",
                    package.PackageId, package.Version, writableSource.Name);

                return await writableSource.PublishAsync(package, content, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish package {PackageId} v{Version} to source {Name}",
                    package.PackageId, package.Version, writableSource.Name);
                return false;
            }
        }

        /// <summary>
        /// Publishes a package to a specific source by name.
        /// </summary>
        /// <param name="sourceName">The source name to publish to.</param>
        /// <param name="package">The package metadata.</param>
        /// <param name="content">The package content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if published successfully.</returns>
        public async Task<bool> PublishToSourceAsync(
            string sourceName,
            GrainPackage package,
            LoadedGrainPackage content,
            CancellationToken cancellationToken = default)
        {
            var source = Sources.FirstOrDefault(s =>
                s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

            if (source == null)
            {
                _logger.LogError("Source {Name} not found", sourceName);
                return false;
            }

            if (!source.IsWritable)
            {
                _logger.LogError("Source {Name} is not writable", sourceName);
                return false;
            }

            try
            {
                return await source.PublishAsync(package, content, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish package {PackageId} v{Version} to source {Name}",
                    package.PackageId, package.Version, sourceName);
                return false;
            }
        }
    }
}
