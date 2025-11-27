# Clustering and Membership System

## Overview

Orleans' clustering system maintains a consistent view of which silos are part of the cluster, monitors their health, and handles failure detection. This is critical for distributed grain location, message routing, and fault tolerance.

**Location**: `src/Orleans.Runtime/MembershipService/`

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                 Membership Table                         │
│           (External Storage: SQL, Azure, etc.)           │
│                                                          │
│  SiloAddress  │  Status  │  Generation  │  Heartbeat   │
│  10.0.0.1:111 │  Active  │     5        │  10:30:45    │
│  10.0.0.2:111 │  Active  │     3        │  10:30:44    │
│  10.0.0.3:111 │  Dead    │     2        │  10:25:10    │
└──────────────────────────────────────────────────────────┘
                          ↑ ↓
      ┌──────────────────┼──────────────────┐
      │                  │                  │
┌─────┴────────┐  ┌──────┴──────┐  ┌──────┴──────┐
│   Silo 1     │  │   Silo 2    │  │   Silo 3    │
│              │  │             │  │             │
│ ClusterMember│  │ClusterMember│  │ClusterMember│
│ shipService  │  │shipService  │  │shipService  │
│              │  │             │  │             │
│ SiloHealth   │  │ SiloHealth  │  │ SiloHealth  │
│ Monitor      │  │ Monitor     │  │ Monitor     │
│              │  │             │  │             │
│ Membership   │  │ Membership  │  │ Membership  │
│ Gossiper     │  │ Gossiper    │  │ Gossiper    │
└──────────────┘  └─────────────┘  └─────────────┘
```

## Key Components

### ClusterMembershipService

**File**: `src/Orleans.Runtime/MembershipService/ClusterMembershipService.cs`

**Purpose**: Authoritative source of cluster membership for a silo.

**Responsibilities**:
- Read membership table from storage
- Publish membership updates to local silo
- Coordinate silo join/leave
- Trigger membership change callbacks

**Key State**:
```csharp
class ClusterMembershipService
{
    // Current cluster view
    private ClusterMembershipSnapshot currentSnapshot;

    // External storage
    private readonly IMembershipTable membershipTable;

    // Update notification
    private readonly List<IClusterMembershipObserver> observers;
}
```

**ClusterMembershipSnapshot**:
```csharp
public class ClusterMembershipSnapshot
{
    // All known silos
    public ImmutableDictionary<SiloAddress, MembershipEntry> Members { get; }

    // Version of this snapshot
    public MembershipVersion Version { get; }

    public IEnumerable<SiloAddress> GetActiveSilos();
    public SiloStatus GetSiloStatus(SiloAddress silo);
}
```

**MembershipEntry**:
```csharp
public class MembershipEntry
{
    public SiloAddress SiloAddress { get; set; }
    public string SiloName { get; set; }
    public SiloStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime IAmAliveTime { get; set; }
    public List<SuspectingSilo> Suspectors { get; set; }
}
```

### Silo States

```csharp
public enum SiloStatus
{
    None,          // Not initialized
    Created,       // Created but not started
    Joining,       // Attempting to join cluster
    Active,        // Healthy and serving traffic
    ShuttingDown,  // Graceful shutdown initiated
    Stopping,      // Forceful shutdown
    Dead           // Failed or stopped
}
```

**State Machine**:
```
Created
  ↓
Joining (register with membership table)
  ↓
Active (healthy, processing requests)
  ↓
ShuttingDown (graceful shutdown)
  ↓
Stopping (forced shutdown)
  ↓
Dead (terminated)
```

### SiloAddress

**File**: `src/Orleans.Core.Abstractions/IDs/SiloAddress.cs`

**Purpose**: Unique identifier for a silo instance.

```csharp
public readonly struct SiloAddress
{
    public IPEndPoint Endpoint { get; }
    public int Generation { get; }

    // Format: 10.0.0.1:11111:5 (IP:Port:Generation)
    public override string ToString() =>
        $"{Endpoint}:{Generation}";
}
```

**Generation Number**:
- Incremented each time a silo restarts
- Prevents "ghost" silos (old incarnation after network partition)
- Enables detection of stale addresses

**Example**:
```
First start:  10.0.0.1:11111:0
After crash:  10.0.0.1:11111:1
After crash:  10.0.0.1:11111:2
```

### IMembershipTable

**File**: `src/Orleans.Core.Abstractions/Membership/IMembershipTable.cs`

**Purpose**: Abstraction for storing membership information in external storage.

**Interface**:
```csharp
public interface IMembershipTable
{
    // Read entire membership table
    Task<MembershipTableData> ReadAll();

