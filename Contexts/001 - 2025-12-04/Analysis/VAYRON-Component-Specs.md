# VAYRON Component Specifications

> **Document Type:** Technical Specifications
> **Version:** 1.0
> **Date:** 2025-12-07
> **Parent:** VAYRON-Architecture-Master.md
> **Status:** Design Phase

---

## 1. VCOM (VAYRON Component-Object Model)

### 1.1 Purpose

VCOM is the object model that gives VAYRON objects their identity, lifecycle, and capabilities. It sits between the VAYRON Kernel (grain types) and VARIA (developer surface).

### 1.2 Core Type: VObject

Every VCOM object inherits from VObject (or is transformed to behave as if it does).

```csharp
// Conceptual definition - actual implementation TBD
public abstract class VObject
{
    // === IDENTITY ===

    /// <summary>
    /// Universally unique identifier. Assigned at creation. Never changes.
    /// </summary>
    public Guid UUID { get; }

    /// <summary>
    /// Type information including code location, version, etc.
    /// </summary>
    public VTypeInfo VType { get; }

    // === LIFECYCLE ===

    /// <summary>
    /// Current activation state.
    /// </summary>
    public VObjectState State { get; }

    /// <summary>
    /// Called when object is activated (loaded into memory).
    /// </summary>
    protected virtual Task OnActivateAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when object is deactivating (leaving memory).
    /// </summary>
    protected virtual Task OnDeactivateAsync() => Task.CompletedTask;

    // === CODE ACCESS ===

    /// <summary>
    /// Access to this object's type's source code.
    /// </summary>
    public VCodeAccess Code { get; }

    // === RELATIONSHIPS ===

    /// <summary>
    /// Graph of relationships this object participates in.
    /// </summary>
    public IVRelationGraph Relations { get; }

    // === INTERNAL ===

    /// <summary>
    /// The grain backing this object. Never exposed to VARIA layer.
    /// </summary>
    internal IVCOMPodGrain BackingGrain { get; }
}

public enum VObjectState
{
    /// <summary>Object exists but is not in memory.</summary>
    Dormant,

    /// <summary>Object is currently activating.</summary>
    Activating,

    /// <summary>Object is active in memory.</summary>
    Active,

    /// <summary>Object is currently deactivating.</summary>
    Deactivating
}
```

### 1.3 VTypeInfo

Represents type metadata for a VCOM type.

```csharp
public class VTypeInfo
{
    /// <summary>UUID of the type itself (types are objects too).</summary>
    public Guid TypeUUID { get; }

    /// <summary>Fully qualified type name.</summary>
    public string FullName { get; }

    /// <summary>Assembly/module this type belongs to.</summary>
    public string ModuleName { get; }

    /// <summary>Version of the type definition.</summary>
    public VTypeVersion Version { get; }

    /// <summary>Location of source code in VAYRON persistence.</summary>
    public VCodeLocation CodeLocation { get; }

    /// <summary>Cached binary location (may be null if not compiled).</summary>
    public VBinaryLocation? BinaryCache { get; }

    /// <summary>Base type info (null for VObject itself).</summary>
    public VTypeInfo? BaseType { get; }

    /// <summary>Implemented interfaces.</summary>
    public IReadOnlyList<VTypeInfo> Interfaces { get; }
}
```

### 1.4 VCOM Resolution

The critical operation: given a UUID, return the live object.

