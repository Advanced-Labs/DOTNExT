# Dynamic Grain Loading - Testing Guide

**Version**: 1.0
**Date**: November 13, 2025
**Purpose**: Validate dynamic grain loading implementation in Orleans

---

## Overview

This guide provides step-by-step instructions for testing the dynamic grain loading feature. Two test applications are provided:

1. **Single-Silo Test**: Tests basic dynamic loading on a single Orleans silo
2. **Multi-Silo Test**: Tests cluster-wide manifest propagation and cross-silo communication

---

## Prerequisites

### System Requirements

- .NET 9.0 SDK or later
- Windows, Linux, or macOS
- 4GB RAM minimum (for multi-silo tests)
- Terminal/Command Prompt

### Build Orleans

First, ensure Orleans is built with the dynamic loading changes:

```bash
cd /home/user/Orleans
dotnet build Orleans.slnx
```

**Expected Output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Test 1: Single-Silo Dynamic Loading

### Purpose

Validates:
- Assembly loading and validation
- Manifest updates
- Serialization registration
- Cache invalidation
- Grain activation
- Method invocation

### Step 1: Build Test Grains

```bash
cd playground/DynamicGrainLoading.TestGrains
dotnet build
```

**Expected Output**:
```
Build succeeded.
```

**Verify**: Check that `bin/Debug/net9.0/DynamicGrainLoading.TestGrains.dll` exists

### Step 2: Build Test Application

```bash
cd ../DynamicGrainLoading.SingleSilo
dotnet build
```

**Expected Output**:
```
Build succeeded.
```

### Step 3: Run Test

```bash
dotnet run
```

### Expected Output

The test should progress through these phases:

#### Phase 1: Assembly Loading

```
═══════════════════════════════════════════════════════
  Test Phase 1: Load Test Grain Assembly
═══════════════════════════════════════════════════════

Found test grains assembly: /path/to/DynamicGrainLoading.TestGrains.dll

Loading assembly...
dbug: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Phase 1: Loading and validating assembly
info: Orleans.Runtime.DynamicGrains.DynamicAssemblyLoader[0]
      Loading grain assembly from /path/to/DynamicGrainLoading.TestGrains.dll
dbug: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Phase 2: Updating local silo manifest
info: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Updating silo manifest with 3 grain classes and 3 interfaces
info: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Successfully updated silo manifest with 3 new grain types
dbug: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Phase 3: Updating serialization system
info: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Registering X serializers and Y copiers
info: Orleans.Runtime.DynamicGrains.DynamicSerializationManager[0]
      Registering X serializers and Y copiers for dynamic grain types
dbug: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Phase 4: Invalidating caches
info: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Invalidated caches for 3 grain types
dbug: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Phase 5: Propagating manifest to cluster
info: Orleans.Runtime.Metadata.ClusterManifestProvider[0]
      Updated local grain manifest and propagated to cluster. New version: 0.1
dbug: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Phase 6: Publishing load event
info: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Successfully completed dynamic load of assembly DynamicGrainLoading.TestGrains in Xms with 3 grain types

✓ Assembly loaded successfully!
  Duration: ~100-500ms
  Grain types loaded: 3
  Manifest version: 0.1

Loaded grain types:
  - hellograin
  - countergrain
  - echograin

Assembly metadata:
  Grain classes: 3
    - DynamicGrainLoading.TestGrains.HelloGrain
    - DynamicGrainLoading.TestGrains.CounterGrain
    - DynamicGrainLoading.TestGrains.EchoGrain
  Grain interfaces: 3
    - DynamicGrainLoading.TestGrains.IHelloGrain
    - DynamicGrainLoading.TestGrains.ICounterGrain
    - DynamicGrainLoading.TestGrains.IEchoGrain
  Serializers: X
  Copiers: Y
  Proxies: 3
```

#### Phase 2: Grain Activation

