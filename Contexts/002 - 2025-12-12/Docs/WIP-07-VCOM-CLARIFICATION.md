# WIP-07: VCOM (Virtual Component-Object Model) Clarification

> **Document Type:** Work In Progress - Architectural Clarification
> **Version:** 0.1
> **Date:** 2025-12-15
> **Status:** EXPLORATORY - Design direction established, details in flux
> **Context:** Clarification from Louis on VCOM's nature and capabilities

---

## 1. Executive Summary

**VCOM** (Virtual Component-Object Model) is an **exploratory but necessary** component of the DOTNExT platform. While the exact form is not solidified, the design objectives are clear:

**Key insight from Louis:**

> "In which exact form we don't know but designing and implementing a Component-Object Model for our platform seems to be necessary, or at least viable... VCOM is not just a Component-Object Model, but a Virtual one."

**VCOM is:**
- A **Virtual** Component-Object Model
- Designed to **wrap around** other COMs
- Able to **wrap distribution library binaries** and possibly **package systems**
- Closely related to **VTS, VNS, Memantics/Engrams**
- Using **Memantic Metadata** like everything else in the platform

---

## 2. VCOM Design Objectives

### 2.1 Wrapping Other Component Models

**Louis's guidance:**

> "One design objective will be for VCOM to be able to wrap around other Component-Object Models, Distribution 'Library' Binaries, possibly even package systems although this likely may seem to you unintuitive how/why/etc."

VCOM should be able to wrap:

| What | Why |
|------|-----|
| **COM (Windows)** | Interop with existing Windows components |
| **D-Bus objects (Linux)** | Linux service integration |
| **gRPC services** | Modern distributed services |
| **REST APIs** | Web services |
| **Package systems (NuGet, npm, pip)** | Unified package discovery/loading |
| **Distribution binaries** | Native libraries, DLLs |

### 2.2 Bidirectional Encoding

**Louis's guidance:**

> "VCOM could be designed to be both able to wrap others as well as being able to be encoded into those, and possibly sometime both and in an integrated way."

VCOM has two modes:

```
Mode A: VCOM Wrapping Others
┌────────────────────────────────────────┐
│  VCOM Layer                             │
│  (Universal interface)                  │
├────────────────────────────────────────┤
    ↓           ↓           ↓
┌──────┐   ┌──────┐   ┌──────┐
│ COM  │   │ D-Bus│   │ gRPC │
└──────┘   └──────┘   └──────┘

Mode B: VCOM Encoded Into Others
┌──────┐   ┌──────┐   ┌──────┐
│ COM  │   │ D-Bus│   │ gRPC │
├──────┼───┼──────┼───┼──────┤
│  ↑ VCOM exposed as COM/D-Bus/gRPC ↑   │
└──────────────────────────────────────┘
```

### 2.3 Memantic Integration

VCOM would adopt the same Memantic Metadata as everything else:

**Louis's guidance:**

> "It will adopt the same type/object/relation/etc metadata - necessary for all of our platform paradigms - the rest of our platform will adopt, including our VCR, and its MMS (Memantic Memory System) and its VEE, and the VTS and the VNS."

Every VCOM object has:
- **Memantic UUID** (unique identity)
- **Memantic names/namespaces** (VNS integration)
- **Memantic relations** (graph connectivity)
- **Semantic embeddings** (AI/search)
- **Security metadata** (VSS integration)

---

## 3. VCOM vs VMOM

**Louis's clarification:**

> "VCOM is VCOM and not VMOM/MOM (Memantic Object-Model) because our COM is meant to work with all others (and beyond..) which aren't all using Memantic paradigms, obviously. But that may be not a good point if VCOM ends up adding to all of which it wraps and/or infiltrate 'Memantism' .. to make these compatible to the 'Memantic Universe' of our platform. Either way, lets keeps VCOM as VCOM for now... but yes, VCOM is not just a Virtual COM but also a Memantic one, as well as a Distributed one, etc."

