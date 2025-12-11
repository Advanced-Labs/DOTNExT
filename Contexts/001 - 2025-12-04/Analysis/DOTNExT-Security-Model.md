# DOTNExT Security Model

> **Document Type:** Architecture Design (Stub)
> **Version:** 0.2
> **Date:** 2025-12-11
> **Status:** STUB - Questions and outline for security architecture
> **Prerequisite Reading:** DOTNExT-Singularity-Midori-Research.md, DOTNExT-Process-Model.md
> **Key Update v0.2:** Reframed from Midori-style compile-time CBS to VOS pluggable security subsystem with runtime enforcement.

---

## 1. Purpose

This document will define the security model for DOTNExT - how capabilities are managed, how trust is established, and how access control works in the VOS.

---

## 2. DOTNExT Security Philosophy (Different from Midori)

### 2.1 VOS Pluggable Security Subsystem

**Midori approach:** Capability-based security baked into type system. Compile-time enforcement. ~Zero runtime cost.

**DOTNExT approach:** Security as VOS pluggable subsystem.
- Multiple security models available (CBS, RBAC, crypto, ZK, etc.)
- Runtime enforcement with variable cost
- Pluggable via drivers
- Can tap into underlying OS security, classical infras/servers/services
- Security can be enabled/disabled per execution context

### 2.2 Dynamic Over Static

DOTNExT values dynamism highly:
- Dynamic capability granting (for AI adaptability)
- Runtime flexibility over compile-time guarantees
- Security models can be switched/combined at runtime
- Not "by-construction" but "by-enforcement"

### 2.3 Variable Cost Model

| Execution Context | Security Level | Cost |
|-------------------|----------------|------|
| Max compatibility (.NET interop) | Security ignored | Zero |
| Trusted internal | Minimal checks | Low |
| Standard | Normal enforcement | Medium |
| High security | Every check enforced | High |
| Paranoid | Multiple models active | Very High |

**Trade-off accepted:** 100-10000x more expensive per check than Midori. Acceptable because:
- AI is the bottleneck (security overhead negligible comparatively)
- Security can be dialed down when not needed
- Optimization spectrum reduces many checks to zero cost

### 2.4 Security Optimization Spectrum

| Level | Example | Cost |
|-------|---------|------|
| **Compile-time resolved** | "Code X in namespace System always has DateTime access" → no check | Zero |
| **Compile-time error** | "Code Y tries DateTime access without rights" → rejected at compile | Zero (prevented) |
| **JIT-resolved once** | "Predicate P evaluated at JIT, result baked into code" | Near-zero |
| **Runtime cached** | "First check evaluates, result cached" | First call, then cheap |
| **Runtime every time** | "Dynamic predicate evaluated each access" | Full cost |

---

## 3. Core Concepts (Plain English)

### 3.1 Capability

**Simple:** A key/token that lets you do something. Having the object IS the permission.

```csharp
// Ambient authority (bad for control):
var now = DateTime.Now;  // Anyone can call

// Capability-based (good for control):
void DoWork(IClock clock) { var now = clock.Now; }  // Must be given clock
```

### 3.2 Ambient Authority

**Simple:** Stuff accessible just because you exist, without explicit permission.

**Examples:** `DateTime.Now`, `File.ReadAllText(...)`, `Console.WriteLine(...)`

**Problem:** Can't control/sandbox code that has ambient access to everything.

**DOTNExT stance:** Can't ban ambient authority (.NET compatibility), but:
- Design DOTNExT APIs to receive capabilities explicitly
- VOS security can intercept ambient calls when enabled
- AI/runtime controls what capabilities Pathways receive

---

## 4. Key Questions to Answer

### 4.1 Capability Model
- [ ] What are the fundamental capability types?
- [ ] How are capabilities represented? (Objects? Tokens?)
- [ ] How are capabilities passed to Processes?
- [ ] Can capabilities be revoked?
- [ ] Capability composition (fine-grained → coarse)?

