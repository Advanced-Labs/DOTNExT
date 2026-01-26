# IMP-005: Dynamic Driver Loading

> **Status:** Backlog
> **Origin:** Phase 1 Gap Closure - Static Only Decision
> **Priority:** Low (Convenience feature for experimentation)
> **Target Phase:** Phase 4+

---

## Summary

Phase 1 uses **static driver loading only** - all drivers are compiled directly into the runtime. This improvement enables **dynamic driver loading** from external modules, allowing driver experimentation without runtime rebuilds.

---

## Current State (Phase 1)

```cpp
// Phase 1: All drivers are global singletons compiled in
extern IObjectModelOps  g_DefaultObjectModelOps;
extern IFieldAccessOps  g_DefaultFieldAccessOps;

OpsRoot g_DefaultOpsRoot = {
    1,  // version
    0,  // flags
    &g_DefaultObjectModelOps,
    &g_DefaultFieldAccessOps,
    nullptr,  // storageOps
    nullptr,  // callDispatchOps
    { ... }
};
```

**Limitations:**
- Adding new drivers requires runtime rebuild
- Cannot experiment with driver implementations easily
- All driver code ships with runtime

---

## Use Cases for Dynamic Loading

### 1. Driver Development
Iterate on driver implementations without rebuilding CoreCLR:
```
1. Build driver.dll with IStorageOps implementation
2. Load via DDSRuntime.LoadDriver("driver.dll")
3. Test, modify, reload
```

### 2. Plugin Architecture
Different storage backends as plugins:
```
- StorageDriver_Voron.dll
- StorageDriver_SQLite.dll
- StorageDriver_InMemory.dll
```

### 3. Environment-Specific Drivers
Load different drivers based on deployment:
```csharp
if (IsCloudEnvironment())
    DDSRuntime.LoadDriver("StorageDriver_CosmosDB.dll");
else
    DDSRuntime.LoadDriver("StorageDriver_Voron.dll");
```

---

## Proposed Design

### Driver Module Interface

```cpp
// Required exports from driver module
extern "C" {
    // Module initialization
    bool DDS_ModuleInit(DDSHostInterface* host);
    void DDS_ModuleShutdown();

    // Driver factory
    IObjectModelOps* DDS_CreateObjectModelDriver(const char* config);
    IFieldAccessOps* DDS_CreateFieldAccessDriver(const char* config);
    IStorageOps* DDS_CreateStorageDriver(const char* config);

    // Cleanup
    void DDS_DestroyDriver(void* driver);

    // Version/capability query
    uint32_t DDS_GetABIVersion();
    uint32_t DDS_GetCapabilities();
}
```

### Host Interface (Runtime to Module)

```cpp
struct DDSHostInterface {
    uint32_t version;

    // Memory allocation (use runtime allocator)
    void* (*Alloc)(size_t size);
    void (*Free)(void* ptr);

    // Logging
    void (*Log)(int level, const char* message);

    // Runtime queries
    MethodTable* (*GetMethodTable)(Object* obj);
    FieldDesc* (*GetFieldDesc)(MethodTable* mt, int index);

    // GC interaction
    void (*WriteBarrier)(Object** dst, Object* ref);
};
```

### Managed API

```csharp
namespace System.Runtime.DDS
{
    public static class DDSRuntime
    {
        // Load driver module
        public static DriverModule LoadModule(string path);

        // Create driver from module
        public static IStorageOps CreateStorageDriver(
            DriverModule module, string config = null);

        // Register as default for type
        public static void RegisterDriverForType<T>(OpsRoot ops);
    }

    public class DriverModule : IDisposable
    {
        public uint ABIVersion { get; }
        public DriverCapabilities Capabilities { get; }
        public void Dispose();
    }
}
```

---

## ABI Stability Requirements

### Version Checks

```cpp
// On module load
uint32_t moduleVersion = module->DDS_GetABIVersion();
if (moduleVersion != DDS_CURRENT_ABI_VERSION) {
    // Version mismatch - refuse to load or use compat layer
    return false;
}
```

### Interface Versioning

```cpp
// Each interface has version field
struct IStorageOps {
    uint32_t version;  // Must match expected

    // Methods...
    void* reserved[8];  // Future expansion without ABI break
};
```

### Compatibility Rules

1. **Additions to reserved slots** - Compatible (module ignores new methods)
2. **New interfaces** - Compatible (module returns null if not supported)
3. **Method signature changes** - Breaking (version bump required)
4. **Struct layout changes** - Breaking (version bump required)

---

## Module Lifetime Management

### Loading

```cpp
DriverModule* LoadDriverModule(const char* path)
{
    // Platform-specific load
    HMODULE handle = LoadLibraryW(path);

    // Get exports
    auto init = GetProcAddress(handle, "DDS_ModuleInit");
    auto getVersion = GetProcAddress(handle, "DDS_GetABIVersion");

    // Version check
    if (getVersion() != DDS_CURRENT_ABI_VERSION) {
        FreeLibrary(handle);
        return nullptr;
    }

    // Initialize
    DDSHostInterface host = CreateHostInterface();
    if (!init(&host)) {
        FreeLibrary(handle);
        return nullptr;
    }

    return new DriverModule(handle, ...);
}
```

### Unloading

```cpp
void UnloadDriverModule(DriverModule* module)
{
    // Ensure no active OpsRoots reference this module's drivers
    if (module->GetRefCount() > 0) {
        throw InvalidOperationException("Drivers still in use");
    }

    // Shutdown
    module->Shutdown();

    // Unload
    FreeLibrary(module->handle);
    delete module;
}
```

---

## Safety Considerations

### Reference Counting
- Track OpsRoots using each module's drivers
- Prevent unload while drivers in use

### Fault Isolation
- Driver crashes shouldn't crash runtime (where possible)
- Consider process isolation for untrusted drivers (future)

### Security
- Only load from trusted paths
- Signature verification (optional)
- Capability restrictions

---

## Implementation Tasks

1. [ ] Define stable ABI (interface structs, version scheme)
2. [ ] Implement DDSHostInterface
3. [ ] Implement module loading (LoadLibrary/dlopen)
4. [ ] Implement factory pattern for driver creation
5. [ ] Add reference counting for module lifetime
6. [ ] Create managed API (DDSRuntime)
7. [ ] Build sample external driver module
8. [ ] Add security checks (path validation, signatures)

---

## Prerequisites

Before implementing:
- [ ] Phase 1-3 complete (stable driver interfaces)
- [ ] OpsRoot ABI stable (no signature changes expected)
- [ ] Reserved slots populated with future methods

---

## References

- Phase 1 Doc: Part VI §6.1 (Driver loading = Static only)
- Native module loading: LoadLibrary (Win), dlopen (Linux)
- ABI versioning patterns: COM, Linux kernel modules