```csharp
public static class VCOM
{
    /// <summary>
    /// Resolve a UUID to a live VObject.
    ///
    /// This is the fundamental operation that enables:
    /// - Async+ continuation (rehydrating references)
    /// - Relationship traversal
    /// - Lazy loading
    ///
    /// Resolution flow:
    /// 1. Check local cache (already activated?)
    /// 2. Ask VAYRON Kernel for grain location
    /// 3. Activate grain if needed
    /// 4. Return VARIA wrapper
    /// </summary>
    public static async Task<VObject> ResolveAsync(Guid uuid)
    {
        // Implementation uses VAYRON Kernel grain types
        throw new NotImplementedException();
    }

    /// <summary>
    /// Resolve with expected type.
    /// </summary>
    public static async Task<T> ResolveAsync<T>(Guid uuid) where T : VObject
    {
        var obj = await ResolveAsync(uuid);
        return (T)obj;  // VARIA wrapper handles the cast
    }

    /// <summary>
    /// Create a new VCOM object.
    ///
    /// This is what `new MyType()` becomes after codegen.
    /// </summary>
    public static async Task<T> CreateAsync<T>() where T : VObject, new()
    {
        // 1. Generate new UUID
        // 2. Determine VTypeInfo
        // 3. Activate grain via VAYRON Kernel
        // 4. Initialize object
        // 5. Return VARIA wrapper
        throw new NotImplementedException();
    }
}
```

### 1.5 Code-as-First-Class

VCOM objects "own" their code. This is stored via VAYRON Kernel.

```csharp
public class VCodeAccess
{
    /// <summary>
    /// The source code of this object's type.
    /// </summary>
    public string SourceCode { get; }

    /// <summary>
    /// Syntax tree (parsed, but not compiled).
    /// </summary>
    public SyntaxTree SyntaxTree { get; }

    /// <summary>
    /// Version history of this type's code.
    /// </summary>
    public IReadOnlyList<VCodeVersion> History { get; }

    /// <summary>
    /// Request a code mutation. Does not immediately apply.
    /// </summary>
    public VCodeMutationBuilder Mutate();

    /// <summary>
    /// Fork the type's code to create a new type.
    /// </summary>
    public VTypeForkBuilder Fork();
}
```

---

## 2. VAYRON Kernel

### 2.1 Purpose

The VAYRON Kernel is the set of grain types that provide infrastructure services. These are always loaded on every VAYRON Node.

### 2.2 VCOMPodGrain

The "runtime pod" that hosts VCOM object instances.

```csharp
/// <summary>
/// Hosts a VCOM object instance.
///
/// One grain = one VObject instance.
/// Grain key = VObject UUID (as string).
/// </summary>
public interface IVCOMPodGrain : IGrainWithStringKey
{
    // === LIFECYCLE ===

    /// <summary>
    /// Initialize the pod with type and initial state.
    /// Called on first activation (new object).
    /// </summary>
    Task InitializeAsync(VTypeInfo typeInfo, VObjectInitialState? initialState);

    /// <summary>
    /// Get current object state.
    /// </summary>
    Task<VObjectSnapshot> GetStateAsync();

    /// <summary>
    /// Update object state.
    /// </summary>
    Task SetStateAsync(VObjectStateDelta delta);

    // === METHOD INVOCATION ===

    /// <summary>
    /// Invoke a method on the hosted object.
    /// </summary>
    Task<VMethodResult> InvokeAsync(VMethodInvocation invocation);

    // === CODE ===

    /// <summary>
    /// Get the type info for this object.
    /// </summary>
    Task<VTypeInfo> GetTypeInfoAsync();

    /// <summary>
    /// Notify that type code has changed. Triggers recompilation.
    /// </summary>
    Task OnTypeCodeChangedAsync(VTypeVersion newVersion);
}
```

### 2.3 VTypeGrain

Manages VCOM type definitions and their code.

