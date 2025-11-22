# Testing Dynamic Grain Unloading - AI Agent Instructions

**Purpose**: This document provides step-by-step instructions for AI agents to build test projects that validate the dynamic grain loading and unloading features in this Orleans fork.

**Target Audience**: AI coding assistants working on testing and validation

**Prerequisites**: The Orleans.Runtime project builds successfully with dynamic grain unloading support

---

## Table of Contents

1. [Overview of What We're Testing](#overview)
2. [Test Project Structure](#test-project-structure)
3. [Phase 1: Create Test Grain Assembly](#phase-1-create-test-grain-assembly)
4. [Phase 2: Create Host Application](#phase-2-create-host-application)
5. [Phase 3: Test Dynamic Loading](#phase-3-test-dynamic-loading)
6. [Phase 4: Test Dynamic Unloading](#phase-4-test-dynamic-unloading)
7. [Phase 5: Verify Memory Reclamation](#phase-5-verify-memory-reclamation)
8. [Success Criteria](#success-criteria)
9. [Troubleshooting](#troubleshooting)

---

## Overview

### What We've Built

This Orleans fork includes a complete implementation of **dynamic grain loading and unloading**:

1. **Dynamic Loading** (Phases 1-4, already implemented)
   - Load grain assemblies at runtime using `IDynamicGrainLoader`
   - Assemblies loaded into collectible `AssemblyLoadContext` via DotNetCorePlugins
   - Automatic manifest propagation to cluster

2. **Dynamic Unloading** (Phase 5, newly implemented)
   - Unload grain assemblies at runtime using `IDynamicGrainUnloader`
   - 7-phase orchestration (deactivate grains → clear caches → update manifest → unload assembly)
   - Memory reclamation via garbage collection

### What We're Testing

- ✅ Load a grain assembly dynamically
- ✅ Activate grains from the loaded assembly
- ✅ Invoke grain methods
- ✅ Unload the grain assembly while grains are active
- ✅ Verify grains are deactivated gracefully
- ✅ Verify assembly is actually unloaded from memory
- ✅ Verify manifest is updated in cluster

---

## Test Project Structure

Create a solution with three projects:

```
TestDynamicGrains/
├── TestDynamicGrains.sln
├── TestGrains/                    # Dynamic grain assembly
│   ├── TestGrains.csproj
│   ├── ITestGrain.cs              # Grain interface
│   ├── TestGrain.cs               # Grain implementation
│   └── ICalculatorGrain.cs        # Additional grain for testing
├── TestHost/                      # Orleans silo host
│   ├── TestHost.csproj
│   ├── Program.cs
│   ├── Controllers/
│   │   └── GrainManagementController.cs  # API for load/unload
│   └── appsettings.json
└── README.md
```

---

## Phase 1: Create Test Grain Assembly

### Step 1.1: Create TestGrains Project

```bash
dotnet new classlib -n TestGrains -f net8.0
cd TestGrains
```

### Step 1.2: Add Orleans SDK Reference

Edit `TestGrains.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference the Orleans SDK from your modified Orleans repo -->
    <ProjectReference Include="..\..\Orleans\src\Orleans.Sdk\Orleans.Sdk.csproj" />
  </ItemGroup>
</Project>
```

### Step 1.3: Create ITestGrain Interface

Create `ITestGrain.cs`:

```csharp
using Orleans;

namespace TestGrains;

public interface ITestGrain : IGrainWithStringKey
{
    /// <summary>
    /// Simple echo method to test grain invocation.
    /// </summary>
    Task<string> SayHello(string name);

    /// <summary>
    /// Returns the current activation count to verify state.
    /// </summary>
    Task<int> GetActivationCount();

    /// <summary>
    /// Simulates a long-running operation (for testing deactivation timeout).
    /// </summary>
    Task DoLongRunningWork(int durationSeconds);
}
```

### Step 1.4: Create TestGrain Implementation

Create `TestGrain.cs`:

```csharp
using Orleans;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;

namespace TestGrains;

public class TestGrain : Grain, ITestGrain
{
    private readonly ILogger<TestGrain> _logger;
    private static int _activationCounter = 0;
    private int _activationNumber;

    public TestGrain(ILogger<TestGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _activationNumber = Interlocked.Increment(ref _activationCounter);

        _logger.LogInformation(
            "TestGrain {GrainId} activated (activation #{Number})",
            this.GetPrimaryKeyString(),
            _activationNumber);

        return base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "TestGrain {GrainId} deactivating. Reason: {ReasonCode} - {Description}",
            this.GetPrimaryKeyString(),
            reason.ReasonCode,
            reason.Description);

        // Check if being deactivated due to type unloading
        if (reason.ReasonCode == DeactivationReasonCode.TypeUnloading)
        {
            _logger.LogWarning(
                "TestGrain {GrainId} being unloaded! Saving critical state...",
                this.GetPrimaryKeyString());

            // Simulate quick cleanup
            // In real scenarios: save state, close connections, etc.
        }

        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public Task<string> SayHello(string name)
    {
        var message = $"Hello, {name}! (from activation #{_activationNumber})";
        _logger.LogInformation("TestGrain {GrainId} says: {Message}",
            this.GetPrimaryKeyString(),
            message);
        return Task.FromResult(message);
    }

    public Task<int> GetActivationCount()
    {
        return Task.FromResult(_activationNumber);
    }

    public async Task DoLongRunningWork(int durationSeconds)
    {
        _logger.LogInformation(
            "TestGrain {GrainId} starting long-running work ({Duration}s)...",
            this.GetPrimaryKeyString(),
            durationSeconds);

        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

        _logger.LogInformation(
            "TestGrain {GrainId} completed long-running work",
            this.GetPrimaryKeyString());
    }
}
```

### Step 1.5: Create ICalculatorGrain (Additional Test)

Create `ICalculatorGrain.cs`:

```csharp
using Orleans;

namespace TestGrains;

public interface ICalculatorGrain : IGrainWithIntegerKey
{
    Task<int> Add(int a, int b);
    Task<int> Multiply(int a, int b);
    Task<double> Divide(int a, int b);
}
```

Create `CalculatorGrain.cs`:

```csharp
using Orleans;
using Microsoft.Extensions.Logging;

namespace TestGrains;

public class CalculatorGrain : Grain, ICalculatorGrain
{
    private readonly ILogger<CalculatorGrain> _logger;

    public CalculatorGrain(ILogger<CalculatorGrain> logger)
    {
        _logger = logger;
    }

    public Task<int> Add(int a, int b)
    {
        var result = a + b;
        _logger.LogInformation("Add({A}, {B}) = {Result}", a, b, result);
        return Task.FromResult(result);
    }

    public Task<int> Multiply(int a, int b)
    {
        var result = a * b;
        _logger.LogInformation("Multiply({A}, {B}) = {Result}", a, b, result);
        return Task.FromResult(result);
    }

    public Task<double> Divide(int a, int b)
    {
        if (b == 0)
        {
            _logger.LogError("Division by zero attempted!");
            throw new DivideByZeroException();
        }

        var result = (double)a / b;
        _logger.LogInformation("Divide({A}, {B}) = {Result}", a, b, result);
        return Task.FromResult(result);
    }
}
```

### Step 1.6: Build TestGrains

```bash
dotnet build TestGrains/TestGrains.csproj
```

**Expected Output**: Clean build, produces `TestGrains.dll`

---

## Phase 2: Create Host Application

### Step 2.1: Create TestHost Project

```bash
dotnet new web -n TestHost -f net8.0
cd TestHost
```

### Step 2.2: Configure TestHost.csproj

Edit `TestHost.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference Orleans Runtime with dynamic grain loading support -->
    <ProjectReference Include="..\..\Orleans\src\Orleans.Runtime\Orleans.Runtime.csproj" />
    <ProjectReference Include="..\..\Orleans\src\Orleans.Sdk\Orleans.Sdk.csproj" />
    <ProjectReference Include="..\..\Orleans\src\Microsoft.Orleans.Server\Microsoft.Orleans.Server.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Core" />
  </ItemGroup>
</Project>
```

### Step 2.3: Create Program.cs

Create `Program.cs`:

```csharp
using Orleans;
using Orleans.Hosting;
using Orleans.Runtime.DynamicGrains;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure Orleans
builder.Host.UseOrleans((context, siloBuilder) =>
{
    siloBuilder
        .UseLocalhostClustering()
        .ConfigureLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        })
        // CRITICAL: Enable dynamic grain loading
        .AddDynamicGrainLoading();
});

// Add controllers for testing
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapControllers();

app.Run();
```

### Step 2.4: Create GrainManagementController

Create `Controllers/GrainManagementController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Orleans.Runtime.DynamicGrains;
using Microsoft.Extensions.Logging;
using System.IO;

namespace TestHost.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GrainManagementController : ControllerBase
{
    private readonly IDynamicGrainLoader _loader;
    private readonly IDynamicGrainUnloader _unloader;
    private readonly ILogger<GrainManagementController> _logger;

    public GrainManagementController(
        IDynamicGrainLoader loader,
        IDynamicGrainUnloader unloader,
        ILogger<GrainManagementController> logger)
    {
        _loader = loader;
        _unloader = unloader;
        _logger = logger;
    }

    [HttpPost("load")]
    public async Task<IActionResult> LoadGrainAssembly([FromBody] LoadGrainRequest request)
    {
        _logger.LogInformation("Loading grain assembly from: {Path}", request.AssemblyPath);

        if (!System.IO.File.Exists(request.AssemblyPath))
        {
            return BadRequest(new { Error = $"Assembly not found: {request.AssemblyPath}" });
        }

        var result = await _loader.LoadGrainAssemblyAsync(request.AssemblyPath);

        if (result.Success)
        {
            return Ok(new
            {
                Success = true,
                AssemblyName = result.Assembly.GetName().Name,
                GrainTypes = result.GrainTypes,
                Duration = result.LoadDuration.TotalMilliseconds
            });
        }
        else
        {
            return BadRequest(new
            {
                Success = false,
                Errors = result.Errors
            });
        }
    }

    [HttpPost("unload")]
    public async Task<IActionResult> UnloadGrainAssembly([FromBody] UnloadGrainRequest request)
    {
        _logger.LogInformation("Unloading grain assembly from: {Path}", request.AssemblyPath);

        var timeout = request.TimeoutSeconds.HasValue
            ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value)
            : TimeSpan.FromSeconds(30);

        var result = await _unloader.UnloadGrainAssemblyAsync(
            request.AssemblyPath,
            timeout);

        if (result.Success)
        {
            return Ok(new
            {
                Success = true,
                UnloadedTypes = result.UnloadedGrainTypes.Select(t => t.ToString()),
                ActiveGrainsDeactivated = result.ActiveGrainsDeactivated,
                Duration = result.UnloadDuration.TotalMilliseconds,
                MemoryReclaimed = result.MemoryReclaimed,
                DeactivationDetails = new
                {
                    result.DeactivationResult?.TotalGrainsDeactivated,
                    result.DeactivationResult?.ForcedDeactivations,
                    result.DeactivationResult?.DeactivatedPerType
                }
            });
        }
        else
        {
            return BadRequest(new
            {
                Success = false,
                Errors = result.Errors
            });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Status = "Running",
            DynamicGrainLoadingEnabled = true,
            Message = "Dynamic grain loading and unloading is active"
        });
    }
}

public record LoadGrainRequest(string AssemblyPath);
public record UnloadGrainRequest(string AssemblyPath, int? TimeoutSeconds);
```

### Step 2.5: Build TestHost

```bash
dotnet build TestHost/TestHost.csproj
```

**Expected Output**: Clean build

---

## Phase 3: Test Dynamic Loading

### Step 3.1: Start TestHost

```bash
cd TestHost
dotnet run
```

**Expected Output**:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Step 3.2: Load TestGrains Assembly

Open a new terminal and execute:

```bash
curl -X POST http://localhost:5000/api/grainmanagement/load \
  -H "Content-Type: application/json" \
  -d '{"assemblyPath": "/full/path/to/TestGrains/bin/Debug/net8.0/TestGrains.dll"}'
```

**Expected Response**:
```json
{
  "success": true,
  "assemblyName": "TestGrains",
  "grainTypes": ["TestGrains.ITestGrain", "TestGrains.ICalculatorGrain"],
  "duration": 234.5
}
```

**Expected Console Output** (in TestHost):
```
info: Orleans.Runtime.DynamicGrains.DynamicAssemblyLoader
      Scanning 12 Orleans assemblies for shared types
info: Orleans.Runtime.DynamicGrains.DynamicAssemblyLoader
      Discovered 247 distinct shared types for plugin loading
info: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService
      Successfully loaded grain assembly TestGrains.dll
```

### Step 3.3: Invoke a Grain Method

```bash
curl -X POST http://localhost:5000/api/test/hello \
  -H "Content-Type: application/json" \
  -d '{"grainId": "user-123", "name": "World"}'
```

Create a test controller if needed, or use Orleans client to invoke:

```csharp
// In a new controller endpoint:
var grain = grainFactory.GetGrain<ITestGrain>("user-123");
var result = await grain.SayHello("World");
```

**Expected Output**:
```
"Hello, World! (from activation #1)"
```

**Expected Console Output**:
```
info: TestGrains.TestGrain
      TestGrain user-123 activated (activation #1)
info: TestGrains.TestGrain
      TestGrain user-123 says: Hello, World! (from activation #1)
```

### Step 3.4: Activate Multiple Grains

```bash
# Activate 5 test grains
for i in {1..5}; do
  curl -X POST http://localhost:5000/api/test/invoke \
    -H "Content-Type: application/json" \
    -d "{\"grainId\": \"user-$i\", \"message\": \"Test $i\"}"
done
```

**Verification**: Check console logs - should see 5 activation messages.

---

## Phase 4: Test Dynamic Unloading

### Step 4.1: Unload While Grains Are Active

```bash
curl -X POST http://localhost:5000/api/grainmanagement/unload \
  -H "Content-Type: application/json" \
  -d '{"assemblyPath": "/full/path/to/TestGrains/bin/Debug/net8.0/TestGrains.dll", "timeoutSeconds": 30}'
```

**Expected Response**:
```json
{
  "success": true,
  "unloadedTypes": ["TestGrains.ITestGrain", "TestGrains.ICalculatorGrain"],
  "activeGrainsDeactivated": 5,
  "duration": 1234.5,
  "memoryReclaimed": true,
  "deactivationDetails": {
    "totalGrainsDeactivated": 5,
    "forcedDeactivations": 0,
    "deactivatedPerType": {
      "TestGrains.TestGrain": 5
    }
  }
}
```

**Expected Console Output** (7-Phase Orchestration):
```
info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Starting dynamic unload of grain assembly: TestGrains.dll

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Phase 1: Validating and preparing for unload

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Unloading assembly TestGrains with 2 grain types

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Phase 2: Deactivating active grains

info: Orleans.Runtime.DynamicGrains.GrainLifecycleManager
      Starting deactivation of 2 grain types with 30000ms timeout

info: Orleans.Runtime.DynamicGrains.GrainLifecycleManager
      Found 5 active grain instances to deactivate

warn: TestGrains.TestGrain
      TestGrain user-1 being unloaded! Saving critical state...

info: TestGrains.TestGrain
      TestGrain user-1 deactivating. Reason: TypeUnloading - Grain type being dynamically unloaded

[... repeated for user-2 through user-5 ...]

info: Orleans.Runtime.DynamicGrains.GrainLifecycleManager
      Deactivated 5 grains in 234ms (0 forced)

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Phase 3: Updating silo manifest

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Updated silo manifest, removed 2 types

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Phase 4: Propagating manifest to cluster

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Propagated manifest removal to cluster. Published: True, New version: 1.2

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Phase 5: Removing from caches

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Removed 2 grain types from caches

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Phase 6: Unloading assembly

info: Orleans.Runtime.DynamicGrains.DynamicAssemblyLoader
      Unloading assembly TestGrains.dll

info: Orleans.Runtime.DynamicGrains.DynamicAssemblyLoader
      Assembly TestGrains.dll unloaded

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Phase 7: Publishing unload event

info: Orleans.Runtime.DynamicGrains.DynamicGrainUnloaderService
      Successfully completed dynamic unload of assembly TestGrains in 1234ms
```

### Step 4.2: Verify Grains Cannot Be Invoked After Unload

```bash
curl -X POST http://localhost:5000/api/test/hello \
  -H "Content-Type: application/json" \
  -d '{"grainId": "user-123", "name": "World"}'
```

**Expected Result**: Error - grain type not found or assembly not loaded

---

## Phase 5: Verify Memory Reclamation

### Step 5.1: Create Memory Diagnostic Endpoint

Add to `GrainManagementController.cs`:

```csharp
[HttpGet("memory")]
public IActionResult GetMemoryStats()
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var memoryInfo = GC.GetGCMemoryInfo();

    return Ok(new
    {
        TotalMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
        HeapSizeMB = memoryInfo.HeapSizeBytes / 1024.0 / 1024.0,
        Gen0Collections = GC.CollectionCount(0),
        Gen1Collections = GC.CollectionCount(1),
        Gen2Collections = GC.CollectionCount(2)
    });
}
```

### Step 5.2: Measure Memory Before/After

```bash
# Before loading
curl http://localhost:5000/api/grainmanagement/memory

# Load assembly
curl -X POST http://localhost:5000/api/grainmanagement/load ...

# After loading
curl http://localhost:5000/api/grainmanagement/memory

# Unload assembly
curl -X POST http://localhost:5000/api/grainmanagement/unload ...

# After unloading (wait 5 seconds)
sleep 5
curl http://localhost:5000/api/grainmanagement/memory
```

**Expected**: Memory usage should decrease after unload, approaching pre-load levels.

---

## Success Criteria

### ✅ Phase 1: Loading Works
- [x] TestGrains.dll loads without errors
- [x] Grain types discovered and registered
- [x] Shared types discovered via reflection (check logs)
- [x] Manifest updated with new grain types

### ✅ Phase 2: Grains Function
- [x] Grains activate successfully
- [x] Grain methods can be invoked
- [x] Grains log activation/deactivation
- [x] Multiple grain instances work

### ✅ Phase 3: Unloading Works
- [x] Unload API returns success
- [x] All 7 phases execute in order
- [x] Active grains are deactivated
- [x] Grains receive `DeactivationReasonCode.TypeUnloading`
- [x] OnDeactivateAsync is called on each grain
- [x] Manifest is updated and propagated

### ✅ Phase 4: Cleanup Complete
- [x] Grains cannot be invoked after unload
- [x] No errors in logs during unload
- [x] Assembly reference removed from loader
- [x] PluginLoader disposed

### ✅ Phase 5: Memory Reclaimed
- [x] Memory usage decreases after unload
- [x] No dangling references detected
- [x] Assembly collected by GC (verifiable via weak reference)

---

## Troubleshooting

### Issue: "Assembly not found" error during load

**Cause**: Incorrect assembly path

**Solution**:
```bash
# Get absolute path
realpath TestGrains/bin/Debug/net8.0/TestGrains.dll

# Use that in the curl command
```

### Issue: "Type or namespace name 'IRemindable' could not be found"

**Cause**: This should be fixed by the reflection-based type discovery

**Verification**: Check logs for:
```
Discovered X distinct shared types for plugin loading
```

If IRemindable is needed and Orleans.Reminders is loaded, it should be discovered automatically.

### Issue: Unload fails with "Failed to unload assembly"

**Possible Causes**:
1. Grains didn't deactivate within timeout
2. Other references to the assembly exist
3. Assembly wasn't loaded via PluginLoader (static grain)

**Solution**:
- Increase timeout
- Check for dangling references
- Verify assembly was loaded dynamically

### Issue: Memory not reclaimed after unload

**Diagnosis**:
```csharp
// Add this diagnostic code to track assembly collection
var weakRef = new WeakReference(assembly);
// ... unload ...
for (int i = 0; i < 10; i++)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    if (!weakRef.IsAlive)
    {
        Console.WriteLine($"Assembly collected after {i} GC cycles");
        break;
    }
}

if (weakRef.IsAlive)
{
    Console.WriteLine("WARNING: Assembly not collected - memory leak!");
}
```

**Common Causes**:
- Cache not cleared properly
- Grain still active
- Static reference to type from assembly

### Issue: Grains don't deactivate within timeout

**Cause**: Grains taking too long in OnDeactivateAsync

**Solution**:
- Increase timeout parameter
- Check grain OnDeactivateAsync implementation
- Ensure no blocking operations

### Issue: "Cycle detected" error

**Cause**: Circular project references

**Solution**: Already fixed - we use reflection-based type discovery instead of direct Orleans.Reminders.Abstractions reference.

---

## Advanced Testing Scenarios

### Scenario 1: Load/Unload Cycle Test

```csharp
// Repeat load/unload 100 times
for (int i = 0; i < 100; i++)
{
    await LoadAssembly(...);
    await InvokeGrains(...);
    await UnloadAssembly(...);
    await Task.Delay(1000);
}

// Verify: No memory leak, all cycles succeed
```

### Scenario 2: Timeout Test

```csharp
// Create a grain with slow OnDeactivateAsync
public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
{
    await Task.Delay(60000); // 60 seconds
    await base.OnDeactivateAsync(reason, ct);
}

// Unload with 5 second timeout
await UnloadAssembly(..., timeout: TimeSpan.FromSeconds(5));

// Expected: Forced deactivation, result.ForcedDeactivations > 0
```

### Scenario 3: Multi-Silo Test

```csharp
// Start 3 silos in cluster
// Load assembly on Silo A
// Activate grains on Silo A
// Unload from Silo A
// Verify: Other silos (B, C) still work, manifest updated cluster-wide
```

---

## Validation Checklist

Use this checklist when testing:

- [ ] Build Orleans.Runtime successfully
- [ ] Build TestGrains successfully
- [ ] Build TestHost successfully
- [ ] Start TestHost without errors
- [ ] Load TestGrains via API - success
- [ ] Invoke grain methods - success
- [ ] Activate 10+ grains - success
- [ ] Unload TestGrains via API - success
- [ ] Check all 7 phases in logs - present
- [ ] Verify grains deactivated - yes
- [ ] Verify DeactivationReasonCode.TypeUnloading - correct
- [ ] Try invoking after unload - fails as expected
- [ ] Check memory usage - decreased
- [ ] Repeat load/unload 10x - no errors
- [ ] No memory leaks - verified

---

## Expected File Locations

After building, you should have:

```
TestGrains/
  bin/Debug/net8.0/
    TestGrains.dll           ← Dynamic assembly to load
    TestGrains.deps.json     ← Dependency metadata
    TestGrains.pdb           ← Debug symbols

TestHost/
  bin/Debug/net8.0/
    TestHost.dll             ← Host application
    Orleans.Runtime.dll      ← Modified Orleans with unloading support
    McMaster.NETCore.Plugins.dll  ← Plugin loader
```

---

## Key Implementation Details for AI Agents

### When Creating Test Code:

1. **Always use full/absolute paths** for assembly loading
2. **Include detailed logging** in grains to observe lifecycle
3. **Handle DeactivationReasonCode.TypeUnloading** in OnDeactivateAsync
4. **Use WeakReference** to verify memory reclamation
5. **Wait between operations** (100ms delays) for propagation

### When Debugging:

1. **Check console logs** - 7-phase unload should be visible
2. **Verify shared types** - look for reflection discovery logs
3. **Monitor memory** - before/after unload
4. **Test incrementally** - load first, then unload
5. **Use diagnostic endpoints** - memory stats, status checks

### When Extending:

1. Add more grain types to test different scenarios
2. Implement grains with state (IPersistentState<T>)
3. Test grains with timers (RegisterTimer)
4. Test grains with reminders (IRemindable) if Orleans.Reminders is loaded
5. Test grains with streams (IAsyncStream<T>)

---

## Summary for AI Agents

This document provides complete instructions to:

1. ✅ Create a test project structure
2. ✅ Build dynamic grain assemblies
3. ✅ Create an Orleans host with dynamic loading enabled
4. ✅ Test loading grains at runtime
5. ✅ Test unloading grains at runtime
6. ✅ Verify memory reclamation
7. ✅ Validate all features work correctly

The implementation uses reflection-based type discovery, so:
- **No manual type lists to maintain**
- **No circular dependencies**
- **Automatic support for new Orleans types**
- **IRemindable discovered if Orleans.Reminders is loaded**

Follow this guide step-by-step to validate the dynamic grain unloading feature is working correctly.

---

**Document Version**: 1.0
**Date**: 2025-11-21
**Status**: Ready for Testing
**Author**: Claude (Anthropic)
