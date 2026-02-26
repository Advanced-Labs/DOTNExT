using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Scynapse.Metadata;

#nullable enable

namespace Scynapse.DynamicGrains
{
    /// <summary>
    /// Handle to a loaded grain package.
    /// Provides access to grain types and references from the package.
    /// </summary>
    public sealed class GrainPackageHandle : IAsyncDisposable
    {
        private readonly IGrainFactory _grainFactory;
        private readonly Func<GrainPackageHandle, Task>? _unloadCallback;
        private readonly ConcurrentDictionary<string, Type> _loadedTypes = new();
        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GrainPackageHandle"/> class.
        /// </summary>
        /// <param name="package">The grain package.</param>
        /// <param name="content">The package content with assemblies.</param>
        /// <param name="grainFactory">The grain factory for creating references.</param>
        /// <param name="loadContext">Optional assembly load context for the loaded assemblies.</param>
        /// <param name="unloadCallback">Callback invoked when the handle is disposed.</param>
        internal GrainPackageHandle(
            GrainPackage package,
            LoadedGrainPackage content,
            IGrainFactory grainFactory,
            AssemblyLoadContext? loadContext = null,
            Func<GrainPackageHandle, Task>? unloadCallback = null)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
            LoadContext = loadContext;
            _unloadCallback = unloadCallback;
            LoadedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the grain package metadata.
        /// </summary>
        public GrainPackage Package { get; }

        /// <summary>
        /// Gets the package content (assemblies).
        /// </summary>
        public LoadedGrainPackage Content { get; }

        /// <summary>
        /// Gets the assembly load context, if available.
        /// </summary>
        public AssemblyLoadContext? LoadContext { get; }

        /// <summary>
        /// Gets the time when the package was loaded.
        /// </summary>
        public DateTime LoadedAt { get; }

