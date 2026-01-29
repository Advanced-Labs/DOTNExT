# T07: Managed API Surface

> **Work Package:** WP7
> **Dependencies:** T01-T06
> **Estimated Complexity:** Low
> **Status:** Pending

---

## Objective

Expose minimal C# API for testing TypeDriver infrastructure from managed code.

---

## Naming Conventions

| Context | Convention | Example |
|---------|------------|---------|
| C# namespace | `System.OS` | `using System.OS;` |
| C# runtime helper | `TypeDriverHelper` | `TypeDriverHelper.IsNonDefaultRouted(obj)` |
| C# intrinsics | `VIntrinsics` | `VIntrinsics.ReadField<T>(...)` |
| C++ QCalls | `TDSNative_*` | `TDSNative_EnableNonDefaultRouting` |
| C++ header bit | `BIT_SBLK_TDS_NONDEFAULT` | Bit 31 in ObjHeader |

**Rationale:**
- `System.OS` is short, frequently imported, and reflects VAYRON's "OS-like" vision
- `TypeDriver` in C# is human-readable; `TDS` (TypeDriver System) in C++ for brevity
- `VIntrinsics` = VAYRON Intrinsics (low-level)

---

## New Files to Create

| File | Purpose |
|------|---------|
| `src/.../System.Private.CoreLib/src/System/OS/VirtualAttribute.cs` | Marker attribute |
| `src/.../System.Private.CoreLib/src/System/OS/TypeDriverHelper.cs` | Runtime API |
| `src/.../System.Private.CoreLib/src/System/OS/VIntrinsics.cs` | Low-level intrinsics |
| `src/runtime/src/coreclr/vm/tds/tdsqcalls.cpp` | Native QCall implementations |

---

## Implementation Steps

### Step 1: Create Marker Attributes

**File:** `VirtualAttribute.cs`

```csharp
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
```

### Step 2: Create TypeDriverHelper Class

**File:** `TypeDriverHelper.cs`

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.OS
{
    /// <summary>
    /// Runtime services for TypeDriver System (TDS).
    /// Phase 1: Testing and diagnostics only.
    /// </summary>
    public static class TypeDriverHelper
    {
        /// <summary>
        /// Check if object is using non-default TypeDriver routing.
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

### Step 3: Create VIntrinsics Class (for field access testing)

**File:** `VIntrinsics.cs`

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace System.OS
{
    /// <summary>
    /// Low-level field access through TypeDriver routing.
    /// Phase 1: For testing driver dispatch.
    /// </summary>
    internal static class VIntrinsics
    {
        /// <summary>
        /// Read a value-type field through TypeDriver routing.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern T ReadField<T>(object obj, int fieldOffset) where T : unmanaged;

        /// <summary>
        /// Write a value-type field through TypeDriver routing.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void WriteField<T>(object obj, int fieldOffset, T value) where T : unmanaged;

        /// <summary>
        /// Write a reference field through TypeDriver routing (with barrier).
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void WriteRefField(object obj, int fieldOffset, object? value);

        /// <summary>
        /// Read a reference field through TypeDriver routing.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern object? ReadRefField(object obj, int fieldOffset);
    }
}
```

### Step 4: Implement Native QCalls

**File:** `tdsqcalls.cpp`

```cpp
#include "common.h"
#include "tds/opsroot.h"
#include "tds/opsroottable.h"
#include "tds/tdsintrinsics.h"
#include "qcall.h"

//=============================================================================
// TypeDriverHelper QCalls
//=============================================================================

extern "C" BOOL QCALLTYPE TDSNative_IsNonDefaultRouted(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BOOL result = FALSE;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        result = objRef->GetHeader()->IsTDSNonDefault() ? TRUE : FALSE;
    }

    END_QCALL;

    return result;
}

extern "C" void QCALLTYPE TDSNative_EnableNonDefaultRouting(QCall::ObjectHandleOnStack obj)
{
    QCALL_CONTRACT;

    BEGIN_QCALL;

    GCX_COOP();
    OBJECTREF objRef = obj.Get();
    if (objRef != NULL)
    {
        // Create default OpsRoot (all default drivers)
        OpsRoot* ops = TDS_CreateOpsRoot(nullptr, nullptr, nullptr, nullptr);
        TDS_SetOpsRoot(OBJECTREFToObject(objRef), ops);
    }

    END_QCALL;
}

extern "C" void QCALLTYPE TDSNative_DisableNonDefaultRouting(QCall::ObjectHandleOnStack obj)
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

extern "C" UINT32 QCALLTYPE TDSNative_GetDriverFlags(QCall::ObjectHandleOnStack obj)
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

extern "C" INT32 QCALLTYPE TDSNative_GetRoutedObjectCount()
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
FCFuncElement("IsNonDefaultRouted", TDSNative_IsNonDefaultRouted)
FCFuncElement("EnableNonDefaultRouting", TDSNative_EnableNonDefaultRouting)
FCFuncElement("DisableNonDefaultRouting", TDSNative_DisableNonDefaultRouting)
FCFuncElement("GetDriverFlags", TDSNative_GetDriverFlags)
FCFuncElement("GetRoutedObjectCount", TDSNative_GetRoutedObjectCount)
```

---

## Usage Example

```csharp
using System.OS;
using System.Diagnostics;

// Test code
public class TypeDriverTest
{
    public static void TestBasicRouting()
    {
        var obj = new TestClass { Value = 42 };

        // Initially not routed
        Debug.Assert(!TypeDriverHelper.IsNonDefaultRouted(obj));

        // Enable routing
        TypeDriverHelper.EnableNonDefaultRouting(obj);
        Debug.Assert(TypeDriverHelper.IsNonDefaultRouted(obj));

        // Field access still works
        Debug.Assert(obj.Value == 42);

        // Disable routing
        TypeDriverHelper.DisableNonDefaultRouting(obj);
        Debug.Assert(!TypeDriverHelper.IsNonDefaultRouted(obj));
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
- [ ] `TypeDriverHelper.IsNonDefaultRouted()` returns correct value
- [ ] `TypeDriverHelper.EnableNonDefaultRouting()` enables routing
- [ ] `TypeDriverHelper.DisableNonDefaultRouting()` disables routing
- [ ] `TypeDriverHelper.GetDriverFlags()` returns correct flags
- [ ] `TypeDriverHelper.GetRoutedObjectCount()` returns correct count
- [ ] `VIntrinsics` methods work for field access
- [ ] All QCalls properly registered
- [ ] Managed tests pass

---

## References

- Main Doc: Part III SS3.2 WP7
- CLR QCall documentation in runtime sources
