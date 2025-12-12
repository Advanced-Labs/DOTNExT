# Async+ Continuation Problem: The VCOM Solution

> **Document Type:** Technical Solution Design
> **Version:** 1.1
> **Date:** 2025-12-08 (Updated with Runtime-Async option)
> **Status:** ⏸️ DEFERRED - Implementation waiting for VCOM
> **Supersedes:** Previous Engram-based continuation approaches

---

## ⏸️ IMPLEMENTATION DEFERRED (2025-12-07)

**This solution design is valid but implementation is deferred.**

Per VDEC-002 in `VAYRON-Decision-Log.md`:
- Async+ continuation depends on VCOM.Resolve() existing
- VCOM must be built first
- We're not building throwaway UUID resolution

**Current Async+ status:**
- ✅ State machine states persist and reload
- ❌ Reference rehydration not implemented
- ❌ Awaiter resume point not restored

**When will this be implemented?**
After VCOM Phase 1 is complete (VObject, VCOMPodGrain, basic resolution).

---

---

## 1. The Problem Restated

**Current state:** Async+ can persist and reload state machine states. But:
- States contain **reference fields** pointing to objects
- When state machine is "hibernated", those referenced objects are gone
- On reload, the references point to nothing - or worse, wrong things
- The continuation **step** (which `await` to resume from) isn't properly restored

**The naive solution** would be to serialize/deserialize all referenced objects. But:
- Not all references can be serialized
- Not all references *should* be serialized
- Graph serialization is complex, slow, and fragile
- This is exactly what Engram was designed to solve

---

## 2. The VCOM Solution: Reference = UUID

**Key insight:** If all objects are VCOM objects (backed by grains), then:
- Every object has a persistent identity (UUID/key)
- References can be stored as **identities, not object graphs**
- Rehydration = "get grain by identity" (which Orleans already does perfectly)

### 2.1 How It Works

```
HIBERNATION (State Machine Suspend):
1. Async+ state machine checkpoint triggers
2. For each reference field in state:
   - Extract VCOM identity (UUID + type info)
   - Store identity, NOT the object
3. Persist: { state_step, state_values, reference_identities[] }

REHYDRATION (State Machine Resume):
1. Load checkpoint: { state_step, state_values, reference_identities[] }
2. For each reference identity:
   - Call VCOM "get object" (maps to grain resolution)
   - If grain already active: return reference
   - If grain inactive: activate it, return reference
   - If grain type not loaded: load type, activate, return reference
3. Inject resolved references into state machine fields
4. Resume at state_step
```

### 2.2 Why This Works

Orleans already solves this for grains:
```csharp
var grain = grainFactory.GetGrain<IMyGrain>(grainId);
// Grain is either:
// - Already active somewhere → returns proxy
// - Not active → activates it, returns proxy
// - Type not loaded → loads type, activates, returns proxy
```

VCOM extends this to **all objects**:
```csharp
var obj = vcom.Get<MyVCOMType>(uuid);
// Same resolution logic, but for VCOM objects
// Under the hood, this IS a grain operation
```

### 2.3 The Cascade Effect

When object A is rehydrated, its fields may reference objects B, C, D:
```
Rehydrate A
  → A.fieldB needs object B → Rehydrate B
    → B.fieldC needs object C → Rehydrate C
  → A.fieldD needs object D → Rehydrate D
```

This cascade is **natural** for the virtual actor model:
- Grains activate on demand
- References resolve lazily
- No need to pre-load entire object graphs

---

## 3. Developer Experience

### 3.1 What Developers Write

```csharp
// Regular C# - no Orleans concepts visible
public class OrderProcessor
{
    public async Task ProcessOrder(Order order)
    {
        var customer = await GetCustomer(order.CustomerId);
        var inventory = await CheckInventory(order.Items);

        // Long-running operation - might hibernate here
        await WaitForPayment(order);

        // References still valid after hibernation!
        await ShipOrder(order, customer.Address);
    }
}
```

### 3.2 What's Generated (Conceptual)

```csharp
// Codegen transforms this to:
public class OrderProcessor_StateMachine
{
    // State fields become UUID references
    private Guid _order_id;
    private Guid _customer_id;
    private Guid _inventory_id;

    public async Task MoveNext()
    {
        switch (_state)
        {
            case 2: // Resuming after WaitForPayment
                // Rehydrate references from UUIDs
                var order = await VCOM.Get<Order>(_order_id);
                var customer = await VCOM.Get<Customer>(_customer_id);
                // Continue execution...
        }
    }
}
```

