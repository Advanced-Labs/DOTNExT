# Phase 2 v1 — StorageDevice + Voron-backed Memory Driver (Persistence Vertical Slice)
_VAYRON R1 (VR1) — DDS/SAL microkernel progression_

> **Document Version:** 1.0
> **Status:** Planning
> **Phase:** 2 (Persistence Vertical Slice)

> **Purpose of Phase 2:** make VAYRON capable of **durable memory** for routed/virtual objects using a first Memory/Storage driver (Voron-based), without yet committing to the full long-term language semantics (C=) or advanced virtues (relations, distribution, versioning, semantics).

> **Related Documents:**
> - [VAYRON-R1-Platform-Vision.md](../VAYRON-R1-Platform-Vision.md) — Architecture and design philosophy
> - [VAYRON-R1-Roadmap-and-Codebase-Map.md](../VAYRON-R1-Roadmap-and-Codebase-Map.md) — Voron/NewOrleans codebase analysis
> - [Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md](../Phase1/01-Phase1-DDS-Microkernel-and-Persistence.md) — Phase 1 prerequisites

This document is written to be **true-to-what-we-know** today:
- ✅ what is decided and stable
- ⚠️ what is intentionally TBD
- 🔁 options and conceptual illustrations (not specs)

---

## 0) Phase 2 Summary

### Phase 2 Goal (crisp)
**Make this work end-to-end:**

1. Create a virtual/routed object
2. Mutate fields
3. Shutdown process (clean or crash-tolerant)
4. Restart
5. Re-load by VUID
6. Observe fields persisted

**End condition:** "durable state survives restart" for the Phase-2 test suite.

### Phase 2 Non-goals
Phase 2 explicitly does **NOT** deliver:
- distributed execution / remote dispatch (CallDispatchDevice remains stub)
- replication / quorum protocols
- relational/graph substrate
- versioning/time travel
- security capability enforcement (only reserve hooks)
- finalized C# semantics (C=) for persistence / transactions / virtues

---

## 1) Prerequisites (Phase 1 outputs)

Phase 2 assumes Phase 1 has already delivered:

✅ DDS routing bit semantics (default vs non-default routed object)
✅ `ops_root` resolution infra (stable-key routing via SyncBlockIndex)
✅ DefaultDrivers for baseline CLR behavior
✅ ObjectModelDevice + FieldAccessDevice contracts
✅ VContext placeholder structure (for forward-compatible driver interfaces)
✅ syscall/intrinsic helper layer usable as bootstrapping for interception

Phase 2 builds on these without reshaping them.

---

## 2) What "Persistence" means in VR1

### 2.1 "Memory" framing
VAYRON treats persistence as **durable memory**. Voron is the initial durable substrate.

Key interpretation:
- "Persisted" state is still "Memory" from the platform's point of view.
- Voron is not "a database dependency"; it is a **StorageDevice driver** in the kernel sense.

### 2.2 Durable authority and caching
**Decided (Phase 2):**
- Voron-backed StorageDriver is the **authority for durable state** for persisted objects.
- A routed object may have a RAM-resident "activation copy" as a cache.

**TBD (but illustrated below):**
- cache invalidation policy
- write coalescing strategy
- eviction policy
- crash-consistency boundaries for "auto persistence" mode

---

## 3) Driver Model in Phase 2

Phase 2 activates the first **non-default** drivers:

### 3.1 StorageDevice becomes real
`StorageDevice` is implemented by a **Voron driver** (`Storage_Voron`), responsible for:
- transactions
- durable read/write of the `Body` layer (initially)
- mapping `VUID → durable key`

This implements the `IStorageOps` interface defined in Phase 1:
- `Persist()`, `Materialize()`, `IsDirty()`, `MarkDirty()`
- `BeginTransaction()`, `CommitTransaction()`, `RollbackTransaction()`

### 3.2 FieldAccessDevice gains persistence semantics
A non-default `FieldAccessDevice` handles at least one of:
- eager persistence on every write (auto-persist)
- deferred persistence (flush/commit)
- manual persistence mode (`Save()` conceptual)

In Phase 2, we pick a **minimal policy** (see Section 6).

### 3.3 ObjectModelDevice remains simple (Phase 2)
Phase 2 should **not** introduce exotic object layouts yet.
ObjectModelDevice likely remains:
- Default CLR object layout for activation copy (Pattern B)
- plus "materialize-from-bytes" / "serialize-to-bytes" helpers (driver-side)

More advanced models (external bodies, zero-copy stable addressing) are Phase 3+ concerns.

---

## 4) Virtual Types and "default drivers" semantics (important)

### 4.1 Decided direction
The canonical intent:

> A class becomes virtual by being declared `virtual` at the language level (future C=), and **virtual types automatically use default VAYRON drivers**, which might include persistence if the default MemoryDriver is persistence-backed.

So:
- `virtual` does **not** mean "persistent"
- it means "routed through VAYRON driver defaults for virtual types"

Persistence semantics become:
- a **default driver selection** (TypeDriver policy)
- later complemented by explicit language semantics/options