```csharp
/// <summary>
/// Manages a VCOM type definition.
///
/// One grain = one type (across all instances of that type).
/// Grain key = Type UUID (as string).
/// </summary>
public interface IVTypeGrain : IGrainWithStringKey
{
    // === TYPE INFO ===

    /// <summary>
    /// Get the current type info.
    /// </summary>
    Task<VTypeInfo> GetTypeInfoAsync();

    /// <summary>
    /// Get the current source code.
    /// </summary>
    Task<string> GetSourceCodeAsync();

    // === COMPILATION ===

    /// <summary>
    /// Ensure the type is compiled. Returns binary location.
    /// </summary>
    Task<VBinaryLocation> EnsureCompiledAsync();

    /// <summary>
    /// Force recompilation (e.g., after code change).
    /// </summary>
    Task<VBinaryLocation> RecompileAsync();

    // === CODE MUTATION ===

    /// <summary>
    /// Apply a code mutation to this type.
    /// </summary>
    Task<VCodeMutationResult> ApplyMutationAsync(VCodeMutation mutation);

    /// <summary>
    /// Create a fork of this type.
    /// </summary>
    Task<VTypeInfo> ForkAsync(VTypeForkRequest request);

    // === INSTANCES ===

    /// <summary>
    /// Register a new instance of this type.
    /// </summary>
    Task RegisterInstanceAsync(Guid instanceUUID);

    /// <summary>
    /// Get all known instances of this type.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetInstancesAsync();
}
```

### 2.4 VNamespaceGrain

Provides VNS resolution services.

```csharp
/// <summary>
/// VNS namespace resolver.
///
/// Grain key = namespace path (e.g., "MyApp.Orders").
/// Root namespace grain key = "".
/// </summary>
public interface IVNamespaceGrain : IGrainWithStringKey
{
    // === RESOLUTION ===

    /// <summary>
    /// Find objects by name in this namespace.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindByNameAsync(string name);

    /// <summary>
    /// Find objects by query.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindByQueryAsync(VNSQuery query);

    /// <summary>
    /// Find objects by semantic similarity.
    /// </summary>
    Task<IReadOnlyList<VNSSemanticResult>> FindBySemanticAsync(string naturalLanguage);

    // === REGISTRATION ===

    /// <summary>
    /// Register an object in this namespace.
    /// </summary>
    Task RegisterAsync(Guid uuid, VNSRegistration registration);

    /// <summary>
    /// Unregister an object from this namespace.
    /// </summary>
    Task UnregisterAsync(Guid uuid);

    // === HIERARCHY ===

    /// <summary>
    /// Get child namespaces.
    /// </summary>
    Task<IReadOnlyList<string>> GetChildNamespacesAsync();

    /// <summary>
    /// Get types defined in this namespace.
    /// </summary>
    Task<IReadOnlyList<VTypeInfo>> GetTypesAsync();
}
```

### 2.5 VCompilerGrain

Runtime compilation service.

```csharp
/// <summary>
/// Runtime compilation service.
///
/// Singleton grain (key = "compiler").
/// </summary>
public interface IVCompilerGrain : IGrainWithStringKey
{
    /// <summary>
    /// Compile source code to binary.
    /// </summary>
    Task<VCompilationResult> CompileAsync(VCompilationRequest request);

    /// <summary>
    /// Compile with incremental changes.
    /// </summary>
    Task<VCompilationResult> CompileIncrementalAsync(
        VBinaryLocation baseline,
        VCodeDelta delta);

    /// <summary>
    /// Validate source code without producing binary.
    /// </summary>
    Task<VValidationResult> ValidateAsync(string sourceCode);
}
```

---

## 3. VNS (Virtual Name System)

### 3.1 Purpose

VNS provides human-friendly discovery and addressing. It's the "DNS for objects."

### 3.2 Address Formats

```
// Named addressing
vayron://Orders/ORD-123
vayron://Customers/C-456

// Namespace addressing
vayron://MyApp.Sales/Orders
vayron://MyApp.Sales/Customers

// Query addressing
vayron://Orders?status=pending&customer=C-456

// Semantic addressing (natural language)
vayron://?"pending orders from last week"
```

### 3.3 Resolution API

