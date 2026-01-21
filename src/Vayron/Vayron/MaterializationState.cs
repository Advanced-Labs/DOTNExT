// VAYRON - Runtime-Integrated Persistent Storage
// Materialization state for handle bodies

namespace Vayron;

/// <summary>
/// Describes the materialization state of a VAYRON handle's body.
/// </summary>
public enum MaterializationState
{
    /// <summary>
    /// The body has not been loaded from storage.
    /// </summary>
    NotMaterialized = 0,

    /// <summary>
    /// The body is currently being loaded from storage.
    /// </summary>
    Materializing = 1,

    /// <summary>
    /// The body has been loaded and is cached.
    /// </summary>
    Materialized = 2,

    /// <summary>
    /// The body has been modified and needs to be persisted.
    /// </summary>
    Dirty = 3,

    /// <summary>
    /// The cached body is stale (transaction epoch changed).
    /// </summary>
    Stale = 4
}