### 3.2 Trust Model
- [ ] What are trust levels? (Domain, Federation, Confederation, Public)
- [ ] How is trust established between nodes?
- [ ] How does trust affect capability granting?
- [ ] Dynamic trust changes?

### 3.3 Process Security
- [ ] What capabilities does a Process start with?
- [ ] How do child Processes inherit capabilities?
- [ ] Process isolation guarantees?
- [ ] Privilege escalation prevention?

### 3.4 Distributed Security
- [ ] How do capabilities work across nodes?
- [ ] Cross-node capability validation?
- [ ] Cryptographic capability tokens?
- [ ] Zero-trust / zero-knowledge options?

### 3.5 Cryptographic Integration
- [ ] End-to-end encryption?
- [ ] Capability tokens with crypto signatures?
- [ ] Key management?
- [ ] "Talk same encryption = understand, else meaningless" model?

### 3.6 Access Modifier Evolution
- [ ] How do `public/private/internal/protected` evolve?
- [ ] Capability-based modifiers?
- [ ] Language integration?

### 3.7 Audit and Compliance
- [ ] Capability usage logging?
- [ ] Security event auditing?
- [ ] Compliance reporting?

---

## 4. Capability Types to Define

```
Resource Capabilities:
├── CpuCapability (computation budget)
├── MemoryCapability (allocation limit)
├── IoCapability (I/O bandwidth)
└── StorageCapability (persistence access)

Access Capabilities:
├── FileSystemCapability (path, read/write)
├── NetworkCapability (endpoints, protocols)
├── VcomCapability (which grains accessible)
└── ProcessCapability (spawn, control)

Special Capabilities:
├── MigrationCapability (can migrate)
├── CheckpointCapability (can checkpoint)
├── ReflectionCapability (runtime introspection)
└── KernelCapability (privileged operations)
```

---

## 5. Design Considerations from Research

### From Midori
- Objects as capabilities (unforgeable tokens)
- No mutable statics (eliminate ambient authority)
- Revocable capabilities (wrapper pattern)
- Remote capabilities (async dispatch)

### From Singularity
- Manifest-based (capabilities declared at compile time)
- Sealed processes (no dynamic capability acquisition)
- Channel endpoints as capabilities

### DOTNExT Specific
- Dynamic capability granting (AI adaptability)
- Distributed capability validation
- Trust-level-based capability depth

---

## 6. Outline (To Be Developed)

1. Security Philosophy
2. Capability Model
3. Trust Model and Levels
4. Process Security Boundaries
5. Capability Types Catalog
6. Capability Lifecycle (grant, transfer, revoke)
7. Distributed Security
8. Cryptographic Integration
9. Language Integration
10. Audit and Monitoring
11. Implementation Phases

---

## 7. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Singularity-Midori-Research.md | Security model inspiration |
| DOTNExT-Process-Model.md | Process security boundaries |
| DOTNExT-Distribution-Levels.md | Trust levels and distribution |
| DOTNExT-VOS-Architecture.md | Security as OS subsystem |

---

---

## 8. Gen-1 Design Considerations

### 8.1 Security Hook Points

For runtime security enforcement, the execution model needs interception points:
- Method calls (check: can caller invoke this?)
- Object access (check: can code touch this object?)
- Resource access (check: can code use this capability?)

**For gen-1:** Ensure Pathways/Scheduler design includes these hook points. Can be no-ops initially, but interception points must exist to avoid retrofitting later.

### 8.2 .NET Interop Modes

| Mode | Security | Use Case |
|------|----------|----------|
| Max compatibility | Disabled | Running unmodified .NET code |
| Wrapped | Intercept + check | .NET code under DOTNExT security |
| Sandboxed | Restricted Pathway | Untrusted .NET code |

---

*Stub document - to be expanded with security model details.*

*Version 0.2 - 2025-12-11 - Reframed to VOS pluggable security; added philosophy, cost model, optimization spectrum*

*Version 0.1 - 2025-12-10 - Initial stub with questions*