```csharp
public static class VNS
{
    /// <summary>
    /// Find objects by VNS address.
    /// </summary>
    public static async Task<IReadOnlyList<T>> FindAsync<T>(string address)
        where T : VObject
    {
        // Parse address
        // Route to appropriate VNamespaceGrain
        // Resolve to UUIDs
        // VCOM.Resolve each UUID
        // Return VARIA wrappers
        throw new NotImplementedException();
    }

    /// <summary>
    /// Find single object (throws if not exactly one).
    /// </summary>
    public static async Task<T> FindOneAsync<T>(string address)
        where T : VObject
    {
        var results = await FindAsync<T>(address);
        if (results.Count != 1)
            throw new VNSResolutionException($"Expected 1 result, got {results.Count}");
        return results[0];
    }

    /// <summary>
    /// Semantic search.
    /// </summary>
    public static async Task<IReadOnlyList<T>> SearchAsync<T>(string naturalLanguage)
        where T : VObject
    {
        // Route to VNamespaceGrain.FindBySemanticAsync
        // Apply type filter
        // Resolve and return
        throw new NotImplementedException();
    }
}
```

### 3.4 IDE Integration

VNS must integrate with IDE for IntelliSense:

```csharp
// In IDE, when developer types:
var order = vayron.Find<Order>("

// IDE queries VNS for Order instances/addresses:
// - "ORD-123"
// - "ORD-456"
// - Suggests query patterns

// When developer types:
dynamic x = vayron.Find("

// IDE queries VNS for any addressable object:
// - Shows types
// - Shows named instances
// - Suggests semantic search
```

---

## 4. VARIA

### 4.1 Purpose

VARIA makes VCOM objects feel like regular C# objects. It's the developer surface layer.

### 4.2 Transformation

Developer writes:
```csharp
public class Order
{
    public Customer Customer { get; set; }
    public List<OrderItem> Items { get; set; }
    public OrderStatus Status { get; set; }

    public async Task Submit()
    {
        Status = OrderStatus.Submitted;
        await NotifyCustomer();
    }
}
```

VARIA codegen transforms to (conceptual):
```csharp
public class Order : VObject  // or wrapped in proxy
{
    // Property becomes VCOM-aware
    public Customer Customer
    {
        get => VCOM.ResolveAsync<Customer>(_customerUuid).GetAwaiter().GetResult();
        set => SetRelationship(nameof(Customer), value.UUID);
    }

    // Collection becomes VCOM collection
    public VCOMList<OrderItem> Items { get; }

    // Value type works normally but persists
    public OrderStatus Status
    {
        get => GetState<OrderStatus>(nameof(Status));
        set => SetState(nameof(Status), value);
    }

    // Method becomes grain invocation
    public async Task Submit()
    {
        await BackingGrain.InvokeAsync(new VMethodInvocation
        {
            MethodName = nameof(Submit),
            Arguments = Array.Empty<object>()
        });
    }
}
```

### 4.3 new() Transformation

```csharp
// Developer writes:
var order = new Order();

// VARIA transforms to:
var order = await VCOM.CreateAsync<Order>();

// Or with UUID (retrieval):
var order = new Order(existingUuid);

// Transforms to:
var order = await VCOM.ResolveAsync<Order>(existingUuid);
```

### 4.4 Codegen Strategy

Options for VARIA implementation:

| Approach | Pros | Cons |
|----------|------|------|
| Source Generator | Standard C# tooling, IDE support | Limited transformation capability |
| Roslyn Analyzer + Fixer | Can offer fixes | Not automatic |
| IL Weaving (Fody-style) | Full transformation | Post-compile, complex |
| Custom Compiler (Roslyn fork) | Maximum control | We already have this! |

**Recommended:** Use our Roslyn fork for full transformation capability. We already have it.

---

## 5. VAYRON SDK

### 5.1 Project Template Structure

```
MyVayronApp/
├── MyVayronApp.csproj          # VAYRON project file
├── Program.cs                   # Entry point
├── vayron.config.json          # VAYRON configuration
├── Types/
│   ├── Order.cs                # VCOM types
│   └── Customer.cs
└── Properties/
    └── launchSettings.json     # VS launch settings
```

### 5.2 Project File

