# T08: Driver Registry

> **Work Package:** WP2.0
> **Dependencies:** T05 (Storage_Voron Driver), T07 (FieldAccess_Persist Driver)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Implement a driver registry system that maps virtual types to their OpsRoot configurations, enabling automatic driver selection when objects are created or loaded.

---

## Background

In Phase 1, drivers are manually assigned to objects. Phase 2 requires:
1. Automatic driver selection based on type attributes
2. Registration of custom drivers for specific types
3. Default driver fallback behavior
4. Type-to-OpsRoot mapping for materialization

---

## Implementation

### 1. DriverRegistry Class

**File:** `src/runtime/src/coreclr/vm/tds/driverregistry.h` (new)

```cpp
#pragma once

#include "common.h"
#include "shash.h"
#include "crst.h"
#include "opsroot.h"

// Forward declarations
class MethodTable;

namespace TDS
{
    // Registration entry for a type
    struct DriverRegistration
    {
        MethodTable* pMT;
        OpsRoot* pOpsRoot;
        DWORD flags;
    };

    // Traits for MethodTable* -> DriverRegistration mapping
    class DriverRegistrationTraits : public DefaultSHashTraits<DriverRegistration>
    {
    public:
        typedef MethodTable* key_t;

        static key_t GetKey(const DriverRegistration& entry) { return entry.pMT; }
        static BOOL Equals(key_t k1, key_t k2) { return k1 == k2; }
        static count_t Hash(key_t k) { return (count_t)(size_t)k; }

        static const DriverRegistration Null() { return { nullptr, nullptr, 0 }; }
        static bool IsNull(const DriverRegistration& entry) { return entry.pMT == nullptr; }
    };

    // Registry flags
    enum DriverRegistryFlags
    {
        DRF_NONE = 0,
        DRF_PERSIST = 0x01,        // Enable persistence for this type
        DRF_DIRTY_TRACK = 0x02,    // Enable dirty tracking
        DRF_AUTO_FLUSH = 0x04,     // Auto-flush on transaction commit
        DRF_IMMUTABLE = 0x08,      // Objects are immutable (no dirty tracking)
    };

    class DriverRegistry
    {
    private:
        SHash<DriverRegistrationTraits> m_registrations;
        CrstExplicitInit m_lock;
        OpsRoot* m_pDefaultPersistOpsRoot;

        static DriverRegistry* s_pInstance;

    public:
        static void Initialize();
        static void Shutdown();
        static DriverRegistry* Instance() { return s_pInstance; }

        // Register a type with specific drivers
        void Register(MethodTable* pMT, OpsRoot* pOpsRoot, DWORD flags);

        // Unregister a type
        void Unregister(MethodTable* pMT);

        // Get OpsRoot for a type (returns default if not registered)
        OpsRoot* GetOpsRoot(MethodTable* pMT);

        // Get registration flags for a type
        DWORD GetFlags(MethodTable* pMT);

        // Check if type should be persisted
        bool ShouldPersist(MethodTable* pMT);

        // Set default persist OpsRoot
        void SetDefaultPersistOpsRoot(OpsRoot* pOpsRoot);

    private:
        DriverRegistry();
        ~DriverRegistry();
    };
}
```

### 2. DriverRegistry Implementation

**File:** `src/runtime/src/coreclr/vm/tds/driverregistry.cpp` (new)

