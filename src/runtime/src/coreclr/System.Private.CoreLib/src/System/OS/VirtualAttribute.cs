// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.OS
{
    /// <summary>
    /// Marks a type as participating in TypeDriver routing.
    /// Phase 1: Used for testing infrastructure.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class VirtualAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a type as persistent (Phase 2+).
    /// Phase 1: Reserved, no effect.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class PersistentAttribute : Attribute
    {
    }
}