```
═══════════════════════════════════════════════════════
  Test Phase 2: Activate and Use Dynamically Loaded Grains
═══════════════════════════════════════════════════════

Test 1: HelloGrain
------------------
info: DynamicGrainLoading.TestGrains.HelloGrain[0]
      HelloGrain test-user activated (Loaded Dynamically!)
info: DynamicGrainLoading.TestGrains.HelloGrain[0]
      HelloGrain saying: Hello, World! You are visitor #1. (Loaded dynamically at runtime!)
✓ Response: Hello, World! You are visitor #1. (Loaded dynamically at runtime!)
✓ Call count: 1

Test 2: CounterGrain
--------------------
info: DynamicGrainLoading.TestGrains.CounterGrain[0]
      CounterGrain 123 activated with count: 0
info: DynamicGrainLoading.TestGrains.CounterGrain[0]
      Counter incremented to 1
✓ Incremented counter
info: DynamicGrainLoading.TestGrains.CounterGrain[0]
      Counter incremented to 2
✓ Incremented counter again
✓ Current count: 2
info: DynamicGrainLoading.TestGrains.CounterGrain[0]
      Counter reset from 2 to 0
✓ Reset counter
✓ Count after reset: 0

Test 3: EchoGrain (Serialization Test)
---------------------------------------
info: DynamicGrainLoading.TestGrains.EchoGrain[0]
      Echoing message: Hello from dynamic grain!
✓ Simple echo: Echo: Hello from dynamic grain!
Sending complex data:
  Name: Test Data
  Value: 42
  Tags: dynamic, test, orleans
info: DynamicGrainLoading.TestGrains.EchoGrain[0]
      Echoing complex data: Name=Test Data, Value=42, TagCount=3
✓ Received complex echo:
  Name: Echo of Test Data
  Value: 84
  Timestamp: 2025-11-13T12:34:56.789Z
  Tags: dynamic, test, orleans, echoed
```

#### Success Summary

```
═══════════════════════════════════════════════════════
  ✓ ALL TESTS PASSED!
═══════════════════════════════════════════════════════

Summary:
  - Loaded 3 grain types dynamically
  - Activated and used 3 different grain types
  - Verified serialization of custom types
  - Load duration: ~100-500ms

Press any key to shut down...
```

### What to Check

✅ **Assembly Loading**:
- Duration < 2000ms
- No errors in Phase 1-6 logs
- Grain types count = 3
- Manifest version incremented

✅ **Grain Activation**:
- All 3 grain types activated successfully
- Logs show "activated" messages
- No serialization errors

✅ **Functionality**:
- HelloGrain returns correct message with call count
- CounterGrain increments and resets correctly
- EchoGrain serializes complex types successfully

---

## Test 2: Multi-Silo Cluster

### Purpose

Validates:
- Cluster-wide manifest propagation
- Multi-silo grain distribution
- Cross-silo communication
- Serialization across silos

### Prerequisites

**Note**: The multi-silo test uses ADO.NET clustering which requires SQL Server LocalDB. If you don't have LocalDB, you can:

**Option 1**: Install SQL Server LocalDB
```bash
# Windows
winget install Microsoft.SQLServer.2022.LocalDB

# Verify
sqllocaldb info
```

**Option 2**: Modify the test to use in-memory clustering (edit Program.cs):
```csharp
// Replace UseAdoNetClustering with:
.UseLocalhostClustering(
    siloPort: siloPort,
    gatewayPort: gatewayPort,
    primarySiloEndpoint: new IPEndPoint(IPAddress.Loopback, primarySiloPort))
```

### Step 1: Build Test Application

```bash
cd playground/DynamicGrainLoading.MultiSilo
dotnet build
```

### Step 2: Run Test

```bash
dotnet run
```

### Expected Output

#### Cluster Startup

```
═══════════════════════════════════════════════════════
  Orleans Dynamic Grain Loading - Multi-Silo Test
═══════════════════════════════════════════════════════

Building 3-silo cluster...

Starting Silo1...
✓ Silo1 started (port 11111, gateway 30000)
Starting Silo2...
✓ Silo2 started (port 11112, gateway 30001)
Starting Silo3...
✓ Silo3 started (port 11113, gateway 30002)
✓ All silos started successfully

Waiting for cluster to stabilize...
✓ Cluster stabilized
```

#### Phase 1: Load on Silo 1

```
═══════════════════════════════════════════════════════
  Test Phase 1: Load Assembly on Silo 1
═══════════════════════════════════════════════════════

Found test grains assembly: /path/to/DynamicGrainLoading.TestGrains.dll

Loading assembly on Silo 1...
[... similar Phase 1-6 logs as single-silo test ...]

✓ Assembly loaded successfully on Silo 1!
  Duration: ~100-500ms
  Grain types: 3
  Manifest version: 0.1

Loaded grain types on Silo 1:
  - hellograin
  - countergrain
  - echograin
```

#### Phase 2: Cluster Propagation