### 4.2 Phase 2 scope compromise
Phase 2 does **not** require final C# syntax or C=.
Instead we support a transitional trigger using the attributes already defined in Phase 1:

**Phase 1 already provides** (in `System.Runtime.DDS`):
- `[Virtual]` — marks type as participating in DDS routing
- `[Persistent]` — marks type for durable storage

**Phase 2 usage:**
```csharp
[Virtual]
[Persistent]  // triggers StorageDevice driver selection
public class Account
{
    public decimal Balance { get; set; }
}
```

**Alternative options (if attribute approach proves insufficient):**

**Option B — Type registration (runtime config)**
```csharp
VKernel.RegisterVirtualType<Account>();
```

**Option C — Tooling/Build step**
A compiler switch or assembly-level metadata marks types as "virtual".

Phase 2 only needs one. The attribute-based approach minimizes Roslyn edits and builds on Phase 1 infrastructure.

---

## 5) VUID (global object identity)

### 5.1 Decided intent

* VUID is global across the Internet (not cluster-local)
* UUID v7 is the preferred shape (128-bit)

### 5.2 What Phase 2 must decide

**Yes, Phase 2 must pick at least:**

* a stable VUID structure (128-bit)
* who issues it (kernel vs driver vs managed helper)
* where it is stored (in persistent body + optionally cached in activation)

### 5.3 Where VUID lives (recommended Phase 2 design)

* VUID is stored in the durable `Body` layer record
* activation has access to it via kernel-managed metadata ("object identity view")

---

## 6) Persistence semantics in Phase 2 (policy choices)

Phase 2 should implement **one minimal persistence mode** and clearly reserve the rest.

### 6.1 Option 1 — AutoPersist (eager)

Every field write triggers a persistence update (or a batched write-behind that behaves as eager).

**Pros**

* simplest mental model
* easiest to validate ("write == durable soon")

**Cons**

* potentially heavy write amplification
* requires careful batching to be efficient

### 6.2 Option 2 — FlushPersist (deferred)

Writes accumulate in memory; persistence occurs on:

* `Flush(obj)`
* end-of-turn
* timer
* transaction commit

**Pros**

* closer to how real systems behave
* avoids write storm

**Cons**

* requires defining "durability boundary"

### 6.3 Option 3 — ManualPersist (`Save()`)

Object only persists when explicitly saved.

**Pros**

* simplest to implement robustly
* avoids hidden durability costs

**Cons**

* deviates from "managed memory" feeling

✅ **Phase 2 recommendation (v1):**
Implement **Option 2 (FlushPersist)** with a **simple default**:

* writes mark object dirty
* object flush happens at end of a "kernel turn" or on explicit call

This preserves flexibility and avoids locking VAYRON into the expensive worst case.

---

## 7) Transactions (Phase 2: minimal)

### 7.1 What must exist in Phase 2

* Voron transaction scopes (read/write tx)
* "coherence boundary" for a single object's body serialization

### 7.2 VContext integration

Phase 2 populates `VContext.reserved` slots for transaction handles when interacting with StorageDevice operations. The `VContext` structure defined in Phase 1 was designed for this forward-compatible extension:

```cpp
struct VContext {
    uint32_t version;
    uint32_t flags;
    void*    reserved[6];  // reserved[0] = transaction handle in Phase 2
};
```

This allows `Storage_Voron` driver operations to access the current transaction context without interface changes.

### 7.3 What is explicitly TBD

* multi-object transactions
* distributed transactions
* isolation levels beyond "good enough"
* conflict resolution and concurrency rules for multi-writer (not in Phase 2)

### 7.4 Minimal conceptual model

Phase 2 can support:

* `BeginWriteTx()` internally when flushing dirty objects
* commit/rollback per object flush batch

---

## 8) Body Layer Encoding (big TBD, but Phase 2 must choose a v1)

Phase 2 needs a durable representation of "object fields".

We intentionally do **not** lock the final format today, but we must choose a v1.

### 8.1 Encoding Options (illustrative)

#### Option A — Schemaful (fastest later)

* per Virtual Type: field schema ID
* body bytes are structured per schema

**Pros**

* fast materialization
* easier evolution rules

**Cons**

* requires schema identity/version rules earlier

#### Option B — Tagged field map (flexible)

* store `(FieldId → bytes)` pairs (dictionary-like)

**Pros**

* robust to type evolution
* easy to implement

**Cons**

* slower, more overhead

#### Option C — Managed serializer snapshot (fast to prototype, not ideal)

* serialize whole object graph-like blob

**Pros**

* quickest prototype

**Cons**

* risks lock-in and poor performance
* type evolution and reference identity become messy

✅ **Phase 2 v1 recommendation:**
Use **Option B**: Tagged field map.
Because Phase 2 is a vertical slice, not final performance engineering.

---

## 9) Materialization (activation) model — Pattern B first

Phase 2 should default to **Pattern B: Activation Copy** (as defined in Platform Vision Section 11):

