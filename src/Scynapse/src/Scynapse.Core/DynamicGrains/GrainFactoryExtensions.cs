using System;
using System.Collections.Concurrent;
using System.Linq;
using Scynapse.Metadata;
using Scynapse.Runtime;

#nullable enable

namespace Scynapse.DynamicGrains
{
    /// <summary>
    /// Extension methods for <see cref="IGrainFactory"/> to support dynamic grain access.
    /// </summary>
    public static class GrainFactoryExtensions
    {
        private static readonly ConcurrentDictionary<string, Type?> _typeCache = new();

        // =============================================
        // GetGrainDynamic by type name
        // =============================================

        /// <summary>
        /// Gets a grain reference as a dynamic object by type name.
        /// Enables late-bound method invocation without compile-time type reference.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeName">Fully qualified grain interface name (e.g., "MyNamespace.IHelloGrain").</param>
        /// <param name="primaryKey">The string primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        /// <exception cref="ArgumentException">Thrown when the type cannot be resolved.</exception>
        public static dynamic GetGrainDynamic(this IGrainFactory factory, string grainTypeName, string primaryKey)
        {
            var grainType = ResolveGrainType(grainTypeName);
            var grain = factory.GetGrain(grainType, primaryKey);
            return new DynamicGrainReference(grain, grainType);
        }

        /// <summary>
        /// Gets a grain reference as a dynamic object by type name.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The Guid primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        public static dynamic GetGrainDynamic(this IGrainFactory factory, string grainTypeName, Guid primaryKey)
        {
            var grainType = ResolveGrainType(grainTypeName);
            var grain = factory.GetGrain(grainType, primaryKey);
            return new DynamicGrainReference(grain, grainType);
        }

        /// <summary>
        /// Gets a grain reference as a dynamic object by type name.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The long primary key.</param>
        /// <returns>A dynamic grain reference.</returns>
        public static dynamic GetGrainDynamic(this IGrainFactory factory, string grainTypeName, long primaryKey)
        {
            var grainType = ResolveGrainType(grainTypeName);
            var grain = factory.GetGrain(grainType, primaryKey);
            return new DynamicGrainReference(grain, grainType);
        }

        // =============================================
        // GetGrain with GrainTypeMeta
        // =============================================

        /// <summary>
        /// Gets a grain reference using type metadata from GTD.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeMeta">Grain type metadata from GTD.</param>
        /// <param name="primaryKey">The string primary key.</param>
        /// <returns>A dynamic grain reference with routing info from metadata.</returns>
        public static dynamic GetGrain(this IGrainFactory factory, GrainTypeMeta grainTypeMeta, string primaryKey)
        {
            ArgumentNullException.ThrowIfNull(grainTypeMeta);

            var grainType = ResolveGrainType(grainTypeMeta.FullName);
            var grain = factory.GetGrain(grainType, primaryKey);
            return new DynamicGrainReference(grain, grainType, grainTypeMeta);
        }

        /// <summary>
        /// Gets a grain reference using type metadata from GTD.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeMeta">Grain type metadata from GTD.</param>
        /// <param name="primaryKey">The Guid primary key.</param>
        /// <returns>A dynamic grain reference with routing info from metadata.</returns>
        public static dynamic GetGrain(this IGrainFactory factory, GrainTypeMeta grainTypeMeta, Guid primaryKey)
        {
            ArgumentNullException.ThrowIfNull(grainTypeMeta);

            var grainType = ResolveGrainType(grainTypeMeta.FullName);
            var grain = factory.GetGrain(grainType, primaryKey);
            return new DynamicGrainReference(grain, grainType, grainTypeMeta);
        }