### 3.3 The Illusion

From the developer's perspective:
- Objects look and feel like regular C# objects
- `new Order()` creates a VCOM object (grain under the hood)
- `new Order(existingUuid)` retrieves existing VCOM object
- References "just work" across hibernation
- No Orleans concepts leak into application code

---

## 4. What This Requires

### 4.1 From NewOrleans

| Requirement | Status | Notes |
|-------------|--------|-------|
| Dynamic grain loading | ✅ Complete | MDCP system |
| Grain Type Directory | ✅ Complete | Cluster-wide registry |
| UUID-based grain resolution | Needed | Extension to current key system |
| VCOM kernel grain types | Needed | Runtime pod grains |

### 4.2 From Roslyn/Codegen

| Requirement | Status | Notes |
|-------------|--------|-------|
| State machine field analysis | ✅ Exists | Async+ already does this |
| Reference → UUID extraction | Needed | Codegen enhancement |
| UUID → Reference rehydration | Needed | Codegen enhancement |
| VCOM type transformation | Needed | class → grain-backed class |

### 4.3 From VCOM Design

| Requirement | Status | Notes |
|-------------|--------|-------|
| VObject base type | Design phase | Minimal augmentations |
| UUID generation | Design phase | UUIDv7 recommended |
| Type metadata persistence | Design phase | Code-as-data |
| Resolution API | Design phase | VCOM.Get<T>(uuid) |

---

## 5. What This Means for DOTNExT

### 5.1 Engram Scope Reduction

The full Engram system (CMS, MOM, ORION at runtime level) may **not be needed** for VAYRON. Instead:
- **Engram concepts** are useful (identity, relationships, semantics)
- **Engram implementation** can stay at VCOM/NewOrleans level
- Runtime modifications minimized
- DOTNExT focus shifts to: Roslyn codegen + NewOrleans kernel

### 5.2 Memantics Repositioning

Memantics becomes a **VAYRON product component**, not a DOTNExT runtime feature:
- Built on top of VCOM infrastructure
- Uses RavenDB/Neo4j/AuraDB as backing stores
- Provides semantic memory APIs to VCOM objects
- Not baked into the runtime itself

### 5.3 Priority Shift

| Area | Previous Priority | New Priority | Reason |
|------|-------------------|--------------|--------|
| DOTNExT runtime mods | High | Lower | VCOM solves reference problem |
| NewOrleans kernel | Medium | **Critical** | Foundation for VCOM |
| Roslyn codegen | Medium | **High** | VCOM type transformation |
| Async+ awaiter resume | Critical | **Critical** | Still blocked |

---

## 6. Open Questions

1. **Performance:** How does UUID-based resolution compare to direct references?
   - Hypothesis: Acceptable for VAYRON's "slow but smart" philosophy
   - Need benchmarks

2. **Non-VCOM objects:** What happens with references to System types, third-party libs?
   - Strategy: Value types serialize directly; reference types need wrapping or exclusion

3. **Circular references:** How to handle A→B→A resolution?
   - Orleans already handles this for grains; VCOM inherits this

4. **Local optimization:** Can some objects be truly local (not grain-backed)?
   - Maybe: "value VCOM" vs "identity VCOM" types

---

## 7. Next Steps

1. **Prototype VCOM.Get<T>(uuid)** - Resolution API over NewOrleans
2. **Extend Async+ codegen** - Reference → UUID extraction and rehydration
3. **Test with simple scenario** - Single hibernation with one reference
4. **Scale to complex scenario** - Nested references, cascading activation

---

*This solution leverages Orleans' existing virtual actor identity model to solve what seemed like a runtime-level problem. Sometimes the answer is at a different layer than expected.*

---

## 8. Alternative Foundation: .NET Runtime-Async

> **Added:** 2025-12-08 research session
> **See:** DOTNExT-Runtime-Async-Research.md for full details

### 8.1 The Discovery

.NET 10 introduces **Runtime-Async** - async support moved from compiler-generated state machines to JIT-managed execution suspension. This changes the landscape for Async+ significantly.

### 8.2 Why Runtime-Async Matters for Async+