    // Read specific row
    Task<MembershipTableData> ReadRow(SiloAddress silo);

    // Insert new silo (atomic)
    Task<bool> InsertRow(
        MembershipEntry entry,
        TableVersion version);

    // Update existing silo (atomic with version check)
    Task<bool> UpdateRow(
        MembershipEntry entry,
        string etag,
        TableVersion version);

    // Update IAmAlive timestamp
    Task UpdateIAmAlive(MembershipEntry entry);

    // Delete old entries
    Task CleanupDefunctSiloEntries(DateTimeOffset beforeDate);
}
```

**Implementations**:
- **Orleans.Clustering.AdoNet**: SQL Server, PostgreSQL, MySQL
- **Orleans.Clustering.AzureStorage**: Azure Table Storage
- **Orleans.Clustering.DynamoDB**: AWS DynamoDB
- **Orleans.Clustering.Consul**: HashiCorp Consul
- **Orleans.Clustering.ZooKeeper**: Apache ZooKeeper
- **Memory**: In-memory (for testing/development)

**Consistency Requirements**:
- **Atomic updates**: Version-based optimistic concurrency
- **Read-your-writes**: See your own updates
- **Linearizability**: Not required (eventual consistency OK)

### MembershipTableManager

**File**: `src/Orleans.Runtime/MembershipService/MembershipTableManager.cs`

**Purpose**: Manages reads/writes to the membership table.

**Responsibilities**:
- Coordinate silo join process
- Update membership table with status changes
- Read membership table periodically
- Handle concurrent updates

**Join Protocol**:
```
1. Silo starts, status = Joining
   ↓
2. Read membership table to get current version
   ↓
3. Generate new generation number (last + 1)
   ↓
4. Create SiloAddress with new generation
   ↓
5. Create MembershipEntry
   ↓
6. InsertRow(entry, version) [atomic]
   ↓
7. If success → status = Active
   If failure → retry with new version
   ↓
8. Start heartbeat updates
```

**Heartbeat**:
```csharp
// Periodic update (default: every 30 seconds)
async Task UpdateIAmAlive()
{
    while (status == SiloStatus.Active)
    {
        await membershipTable.UpdateIAmAlive(myEntry);
        await Task.Delay(heartbeatInterval);
    }
}
```

### SiloHealthMonitor

**File**: `src/Orleans.Runtime/MembershipService/SiloHealthMonitor.cs`

**Purpose**: Monitors health of other silos and detects failures.

**Health Check Strategy**:
```csharp
// Probe silos periodically
async Task MonitorSilos()
{
    foreach (var silo in cluster.GetActiveSilos())
    {
        if (silo == mySiloAddress)
            continue; // Don't probe self

        try
        {
            // Send ping message
            var response = await SendPing(silo, timeout: 5.seconds);

            if (response.IsHealthy)
            {
                // Silo is healthy
                continue;
            }
        }
        catch (Exception ex)
        {
            // Probe failed
            await HandleSuspectedSilo(silo, ex);
        }
    }
}
```

**Failure Detection**:
```
1. Probe timeout or failure
   ↓
2. Vote that silo is suspect
   ↓
3. Write "suspect vote" to membership table
   ↓
4. If N silos (quorum) vote suspect
   ↓
5. Declare silo Dead
   ↓
6. Update membership table (status = Dead)
   ↓
7. Publish membership change
```

**Suspicion Voting**:
```csharp
// Multiple silos must agree before declaring failure
class SuspectingSilo
{
    public SiloAddress Silo { get; set; }
    public DateTime Time { get; set; }
}

// In membership entry
List<SuspectingSilo> Suspectors;