VCOM is:
- **V**irtual (abstraction layer)
- **Memantic** (carries Memantic Metadata)
- **Distributed** (location transparent)
- **Universal** (wraps anything)

---

## 4. VCOM Architecture

### 4.1 Conceptual Stack

```
┌─────────────────────────────────────────────────────────────────┐
│  Application Layer                                               │
│  Developer code using VCOM objects                               │
├─────────────────────────────────────────────────────────────────┤
│  VCOM Layer                                                      │
│  ├── VObject base (UUID, VType, Relations)                      │
│  ├── VCOM Proxies (wrap/expose)                                 │
│  ├── Memantic Metadata integration                              │
│  └── VNS registration                                           │
├─────────────────────────────────────────────────────────────────┤
│  Wrapper/Adapter Layer                                           │
│  ├── COM Wrapper                                                │
│  ├── D-Bus Wrapper                                              │
│  ├── gRPC Wrapper                                               │
│  ├── Package Wrapper (NuGet, npm, pip)                          │
│  └── Native Binary Wrapper                                      │
├─────────────────────────────────────────────────────────────────┤
│  Underlying Systems                                              │
│  COM, D-Bus, gRPC, File system, etc.                            │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 VObject (VCOM Base)

As previously defined but with clarification:

```csharp
public abstract class VObject
{
    // === Memantic Identity ===
    public MemanticMetadata Metadata { get; }
    // Includes: UUID, Names, Relations, Embedding, Security

    // === Type Reference (VTS) ===
    public VTSTypeRef VType => Metadata.VType;

    // === Convenience Accessors ===
    public Guid UUID => Metadata.UUID;
    public VRelations Relations => new VRelations(Metadata.Relations);

    // === Lifecycle ===
    protected virtual void OnActivate() { }
    protected virtual void OnDeactivate() { }
}
```

### 4.3 VCOM Proxy

For wrapping external objects:

```csharp
public class VCOMProxy : VObject, IDynamicMetaObjectProvider
{
    private readonly IVCOMWrapper _wrapper;
    private readonly object _target;

    // Dynamic dispatch to wrapped object
    public DynamicMetaObject GetMetaObject(Expression parameter)
    {
        return new VCOMDynamicMetaObject(parameter, this);
    }

    // Forward calls through wrapper
    public object Invoke(string methodName, object[] args)
    {
        // 1. Security check (VSS)
        // 2. Log/audit
        // 3. Forward to wrapper
        return _wrapper.Invoke(_target, methodName, args);
    }
}
```

---

## 5. VCOM and VNS

### 5.1 VNS Registration

Every VCOM object can register with VNS:

```
VNS Address: vayron://MyApp/Components/MyComponent

VCOM Object:
├── UUID: "abc-123..."
├── Names: ["MyComponent", "MyApp.Components.MyComponent"]
├── VNS Address: vayron://MyApp/Components/MyComponent
└── Wrapped: COM object at CLSID {...}
```

### 5.2 Unified Discovery

VNS can discover VCOM objects regardless of what they wrap:

```csharp
// Find a component - might be VCOM-native, might wrap COM/gRPC/etc.
var component = await VNS.Find("MyApp/Components/PaymentProcessor");

// Use it - VCOM handles the wrapping
await component.ProcessPayment(order);
```

---

## 6. VCOM and Package Systems

### 6.1 Why Package Wrapping?

**Unintuitive but powerful:** VCOM can treat packages as component sources:

```
NuGet Package "MyLib.Payments"
    │
    ▼ (VCOM Package Wrapper)
    │
VCOM Registry Entry:
├── Package: "MyLib.Payments@1.2.3"
├── Types: [PaymentProcessor, Transaction, ...]
├── VNS Registration: vayron://Packages/NuGet/MyLib.Payments/...
└── Dependencies: [MyLib.Core, ...]
```

### 6.2 Unified Package Discovery

```csharp
// Discover types across package systems
var processors = await VNS.Discover(
    semantic: "payment processing components",
    sources: [PackageSource.NuGet, PackageSource.NPM]
);

