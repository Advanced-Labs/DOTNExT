# T04: Voron Embedding

> **Work Package:** WP2.1
> **Dependencies:** None (parallel track)
> **Estimated Complexity:** Medium
> **Status:** Pending

---

## Objective

Decide and implement how Voron (RavenDB's storage engine) is hosted within or alongside the .NET runtime for Phase 2.

---

## Background

Voron is a managed (.NET) storage engine. Phase 2 needs to:
- Host Voron as a durable storage backend
- Allow runtime code to access Voron APIs
- Manage Voron lifecycle with runtime lifecycle

**Phase 2 Recommendation:** Option A - Embedded C# Voron inside runtime (fastest integration).

---

## Implementation Options

### Option A: Embedded Managed Voron (Recommended for Phase 2)

Voron runs as managed code within the same runtime:
- Runtime loads Voron.dll as a privileged assembly
- VKernel (managed API) directly uses Voron APIs
- Simplest integration, fastest to implement

**Pros:**
- Direct API access from managed code
- No IPC overhead
- Leverages existing Voron as-is

**Cons:**
- Voron and runtime share same process/GC
- Voron assemblies must be available at runtime

### Option B: Native Voron (Future)

Port Voron to native C++ or use P/Invoke:
- More isolation from managed heap
- More complex, not needed for Phase 2

---

## Implementation (Option A)

### 1. Voron Assembly Reference

**Structure:**
```
src/runtime/artifacts/bin/coreclr/.../
├── System.Private.CoreLib.dll
├── Voron.dll                    ← Voron storage engine
├── Sparrow.dll                  ← Voron dependency
└── ...
```

### 2. Voron Initialization Service

**File:** `System.Private.CoreLib/src/System/OS/Storage/VoronStorage.cs` (new)

```csharp
namespace System.OS.Storage
{
    using Voron;
    using Voron.Impl;

    /// <summary>
    /// Voron storage environment wrapper for VAYRON.
    /// Manages the lifecycle of the Voron storage backend.
    /// </summary>
    internal sealed class VoronStorage : IDisposable
    {
        private static VoronStorage? _instance;
        private static readonly object _lock = new();

        private readonly StorageEnvironment _env;
        private readonly string _dataPath;
        private bool _disposed;

        public static VoronStorage Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new VoronStorage(GetDefaultDataPath());
                    }
                }
                return _instance;
            }
        }

        private VoronStorage(string dataPath)
        {
            _dataPath = dataPath;
            Directory.CreateDirectory(_dataPath);

            var options = StorageEnvironmentOptions.ForPath(_dataPath);
            options.InitialFileSize = 64 * 1024 * 1024;  // 64MB
            options.MaxLogFileSize = 256 * 1024 * 1024;  // 256MB journal

            _env = new StorageEnvironment(options);
            InitializeTrees();
        }

        private void InitializeTrees()
        {
            // Create required trees on first startup
            using var tx = _env.WriteTransaction();
            tx.CreateTree("vobjects");     // Main VObject storage
            tx.CreateTree("typeIndex");    // Type -> VUIDs index
            tx.CreateTree("metadata");     // Runtime metadata
            tx.Commit();
        }

        public StorageEnvironment Environment => _env;

        public Transaction ReadTransaction() => _env.ReadTransaction();
        public Transaction WriteTransaction() => _env.WriteTransaction();

        private static string GetDefaultDataPath()
        {
            // Default: ./vayron-data/ in current directory
            // Can be configured via environment variable
            var path = Environment.GetEnvironmentVariable("VAYRON_DATA_PATH");
            return path ?? Path.Combine(AppContext.BaseDirectory, "vayron-data");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _env?.Dispose();
                _disposed = true;
            }
        }

        // Shutdown hook for runtime termination
        internal static void Shutdown()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }
    }
}
```

### 3. Runtime Initialization Hook

Ensure Voron is initialized early and shut down cleanly:

**File:** `System.Private.CoreLib/src/System/OS/VKernel.cs` (new)

```csharp
namespace System.OS
{
    /// <summary>
    /// VAYRON Kernel - main entry point for virtual object operations.
    /// </summary>
    public static class VKernel
    {
        private static bool _initialized;

        /// <summary>
        /// Initialize VAYRON subsystem.
        /// Called automatically on first use, or explicitly for control.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            // Ensure Voron storage is ready
            _ = Storage.VoronStorage.Instance;

            // Register shutdown hook
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();

            _initialized = true;
        }

        internal static void Shutdown()
        {
            if (!_initialized) return;

            // Flush pending changes
            FlushAll();

            // Shutdown storage
            Storage.VoronStorage.Shutdown();

            _initialized = false;
        }

        /// <summary>
        /// Flush all dirty virtual objects to storage.
        /// </summary>
        public static void FlushAll()
        {
            // Will be implemented in T07 (FieldAccess_Persist)
        }
    }
}
```

### 4. Voron Assembly Deployment

The build system needs to copy Voron assemblies to the output:

**File:** Build configuration (approach depends on build system)

For development/testing:
```powershell
# Copy Voron assemblies to Core_Root
$voronPath = "D:\Dev\DOTNExT\src\Raven\src\Voron\bin\Release\net9.0"
$coreRoot = "D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Debug\Tests\Core_Root"

Copy-Item "$voronPath\Voron.dll" $coreRoot
Copy-Item "$voronPath\Sparrow.dll" $coreRoot
Copy-Item "$voronPath\Sparrow.Server.dll" $coreRoot
```

### 5. Voron Build Integration

Ensure Voron is built as part of the overall solution:

**File:** `src/Raven/src/Voron/Voron.csproj` should target compatible framework

```xml
<PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Library</OutputType>
</PropertyGroup>
```

---

## Configuration

### Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `VAYRON_DATA_PATH` | Storage directory | `./vayron-data` |
| `VAYRON_INITIAL_SIZE` | Initial DB size | 64MB |
| `VAYRON_MAX_JOURNAL` | Max journal size | 256MB |

### Programmatic Configuration

```csharp
// Future: allow configuration before first access
VKernel.Configure(config => {
    config.DataPath = "/custom/path";
    config.InitialSize = 128 * 1024 * 1024;
});
VKernel.Initialize();
```

---

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `System/OS/Storage/VoronStorage.cs` | Create | Voron wrapper |
| `System/OS/VKernel.cs` | Create | Kernel entry point |
| `System.Private.CoreLib.csproj` | Modify | Reference Voron |
| Build scripts | Modify | Copy Voron to Core_Root |

---

## Acceptance Criteria

- [ ] VoronStorage singleton initializes correctly
- [ ] Voron.dll and dependencies copied to output
- [ ] VoronStorage.Instance.Environment is usable
- [ ] Read/Write transactions can be created
- [ ] Default trees (vobjects, typeIndex, metadata) exist
- [ ] Clean shutdown on process exit
- [ ] Data path configurable via environment variable

---

## Testing

```csharp
[Fact]
public void VoronStorage_InitializesSuccessfully()
{
    // Access the singleton
    var storage = VoronStorage.Instance;
    Assert.NotNull(storage);
    Assert.NotNull(storage.Environment);
}

[Fact]
public void VoronStorage_CanReadWrite()
{
    var storage = VoronStorage.Instance;

    // Write
    using (var tx = storage.WriteTransaction())
    {
        var tree = tx.CreateTree("test");
        tree.Add("key1", Encoding.UTF8.GetBytes("value1"));
        tx.Commit();
    }

    // Read
    using (var tx = storage.ReadTransaction())
    {
        var tree = tx.ReadTree("test");
        var result = tree.Read("key1");
        Assert.NotNull(result);
        Assert.Equal("value1", Encoding.UTF8.GetString(result.Reader.AsSpan()));
    }
}

[Fact]
public void VoronStorage_SurvivesRestart()
{
    var dataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    try
    {
        // Write and close
        {
            var options = StorageEnvironmentOptions.ForPath(dataPath);
            using var env = new StorageEnvironment(options);
            using var tx = env.WriteTransaction();
            var tree = tx.CreateTree("persist");
            tree.Add("key", Encoding.UTF8.GetBytes("survives"));
            tx.Commit();
        }

        // Reopen and read
        {
            var options = StorageEnvironmentOptions.ForPath(dataPath);
            using var env = new StorageEnvironment(options);
            using var tx = env.ReadTransaction();
            var tree = tx.ReadTree("persist");
            var result = tree.Read("key");
            Assert.Equal("survives", Encoding.UTF8.GetString(result.Reader.AsSpan()));
        }
    }
    finally
    {
        Directory.Delete(dataPath, true);
    }
}
```

---

## References

- Phase 2 Main Doc: Section 11.1 (WP2.1 Voron Embedding Strategy)
- Voron-Integration-Guide.md: Complete Voron API reference
- src/Raven/src/Voron/: Voron source code