**Current Compiler-Async:**
- Roslyn generates state machine struct
- State fields are compiler's interpretation of what to preserve
- Resume point is implicit (state integer → switch case)
- We must modify Roslyn's state machine generation

**Runtime-Async:**
- JIT captures complete stack frames to **Tasklets**
- All locals, temporaries, parameters captured
- Exact instruction pointer preserved
- Callee-saved registers captured
- Frame capture/restore is a runtime primitive

### 8.3 Tasklet Advantages for Async+

| Aspect | Roslyn State Machine | Runtime-Async Tasklet |
|--------|---------------------|----------------------|
| **State captured** | Compiler-selected fields | Everything (perfect snapshot) |
| **Resume point** | State int → switch case | Exact IP |
| **What we modify** | Roslyn codegen | Tasklet lifecycle hooks |
| **Complexity** | High (compiler internals) | Lower (well-defined structure) |
| **Maintenance** | Must track Roslyn changes | Uses stable runtime API |

### 8.4 Async+ Over Runtime-Async: The Approach

```
┌─────────────────────────────────────────────────────────────────┐
│  Async+ on Runtime-Async Foundation                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. INTERCEPT: Hook Tasklet creation on await                   │
│     └── Runtime creates Tasklet, we get notification            │
│                                                                 │
│  2. SERIALIZE: Convert Tasklet to persistent form               │
│     ├── Frame data → byte[]                                     │
│     ├── IP → method token + offset                              │
│     ├── VCOM references → UUIDs (same as Section 2)             │
│     └── Registers → saved values                                │
│                                                                 │
│  3. STORE: Persist to NewOrleans/RavenDB                        │
│                                                                 │
│  4. RESTORE: On process restart / different node                │
│     ├── Load serialized Tasklet data                            │
│     ├── Rehydrate VCOM references via UUID (Section 2)          │
│     └── Reconstruct Tasklet                                     │
│                                                                 │
│  5. RESUME: Let Runtime-Async resume from Tasklet               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 8.5 Comparison: Two Async+ Implementation Paths

| Aspect | Path A: Roslyn Codegen | Path B: Runtime-Async |
|--------|------------------------|----------------------|
| **Foundation** | Modify Roslyn state machine | Hook Tasklet lifecycle |
| **State access** | Parse generated struct | Native frame data |
| **Resume mechanism** | MoveNext() with state | JIT jump to IP |
| **VCOM integration** | UUID in generated fields | UUID extracted from frame |
| **Difficulty** | High | Medium |
| **Requires .NET 10** | No | Yes |
| **Captures more state** | No | Yes |

### 8.6 Recommendation

**Consider Path B (Runtime-Async) as primary approach:**

1. **Cleaner architecture** - Tasklets are designed for capture/restore
2. **More complete state** - Everything captured, not compiler's selection
3. **Less Roslyn maintenance** - Don't fight compiler internals
4. **Aligns with other goals** - Same infrastructure enables:
   - Process image persistence
   - BEAM-like preemption
   - Unified safe points

**Path A remains valid fallback** for:
- Pre-.NET 10 support
- Scenarios where Runtime-Async overhead is too high
- Cases where Tasklet API isn't sufficient

### 8.7 Open Questions for Runtime-Async Path

1. **Tasklet API stability** - Is structure documented/stable enough?
2. **Serialization API** - Can we serialize/deserialize Tasklets?
3. **Cross-process restore** - Memory layout differences?
4. **Hooks availability** - Can we intercept Tasklet creation?

### 8.8 Next Steps (Updated)

**If pursuing Runtime-Async path:**
1. Enable Runtime-Async in DOTNExT build
2. Explore Tasklet internals and API surface
3. Prototype Tasklet serialization
4. Integrate with VCOM UUID resolution
5. Test hibernation/resume across processes

---

## 9. Related Documents (Updated)

| Document | Relationship |
|----------|--------------|
| DOTNExT-Runtime-Async-Research.md | **NEW** - Detailed Runtime-Async analysis |
| DOTNExT-Process-Image-Persistence.md | **NEW** - Broader checkpoint/restore vision |
| DOTNExT-Unified-SafePoints.md | **NEW** - Safe point convergence |
| VAYRON-Decision-Log.md | VDEC-002 decision on deferral |

---

*Version 1.1 - 2025-12-08 (Added Runtime-Async alternative foundation)*