// VCOM wraps whatever is found
foreach (var processor in processors)
{
    // Processor might be from NuGet (.NET), NPM (JS), etc.
    // VCOM provides unified interface
}
```

---

## 7. VCOM Wrapping Strategies

### 7.1 Static Wrapping (Compile-time)

```csharp
// Generator creates wrapper at compile time
[VCOMWrap(typeof(SomeCOMObject))]
public partial class WrappedCOMObject : VObject
{
    // Generated: property/method forwarding
}
```

### 7.2 Dynamic Wrapping (Runtime)

```csharp
// Runtime wrapping of discovered component
var wrapped = VCOM.Wrap(comObject);
// wrapped is now a VObject with Memantic Metadata
```

### 7.3 Projection (Exposing VCOM as Other)

```csharp
// Expose VCOM object as COM
var comProjection = myVObject.ProjectAs<COMProjection>();

// Expose VCOM object as gRPC service
var grpcService = myVObject.ProjectAs<GrpcService>();
```

---

## 8. VCOM Status: What's Decided vs Exploratory

### 8.1 DECIDED

| Aspect | Decision |
|--------|----------|
| **Name** | VCOM (Virtual Component-Object Model) |
| **Purpose** | Universal component wrapping |
| **Metadata** | Uses Memantic Metadata |
| **VNS Integration** | Full integration |
| **Wrapping capability** | Core design objective |

### 8.2 EXPLORATORY

| Aspect | Options |
|--------|---------|
| **Exact form** | Interface-based? Proxy-based? Both? |
| **Wrapper implementations** | Which systems first? |
| **Package integration** | Depth of package system integration |
| **Projection mechanisms** | How to expose VCOM as other models |
| **Performance** | Overhead acceptable? Optimization strategies? |

---

## 9. Relation to Other Platform Components

| Component | VCOM Relationship |
|-----------|-------------------|
| **VTS** | VCOM objects have VTS types |
| **VNS** | VCOM objects register with VNS |
| **MMS** | VCOM objects stored via MMS |
| **VEE** | VCOM objects executed via VEE |
| **VSS** | VCOM access controlled via VSS |
| **Engrams** | VCOM objects can be Engrams |

---

## 10. Implementation Phases

### Phase 1: Core VObject
- [ ] VObject with Memantic Metadata
- [ ] Basic VCOM proxy
- [ ] VNS registration

### Phase 2: First Wrappers
- [ ] COM wrapper (Windows)
- [ ] gRPC wrapper
- [ ] Dynamic wrapping

### Phase 3: Package Integration
- [ ] NuGet package discovery
- [ ] Type extraction from packages
- [ ] VNS registration of package types

### Phase 4: Projections
- [ ] VCOM → COM projection
- [ ] VCOM → gRPC projection
- [ ] Bidirectional operation

---

## 11. Open Questions

### Design Questions
1. Standard wrapper interface?
2. Type mapping complexity handling?
3. Versioning across wrapped systems?
4. Error propagation and handling?

### Implementation Questions
5. Which wrappers are priority?
6. Performance measurement and targets?
7. Testing strategy for wrappers?
8. Documentation for wrapper authors?

---

## 12. Related Documents

| Document | Relationship |
|----------|--------------|
| WIP-05-VIRTUAL-TYPE-SYSTEM.md | VTS types for VCOM |
| WIP-06-MEMANTIC-METADATA.md | VCOM uses Memantic Metadata |
| 02-CONSOLIDATED-VISION.md | VCOM in architecture |
| VAYRON-Component-Specs.md | Original VObject specs |

---

*This document clarifies VCOM's nature as an exploratory but necessary component that wraps other component models, distribution binaries, and potentially package systems, while being fully integrated with the Memantic paradigm.*

*Version 0.1 - 2025-12-15 - Clarification based on Louis's guidance*