```cpp
#include "common.h"
#include "driverregistry.h"
#include "opsroot.h"

namespace TDS
{
    DriverRegistry* DriverRegistry::s_pInstance = nullptr;

    void DriverRegistry::Initialize()
    {
        _ASSERTE(s_pInstance == nullptr);
        s_pInstance = new DriverRegistry();
    }

    void DriverRegistry::Shutdown()
    {
        delete s_pInstance;
        s_pInstance = nullptr;
    }

    DriverRegistry::DriverRegistry()
        : m_pDefaultPersistOpsRoot(nullptr)
    {
        m_lock.Init(CrstDriverRegistry);
    }

    DriverRegistry::~DriverRegistry()
    {
        m_lock.Destroy();
    }

    void DriverRegistry::Register(MethodTable* pMT, OpsRoot* pOpsRoot, DWORD flags)
    {
        CrstHolder holder(&m_lock);

        // Remove existing registration if any
        const DriverRegistration* existing = m_registrations.LookupPtr(pMT);
        if (existing != nullptr)
        {
            m_registrations.RemovePtr(existing);
        }

        DriverRegistration entry = { pMT, pOpsRoot, flags };
        m_registrations.Add(entry);
    }

    void DriverRegistry::Unregister(MethodTable* pMT)
    {
        CrstHolder holder(&m_lock);

        const DriverRegistration* existing = m_registrations.LookupPtr(pMT);
        if (existing != nullptr)
        {
            m_registrations.RemovePtr(existing);
        }
    }

    OpsRoot* DriverRegistry::GetOpsRoot(MethodTable* pMT)
    {
        CrstHolder holder(&m_lock);

        const DriverRegistration* entry = m_registrations.LookupPtr(pMT);
        if (entry != nullptr && entry->pOpsRoot != nullptr)
        {
            return entry->pOpsRoot;
        }

        // Return default persist OpsRoot if registered for persistence
        if (m_pDefaultPersistOpsRoot != nullptr)
        {
            return m_pDefaultPersistOpsRoot;
        }

        // Fall back to default non-persist OpsRoot
        return &g_DefaultOpsRoot;
    }

    DWORD DriverRegistry::GetFlags(MethodTable* pMT)
    {
        CrstHolder holder(&m_lock);

        const DriverRegistration* entry = m_registrations.LookupPtr(pMT);
        if (entry != nullptr)
        {
            return entry->flags;
        }

        return DRF_NONE;
    }

    bool DriverRegistry::ShouldPersist(MethodTable* pMT)
    {
        return (GetFlags(pMT) & DRF_PERSIST) != 0;
    }

    void DriverRegistry::SetDefaultPersistOpsRoot(OpsRoot* pOpsRoot)
    {
        CrstHolder holder(&m_lock);
        m_pDefaultPersistOpsRoot = pOpsRoot;
    }
}
```

### 3. Managed TypeDriverRegistry

**File:** `System.Private.CoreLib/src/System/OS/TypeDriverRegistry.cs` (new)

```csharp
namespace System.OS
{
    /// <summary>
    /// Flags for driver registration.
    /// </summary>
    [Flags]
    public enum DriverFlags
    {
        None = 0,
        Persist = 0x01,
        DirtyTrack = 0x02,
        AutoFlush = 0x04,
        Immutable = 0x08,
    }

    /// <summary>
    /// Registry for mapping types to virtual object drivers.
    /// </summary>
    public static class TypeDriverRegistry
    {
        /// <summary>
        /// Register a type for virtual object behavior.
        /// </summary>
        public static void Register<T>(DriverFlags flags = DriverFlags.Persist | DriverFlags.DirtyTrack)
        {
            Register(typeof(T), flags);
        }

        /// <summary>
        /// Register a type for virtual object behavior.
        /// </summary>
        public static void Register(Type type, DriverFlags flags = DriverFlags.Persist | DriverFlags.DirtyTrack)
        {
            ArgumentNullException.ThrowIfNull(type);
            RegisterInternal(type.TypeHandle.Value, (uint)flags);
        }

        /// <summary>
        /// Unregister a type.
        /// </summary>
        public static void Unregister<T>()
        {
            Unregister(typeof(T));
        }

        /// <summary>
        /// Unregister a type.
        /// </summary>
        public static void Unregister(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            UnregisterInternal(type.TypeHandle.Value);
        }

        /// <summary>
        /// Check if a type is registered for persistence.
        /// </summary>
        public static bool IsRegisteredForPersist<T>()
        {
            return IsRegisteredForPersist(typeof(T));
        }

        /// <summary>
        /// Check if a type is registered for persistence.
        /// </summary>
        public static bool IsRegisteredForPersist(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return ShouldPersistInternal(type.TypeHandle.Value);
        }

        /// <summary>
        /// Get registration flags for a type.
        /// </summary>
        public static DriverFlags GetFlags<T>()
        {
            return GetFlags(typeof(T));
        }

        /// <summary>
        /// Get registration flags for a type.
        /// </summary>
        public static DriverFlags GetFlags(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return (DriverFlags)GetFlagsInternal(type.TypeHandle.Value);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_RegisterType")]
        private static partial void RegisterInternal(IntPtr typeHandle, uint flags);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_UnregisterType")]
        private static partial void UnregisterInternal(IntPtr typeHandle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_ShouldPersist")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShouldPersistInternal(IntPtr typeHandle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSNative_GetTypeFlags")]
        private static partial uint GetFlagsInternal(IntPtr typeHandle);
    }
}
```

### 4. QCalls for Driver Registry

**File:** `src/runtime/src/coreclr/vm/tds/tdsqcalls.cpp` (add)

