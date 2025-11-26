using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.DynamicGrains;
using Orleans.Metadata;

#nullable enable

namespace Orleans.Runtime.DynamicGrains
{
    /// <summary>
    /// A package source that reads grain packages from the file system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Package directory structure:
    /// <code>
    /// {basePath}/
    /// ├── {packageId}/
    /// │   ├── {version}/
    /// │   │   ├── package.json       # Package metadata
    /// │   │   ├── {assembly1}.dll
    /// │   │   ├── {assembly2}.dll
    /// │   │   └── ...
    /// │   └── {version2}/
    /// │       └── ...
    /// └── {packageId2}/
    ///     └── ...
    /// </code>
    /// </para>
    /// </remarks>
    public class FileSystemPackageSource : IGrainPackageSource
    {
        private readonly string _basePath;
        private readonly ILogger<FileSystemPackageSource> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemPackageSource"/> class.
        /// </summary>
        /// <param name="basePath">The base directory for packages.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="priority">The source priority (lower = checked first).</param>
        public FileSystemPackageSource(
            string basePath,
            ILogger<FileSystemPackageSource> logger,
            int priority = 100)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Priority = priority;

            // Ensure directory exists
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        /// <inheritdoc />
        public string Name => $"FileSystem({_basePath})";

        /// <inheritdoc />
        public int Priority { get; }

        /// <inheritdoc />
        public bool IsWritable => true;

        /// <inheritdoc />
        public async Task<LoadedGrainPackage?> FetchAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            var packageDir = Path.Combine(_basePath, packageId);
            if (!Directory.Exists(packageDir))
            {
                _logger.LogDebug("Package directory not found: {Path}", packageDir);
                return null;
            }

            // Find version directory
            string versionDir;
            if (version != null)
            {
                versionDir = Path.Combine(packageDir, version);
                if (!Directory.Exists(versionDir))
                {
                    _logger.LogDebug("Version directory not found: {Path}", versionDir);
                    return null;
                }
            }
            else
            {
                // Get latest version
                var versions = Directory.GetDirectories(packageDir)
                    .Select(Path.GetFileName)
                    .Where(v => v != null)
                    .OrderByDescending(v => v)
                    .ToList();

                if (versions.Count == 0)
                {
                    _logger.LogDebug("No versions found for package: {PackageId}", packageId);
                    return null;
                }

                versionDir = Path.Combine(packageDir, versions[0]!);
            }

            // Read package metadata
            var metadataPath = Path.Combine(versionDir, "package.json");
            GrainPackage package;

            if (File.Exists(metadataPath))
            {
                var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                package = DeserializePackage(json, packageId, Path.GetFileName(versionDir)!);
            }
            else
            {
                // Create minimal package metadata from directory
                package = CreatePackageFromDirectory(packageId, Path.GetFileName(versionDir)!, versionDir);
            }

            // Read assemblies
            var assemblies = new Dictionary<string, byte[]>();
            foreach (var dllPath in Directory.GetFiles(versionDir, "*.dll"))
            {
                var fileName = Path.GetFileName(dllPath);
                assemblies[fileName] = await File.ReadAllBytesAsync(dllPath, cancellationToken);
            }

            _logger.LogInformation(
                "Loaded package {PackageId} v{Version} with {AssemblyCount} assemblies from {Path}",
                packageId, package.Version, assemblies.Count, versionDir);

            return new LoadedGrainPackage(package, assemblies);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<GrainPackageInfo>> ListAsync(CancellationToken cancellationToken = default)
        {
            var result = new List<GrainPackageInfo>();

            if (!Directory.Exists(_basePath))
            {
                return Task.FromResult<IReadOnlyList<GrainPackageInfo>>(result);
            }

            foreach (var packageDir in Directory.GetDirectories(_basePath))
            {
                var packageId = Path.GetFileName(packageDir);
                if (string.IsNullOrEmpty(packageId)) continue;

                foreach (var versionDir in Directory.GetDirectories(packageDir))
                {
                    var version = Path.GetFileName(versionDir);
                    if (string.IsNullOrEmpty(version)) continue;

                    var dllCount = Directory.GetFiles(versionDir, "*.dll").Length;
                    var metadataPath = Path.Combine(versionDir, "package.json");

                    // Try to get content hash from metadata or compute it
                    var contentHash = "unknown";
                    var contentType = Metadata.GrainPackageContent.Full;

                    if (File.Exists(metadataPath))
                    {
                        try
                        {
                            var json = File.ReadAllText(metadataPath);
                            var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("contentHash", out var hashProp))
                            {
                                contentHash = hashProp.GetString() ?? contentHash;
                            }
                        }
                        catch
                        {
                            // Ignore metadata parse errors for listing
                        }
                    }

                    result.Add(new GrainPackageInfo(
                        packageId,
                        version,
                        contentHash,
                        0, // grain type count unknown without full parse
                        contentType,
                        ImmutableList<SiloAddress>.Empty));
                }
            }

            return Task.FromResult<IReadOnlyList<GrainPackageInfo>>(result);
        }