* activation object exists in managed heap
* on load, fields are materialized from durable Body bytes into activation fields
* on flush, activation fields are serialized into durable Body bytes

**Why Pattern B now**

* simplest with CLR unchanged
* avoids stable-address constraints
* aligns with "progressive R&D" framing
* compatible with Phase 1's Mode A object layout (GC-safe stub + external body)

Pattern A (direct pointer indirection into Voron pages) is a later optimization.

---

## 10) Minimal "TypeDriver" concept (reserved, not fully built)

Phase 2 introduces the need for a per-type driver selection policy:

* what OpsRoot is used for this type?
* which drivers are default for "virtual types"?

**Decided in spirit**

* Virtual Types use a default OpsRoot for the "virtual world"

**TBD in mechanism**

* TypeDriver as an explicit device class vs policy layer in registry
* Roslyn syntax for selecting/parameterizing drivers

**Phase 2 pragmatic implementation**

* `DriverRegistry.SelectOpsRootForType(mt)` chooses:

  * DefaultOpsRoot for normal types
  * VirtualDefaultOpsRoot for "virtual types"
* VirtualDefaultOpsRoot includes:

  * ObjectModel = Default CLR
  * FieldAccess = persistence-aware (`FieldAccess_PersistOnFlush`)
  * Storage = Voron (`Storage_Voron`)

---

## 11) Phase 2 Work Packages (implementation plan)

### WP2.1 Voron Embedding Strategy

Decide "how Voron is hosted" for Phase 2.

**Option A — Embedded C# Voron inside runtime**

* runtime hosts it as a privileged managed component

**Option B — Native Voron integration (future)**

* not required in Phase 2

✅ Phase 2 recommendation: Option A (fastest integration).

### WP2.2 Storage_Voron Driver

Implement `IStorageOps` interface:

* `BeginReadTx / BeginWriteTx / CommitTx / RollbackTx`
* `TryLoadLayer / StoreLayer` for `Body` layer
* `Persist()` / `Materialize()` operations
* VUID-to-Voron-key mapping

### WP2.3 Body Encoder v1 (Tagged Field Map)

Implement:

* `SerializeBody(obj) → bytes`
* `DeserializeBody(bytes) → obj fields`

### WP2.4 FieldAccess_PersistOnFlush Driver

Implement:

* `TryWrite`: mark dirty
* `Flush`: serialize body + store via StorageDevice

### WP2.5 Loader API (Materialize by VUID)

Minimal managed API:

* `Get<T>(Vuid)` loads and materializes activation
* `New<T>()` allocates activation + VUID + durable record

### WP2.6 Tests (Phase 2 acceptance)

Required tests:

* Create → mutate → flush → restart → load → compare
* Partial field updates persist
* Two sequential updates persist deterministically

---

## 12) Conceptual C# Examples (illustrative, not spec)

These examples illustrate what Phase 2 *could enable*, without locking the final syntax.

### Example 1 — Virtual Type (future C= form)

```csharp
// Conceptual future form (C=)
public virtual class Account
{
    public decimal Balance { get; set; }
}
```

Interpretation (Phase 2 intent):

* this type uses VirtualDefault drivers
* persistence may happen because VirtualDefault StorageDriver is Voron-backed
* `virtual` is routing + default driver selection, not "persistent keyword"

### Example 2 — Transitional marker for Phase 2

```csharp
[Virtual]
[Persistent]  // uses Phase 1 attributes
public class Account
{
    public decimal Balance { get; set; }
}

var a = VKernel.New<Account>();
a.Balance += 50m;
VKernel.Flush(a);
```

### Example 3 — Restart survival

```csharp
var id = a.Vuid;     // conceptual identity view
Shutdown();

var a2 = VKernel.Get<Account>(id);
Console.Assert(a2.Balance == 50m);
```

### Example 4 — Transaction placeholder (future)

```csharp
using var tx = VKernel.BeginTransaction();
a.Balance += 10m;
b.Balance -= 10m;
tx.Commit();
```

> In Phase 2 this is conceptual only.
> Actual semantics are TBD and may begin as single-object atomicity only.

---

## 13) What remains TBD after Phase 2 (explicit)

Phase 2 will complete "durable memory", but these remain intentionally open:

* how "virtual" becomes first-class in the language (C= design)
* how driver selection is parameterized in code (syntax/grammar)
* multi-object transaction semantics
* placement + distribution + remote activation
* relational substrate as a first-class virtue
* versioning/time travel
* capability-based security enforcement

Phase 2 only lays the structural foundation for these by:

* keeping OpsRoot extensible
* making StorageDevice real
* validating "durable memory" as a kernel service

---

## 14) Phase 2 Exit Criteria ✅

Phase 2 is complete when:

1. StorageDevice (Voron) persists Body layer for virtual/routed objects
2. At least one persistence policy works (FlushPersist recommended)
3. Restart test is deterministic and automated
4. The design remains modular:

   * persistence is a driver, not welded into the kernel

---

*VAYRON R1 Phase 2 Implementation Plan - Advanced-Labs/DOTNExT*
