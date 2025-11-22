# Distributed Grain Directory - Comprehensive Documentation

## Table of Contents
1. [Overview](#overview)
2. [What It Is](#what-it-is)
3. [What It Enables](#what-it-enables)
4. [Architecture](#architecture)
5. [Core Components](#core-components)
6. [How It Works](#how-it-works)
7. [Configuration and Usage](#configuration-and-usage)
8. [Extending and Modifying](#extending-and-modifying)
9. [Key Implementation Details](#key-implementation-details)
10. [Testing and Diagnostics](#testing-and-diagnostics)

---

## Overview

The **Experimental Distributed Grain Directory** (`DistributedGrainDirectory`) is an in-memory, partitioned, distributed grain directory implementation for Microsoft Orleans. It is marked as experimental (`ORLEANSEXP003`) and represents a fundamental reimagining of how Orleans tracks grain activations across a cluster.

**Status**: Experimental - Subject to change or removal in future updates.

**Location**: `src/Orleans.Runtime/GrainDirectory/`

---

## What It Is

The Distributed Grain Directory is a **fully distributed, partitioned key-value store** where:
- **Key**: `GrainId` (the unique identifier for a grain)
- **Value**: `GrainAddress` (the registration entry pointing to the active silo hosting the grain)

### Key Characteristics

1. **Distributed Partitioning**: Uses a consistent hash ring with multiple virtual nodes (partitions) per silo
2. **No Single Point of Failure**: Directory data is distributed across all active silos
3. **Automatic Rebalancing**: Partitions automatically redistribute when silos join or leave
4. **Crash Recovery**: Built-in recovery mechanisms handle silo failures gracefully
5. **Virtual Synchrony**: Uses a two-phase protocol for membership changes

---

## What It Enables

### Capabilities Previously Not Possible

1. **True Scalability**: Unlike traditional directory implementations that may have bottlenecks, the distributed directory scales linearly with cluster size

2. **Elimination of External Dependencies**: No need for external storage systems (Redis, Azure Table Storage, ADO.NET) for the default grain directory

3. **Better Fault Tolerance**: Directory data is automatically replicated and redistributed, surviving arbitrary silo failures

4. **Reduced Latency**: Directory lookups and registrations are distributed across the cluster, reducing hot spots

5. **Automatic Load Distribution**: Each silo owns approximately equal portions of the directory hash space

### What You Can Do With It

1. **Run Large Clusters Without External Storage**: Deploy Orleans clusters without configuring external directory storage

2. **Handle Dynamic Cluster Topology**: Silos can join and leave the cluster without manual directory redistribution

3. **Optimize for In-Memory Performance**: All directory operations execute entirely in-memory with no external I/O

4. **Build Resilient Systems**: The directory survives multiple concurrent silo failures through built-in recovery

---

## Architecture

### High-Level Design

The distributed grain directory follows a **multi-tier partitioned architecture**:

```
┌─────────────────────────────────────────────────────────────┐
│                    Orleans Cluster                          │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │   Silo A     │  │   Silo B     │  │   Silo C     │    │
│  │              │  │              │  │              │    │
│  │ ┌──────────┐ │  │ ┌──────────┐ │  │ ┌──────────┐ │    │
│  │ │DistGrain │ │  │ │DistGrain │ │  │ │DistGrain │ │    │
│  │ │Directory │ │  │ │Directory │ │  │ │Directory │ │    │
│  │ └────┬─────┘ │  │ └────┬─────┘ │  │ └────┬─────┘ │    │
│  │      │       │  │      │       │  │      │       │    │
│  │ ┌────▼─────┐ │  │ ┌────▼─────┐ │  │ ┌────▼─────┐ │    │
│  │ │Partition │ │  │ │Partition │ │  │ │Partition │ │    │
│  │ │  0..29   │ │  │ │  0..29   │ │  │ │  0..29   │ │    │
│  │ └──────────┘ │  │ └──────────┘ │  │ └──────────┘ │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
│                                                             │
│              Consistent Hash Ring (0 to 2^32-1)            │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  Each silo has 30 virtual nodes (partitions) on the ring   │
└─────────────────────────────────────────────────────────────┘
```

### Consistent Hashing Strategy

Based on Amazon Dynamo and Apache Cassandra's approach:
- Each silo owns **30 virtual nodes (partitions)** by default (`PartitionsPerSilo = 30`)
- Partitions are distributed around a hash ring (0 to 2³²-1)
- Grain IDs are hashed to determine which partition owns them
- Multiple partitions per silo ensure even distribution

### Theoretical Foundation

The directory follows two established distributed systems methodologies:

1. **Virtual Synchrony** ([Microsoft Research Paper](https://www.microsoft.com/en-us/research/publication/virtually-synchronous-methodology-for-dynamic-service-replication/))
   - Two-phase operation: normal operation and view change
   - Processes operate independently during normal operation
   - Coordinate during membership changes (view changes)

2. **Vertical Paxos Similarities** ([Microsoft Research Paper](https://www.microsoft.com/en-us/research/publication/vertical-paxos-and-primary-backup-replication/))
   - State and control transfer between views
   - Fixed set of processes handle requests without failures in normal phase
   - View change phase handles membership changes

---

## Core Components

### 1. DistributedGrainDirectory

**File**: `src/Orleans.Runtime/GrainDirectory/DistributedGrainDirectory.cs`

The main coordinator that:
- Implements `IGrainDirectory` interface
- Manages 30 `GrainDirectoryPartition` instances per silo
- Routes directory operations to the correct partition
- Handles membership updates from `DirectoryMembershipService`
- Coordinates recovery operations

**Key Methods**:
- `Lookup(GrainId)`: Find the silo hosting a grain
- `Register(GrainAddress)`: Register a grain activation
- `Unregister(GrainAddress)`: Remove a grain registration
- `RecoverRegisteredActivations()`: Recover directory state after failures

### 2. GrainDirectoryPartition

**Files**:
- `src/Orleans.Runtime/GrainDirectory/GrainDirectoryPartition.cs`
- `src/Orleans.Runtime/GrainDirectory/GrainDirectoryPartition.Interface.cs`

Individual partition that:
- Stores a `Dictionary<GrainId, GrainAddress>` for its hash range
- Implements the partition protocol interface (`IGrainDirectoryPartition`)
- Manages range locks during view changes
- Handles snapshot transfers to/from other partitions
- Executes recovery when necessary

**Core Operations**:
- `RegisterAsync()`: Register a grain within the partition's range
- `LookupAsync()`: Lookup a grain within the partition's range
- `DeregisterAsync()`: Remove a grain registration
- `GetSnapshotAsync()`: Create snapshot for range transfer
- `AcknowledgeSnapshotTransferAsync()`: Acknowledge successful transfer

### 3. DirectoryMembershipService

**File**: `src/Orleans.Runtime/GrainDirectory/DirectoryMembershipService.cs`

Bridges cluster membership and directory partitioning:
- Subscribes to `ClusterMembershipService` updates
- Converts cluster membership to `DirectoryMembershipSnapshot`
- Publishes directory-specific membership views
- Manages membership version progression

### 4. DirectoryMembershipSnapshot

**File**: `src/Orleans.Runtime/GrainDirectory/DirectoryMembershipSnapshot.cs`

Immutable snapshot of directory membership:
- Maps hash ring positions to silos and partitions
- Calculates which partition owns which hash ranges
- Provides fast lookup: `TryGetOwner(GrainId)` → partition reference
- Manages ring boundaries and partition ranges
- Each silo owns multiple `RingRange` instances

**Key Data Structures**:
```csharp
// Ring boundaries: sorted list of (hash, memberIndex, partitionIndex)
ImmutableArray<(uint Start, int MemberIndex, int PartitionIndex)> _ringBoundaries

// Partition references organized by member
ImmutableArray<ImmutableArray<IGrainDirectoryPartition>> _partitionsByMember

// Ranges owned by each member-partition combination
ImmutableArray<ImmutableArray<RingRange>> _rangesByMemberPartition
```

### 5. RingRange

**File**: `src/Orleans.Runtime/GrainDirectory/RingRange.cs`

Represents a contiguous range on the hash ring:
- Start and End points (uint values from 0 to 2³²-1)
- Can wrap around (e.g., [2³²-100, 100])
- Supports set operations: intersection, difference, complement
- Efficient containment checks for grain IDs

**Special Cases**:
- **Empty Range**: Start = End = 0
- **Full Range**: Start = End = 1 (special value)
- **Wrapped Range**: Start > End (includes uint.MaxValue)

### 6. RingRangeCollection

**File**: `src/Orleans.Runtime/GrainDirectory/RingRangeCollection.cs`

Immutable collection of non-overlapping ranges:
- Sorted by start position
- Used to represent all ranges owned by a silo
- Supports set operations across multiple ranges
- Calculates total size as percentage of full ring

---

## How It Works

### Normal Operation

#### 1. Grain Registration

When a grain activates:

```
┌─────────┐                ┌──────────────────┐                ┌──────────────┐
│  Grain  │                │ DistributedGrain │                │  Partition   │
│Activates│                │    Directory     │                │  (Owner)     │
└────┬────┘                └────────┬─────────┘                └──────┬───────┘
     │                              │                                  │
     │  Register(GrainAddress)      │                                  │
     │─────────────────────────────>│                                  │
     │                              │                                  │
     │                              │  1. Hash GrainId                 │
     │                              │  2. Find owner partition         │
     │                              │                                  │
     │                              │  RegisterAsync(version, address) │
     │                              │─────────────────────────────────>│
     │                              │                                  │
     │                              │                                  │  3. Check ownership
     │                              │                                  │  4. Store in local dict
     │                              │                                  │
     │                              │  DirectoryResult<GrainAddress>   │
     │                              │<─────────────────────────────────│
     │  GrainAddress (registered)   │                                  │
     │<─────────────────────────────│                                  │
```

**Steps**:
1. Hash the `GrainId` to a uint (0 to 2³²-1)
2. Binary search in the ring boundaries to find the owning partition
3. Send `RegisterAsync()` RPC to the partition
4. Partition verifies it owns the range and stores the address
5. Return the effectively registered address (may differ if already registered)

#### 2. Grain Lookup

```
Client/Caller                DistributedGrainDirectory         Partition
     │                              │                              │
     │  Lookup(GrainId)             │                              │
     │─────────────────────────────>│                              │
     │                              │                              │
     │                              │  1. Hash GrainId             │
     │                              │  2. Find owner               │
     │                              │                              │
     │                              │  LookupAsync(version, id)    │
     │                              │─────────────────────────────>│
     │                              │                              │
     │                              │                              │  3. Dictionary lookup
     │                              │                              │
     │                              │  DirectoryResult<Address?>   │
     │                              │<─────────────────────────────│
     │  GrainAddress or null        │                              │
     │<─────────────────────────────│                              │
```

#### 3. Grain Deregistration

When a grain deactivates, it unregisters from the directory:
1. Hash the `GrainId` to find the owner
2. Send `DeregisterAsync()` to the partition
3. Partition removes the entry if it matches or the silo is dead

### View Changes (Membership Changes)

When cluster membership changes (silo joins or leaves), a **view change** occurs:

#### Phase 1: Detect Membership Change

```
ClusterMembershipService ──> DirectoryMembershipService ──> DistributedGrainDirectory
        │                           │                               │
        │  New membership view      │                               │
        │──────────────────────────>│                               │
        │                           │                               │
        │                           │  Create DirectoryMembership   │
        │                           │  Snapshot                     │
        │                           │                               │
        │                           │  Publish new snapshot         │
        │                           │──────────────────────────────>│
        │                           │                               │
        │                           │                               │  Distribute to all
        │                           │                               │  partitions
```

#### Phase 2: Range Transfer (Partition Shrinking)

When a partition must **release a range** (e.g., new silo joins):

```
Partition (Previous Owner)              Partition (New Owner)
        │                                       │
        │  1. Lock the range being released     │
        │     (using range lock)                │
        │                                       │
        │  2. Create snapshot of entries in     │
        │     the released range                │
        │                                       │
        │  3. Remove entries from local dict    │
        │                                       │
        │  4. Store snapshot for retrieval      │
        │                                       │
        │                                       │  5. Lock the range being acquired
        │                                       │
        │  GetSnapshotAsync(version, range)     │
        │<──────────────────────────────────────│
        │                                       │
        │  GrainDirectoryPartitionSnapshot      │
        │──────────────────────────────────────>│
        │                                       │
        │                                       │  6. Apply snapshot to local dict
        │                                       │
        │  AcknowledgeSnapshotTransferAsync()   │
        │<──────────────────────────────────────│
        │                                       │
        │  7. Delete snapshot                   │
        │                                       │  8. Unlock range
        │  8. Unlock range                      │
```

#### Phase 3: Recovery (Non-Contiguous View Change or Failure)

When view changes are **non-contiguous** (skipped versions) or a silo crashes:

```
Partition (New Owner)          All Active Silos in Cluster
        │                              │
        │  1. Detect non-contiguous    │
        │     or missing snapshot      │
        │                              │
        │  2. Initiate recovery        │
        │                              │
        │  RecoverRegisteredActivations(version, range, siloAddr, partitionIdx)
        │─────────────────────────────>│
        │                              │
        │                              │  3. Scan local ActivationDirectory
        │                              │  4. Deactivate unregistered grains
        │                              │  5. Return matching activations
        │                              │
        │  List<GrainAddress>          │
        │<─────────────────────────────│
        │                              │
        │  6. Merge all responses      │
        │  7. Store in local dict      │
        │  8. Unlock range             │
```

**Recovery Process**:
1. New owner requests all silos for activations in the recovered range
2. Each silo scans its `ActivationDirectory` for matching grains
3. Unregistered or in-doubt activations are deactivated
4. All valid activations are returned to the recovering partition
5. Partition merges results and unlocks the range

### Range Locks (Wedges)

Range locks prevent invalid access during view changes:

```csharp
// Lock structure
(RingRange Range, MembershipVersion Version, TaskCompletionSource Completion)
```

**Behavior**:
- Requests for locked ranges **wait** until the lock is released
- Locks are versioned to handle overlapping view changes
- Released when view change completes (snapshot transferred or recovery done)
- Analogous to "wedges" in Virtual Synchrony methodology

### Version Management

All operations include a `MembershipVersion`:

```csharp
// Request
ValueTask<DirectoryResult<T>> OperationAsync(MembershipVersion version, ...)

// Response
DirectoryResult<T> {
    T Value;
    MembershipVersion Version;
}
```

**Version Handling**:
- If **partition's version < request version**: Refresh membership and retry
- If **partition's version > request version**: Return `RefreshRequired` result
- If **response version > caller's version**: Refresh and retry
- Ensures all participants eventually see all membership changes

---

## Configuration and Usage

### Enabling the Distributed Grain Directory

```csharp
using Orleans.Hosting;

var siloBuilder = new HostBuilder()
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder.UseLocalhostClustering();

        #pragma warning disable ORLEANSEXP003
        siloBuilder.AddDistributedGrainDirectory();
        #pragma warning restore ORLEANSEXP003
    });
```

### Registering as a Named Directory

```csharp
#pragma warning disable ORLEANSEXP003
siloBuilder.AddDistributedGrainDirectory("MyDistributedDirectory");
#pragma warning restore ORLEANSEXP003
```

### Using with Specific Grain Types

Mark grains to use the distributed directory:

```csharp
using Orleans.GrainDirectory;

[GrainDirectory(GrainDirectoryAttribute.DEFAULT_GRAIN_DIRECTORY)]
public class MyGrain : Grain, IMyGrain
{
    // ...
}

// Or use a named directory
[GrainDirectory("MyDistributedDirectory")]
public class MyOtherGrain : Grain, IMyOtherGrain
{
    // ...
}
```

### Default Configuration

- **Partitions per silo**: 30 (hardcoded via `ConsistentRingOptions.DEFAULT_NUM_VIRTUAL_RING_BUCKETS`)
- **No external configuration needed**: Works out of the box with cluster membership

---

## Extending and Modifying

### Key Extension Points

#### 1. Custom Partition Count

The partition count is currently hardcoded:

```csharp
// src/Orleans.Runtime/GrainDirectory/DirectoryMembershipSnapshot.cs
internal const int PartitionsPerSilo = ConsistentRingOptions.DEFAULT_NUM_VIRTUAL_RING_BUCKETS;
```

**To modify**:
- Change `ConsistentRingOptions.DEFAULT_NUM_VIRTUAL_RING_BUCKETS`
- Consider trade-offs: more partitions = better distribution, more coordination overhead

#### 2. Hash Function Customization

Hash calculation for grain IDs:

```csharp
// Currently uses GrainId.GetUniformHashCode()
public bool TryGetOwner(GrainId grainId, out SiloAddress owner, out IGrainDirectoryPartition partition)
    => TryGetOwner(grainId.GetUniformHashCode(), out owner, out partition);
```

**To customize**:
- Implement custom hash in `DirectoryMembershipSnapshot` constructor
- The constructor accepts `Func<SiloAddress, int, uint[]> getRingBoundaries`

#### 3. Recovery Strategy Customization

Current recovery is exhaustive (asks all silos):

```csharp
// src/Orleans.Runtime/GrainDirectory/GrainDirectoryPartition.cs:564
private async Task RecoverPartitionRange(DirectoryMembershipSnapshot current, RingRange addedRange)
{
    // Queries all active silos
    foreach (var member in clusterMembershipSnapshot.Members.Values)
    {
        if (member.Status is (SiloStatus.Active or SiloStatus.Joining or SiloStatus.ShuttingDown))
        {
            tasks.Add(GetRegisteredActivationsFromClusterMember(...));
        }
    }
}
```

**To optimize**:
- Implement selective recovery (only query likely owners)
- Add heuristics based on previous membership views
- Implement persistent recovery hints

#### 4. Snapshot Transfer Protocol

Currently uses **pull-based** snapshot transfer:

```csharp
// New owner pulls snapshot from previous owner
var snapshot = await partition.GetSnapshotAsync(current.Version, previousVersion, addedRange);
```

**To modify**:
- Implement push-based transfer (previous owner pushes snapshots)
- Add compression for large snapshots
- Implement incremental transfers

#### 5. Metrics and Instrumentation

Add custom metrics via `DirectoryInstruments`:

```csharp
// src/Orleans.Runtime/GrainDirectory/GrainDirectoryPartition.cs
DirectoryInstruments.SnapshotTransferCount.Add(1);
DirectoryInstruments.SnapshotTransferDuration.Record((long)stopwatch.Elapsed.TotalMilliseconds);
DirectoryInstruments.RangeRecoveryCount.Add(1);
DirectoryInstruments.RangeRecoveryDuration.Record((long)stopwatch.Elapsed.TotalMilliseconds);
DirectoryInstruments.RangeLockHeldDuration.Record((long)heldDuration.TotalMilliseconds);
```

### Adding New Features

#### Example: Persistent Snapshots

To add persistent snapshot storage:

1. **Create a snapshot storage interface**:
```csharp
public interface IPartitionSnapshotStore
{
    Task StoreSnapshotAsync(MembershipVersion version, RingRange range, List<GrainAddress> addresses);
    Task<List<GrainAddress>?> RetrieveSnapshotAsync(MembershipVersion version, RingRange range);
}
```

2. **Modify `GrainDirectoryPartition.ReleaseRangeAsync()`**:
```csharp
// After creating snapshot
if (transferPartners.Count > 0)
{
    _partitionSnapshots.Add(new PartitionSnapshotState(...));

    // NEW: Persist snapshot
    await _snapshotStore.StoreSnapshotAsync(previous.Version, removedRange, removedAddresses);
}
```

3. **Modify `GrainDirectoryPartition.AcquireRangeAsync()`**:
```csharp
// Before recovery
var persistedSnapshot = await _snapshotStore.RetrieveSnapshotAsync(previousVersion, addedRange);
if (persistedSnapshot != null)
{
    // Use persisted snapshot instead of recovery
}
```

#### Example: Custom Replication Factor

To add configurable replication:

1. **Define configuration**:
```csharp
public class DistributedGrainDirectoryOptions
{
    public int ReplicationFactor { get; set; } = 1;
}
```

2. **Modify `DirectoryMembershipSnapshot`** to calculate replica owners
3. **Update `RegisterAsync` to write to all replicas**
4. **Implement read-repair in `LookupAsync`**

---

## Key Implementation Details

### Thread Safety and Concurrency

#### Partition Execution Model

Partitions are `SystemTarget` instances with single-threaded execution:

```csharp
internal sealed partial class GrainDirectoryPartition : SystemTarget
{
    // All operations run on the SystemTarget's work item group
    // No concurrent access to _directory
    private readonly Dictionary<GrainId, GrainAddress> _directory = [];
}
```

**Implications**:
- No locks needed within a partition
- Operations are serialized by the Orleans scheduler
- Thread-safe across different partitions (different SystemTargets)

#### Range Locks

Managed synchronously within the partition context:

```csharp
// Check for intersecting locks
foreach (var rangeLock in _rangeLocks)
{
    if (rangeLock.Version <= version && range.Intersects(rangeLock.Range))
    {
        completion = rangeLock.Completion.Task;
        return true; // Wait required
    }
}
```

### Memory Management

#### Per-Partition Storage

Each partition maintains its own dictionary:
```csharp
private readonly Dictionary<GrainId, GrainAddress> _directory = [];
```

**Memory usage**: O(number of grains / number of partitions)

With 30 partitions per silo and even distribution:
- 1M grains across 10 silos → ~3,333 grains per partition
- 10M grains across 100 silos → ~3,333 grains per partition

#### Snapshot Storage

Temporary snapshots during view changes:

```csharp
private readonly List<PartitionSnapshotState> _partitionSnapshots = [];

record PartitionSnapshotState(
    MembershipVersion DirectoryMembershipVersion,
    List<GrainAddress> GrainAddresses,
    HashSet<(SiloAddress, int)> TransferPartners
);
```

**Lifecycle**: Created during range release, deleted after transfer acknowledgment

### Performance Characteristics

#### Lookup Complexity

1. **Hash computation**: O(1) - `GrainId.GetUniformHashCode()`
2. **Ring search**: O(log N) - Binary search in ring boundaries (N = active silos × 30)
3. **RPC to partition**: O(1) network hop
4. **Dictionary lookup**: O(1) average

**Total**: O(log N) where N is the number of partitions in the cluster

#### Registration Complexity

Same as lookup: O(log N) + O(1) dictionary insert

#### View Change Complexity

For a single partition:
- **Range release**: O(M) where M = grains in the released range
- **Range acquisition**: O(M) for snapshot transfer
- **Recovery**: O(S × A) where S = number of silos, A = activations per silo in range

### Failure Handling

#### Silo Crash Scenarios

**Scenario 1: Silo crashes before snapshot transfer**
- New owner cannot retrieve snapshot
- Falls back to recovery protocol
- Queries all active silos for activations in the range

**Scenario 2: Silo crashes after snapshot creation but before transfer**
- Snapshot is lost (in-memory only)
- New owner times out and initiates recovery

**Scenario 3: Silo crashes during recovery**
- Recovery is idempotent
- Subsequent owner re-initiates recovery
- In-doubt activations are deactivated

#### Race Condition Prevention

**Registration vs. Recovery Race**:

```csharp
// From DistributedGrainDirectory.cs:71-78
// The recovery membership value is used to avoid a race between concurrent registration
// & recovery operations which could lead to lost registrations.
private long _recoveryMembershipVersion;
```

**Protection mechanism**:
- Recovery sets `_recoveryMembershipVersion`
- New registrations must have `MembershipVersion >= _recoveryMembershipVersion`
- Prevents accepting stale registrations during recovery

#### Network Partition Handling

The directory relies on cluster membership for network partition detection:
- Uses `ClusterMembershipService` for failure detection
- Dead silos are removed from directory membership
- Their entries are cleaned up when detected
- Range locks prevent serving stale data during transitions

---

## Testing and Diagnostics

### Test Infrastructure

**Location**: `test/TesterInternal/GrainDirectory/DistributedGrainDirectoryTests.cs`

**Test fixture**: Uses `ConfigureDistributedGrainDirectory` configurator:

```csharp
internal class ConfigureDistributedGrainDirectory : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
        => siloBuilder.AddDistributedGrainDirectory();
}
```

### Diagnostic Interfaces

#### ITestHooks

Internal testing interface:

```csharp
internal interface ITestHooks
{
    SiloAddress? GetPrimaryForGrain(GrainId grainId);
    Task<GrainAddress?> GetLocalRecord(GrainId grainId);
}
```

**Usage**: Inspect directory state in tests

#### IGrainDirectoryTestHooks

Partition-level diagnostics:

```csharp
async ValueTask CheckIntegrityAsync()
{
    // 1. Verify all stored entries are in owned range
    // 2. Query all silos for activations
    // 3. Compare with local directory
    // 4. Report missing or mismatched entries
}
```

**Usage**: Validate directory consistency

### Logging

The implementation uses structured logging extensively:

```csharp
[LoggerMessage(Level = LogLevel.Debug, Message = "Updated view from '{PreviousVersion}' to '{Version}'...")]
private partial void LogDebugUpdatedView(...);

[LoggerMessage(Level = LogLevel.Warning, Message = "Remote host became unavailable...")]
private partial void LogWarningRemoteHostUnavailable(...);
```

**Key log events**:
- `"Updated view from X to Y"`: View change occurred
- `"Relinquishing ownership of range"`: Releasing a range
- `"Acquiring range"`: Taking ownership of a range
- `"Recovering activations from range"`: Recovery initiated
- `"Transferred N entries"`: Snapshot transfer completed

### Metrics

**Instrumentation points**:

```csharp
DirectoryInstruments.SnapshotTransferCount.Add(1);
DirectoryInstruments.SnapshotTransferDuration.Record(milliseconds);
DirectoryInstruments.RangeRecoveryCount.Add(1);
DirectoryInstruments.RangeRecoveryDuration.Record(milliseconds);
DirectoryInstruments.RangeLockHeldDuration.Record(milliseconds);
```

**Metrics to monitor**:
- Snapshot transfer frequency and duration
- Recovery frequency and duration
- Range lock contention (held duration)

### Common Issues and Debugging

#### Issue: Slow view changes

**Symptoms**: Long delays when silos join/leave

**Diagnosis**:
- Check `SnapshotTransferDuration` metrics
- Look for `"Error transferring ownership"` warnings
- Monitor network latency between silos

**Solutions**:
- Reduce partition count (trades off with distribution quality)
- Optimize network between silos
- Investigate snapshot size (many grains in one range)

#### Issue: Frequent recoveries

**Symptoms**: `RangeRecoveryCount` increasing rapidly

**Diagnosis**:
- Check for non-contiguous view changes: `"Non-contiguous view change detected"`
- Look for silo crashes during view changes
- Monitor membership stability

**Solutions**:
- Stabilize cluster membership
- Increase snapshot transfer timeout
- Reduce rate of membership changes

#### Issue: Directory inconsistencies

**Symptoms**: Grains not found or duplicate activations

**Diagnosis**:
- Use `IGrainDirectoryTestHooks.CheckIntegrityAsync()`
- Check for `"Integrity violation"` errors
- Review recovery logs

**Solutions**:
- Ensure proper grain lifecycle (register on activation, unregister on deactivation)
- Verify membership version handling
- Check for clock skew between silos

---

## Summary

The **Experimental Distributed Grain Directory** is a sophisticated, fully distributed implementation that eliminates the need for external directory storage in Orleans. It uses proven distributed systems techniques (Virtual Synchrony, consistent hashing) to provide:

- **Scalability**: Linear scaling with cluster size
- **Availability**: No single point of failure
- **Resilience**: Automatic recovery from failures
- **Performance**: In-memory operations with O(log N) lookup

While marked as experimental, it represents a production-ready approach to grain directory management suitable for large-scale Orleans deployments that require:
- High availability without external dependencies
- Automatic load balancing
- Graceful handling of dynamic cluster topology

The implementation is well-documented in code, follows established distributed systems principles, and provides extensive hooks for testing, monitoring, and future enhancements.

---

## References

- **Source Code**: `src/Orleans.Runtime/GrainDirectory/DistributedGrainDirectory.cs`
- **Amazon Dynamo Paper**: https://www.allthingsdistributed.com/files/amazon-dynamo-sosp2007.pdf
- **Apache Cassandra Virtual Nodes**: https://docs.datastax.com/en/cassandra-oss/3.0/cassandra/architecture/archDataDistributeVnodesUsing.html
- **Virtual Synchrony**: https://www.microsoft.com/en-us/research/publication/virtually-synchronous-methodology-for-dynamic-service-replication/
- **Vertical Paxos**: https://www.microsoft.com/en-us/research/publication/vertical-paxos-and-primary-backup-replication/

---

**Document Version**: 1.0
**Last Updated**: 2025-11-21
**Orleans Version**: Current main branch
**Status**: Experimental (ORLEANSEXP003)