```cpp
extern "C" void QCALLTYPE TDSNative_RegisterType(void* typeHandle, UINT32 flags)
{
    QCALL_CONTRACT;
    BEGIN_QCALL;

    MethodTable* pMT = (MethodTable*)typeHandle;
    _ASSERTE(pMT != nullptr);

    // Create OpsRoot with appropriate drivers based on flags
    OpsRoot* pOpsRoot = new OpsRoot();
    *pOpsRoot = g_DefaultOpsRoot;  // Start with defaults

    if (flags & TDS::DRF_PERSIST)
    {
        // Will be set up by T05/T07 integration
        pOpsRoot->pStorageOps = &g_VoronStorageOps;
    }

    if (flags & TDS::DRF_DIRTY_TRACK)
    {
        pOpsRoot->pFieldAccessOps = &g_PersistentFieldAccessOps;
    }

    TDS::DriverRegistry::Instance()->Register(pMT, pOpsRoot, flags);

    END_QCALL;
}

extern "C" void QCALLTYPE TDSNative_UnregisterType(void* typeHandle)
{
    QCALL_CONTRACT;
    BEGIN_QCALL;

    MethodTable* pMT = (MethodTable*)typeHandle;
    if (pMT != nullptr)
    {
        TDS::DriverRegistry::Instance()->Unregister(pMT);
    }

    END_QCALL;
}

extern "C" BOOL QCALLTYPE TDSNative_ShouldPersist(void* typeHandle)
{
    QCALL_CONTRACT;
    BOOL result = FALSE;

    BEGIN_QCALL;

    MethodTable* pMT = (MethodTable*)typeHandle;
    if (pMT != nullptr)
    {
        result = TDS::DriverRegistry::Instance()->ShouldPersist(pMT);
    }

    END_QCALL;
    return result;
}

extern "C" UINT32 QCALLTYPE TDSNative_GetTypeFlags(void* typeHandle)
{
    QCALL_CONTRACT;
    UINT32 result = 0;

    BEGIN_QCALL;

    MethodTable* pMT = (MethodTable*)typeHandle;
    if (pMT != nullptr)
    {
        result = TDS::DriverRegistry::Instance()->GetFlags(pMT);
    }

    END_QCALL;
    return result;
}
```

### 5. VirtualAttribute Enhancement

**File:** `System.Private.CoreLib/src/System/OS/VirtualAttribute.cs` (modify)

```csharp
namespace System.OS
{
    /// <summary>
    /// Marks a class as a virtual object that can be persisted.
    /// </summary>
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
        public VirtualAttribute(DriverFlags flags)
        {
            Flags = flags;
        }
    }
}
```

---

## CrstType Addition

**File:** `src/runtime/src/coreclr/inc/CrstTypes.h` (modify)

Add to CrstType enum:
```cpp
CrstDriverRegistry,
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `vm/tds/driverregistry.h` | Create | Registry header |
| `vm/tds/driverregistry.cpp` | Create | Registry implementation |
| `System/OS/TypeDriverRegistry.cs` | Create | Managed API |
| `System/OS/VirtualAttribute.cs` | Modify | Add Flags property |
| `vm/tds/tdsqcalls.cpp` | Modify | Add registry QCalls |
| `inc/CrstTypes.h` | Modify | Add CrstDriverRegistry |

---

## Acceptance Criteria

- [ ] Types can be registered for persistence
- [ ] Registration flags control behavior
- [ ] GetOpsRoot returns appropriate driver set
- [ ] Unregistration works correctly
- [ ] Thread-safe operations
- [ ] VirtualAttribute uses registration system

---

## Testing

```csharp
[Fact]
public void TypeDriverRegistry_RegisterType()
{
    TypeDriverRegistry.Register<TestObject>(DriverFlags.Persist | DriverFlags.DirtyTrack);

    Assert.True(TypeDriverRegistry.IsRegisteredForPersist<TestObject>());
    Assert.Equal(DriverFlags.Persist | DriverFlags.DirtyTrack,
                 TypeDriverRegistry.GetFlags<TestObject>());
}

[Fact]
public void TypeDriverRegistry_UnregisterType()
{
    TypeDriverRegistry.Register<TestObject>();
    TypeDriverRegistry.Unregister<TestObject>();

    Assert.False(TypeDriverRegistry.IsRegisteredForPersist<TestObject>());
}

[Fact]
public void VirtualAttribute_SetsFlags()
{
    var attr = typeof(TestVirtualObject).GetCustomAttribute<VirtualAttribute>();
    Assert.NotNull(attr);
    Assert.True(attr.Flags.HasFlag(DriverFlags.Persist));
}

[Virtual]
class TestVirtualObject
{
    public int Value { get; set; }
}
```

---

## References

- Phase 2 Main Doc: Section 4 (Per-Type Configuration)
- Phase 1 T02: OpsRoot Side Table
- Phase 1 T07: Managed API Surface