```xml
<Project Sdk="VAYRON.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <VayronVersion>1.0.0</VayronVersion>
  </PropertyGroup>

  <!-- VAYRON SDK brings in everything needed -->
  <!-- No explicit NewOrleans, VCOM, etc. references -->

</Project>
```

### 5.3 Entry Point

```csharp
// Minimal entry point
using VAYRON;

await VayronHost.RunAsync(args);

// Or with configuration
await VayronHost.CreateBuilder(args)
    .ConfigureNode(node =>
    {
        node.Name = "MyNode";
        // No Orleans config visible
    })
    .Build()
    .RunAsync();
```

### 5.4 VS2022 Integration Components

| Component | Purpose |
|-----------|---------|
| Project System | .vayronproj support (or extend .csproj) |
| Item Templates | VCOM Type, VARIA Controller, etc. |
| IntelliSense Provider | VNS-aware completion |
| Debugger Visualizer | VCOM object visualization |
| Property Pages | VAYRON project configuration |
| Analyzer | VARIA transformation warnings/info |

---

## 6. Async+ Integration (Deferred)

### 6.1 How It Will Work (When Implemented)

```csharp
// Developer writes:
public async Task ProcessOrder(Order order)
{
    var customer = await GetCustomer(order.CustomerId);

    // Long operation - state machine may hibernate
    await LongRunningOperation();

    // References still valid after hibernation
    await Ship(order, customer.Address);
}

// State machine persists:
{
    "_state": 2,  // Continuation point
    "_order_uuid": "...",      // UUID, not object
    "_customer_uuid": "..."    // UUID, not object
}

// On resume, codegen rehydrates:
var order = await VCOM.ResolveAsync<Order>(_order_uuid);
var customer = await VCOM.ResolveAsync<Customer>(_customer_uuid);
```

### 6.2 Dependencies (Why Deferred)

| Dependency | Reason |
|------------|--------|
| VCOM.ResolveAsync | Must exist to rehydrate references |
| VCOMPodGrain | Must host the objects |
| VARIA wrappers | Must return correct types |

**All of these must exist before Async+ continuation works.**

---

## 7. Persistence Architecture

### 7.1 Store Responsibilities

| Store | What It Stores | When Used |
|-------|----------------|-----------|
| RavenDB | Object state, type definitions, code | Primary persistence |
| Neo4j/AuraDB | Relationships, type hierarchy, semantic index | Graph queries, VNS |
| File System | Binary cache, bootstrap config | Local node startup |
| Memory | Active grains, hot cache | Runtime |

### 7.2 Persistence Flow

```
VObject.SetProperty(value)
    │
    ├── VCOM records change
    │
    ├── VCOMPodGrain state dirty
    │
    ├── Orleans state persistence (automatic)
    │
    └── RavenDB write (via Orleans storage provider)

Relationship Change:
    │
    ├── VCOM records relationship
    │
    ├── VCOMPodGrain state includes relationship UUIDs
    │
    ├── Orleans state persistence
    │
    └── Neo4j write (via custom observer or storage)
```

---

## 8. Open Questions

### 8.1 VARIA Implementation

**Question:** Source generator, IL weaving, or Roslyn fork for VARIA transformation?

**Current thinking:** Roslyn fork (we have it). Maximum control.

### 8.2 VNS Scope

**Question:** Start with local process VNS or distributed from day one?

**Recommendation:** Local process first. Distributed is just grain placement, which Orleans handles.

### 8.3 Async Initialization

**Question:** `new()` transformation returns VObject, but VCOM.CreateAsync is async. How to handle?

**Options:**
1. Require `await new Order()` (C# doesn't support this syntax)
2. Use factory: `var order = await Order.CreateAsync()`
3. Return lazy wrapper, first access triggers async init
4. Roslyn transforms all construction sites

**Recommendation:** Option 4 - Roslyn transformation. We control the compiler.

---

*This document provides technical specifications for VAYRON components. Update as implementation progresses.*

*Version 1.0 - 2025-12-07*