        /// <summary>
        /// Gets whether the package is currently loaded.
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                lock (_lock)
                {
                    return !_disposed;
                }
            }
        }

        /// <summary>
        /// Gets the package identifier.
        /// </summary>
        public string PackageId => Package.PackageId;

        /// <summary>
        /// Gets the package version.
        /// </summary>
        public string Version => Package.Version;

        /// <summary>
        /// Gets a grain type by name from this package.
        /// </summary>
        /// <param name="name">The grain type name (can be simple name or full name).</param>
        /// <param name="version">Optional version filter.</param>
        /// <returns>The grain type metadata, or null if not found.</returns>
        public GrainTypeMeta? GetGrainType(string name, string? version = null)
        {
            ThrowIfDisposed();

            return Package.GrainTypes.FirstOrDefault(t =>
                (t.FullName == name || t.TypeName == name) &&
                (version == null || t.Version == version));
        }

        /// <summary>
        /// Gets all grain types in this package.
        /// </summary>
        public IReadOnlyList<GrainTypeMeta> GrainTypes => Package.GrainTypes;

        /// <summary>
        /// Gets a dynamic grain reference from this package.
        /// </summary>
        /// <param name="grainTypeName">The grain type name.</param>
        /// <param name="primaryKey">The string primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        public dynamic GetGrain(string grainTypeName, string primaryKey)
        {
            ThrowIfDisposed();

            var grainType = ResolveGrainType(grainTypeName);
            var grain = _grainFactory.GetGrain(grainType, primaryKey);
            var meta = GetGrainType(grainTypeName);
            return new DynamicGrainReference(grain, grainType, meta);
        }

        /// <summary>
        /// Gets a dynamic grain reference from this package.
        /// </summary>
        /// <param name="grainTypeName">The grain type name.</param>
        /// <param name="primaryKey">The Guid primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        public dynamic GetGrain(string grainTypeName, Guid primaryKey)
        {
            ThrowIfDisposed();

            var grainType = ResolveGrainType(grainTypeName);
            var grain = _grainFactory.GetGrain(grainType, primaryKey);
            var meta = GetGrainType(grainTypeName);
            return new DynamicGrainReference(grain, grainType, meta);
        }

        /// <summary>
        /// Gets a dynamic grain reference from this package.
        /// </summary>
        /// <param name="grainTypeName">The grain type name.</param>
        /// <param name="primaryKey">The long primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        public dynamic GetGrain(string grainTypeName, long primaryKey)
        {
            ThrowIfDisposed();

            var grainType = ResolveGrainType(grainTypeName);
            var grain = _grainFactory.GetGrain(grainType, primaryKey);
            var meta = GetGrainType(grainTypeName);
            return new DynamicGrainReference(grain, grainType, meta);
        }

        /// <summary>
        /// Gets a strongly-typed grain reference if the interface is available.
        /// </summary>
        /// <typeparam name="TGrainInterface">The grain interface type.</typeparam>
        /// <param name="primaryKey">The string primary key.</param>
        /// <returns>The strongly-typed grain reference.</returns>
        public TGrainInterface GetGrain<TGrainInterface>(string primaryKey)
            where TGrainInterface : IGrainWithStringKey
        {
            ThrowIfDisposed();
            return _grainFactory.GetGrain<TGrainInterface>(primaryKey);
        }

        /// <summary>
        /// Gets a strongly-typed grain reference if the interface is available.
        /// </summary>
        /// <typeparam name="TGrainInterface">The grain interface type.</typeparam>
        /// <param name="primaryKey">The Guid primary key.</param>
        /// <returns>The strongly-typed grain reference.</returns>
        public TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey)
            where TGrainInterface : IGrainWithGuidKey
        {
            ThrowIfDisposed();
            return _grainFactory.GetGrain<TGrainInterface>(primaryKey);
        }

        /// <summary>
        /// Gets a strongly-typed grain reference if the interface is available.
        /// </summary>
        /// <typeparam name="TGrainInterface">The grain interface type.</typeparam>
        /// <param name="primaryKey">The long primary key.</param>
        /// <returns>The strongly-typed grain reference.</returns>
        public TGrainInterface GetGrain<TGrainInterface>(long primaryKey)
            where TGrainInterface : IGrainWithIntegerKey
        {
            ThrowIfDisposed();
            return _grainFactory.GetGrain<TGrainInterface>(primaryKey);
        }

        /// <summary>
        /// Tries to get a CLR Type for a grain interface in this package.
        /// </summary>
        /// <param name="grainTypeName">The grain type name.</param>
        /// <param name="type">The resolved type if found.</param>
        /// <returns>True if the type was resolved.</returns>
        public bool TryGetType(string grainTypeName, out Type? type)
        {
            ThrowIfDisposed();

            if (_loadedTypes.TryGetValue(grainTypeName, out type))
            {
                return true;
            }

            type = TryResolveType(grainTypeName);
            if (type != null)
            {
                _loadedTypes[grainTypeName] = type;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            _loadedTypes.Clear();

            if (_unloadCallback != null)
            {
                await _unloadCallback(this);
            }
        }

        private Type ResolveGrainType(string grainTypeName)
        {
            if (TryGetType(grainTypeName, out var type) && type != null)
            {
                return type;
            }

            throw new InvalidOperationException(
                $"Could not resolve grain type '{grainTypeName}' in package '{PackageId}'. " +
                "Ensure the assembly containing this type is loaded.");
        }

        private Type? TryResolveType(string typeName)
        {
            // First, check the metadata to get the full type name
            var meta = GetGrainType(typeName);
            var fullName = meta?.FullName ?? typeName;

            // Try Type.GetType
            var type = Type.GetType(fullName);
            if (type != null) return type;

            // Search in the load context if available
            if (LoadContext != null)
            {
                foreach (var assembly in LoadContext.Assemblies)
                {
                    type = assembly.GetType(fullName);
                    if (type != null) return type;
                }
            }

            // Search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName);
                if (type != null) return type;
            }

            // Try matching by simple name
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                if (type != null) return type;
            }

            return null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GrainPackageHandle),
                    $"Package handle for '{PackageId}' v{Version} has been disposed.");
            }
        }

        /// <summary>
        /// Returns a string representation of this handle.
        /// </summary>
        public override string ToString()
            => $"GrainPackageHandle({PackageId} v{Version}, {Package.GrainTypes.Count} types, Loaded={IsLoaded})";
    }
}
