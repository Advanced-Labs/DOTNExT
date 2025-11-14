using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Serialization.Configuration;
using Orleans.Serialization.Serializers;

namespace Orleans.Runtime.DynamicGrains;

/// <summary>
/// Manages dynamic registration of serializers and codecs for dynamically loaded grain types.
/// </summary>
internal sealed class DynamicSerializationManager
{
    private readonly CodecProvider _codecProvider;
    private readonly ILogger<DynamicSerializationManager> _logger;
    private readonly object _registrationLock = new();

    public DynamicSerializationManager(
        CodecProvider codecProvider,
        ILogger<DynamicSerializationManager> logger)
    {
        _codecProvider = codecProvider ?? throw new ArgumentNullException(nameof(codecProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers codecs and serializers from a loaded assembly's metadata.
    /// </summary>
    /// <param name="metadata">The assembly load metadata containing serializer types</param>
    public void RegisterSerializers(AssemblyLoadMetadata metadata)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        lock (_registrationLock)
        {
            _logger.LogInformation(
                "Registering {SerializerCount} serializers and {CopierCount} copiers for dynamic grain types",
                metadata.Serializers.Count,
                metadata.Copiers.Count);

            // Create a TypeManifestOptions with the new types
            var manifestOptions = new TypeManifestOptions();

            // Add serializers
            foreach (var serializerType in metadata.Serializers)
            {
                manifestOptions.Serializers.Add(serializerType);
            }

            // Add copiers
            foreach (var copierType in metadata.Copiers)
            {
                manifestOptions.Copiers.Add(copierType);
            }

            // Register with the codec provider using reflection
            // This calls the private ConsumeMetadata method
            try
            {
                var consumeMetadataMethod = typeof(CodecProvider).GetMethod(
                    "ConsumeMetadata",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (consumeMetadataMethod != null)
                {
                    var optionsWrapper = Options.Create(manifestOptions);
                    consumeMetadataMethod.Invoke(_codecProvider, new object[] { optionsWrapper });

                    _logger.LogInformation("Successfully registered serializers and copiers");
                }
                else
                {
                    _logger.LogWarning(
                        "Could not find ConsumeMetadata method on CodecProvider. " +
                        "Serialization for dynamically loaded types may fall back to generalized codecs.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to register serializers and copiers. " +
                    "Dynamic grain types may use fallback serialization.");
            }

            // Clear relevant caches to ensure new codecs are discovered
            InvalidateCaches(metadata);
        }
    }

    /// <summary>
    /// Invalidates codec caches for the specified types to force re-resolution.
    /// </summary>
    private void InvalidateCaches(AssemblyLoadMetadata metadata)
    {
        // Get the cache dictionaries using reflection
        var codecProviderType = typeof(CodecProvider);

        var untypedCodecsField = codecProviderType.GetField(
            "_untypedCodecs",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var typedCodecsField = codecProviderType.GetField(
            "_typedCodecs",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var untypedCopiersField = codecProviderType.GetField(
            "_untypedCopiers",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var typedCopiersField = codecProviderType.GetField(
            "_typedCopiers",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Clear caches for grain types
        var typesToInvalidate = new HashSet<Type>();
        typesToInvalidate.UnionWith(metadata.GrainClasses);
        typesToInvalidate.UnionWith(metadata.GrainInterfaces);

        foreach (var type in typesToInvalidate)
        {
            TryClearCacheEntry(untypedCodecsField, type);
            TryClearCacheEntry(typedCodecsField, type);
            TryClearCacheEntry(untypedCopiersField, type);
            TryClearCacheEntry(typedCopiersField, type);
        }

        if (typesToInvalidate.Count > 0)
        {
            _logger.LogDebug("Invalidated codec caches for {TypeCount} types", typesToInvalidate.Count);
        }
    }

    private void TryClearCacheEntry(FieldInfo cacheField, Type type)
    {
        if (cacheField == null) return;

        try
        {
            var cache = cacheField.GetValue(_codecProvider);
            if (cache != null)
            {
                var removeMethod = cache.GetType().GetMethod("TryRemove", new[] { typeof(Type), cache.GetType().GetGenericArguments()[0].MakeByRefType() });
                if (removeMethod != null)
                {
                    var parameters = new object[] { type, null };
                    removeMethod.Invoke(cache, parameters);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear cache entry for type {Type}", type.Name);
        }
    }
}