// If Suspectors.Count >= quorum → declare dead
```

**Configuration**:
```csharp
siloBuilder.Configure<ClusterMembershipOptions>(options =>
{
    options.ProbeTimeout = TimeSpan.FromSeconds(5);
    options.NumVotesForDeathDeclaration = 3; // Quorum size
});
```

### MembershipGossiper

**File**: `src/Orleans.Runtime/MembershipService/MembershipGossiper.cs`

**Purpose**: Distributes membership updates via gossip protocol.

**Why Gossip?**
- Reduce load on membership table
- Faster propagation of updates
- Eventual consistency
- Scalability (O(log N) propagation time)

**Gossip Protocol**:
```
Every gossip interval (default: 10 seconds):
1. Pick K random silos (default: 3)
2. Send current membership snapshot to each
3. Receive their snapshot
4. Merge: take newer entries
5. Update local snapshot
```

**Gossip Message**:
```csharp
class MembershipGossipMessage
{
    public MembershipVersion Version { get; set; }
    public List<MembershipEntry> Updates { get; set; }
}
```

**Merge Logic**:
```csharp
ClusterMembershipSnapshot Merge(
    ClusterMembershipSnapshot local,
    ClusterMembershipSnapshot remote)
{
    var merged = new Dictionary<SiloAddress, MembershipEntry>();

    // For each silo
    foreach (var silo in local.Members.Keys.Union(remote.Members.Keys))
    {
        var localEntry = local.Members.GetValueOrDefault(silo);
        var remoteEntry = remote.Members.GetValueOrDefault(silo);

        if (localEntry == null)
            merged[silo] = remoteEntry;
        else if (remoteEntry == null)
            merged[silo] = localEntry;
        else
            // Take entry with newer IAmAliveTime
            merged[silo] = localEntry.IAmAliveTime > remoteEntry.IAmAliveTime
                ? localEntry
                : remoteEntry;
    }

    return new ClusterMembershipSnapshot(merged, ...);
}
```

## Join and Leave Protocols

### Silo Join

```
1. Start silo
   ↓
2. Initialize services
   ↓
3. Read membership table
   ↓
4. Compute new generation = max(existing.generation) + 1
   ↓
5. Create SiloAddress(endpoint, generation)
   ↓
6. InsertRow(entry, tableVersion) - atomic
   ↓
7. If version conflict → retry from step 3
   ↓
8. Status = Active
   ↓
9. Start heartbeat updates
   ↓
10. Publish membership snapshot to local silo
    ↓
11. Other silos discover via gossip or table read
    ↓
12. Start accepting grain activations
```

**Code**:
```csharp
async Task Join()
{
    while (true)
    {
        // Read current membership
        var membership = await membershipTable.ReadAll();

        // Compute generation
        var maxGeneration = membership.Members
            .Where(m => m.SiloAddress.Endpoint.Equals(myEndpoint))
            .Max(m => m.SiloAddress.Generation);
        var myGeneration = maxGeneration + 1;

        // Create entry
        var myAddress = SiloAddress.New(myEndpoint, myGeneration);
        var entry = new MembershipEntry
        {
            SiloAddress = myAddress,
            Status = SiloStatus.Active,
            StartTime = DateTime.UtcNow,
            IAmAliveTime = DateTime.UtcNow
        };

        // Atomic insert
        var success = await membershipTable.InsertRow(
            entry,
            membership.Version);

        if (success)
        {
            mySiloAddress = myAddress;
            status = SiloStatus.Active;
            break;
        }

        // Retry with updated table state
    }
}
```

### Graceful Shutdown

```
1. Receive shutdown signal
   ↓
2. Status = ShuttingDown
   ↓
3. Stop accepting new activations
   ↓
4. Update membership table (status = ShuttingDown)
   ↓
5. Wait for in-flight requests to complete
   ↓
6. Deactivate all grains (call OnDeactivateAsync)
   ↓
7. Close network connections
   ↓
8. Status = Stopping
   ↓
9. Update membership table (status = Dead)
   ↓
10. Stop all services
    ↓
11. Exit process
```

### Failure Detection

**Timeout-Based**:
```
Silo A probes Silo B
  → No response within timeout (e.g., 5 seconds)
  → A votes B as suspect
  → Write to membership table: B.Suspectors.Add(A)
  → If B.Suspectors.Count >= quorum
  → Declare B dead
```

**Heartbeat-Based**:
```
Periodic membership table scan:
  For each Active silo:
    If IAmAliveTime > maxSilenceTime (e.g., 3 minutes)
      → Declare dead
      → Update status = Dead