        /// <summary>
        /// Gets a grain reference using type metadata from GTD.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeMeta">Grain type metadata from GTD.</param>
        /// <param name="primaryKey">The long primary key.</param>
        /// <returns>A dynamic grain reference with routing info from metadata.</returns>
        public static dynamic GetGrain(this IGrainFactory factory, GrainTypeMeta grainTypeMeta, long primaryKey)
        {
            ArgumentNullException.ThrowIfNull(grainTypeMeta);

            var grainType = ResolveGrainType(grainTypeMeta.FullName);
            var grain = factory.GetGrain(grainType, primaryKey);
            return new DynamicGrainReference(grain, grainType, grainTypeMeta);
        }

        // =============================================
        // TryGetGrainDynamic (non-throwing variants)
        // =============================================

        /// <summary>
        /// Attempts to get a grain reference as a dynamic object.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The string primary key.</param>
        /// <param name="result">The dynamic grain reference if successful.</param>
        /// <returns>True if the type was resolved and grain reference created; false otherwise.</returns>
        public static bool TryGetGrainDynamic(
            this IGrainFactory factory,
            string grainTypeName,
            string primaryKey,
            out dynamic? result)
        {
            var grainType = TryResolveGrainType(grainTypeName);
            if (grainType == null)
            {
                result = null;
                return false;
            }

            try
            {
                var grain = factory.GetGrain(grainType, primaryKey);
                result = new DynamicGrainReference(grain, grainType);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to get a grain reference as a dynamic object.
        /// </summary>
        /// <param name="factory">The grain factory.</param>
        /// <param name="grainTypeName">Fully qualified grain interface name.</param>
        /// <param name="primaryKey">The Guid primary key.</param>
        /// <param name="result">The dynamic grain reference if successful.</param>
        /// <returns>True if the type was resolved and grain reference created; false otherwise.</returns>
        public static bool TryGetGrainDynamic(
            this IGrainFactory factory,
            string grainTypeName,
            Guid primaryKey,
            out dynamic? result)
        {
            var grainType = TryResolveGrainType(grainTypeName);
            if (grainType == null)
            {
                result = null;
                return false;
            }

            try
            {
                var grain = factory.GetGrain(grainType, primaryKey);
                result = new DynamicGrainReference(grain, grainType);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        // =============================================
        // Helper methods
        // =============================================

        /// <summary>
        /// Resolves a grain type by name, searching all loaded assemblies.
        /// </summary>
        /// <param name="grainTypeName">The fully qualified type name.</param>
        /// <returns>The resolved type.</returns>
        /// <exception cref="ArgumentException">Thrown when the type cannot be resolved.</exception>
        private static Type ResolveGrainType(string grainTypeName)
        {
            var type = TryResolveGrainType(grainTypeName);
            if (type == null)
            {
                throw new ArgumentException(
                    $"Could not resolve grain type '{grainTypeName}'. " +
                    "Ensure the assembly containing this type is loaded.",
                    nameof(grainTypeName));
            }
            return type;
        }

        /// <summary>
        /// Attempts to resolve a grain type by name.
        /// </summary>
        /// <param name="grainTypeName">The fully qualified type name.</param>
        /// <returns>The resolved type, or null if not found.</returns>
        private static Type? TryResolveGrainType(string grainTypeName)
        {
            return _typeCache.GetOrAdd(grainTypeName, name =>
            {
                // Try Type.GetType first (works if assembly is already loaded)
                var type = Type.GetType(name);
                if (type != null)
                {
                    return type;
                }

                // Search all loaded assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(name);
                    if (type != null)
                    {
                        return type;
                    }
                }

                // Try with common assembly patterns
                var assemblyName = name.Contains('.') ? name.Substring(0, name.LastIndexOf('.')) : null;
                if (assemblyName != null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name?.StartsWith(assemblyName) == true)
                        {
                            type = assembly.GetType(name);
                            if (type != null)
                            {
                                return type;
                            }
                        }
                    }
                }

                return null;
            });
        }

        /// <summary>
        /// Clears the type resolution cache. Useful after loading new assemblies.
        /// </summary>
        public static void ClearTypeCache()
        {
            _typeCache.Clear();
        }
    }
}