```
═══════════════════════════════════════════════════════
  Test Phase 2: Verify Cluster Propagation
═══════════════════════════════════════════════════════

Waiting for manifest propagation across cluster...

Cluster Manifest Status:
  Silo 1 manifest version: 0.1
  Silo 1 knows about 3 silos
  Silo 2 manifest version: 0.1
  Silo 2 knows about 3 silos
  Silo 3 manifest version: 0.1
  Silo 3 knows about 3 silos

✓ All silos have the same manifest version!
```

#### Phase 3: Cross-Silo Activation

```
═══════════════════════════════════════════════════════
  Test Phase 3: Activate Grains on Different Silos
═══════════════════════════════════════════════════════

Test 1: Activate HelloGrain (may be on any silo)
--------------------------------------------------
info: DynamicGrainLoading.TestGrains.HelloGrain[0]
      HelloGrain user1 activated (Loaded Dynamically!)
✓ HelloGrain user1: Hello, from grain 1! You are visitor #1. (Loaded dynamically at runtime!)
info: DynamicGrainLoading.TestGrains.HelloGrain[0]
      HelloGrain user2 activated (Loaded Dynamically!)
✓ HelloGrain user2: Hello, from grain 2! You are visitor #1. (Loaded dynamically at runtime!)
info: DynamicGrainLoading.TestGrains.HelloGrain[0]
      HelloGrain user3 activated (Loaded Dynamically!)
✓ HelloGrain user3: Hello, from grain 3! You are visitor #1. (Loaded dynamically at runtime!)

Test 2: Counter Grains
-----------------------
✓ Counter 1: 2
✓ Counter 2: 1
```

#### Success Summary

```
═══════════════════════════════════════════════════════
  ✓ ALL MULTI-SILO TESTS PASSED!
═══════════════════════════════════════════════════════

Summary:
  - 3-silo cluster running
  - Loaded 3 grain types on Silo 1
  - Manifests propagated across cluster
  - Activated grains on multiple silos
  - Cross-silo communication working

Press any key to shut down cluster...
```

### What to Check

✅ **Cluster Formation**:
- All 3 silos start successfully
- No clustering errors
- Cluster stabilizes within 3 seconds

✅ **Manifest Propagation**:
- All silos have same manifest version after loading
- Version increments correctly (0.0 → 0.1)
- All silos know about all other silos (count = 3)

✅ **Cross-Silo Communication**:
- Grains activate on different silos (check activation logs)
- Method calls succeed regardless of which silo hosts the grain
- Serialization works across silo boundaries

---

## Troubleshooting

### Issue: "Assembly is missing [ApplicationPart] attribute"

**Cause**: TestGrains project not compiled with Orleans.Sdk

**Solution**:
```bash
cd playground/DynamicGrainLoading.TestGrains
dotnet clean
dotnet build
# Verify Orleans.Sdk is in the .csproj file
```

### Issue: "Could not find DynamicGrainLoading.TestGrains.dll"

**Cause**: Test grains not built or in unexpected location

**Solution**:
```bash
cd playground/DynamicGrainLoading.TestGrains
dotnet build
ls bin/Debug/net9.0/  # Verify .dll exists
```

### Issue: Serialization errors during grain calls

**Symptoms**:
```
Error: Could not find serializer for type...
```

**Cause**: Phase 3 (serialization registration) failed

**Solution**:
1. Check Phase 3 logs - should show "Successfully registered serialization types"
2. Verify test grains have `[GenerateSerializer]` attribute
3. Rebuild test grains with Orleans.Sdk

### Issue: Manifest version mismatch in multi-silo test

**Symptoms**:
```
⚠ WARNING: Silos have different manifest versions
  Silo 1: 0.1
  Silo 2: 0.0
  Silo 3: 0.0
```

**Cause**: Manifest propagation delayed or failed

**Solution**:
1. Wait longer (increase delay from 2s to 5s)
2. Check cluster membership logs
3. Verify AsyncEnumerable<ClusterManifest> subscription
4. Check for network issues between silos

### Issue: Multi-silo test fails to start silos

**Symptoms**:
```
Error: Connection refused on port 11111
```

**Cause**: Ports already in use or clustering not configured

**Solution**:
```bash
# Check if ports are in use
netstat -an | grep 11111

# Kill any Orleans processes
pkill -f Orleans

# Try again
dotnet run
```

### Issue: SQL LocalDB not available

**Symptoms**:
```
Error: Cannot connect to (localdb)\MSSQLLocalDB
```

**Solution**:
Either install LocalDB or modify Program.cs to use localhost clustering (see Prerequisites above).

---

## Log Analysis

