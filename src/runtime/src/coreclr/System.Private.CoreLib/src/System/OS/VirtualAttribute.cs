// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.OS
{
    /// <summary>
    /// Marks a type as participating in TypeDriver routing with virtual object behavior.
    ///
    /// Phase 2: Specifies driver flags for persistence and dirty tracking.
    /// Types marked with [Virtual] can be created via VKernel.New&lt;T&gt;() and
    /// will automatically use appropriate drivers for storage and field access.
    /// </summary>
    /// <example>
    /// <code>
    /// [Virtual]  // Default: Persist + DirtyTrack
    /// public class Customer
    /// {
    ///     public string Name;
    ///     public int Age;
    /// }
    ///
    /// [Virtual(DriverFlags.Persist | DriverFlags.Immutable)]
    /// public class ReadOnlyConfig
    /// {
    ///     public string Setting;
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class VirtualAttribute : Attribute
    {
        /// <summary>
        /// Driver flags for this type.
        /// </summary>
        public DriverFlags Flags { get; }

        /// <summary>
        /// Create with default flags (Persist + DirtyTrack).
        /// </summary>
        public VirtualAttribute()
            : this(DriverFlags.Persist | DriverFlags.DirtyTrack)
        {
        }

        /// <summary>
        /// Create with specific flags.
        /// </summary>
        /// <param name="flags">Driver behavior flags.</param>
        public VirtualAttribute(DriverFlags flags)
        {
            Flags = flags;
        }
    }

    /// <summary>
    /// Marks a type as persistent (shorthand for [Virtual(DriverFlags.Persist)]).
    ///
    /// Phase 2: Equivalent to [Virtual] with Persist flag.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class PersistentAttribute : Attribute
    {
        /// <summary>
        /// Driver flags for this type.
        /// </summary>
        public DriverFlags Flags => DriverFlags.Persist | DriverFlags.DirtyTrack;
    }

    /// <summary>
    /// Marks a field as not persisted (excluded from serialization).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TransientAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a field as a reference to another virtual object.
    /// The referenced object is stored by VUID, not embedded.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MemorizeAttribute : Attribute
    {
    }
}