        /// <inheritdoc />
        public async Task<bool> PublishAsync(
            GrainPackage package,
            LoadedGrainPackage content,
            CancellationToken cancellationToken = default)
        {
            var versionDir = Path.Combine(_basePath, package.PackageId, package.Version);

            try
            {
                // Create directory
                Directory.CreateDirectory(versionDir);

                // Write assemblies
                foreach (var (fileName, bytes) in content.Assemblies)
                {
                    var filePath = Path.Combine(versionDir, fileName);
                    await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
                }

                // Write metadata
                var metadataPath = Path.Combine(versionDir, "package.json");
                var json = SerializePackage(package);
                await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

                _logger.LogInformation(
                    "Published package {PackageId} v{Version} with {AssemblyCount} assemblies to {Path}",
                    package.PackageId, package.Version, content.Assemblies.Count, versionDir);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish package {PackageId} v{Version}", package.PackageId, package.Version);
                return false;
            }
        }

        private static GrainPackage CreatePackageFromDirectory(string packageId, string version, string directory)
        {
            var assemblies = new List<GrainPackageAssembly>();

            foreach (var dllPath in Directory.GetFiles(directory, "*.dll"))
            {
                var fileName = Path.GetFileName(dllPath);
                var hash = ComputeFileHash(dllPath);

                assemblies.Add(new GrainPackageAssembly(
                    fileName,
                    Path.GetFileNameWithoutExtension(fileName),
                    version,
                    hash,
                    GrainAssemblyRole.Implementation));
            }

            var contentHash = ComputeDirectoryHash(directory);

            return new GrainPackage(
                packageId,
                version,
                contentHash,
                ImmutableList<GrainTypeMeta>.Empty,
                Metadata.GrainPackageContent.Full,
                assemblies.ToImmutableList(),
                ImmutableDictionary<string, string>.Empty);
        }

        private static string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash)[..16]; // First 16 chars
        }

        private static string ComputeDirectoryHash(string directory)
        {
            using var sha256 = SHA256.Create();
            var files = Directory.GetFiles(directory, "*.dll").OrderBy(f => f);

            foreach (var file in files)
            {
                var fileBytes = File.ReadAllBytes(file);
                sha256.TransformBlock(fileBytes, 0, fileBytes.Length, fileBytes, 0);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha256.Hash!)[..16];
        }

        private static string SerializePackage(GrainPackage package)
        {
            var obj = new
            {
                packageId = package.PackageId,
                version = package.Version,
                contentHash = package.ContentHash,
                contentType = package.ContentType.ToString(),
                assemblies = package.Assemblies.Select(a => new
                {
                    fileName = a.FileName,
                    assemblyName = a.AssemblyName,
                    version = a.Version,
                    hash = a.Hash,
                    role = a.Role.ToString()
                }).ToArray(),
                grainTypes = package.GrainTypes.Select(t => new
                {
                    fullName = t.FullName,
                    typeName = t.TypeName,
                    @namespace = t.Namespace,
                    keyType = t.KeyType.ToString()
                }).ToArray(),
                metadata = package.Metadata
            };

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }

        private static GrainPackage DeserializePackage(string json, string fallbackPackageId, string fallbackVersion)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var packageId = root.TryGetProperty("packageId", out var pidProp)
                ? pidProp.GetString() ?? fallbackPackageId
                : fallbackPackageId;

            var version = root.TryGetProperty("version", out var verProp)
                ? verProp.GetString() ?? fallbackVersion
                : fallbackVersion;

            var contentHash = root.TryGetProperty("contentHash", out var hashProp)
                ? hashProp.GetString() ?? "unknown"
                : "unknown";

            var contentType = Metadata.GrainPackageContent.Full;
            if (root.TryGetProperty("contentType", out var ctProp))
            {
                Enum.TryParse<Metadata.GrainPackageContent>(ctProp.GetString(), out contentType);
            }

            var assemblies = ImmutableList<GrainPackageAssembly>.Empty;
            if (root.TryGetProperty("assemblies", out var asmProp) && asmProp.ValueKind == JsonValueKind.Array)
            {
                var builder = ImmutableList.CreateBuilder<GrainPackageAssembly>();
                foreach (var asmEl in asmProp.EnumerateArray())
                {
                    var fileName = asmEl.GetProperty("fileName").GetString() ?? "";
                    var asmName = asmEl.TryGetProperty("assemblyName", out var anProp)
                        ? anProp.GetString() ?? Path.GetFileNameWithoutExtension(fileName)
                        : Path.GetFileNameWithoutExtension(fileName);
                    var asmVer = asmEl.TryGetProperty("version", out var avProp)
                        ? avProp.GetString() ?? version
                        : version;
                    var asmHash = asmEl.TryGetProperty("hash", out var ahProp)
                        ? ahProp.GetString() ?? ""
                        : "";
                    var role = GrainAssemblyRole.Implementation;
                    if (asmEl.TryGetProperty("role", out var roleProp))
                    {
                        Enum.TryParse<GrainAssemblyRole>(roleProp.GetString(), out role);
                    }

                    builder.Add(new GrainPackageAssembly(fileName, asmName, asmVer, asmHash, role));
                }
                assemblies = builder.ToImmutable();
            }

            return new GrainPackage(
                packageId,
                version,
                contentHash,
                ImmutableList<GrainTypeMeta>.Empty, // Types loaded separately
                contentType,
                assemblies,
                ImmutableDictionary<string, string>.Empty);
        }
    }
}