### Key Log Messages to Look For

**✅ SUCCESS Indicators**:
```
info: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Successfully completed dynamic load of assembly X in Yms with Z grain types

info: Orleans.Runtime.Metadata.ClusterManifestProvider[0]
      Updated local grain manifest and propagated to cluster. New version: X.Y

info: DynamicGrainLoading.TestGrains.HelloGrain[0]
      HelloGrain X activated (Loaded Dynamically!)
```

**❌ ERROR Indicators**:
```
warn: Orleans.Runtime.DynamicGrains.DynamicSerializationManager[0]
      Could not find ConsumeMetadata method on CodecProvider

warn: Orleans.Runtime.Metadata.ClusterManifestProvider[0]
      Failed to publish updated local grain manifest

fail: Orleans.Runtime.DynamicGrains.DynamicGrainLoaderService[0]
      Failed to load assembly X: Y
```

### Enabling Verbose Logging

Edit `appsettings.json` (or modify in code):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Orleans.Runtime.DynamicGrains": "Trace",
      "Orleans.Runtime.Metadata": "Debug",
      "Orleans.Serialization": "Debug"
    }
  }
}
```

---

## Performance Benchmarks

### Single-Silo Load Times

**Expected ranges** (development machine):
- Small assembly (1-5 grains): 50-150ms
- Medium assembly (10-50 grains): 150-500ms
- Large assembly (100+ grains): 500-2000ms

### Multi-Silo Propagation Times

**Expected ranges**:
- Manifest propagation: 100-500ms
- Cluster stabilization: 1-3 seconds
- Cross-silo activation: Same as single-silo

### What Impacts Performance

- **Assembly size**: Larger = slower
- **Number of grain types**: More = slower
- **Generated code size**: More serializers = slower Phase 3
- **Network latency**: Affects multi-silo propagation
- **Disk I/O**: Affects assembly loading

---

## Copying Logs for Issues

If you encounter issues, copy the full log output:

### Windows (PowerShell)
```powershell
dotnet run > test-output.txt 2>&1
# Send test-output.txt
```

### Linux/macOS
```bash
dotnet run > test-output.txt 2>&1
# Send test-output.txt
```

### What to Include

1. **Full console output** from start to error
2. **Test application** (single-silo or multi-silo)
3. **.NET version**: `dotnet --version`
4. **OS and version**: `uname -a` (Linux/macOS) or `winver` (Windows)
5. **Test grains .dll info**: `ls -lh path/to/TestGrains.dll`

---

## Expected Test Duration

- **Single-Silo Test**: 30-60 seconds (including user input)
- **Multi-Silo Test**: 60-120 seconds (including startup and user input)
- **Building All Projects**: 30-60 seconds

---

## Success Criteria

### Single-Silo Test
- ✅ All 6 phases complete without errors
- ✅ 3 grain types loaded
- ✅ All 3 grain types activated and called
- ✅ Complex type serialization works
- ✅ Load duration < 2000ms

### Multi-Silo Test
- ✅ 3 silos start successfully
- ✅ Assembly loads on Silo 1
- ✅ Manifest propagates to all silos
- ✅ All silos have same manifest version
- ✅ Grains activate on different silos
- ✅ Cross-silo calls succeed

---

## Next Steps After Successful Testing

1. **Report Results**: Share test output showing success
2. **Performance Data**: Note load times and any delays
3. **Issues Found**: Report any warnings or unexpected behavior
4. **Feature Requests**: Suggest improvements or additional tests

---

## Additional Test Ideas

### Manual Testing

1. **Load Multiple Assemblies**: Call `LoadGrainAssemblyAsync()` multiple times with different assemblies
2. **Concurrent Loading**: Try loading from multiple threads (should serialize via SemaphoreSlim)
3. **Large Assembly**: Create an assembly with 100+ grain types
4. **Hot Reload Simulation**: Load assembly, use grains, load another assembly, verify both work

### Integration Tests

1. **With Persistence**: Add grain state and verify dynamic grains can persist
2. **With Reminders**: Add reminders to dynamic grains
3. **With Streams**: Use streams with dynamic grains
4. **With Transactions**: Use ACID transactions with dynamic grains

---

## Support

For issues or questions:
- Check troubleshooting section above
- Review implementation docs: `DYNAMIC_GRAIN_LOADING_IMPLEMENTATION.md`
- Include full logs when reporting issues
- Note Orleans version, .NET version, and OS

---

**End of Testing Guide**

Last Updated: November 13, 2025