```

**Hybrid Approach** (Orleans uses both):
- Probing for fast detection (seconds)
- Heartbeat for eventual detection (minutes)
- Voting for robustness (avoid false positives)

## Consistency and CAP

### CAP Theorem Trade-offs

Orleans membership prioritizes **Availability** and **Partition Tolerance**:

- **Consistency**: Eventual (not strict)
- **Availability**: Always accept reads/writes
- **Partition Tolerance**: Continue operating during network partitions

### Split-Brain Handling

**Problem**: Network partition divides cluster into two groups.

**Solution**:
1. **Generation numbers**: Prevent old incarnations from rejoining
2. **Membership table**: Acts as tie-breaker
3. **Quorum voting**: Require multiple silos to agree on failure
4. **No automatic merge**: Require manual intervention for split-brain recovery

**Example**:
```
Cluster: [A, B, C, D, E]
Partition: [A, B] | [C, D, E]

Both sides can continue operating:
- [A, B]: Can declare C, D, E dead if they reach quorum
- [C, D, E]: Can declare A, B dead if they reach quorum

Membership table acts as coordinator:
- Side with access to table wins
- Other side eventually stops (can't update heartbeat)
```

## Configuration

```csharp
// Membership table configuration
siloBuilder.UseAdoNetClustering(options =>
{
    options.ConnectionString = "...";
    options.Invariant = "System.Data.SqlClient";
});

// Membership protocol configuration
siloBuilder.Configure<ClusterMembershipOptions>(options =>
{
    // How often to read membership table
    options.TableRefreshTimeout = TimeSpan.FromSeconds(60);

    // How often to update IAmAlive
    options.IAmAliveTablePublishTimeout = TimeSpan.FromMinutes(5);

    // Probe timeout for health checks
    options.ProbeTimeout = TimeSpan.FromSeconds(5);

    // Number of silos that must vote for death
    options.NumVotesForDeathDeclaration = 2;

    // Time to wait before removing dead silo entries
    options.DeathVoteExpirationTimeout = TimeSpan.FromHours(24);
});

// Gossip configuration
siloBuilder.Configure<GossipOptions>(options =>
{
    options.GossipInterval = TimeSpan.FromSeconds(10);
    options.GossipChannelCount = 3;
});
```

## Performance and Scalability

### Membership Table Load

**Reads**:
- Each silo reads table every ~60 seconds
- Load: O(N) where N = number of silos
- Scales to hundreds of silos

**Writes**:
- Each silo writes heartbeat every ~5 minutes
- Additional writes for status changes (rare)
- Load: O(N)

**Gossip Optimization**:
- Reduces table reads significantly
- Updates propagate in O(log N) time via gossip

### Failure Detection Latency

**Fast Path** (probing):
- Detection: ~5-10 seconds
- Propagation via gossip: ~10-30 seconds
- Total: ~15-40 seconds

**Slow Path** (heartbeat timeout):
- Detection: ~3-5 minutes
- For silos that become completely unreachable

### Scalability Limits

**Tested Scale**:
- Clusters: Hundreds of silos
- Membership table: <1 MB for 1000 silos
- Gossip: Sublinear scalability

**Bottlenecks**:
- Membership table storage (usually not a concern)
- Network bandwidth for gossip (minimal)

## Observability

### Metrics

```csharp
// Number of active silos
orleans_cluster_active_silos

// Membership table version
orleans_membership_table_version

// Health probe failures
orleans_health_probe_failures

// Time since last membership update
orleans_membership_update_age_seconds
```

### Logging

```csharp
// Silo joined
[Information] Silo 10.0.0.1:11111:5 joined the cluster

// Silo suspected
[Warning] Silo 10.0.0.2:11111:3 is suspected by 1 silos

// Silo declared dead
[Error] Silo 10.0.0.2:11111:3 declared dead (suspected by 3 silos)

// Membership updated
[Debug] Cluster membership updated to version 42 (5 active silos)
```

## Summary

The Clustering and Membership System:

1. **Tracks** which silos are in the cluster
2. **Detects** silo failures via probing and heartbeats
3. **Propagates** updates via gossip and membership table
4. **Coordinates** join/leave using external storage
5. **Prevents** split-brain with generation numbers
6. **Scales** to hundreds of silos

Key components:
- **ClusterMembershipService**: Local view of cluster
- **MembershipTableManager**: Storage coordination
- **SiloHealthMonitor**: Failure detection
- **MembershipGossiper**: Fast update propagation
- **IMembershipTable**: Pluggable storage abstraction

---

**Next**: [Messaging and Networking](07-messaging-networking.md)
