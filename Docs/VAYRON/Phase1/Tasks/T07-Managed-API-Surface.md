# T07: Managed API Surface

> **Work Package:** WP7
> **Dependencies:** T01-T06
> **Estimated Complexity:** Low
> **Status:** Pending

---

## Objective

Expose minimal C# API for testing DDS infrastructure from managed code.

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/.../System.Private.CoreLib/src/System/Runtime/DDS/VirtualAttribute.cs` | Marker attribute |
| `src/.../System.Private.CoreLib/src/System/Runtime/DDS/DDSRuntime.cs` | Runtime API |
| `src/runtime/src/coreclr/vm/dds/ddsqcalls.cpp` | Native QCall implementations |

---

## Implementation Steps

### Step 1: Create Marker Attributes

**File:** `VirtualAttribute.cs`

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.DDS
{
    /// <summary>
    /// Marks a type as participating in DDS routing.
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
```

### Step 2: Create DDSRuntime Class

**File:** `DDSRuntime.cs`

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.DDS
{
    /// <summary>
    /// Runtime services for DDS (Device Driver System).
    /// Phase 1: Testing and diagnostics only.
    /// </summary>
    public static class DDSRuntime
    {
        /// <summary>
        /// Check if object is using non-default DDS routing.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool IsNonDefaultRouted(object obj);

        /// <summary>
        /// Enable non-default routing for an object.
        /// Creates default OpsRoot (all default drivers).
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void EnableNonDefaultRouting(object obj);

        /// <summary>
        /// Disable non-default routing for an object.
        /// Returns object to standard CLR behavior.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void DisableNonDefaultRouting(object obj);

        /// <summary>
        /// Get driver flags for an object.
        /// Returns 0 for default objects.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern uint GetDriverFlags(object obj);

        /// <summary>
        /// Get count of routed objects (diagnostics).
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern int GetRoutedObjectCount();
    }
}
```

### Step 3: Create DDSIntrinsics Class (for field access testing)

**File:** `DDSIntrinsics.cs`

```csharp
namespace System.Runtime.DDS
{
    /// <summary>
    /// Low-level field access through DDS routing.
    /// Phase 1: For testing driver dispatch.
    /// </summary>
    internal static class DDSIntrinsics
    {
        /// <summary>
        /// Read a value-type field through DDS routing.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern T ReadField<T>(object obj, int fieldOffset) where T : unmanaged;

        /// <summary>
        /// Write a value-type field through DDS routing.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void WriteField<T>(object obj, int fieldOffset, T value) where T : unmanaged;

        /// <summary>
        /// Write a reference field through DDS routing (with barrier).
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void WriteRefField(object obj, int fieldOffset, object? value);

        /// <summary>
        /// Read a reference field through DDS routing.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern object? ReadRefField(object obj, int fieldOffset);
    }
}
```

### Step 4: Implement Native QCalls

**File:** `ddsqcalls.cpp`

```cpp
#include "common.h"
#include "dds/opsroot.h"
#include "dds/opsroottable.h"
#include "dds/ddsintrinsics.h"
#include "qcall.h"

//=============================================================================
// DDSRuntime QCalls
//=============================================================================

extern "C" BOOL QCALLTYPE DDSNative_IsNonDefaultRouted(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        result = objRef->GetHeader()->IsDDSNonDefault() ? TRUE : FALSE;
    }

    END_QCALL;

    return result;
}

extern "C" void QCALLTYPE DDSNative_EnableNonDefaultRouting(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        // Create default OpsRoot (all default drivers)
        OpsRoot* ops = DDS_CreateOpsRoot(nullptr, nullptr, nullptr, nullptr);
        DDS_SetOpsRoot(OBJECTREFToObject(objRef), ops);
    }

    END_QCALL;
}

extern "C" void QCALLTYPE DDSNative_DisableNonDefaultRouting(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        g_OpsRootTable.Remove(OBJECTREFToObject(objRef));
    }

    END_QCALL;
}

extern "C" UINT32 QCALLTYPE DDSNative_GetDriverFlags(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    UINT32 flags = 0;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        OpsRoot* ops = g_OpsRootTable.Get(OBJECTREFToObject(objRef));
        flags = ops->flags;
    }

    END_QCALL;

    return flags;
}

extern "C" INT32 QCALLTYPE DDSNative_GetRoutedObjectCount()
{
    QCALL_CONTRACT;

    INT32 count = 0;

    BEGIN_QCALL;

    count = (INT32)g_OpsRootTable.GetCount();

    END_QCALL;

    return count;
}
```

### Step 5: Register QCalls

Add to QCall registration table (exact location varies by CLR version):

```cpp
// In qcallentrypoints.cpp or similar
FCFuncElement("IsNonDefaultRouted", DDSNative_IsNonDefaultRouted)
FCFuncElement("EnableNonDefaultRouting", DDSNative_EnableNonDefaultRouting)
FCFuncElement("DisableNonDefaultRouting", DDSNative_DisableNonDefaultRouting)
FCFuncElement("GetDriverFlags", DDSNative_GetDriverFlags)
FCFuncElement("GetRoutedObjectCount", DDSNative_GetRoutedObjectCount)
```

---

## Usage Example

```csharp
// Test code
public class DDSTest
{
    public static void TestBasicRouting()
    {
        var obj = new TestClass { Value = 42 };

        // Initially not routed
        Debug.Assert(!DDSRuntime.IsNonDefaultRouted(obj));

        // Enable routing
        DDSRuntime.EnableNonDefaultRouting(obj);
        Debug.Assert(DDSRuntime.IsNonDefaultRouted(obj));

        // Field access still works
        Debug.Assert(obj.Value == 42);

        // Disable routing
        DDSRuntime.DisableNonDefaultRouting(obj);
        Debug.Assert(!DDSRuntime.IsNonDefaultRouted(obj));
    }
}

public class TestClass
{
    public int Value { get; set; }
}
```

---

## Acceptance Criteria

- [ ] `VirtualAttribute` and `PersistentAttribute` compile
- [ ] `DDSRuntime.IsNonDefaultRouted()` returns correct value
- [ ] `DDSRuntime.EnableNonDefaultRouting()` enables routing
- [ ] `DDSRuntime.DisableNonDefaultRouting()` disables routing
- [ ] `DDSRuntime.GetDriverFlags()` returns correct flags
- [ ] `DDSRuntime.GetRoutedObjectCount()` returns correct count
- [ ] `DDSIntrinsics` methods work for field access
- [ ] All QCalls properly registered
- [ ] Managed tests pass

---

## References

- Main Doc: Part III §3.2 WP7
- CLR QCall documentation in runtime sources
