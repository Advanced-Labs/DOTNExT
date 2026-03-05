# NewOrleans Relational Framework

## Design Specification

**Version:** 1.0  
**Date:** January 2026  
**Status:** Draft

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Vision & Goals](#vision--goals)
3. [Architecture Overview](#architecture-overview)
4. [Core Abstractions](#core-abstractions)
   - [Relation Types (Association Classes)](#relation-types-association-classes)
   - [Relational Drivers](#relational-drivers)
   - [Relational Engine](#relational-engine)
5. [Domain Drivers](#domain-drivers)
   - [Security Driver](#security-driver)
   - [Eventing Driver](#eventing-driver)
6. [Query Layer](#query-layer)
   - [LINQ Support](#linq-support)
   - [Cypher Translation](#cypher-translation)
7. [Distribution Layer](#distribution-layer)
   - [NewOrleans Integration](#neworleans-integration)
8. [Persistence Layer](#persistence-layer)
   - [Neo4j Provider](#neo4j-provider)
9. [Usage Examples](#usage-examples)
10. [Benefits & Capabilities](#benefits--capabilities)
11. [Open Questions](#open-questions)
12. [Appendix: Foundational Concepts](#appendix-foundational-concepts)

---

## Executive Summary

The NewOrleans Relational Framework introduces first-class relationships as a core primitive for distributed actor systems. Rather than treating relationships as simple references between objects, this framework elevates them to typed, attributed entities that carry semantic meaning—capabilities, event routing rules, ownership, and more.

**Key Components:**

- **Relation Types** — Association classes where relationships themselves have properties and behavior
- **Relational Drivers** — Domain-specific paradigms (security, eventing, workflow) built on a common engine
- **Relational Engine** — Core graph operations, traversal, and query capabilities (built on QuikGraph concepts)
- **LINQ Provider** — Query relations like collections in C#
- **Cypher Translator** — Bidirectional Neo4j compatibility for persistence and external queries
- **NewOrleans Extension** — Distributed graph coordination across Orleans silos

---

## Vision & Goals

### The Core Insight

In traditional OOP, relationships are implicit—a reference from A to B. But often the relationship itself has properties that don't belong to either participant:

- **Employment** between Person and Company has `startDate`, `jobTitle`, `salary`
- **Subscription** between User and Channel has `since`, `notificationPreferences`, `tier`
- **Permission** between Principal and Resource has `level`, `grantedAt`, `expiresAt`

These are properties of the *relationship*, not the participants.

### Goals

1. **Relationships as First-Class Citizens** — Relationships have identity, attributes, and behavior
2. **Domain Separation** — Different relationship domains (security, eventing) have specialized drivers
3. **Unified Query Model** — LINQ for C#, Cypher for complex graph queries and external tools
4. **Distribution-Aware** — Relationships can span silos with location-transparent operations
5. **Semantic Computing** — Relationships carry meaning (capabilities, event routes, affinity hints)

### Non-Goals

- Replacing Orleans' grain reference system
- General-purpose graph database functionality
- Real-time graph analytics at scale

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Domain Layer                                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐     │
│  │ Security Driver │  │ Eventing Driver │  │ Workflow Driver │ ... │
│  │  CapabilityRel  │  │ SubscriptionRel │  │ DependencyRel   │     │
│  │  OwnershipRel   │  │ PropagationRel  │  │ SequenceRel     │     │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘     │
│           │                    │                    │               │
│           └────────────────────┼────────────────────┘               │
│                                ▼                                    │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    Relational Engine                         │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │   │
│  │  │ Graph Core  │  │ LINQ Provider│  │ Cypher Translator   │  │   │
│  │  │ (QuikGraph+)│  │             │  │ (Neo4j compatible)  │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────────────┘  │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Distribution Layer                               │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                 NewOrleans Extension                         │   │
│  │  ┌──────────────┐  ┌───────────────┐  ┌─────────────────┐   │   │
│  │  │ Silo-Local   │  │ Cross-Silo    │  │ Consistency     │   │   │
│  │  │ Graph Index  │  │ Coordination  │  │ Protocol        │   │   │
│  │  └──────────────┘  └───────────────┘  └─────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Persistence Layer                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │   Neo4j      │  │  In-Memory   │  │   Other      │              │
│  │   Provider   │  │  (Testing)   │  │   Stores     │              │
│  └──────────────┘  └──────────────┘  └──────────────┘              │
└─────────────────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Responsibility |
|-------|----------------|
| **Domain Layer** | Domain-specific relation types, rules, and operations |
| **Relational Engine** | Core graph operations, traversal, query translation |
| **Distribution Layer** | Cross-silo coordination, consistency, local indexing |
| **Persistence Layer** | Durable storage, external query support |

---

## Core Abstractions

### Relation Types (Association Classes)

The fundamental abstraction: relationships as first-class objects with identity, attributes, and behavior.

#### Base Interfaces

```csharp
/// <summary>
/// Base interface for all relation types.
/// Represents a typed, attributed edge between two vertices.
/// </summary>
public interface IRelation
{
    /// <summary>Unique identifier for this relation instance.</summary>
    RelationId Id { get; }
    
    /// <summary>Source vertex of the relation.</summary>
    VertexId Source { get; }
    
    /// <summary>Target vertex of the relation.</summary>
    VertexId Target { get; }
    
    /// <summary>The kind/type of this relation.</summary>
    RelationKind Kind { get; }
    
    /// <summary>When this relation was established.</summary>
    DateTime CreatedAt { get; }
    
    /// <summary>Who/what established this relation.</summary>
    VertexId CreatedBy { get; }
    
    /// <summary>The driver that manages this relation type.</summary>
    IRelationalDriver Driver { get; }
}

/// <summary>
/// Strongly-typed relation between specific vertex types.
/// </summary>
public interface IRelation<TSource, TTarget> : IRelation
    where TSource : IVertex
    where TTarget : IVertex
{
    /// <summary>Strongly-typed source vertex.</summary>
    new TSource Source { get; }
    
    /// <summary>Strongly-typed target vertex.</summary>
    new TTarget Target { get; }
}
```

#### Base Implementation

```csharp
/// <summary>
/// Abstract base class for relation types.
/// Provides lifecycle hooks and driver collaboration.
/// </summary>
public abstract class Relation<TSource, TTarget> : IRelation<TSource, TTarget>
    where TSource : IVertex
    where TTarget : IVertex
{
    public RelationId Id { get; }
    public TSource Source { get; }
    public TTarget Target { get; }
    public abstract RelationKind Kind { get; }
    
    public DateTime CreatedAt { get; }
    public VertexId CreatedBy { get; init; }
    
    // Collaborates with driver for operations
    protected IRelationalDriver Driver { get; }
    
    // ═══════════════════════════════════════════════════════════
    // Lifecycle Hooks - Driver calls these at appropriate times
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Called before the relation is persisted.</summary>
    protected virtual Task OnEstablishing() => Task.CompletedTask;
    
    /// <summary>Called after the relation is successfully established.</summary>
    protected virtual Task OnEstablished() => Task.CompletedTask;
    
    /// <summary>Called before the relation is removed.</summary>
    protected virtual Task OnDissolving() => Task.CompletedTask;
    
    /// <summary>Called after the relation is successfully removed.</summary>
    protected virtual Task OnDissolved() => Task.CompletedTask;
    
    // ═══════════════════════════════════════════════════════════
    // Validation - Driver enforces before establishment
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Validates that this relation can be established.</summary>
    public virtual ValidationResult Validate() => ValidationResult.Success;
}
```

#### Supporting Types

```csharp
/// <summary>
/// Strongly-typed identifier for relations.
/// </summary>
public readonly record struct RelationId(Guid Value)
{
    public static RelationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for vertices (grains, actors, entities).
/// </summary>
public readonly record struct VertexId(string Value)
{
    public static implicit operator VertexId(string value) => new(value);
    public override string ToString() => Value;
}

/// <summary>
/// Identifies the type/kind of a relation.
/// </summary>
public readonly record struct RelationKind(string Domain, string Name)
{
    public override string ToString() => $"{Domain}:{Name}";
}
```

---

### Relational Drivers

Drivers encapsulate domain-specific paradigms, rules, and operations while delegating to the engine for core graph functionality.

#### Driver Interface

```csharp
/// <summary>
/// Domain-specific relational paradigm.
/// Manages a set of related relation types with shared rules and operations.
/// </summary>
public interface IRelationalDriver
{
    /// <summary>Human-readable name for this driver.</summary>
    string Name { get; }
    
    /// <summary>Relation kinds this driver manages.</summary>
    IEnumerable<RelationKind> SupportedKinds { get; }
    
    // ═══════════════════════════════════════════════════════════
    // CRUD Operations - Delegate to engine with driver-specific logic
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Establish a new relation with validation and rules.</summary>
    Task<TRelation> EstablishAsync<TRelation>(TRelation relation) 
        where TRelation : IRelation;
    
    /// <summary>Remove an existing relation.</summary>
    Task DissolveAsync(RelationId id);
    
    /// <summary>Retrieve a relation by ID.</summary>
    Task<TRelation?> GetAsync<TRelation>(RelationId id) 
        where TRelation : IRelation;
    
    // ═══════════════════════════════════════════════════════════
    // Query & Rules
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Query interface scoped to this driver's relations.</summary>
    IRelationalQuery Query { get; }
    
    /// <summary>Domain rules enforced on relations.</summary>
    IEnumerable<IRelationRule> Rules { get; }
    
    /// <summary>Access to the underlying engine.</summary>
    IRelationalEngine Engine { get; }
}
```

#### Base Driver Implementation

```csharp
/// <summary>
/// Base implementation providing common driver functionality.
/// </summary>
public abstract class RelationalDriver : IRelationalDriver
{
    public abstract string Name { get; }
    public abstract IEnumerable<RelationKind> SupportedKinds { get; }
    public abstract IEnumerable<IRelationRule> Rules { get; }
    
    protected IRelationalEngine Engine { get; }
    
    public IRelationalQuery Query => Engine.Query.ForDriver(this);
    
    protected RelationalDriver(IRelationalEngine engine)
    {
        Engine = engine;
    }
    
    /// <summary>
    /// Establishes a relation with full validation and rule enforcement.
    /// </summary>
    public async Task<TRelation> EstablishAsync<TRelation>(TRelation relation)
        where TRelation : IRelation
    {
        // 1. Driver-specific validation
        await ValidateEstablishment(relation);
        
        // 2. Run domain rules
        foreach (var rule in Rules.Where(r => r.AppliesTo(relation.Kind)))
        {
            var result = await rule.EnforceAsync(relation);
            if (!result.IsValid)
            {
                throw new RelationRuleViolationException(rule, result);
            }
        }
        
        // 3. Relation's own validation
        var validationResult = relation.Validate();
        if (!validationResult.IsValid)
        {
            throw new RelationValidationException(validationResult);
        }
        
        // 4. Delegate to engine for storage
        return await Engine.AddRelationAsync(relation);
    }
    
    public async Task DissolveAsync(RelationId id)
    {
        var relation = await Engine.GetRelationAsync(id);
        if (relation == null) return;
        
        await ValidateDissolution(relation);
        await Engine.RemoveRelationAsync(id);
    }
    
    public Task<TRelation?> GetAsync<TRelation>(RelationId id)
        where TRelation : IRelation
        => Engine.GetRelationAsync<TRelation>(id);
    
    /// <summary>Override to add driver-specific establishment validation.</summary>
    protected virtual Task ValidateEstablishment(IRelation relation) 
        => Task.CompletedTask;
    
    /// <summary>Override to add driver-specific dissolution validation.</summary>
    protected virtual Task ValidateDissolution(IRelation relation) 
        => Task.CompletedTask;
}
```

#### Relation Rules

```csharp
/// <summary>
/// A rule that can be enforced on relations.
/// </summary>
public interface IRelationRule
{
    /// <summary>Human-readable name for this rule.</summary>
    string Name { get; }
    
    /// <summary>Determines if this rule applies to a relation kind.</summary>
    bool AppliesTo(RelationKind kind);
    
    /// <summary>Enforces the rule, returning validation result.</summary>
    Task<RuleResult> EnforceAsync(IRelation relation);
}

/// <summary>
/// Result of rule enforcement.
/// </summary>
public record RuleResult(bool IsValid, string? Message = null)
{
    public static RuleResult Valid => new(true);
    public static RuleResult Invalid(string message) => new(false, message);
}
```

---

### Relational Engine

The core engine provides graph operations, traversal, query capabilities, and persistence coordination.

#### Engine Interface

```csharp
/// <summary>
/// Core relational graph engine.
/// Provides graph operations, traversal, query, and persistence coordination.
/// </summary>
public interface IRelationalEngine
{
    // ═══════════════════════════════════════════════════════════
    // Graph Operations
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Add a relation to the graph.</summary>
    Task<TRelation> AddRelationAsync<TRelation>(TRelation relation) 
        where TRelation : IRelation;
    
    /// <summary>Remove a relation from the graph.</summary>
    Task RemoveRelationAsync(RelationId id);
    
    /// <summary>Get a relation by ID.</summary>
    Task<IRelation?> GetRelationAsync(RelationId id);
    
    /// <summary>Get a strongly-typed relation by ID.</summary>
    Task<TRelation?> GetRelationAsync<TRelation>(RelationId id) 
        where TRelation : IRelation;
    
    // ═══════════════════════════════════════════════════════════
    // Traversal
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Check if a path exists between vertices.</summary>
    Task<bool> PathExistsAsync(
        VertexId from, 
        VertexId to, 
        Func<IRelation, bool>? predicate = null);
    
    /// <summary>Find all paths between vertices.</summary>
    Task<IEnumerable<RelationPath>> AllPathsAsync(
        VertexId from, 
        VertexId to,
        PathOptions? options = null);
    
    /// <summary>Traverse the graph from a starting vertex.</summary>
    Task TraverseAsync(
        VertexId start, 
        Func<IRelation, bool> follow, 
        Func<IVertex, int, RelationPath, bool> visit);
    
    // ═══════════════════════════════════════════════════════════
    // Query & Translation
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>LINQ-enabled query interface.</summary>
    IRelationalQuery Query { get; }
    
    /// <summary>Cypher translation for Neo4j interop.</summary>
    ICypherTranslator Cypher { get; }
    
    // ═══════════════════════════════════════════════════════════
    // Persistence
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Persistence coordination.</summary>
    IRelationalPersistence Persistence { get; }
}
```

#### Engine Implementation

```csharp
/// <summary>
/// Default implementation of the relational engine.
/// </summary>
public class RelationalEngine : IRelationalEngine
{
    // In-memory graph (QuikGraph-based or custom)
    private readonly IRelationalGraph _graph;
    
    // Registered drivers
    private readonly Dictionary<RelationKind, IRelationalDriver> _drivers;
    
    // LINQ provider
    private readonly RelationalQueryProvider _queryProvider;
    
    // Cypher translation
    private readonly CypherTranslator _cypherTranslator;
    
    // Persistence
    private readonly IRelationalPersistence _persistence;
    
    // Distribution (optional)
    private readonly IRelationalDistribution? _distribution;
    
    public IRelationalQuery Query => new RelationalQuery(_graph, _queryProvider);
    public ICypherTranslator Cypher => _cypherTranslator;
    public IRelationalPersistence Persistence => _persistence;
    
    public RelationalEngine(
        IRelationalGraph graph,
        IRelationalPersistence persistence,
        IRelationalDistribution? distribution = null)
    {
        _graph = graph;
        _persistence = persistence;
        _distribution = distribution;
        _queryProvider = new RelationalQueryProvider(_graph);
        _cypherTranslator = new CypherTranslator();
        _drivers = new Dictionary<RelationKind, IRelationalDriver>();
    }
    
    public async Task<TRelation> AddRelationAsync<TRelation>(TRelation relation)
        where TRelation : IRelation
    {
        // 1. Add to in-memory graph
        _graph.AddRelation(relation);
        
        // 2. Persist (async, configurable consistency)
        await _persistence.SaveRelationAsync(relation);
        
        // 3. Notify distribution layer if enabled
        if (_distribution != null)
        {
            await _distribution.BroadcastRelationAdded(relation);
        }
        
        return relation;
    }
    
    public async Task RemoveRelationAsync(RelationId id)
    {
        var relation = _graph.GetRelation(id);
        if (relation == null) return;
        
        // 1. Remove from in-memory graph
        _graph.RemoveRelation(id);
        
        // 2. Remove from persistence
        await _persistence.DeleteRelationAsync(id);
        
        // 3. Notify distribution layer
        if (_distribution != null)
        {
            await _distribution.BroadcastRelationRemoved(relation);
        }
    }
    
    public Task<IRelation?> GetRelationAsync(RelationId id)
        => Task.FromResult(_graph.GetRelation(id));
    
    public Task<TRelation?> GetRelationAsync<TRelation>(RelationId id)
        where TRelation : IRelation
        => Task.FromResult(_graph.GetRelation(id) as TRelation);
    
    public Task<bool> PathExistsAsync(
        VertexId from, 
        VertexId to, 
        Func<IRelation, bool>? predicate = null)
    {
        return Task.FromResult(_graph.PathExists(from, to, predicate));
    }
    
    public Task<IEnumerable<RelationPath>> AllPathsAsync(
        VertexId from, 
        VertexId to,
        PathOptions? options = null)
    {
        return Task.FromResult(_graph.FindAllPaths(from, to, options));
    }
    
    public async Task TraverseAsync(
        VertexId start, 
        Func<IRelation, bool> follow, 
        Func<IVertex, int, RelationPath, bool> visit)
    {
        await _graph.TraverseAsync(start, follow, visit);
    }
}
```

#### Supporting Types

```csharp
/// <summary>
/// Represents a path through the relation graph.
/// </summary>
public class RelationPath
{
    public VertexId Start { get; }
    public VertexId End { get; }
    public IReadOnlyList<IRelation> Relations { get; }
    public int Length => Relations.Count;
    
    public RelationPath(VertexId start, IEnumerable<IRelation> relations)
    {
        Start = start;
        Relations = relations.ToList();
        End = Relations.Count > 0 
            ? Relations[^1].Target 
            : start;
    }
}

/// <summary>
/// Options for path finding operations.
/// </summary>
public record PathOptions
{
    public int MaxDepth { get; init; } = 10;
    public int MaxPaths { get; init; } = 100;
    public Func<IRelation, bool>? Filter { get; init; }
    public Func<IRelation, double>? WeightFunction { get; init; }
}
```

---

## Domain Drivers

### Security Driver

Manages capabilities, ownership, delegation, and authorization relationships.

#### Relation Kinds

```csharp
/// <summary>
/// Security-related relation kinds.
/// </summary>
public static class SecurityRelations
{
    public static readonly RelationKind Owns = new("Security", "Owns");
    public static readonly RelationKind GrantsCapability = new("Security", "GrantsCapability");
    public static readonly RelationKind DelegatesTo = new("Security", "DelegatesTo");
    public static readonly RelationKind ActsAs = new("Security", "ActsAs");
}
```

#### Driver Implementation

```csharp
/// <summary>
/// Driver for security-related relations: capabilities, ownership, delegation.
/// </summary>
public class SecurityDriver : RelationalDriver
{
    public override string Name => "Security";
    
    public override IEnumerable<RelationKind> SupportedKinds => new[]
    {
        SecurityRelations.Owns,
        SecurityRelations.GrantsCapability,
        SecurityRelations.DelegatesTo,
        SecurityRelations.ActsAs
    };
    
    public override IEnumerable<IRelationRule> Rules => new IRelationRule[]
    {
        new NoCircularDelegationRule(),
        new CapabilitySubsetRule(),
        new SingleOwnerRule()
    };
    
    public SecurityDriver(IRelationalEngine engine) : base(engine) { }
    
    // ═══════════════════════════════════════════════════════════
    // Domain-Specific Operations
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Grant capabilities from grantor to grantee.
    /// </summary>
    public async Task<CapabilityRelation> GrantAsync(
        IVertex grantor, 
        IVertex grantee, 
        CapabilitySet capabilities,
        GrantOptions? options = null)
    {
        var relation = new CapabilityRelation(grantor, grantee, capabilities)
        {
            ExpiresAt = options?.ExpiresAt,
            Revocable = options?.Revocable ?? true,
            Delegatable = options?.Delegatable ?? false,
            CreatedBy = grantor.Id
        };
        
        return await EstablishAsync(relation);
    }
    
    /// <summary>
    /// Establish ownership of a resource.
    /// </summary>
    public async Task<OwnershipRelation> OwnAsync(
        IVertex owner,
        IVertex resource,
        OwnershipOptions? options = null)
    {
        var relation = new OwnershipRelation(owner, resource)
        {
            Transferable = options?.Transferable ?? true,
            Exclusive = options?.Exclusive ?? true,
            CreatedBy = owner.Id
        };
        
        return await EstablishAsync(relation);
    }
    
    // ═══════════════════════════════════════════════════════════
    // Domain-Specific Queries
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Check if source can reach target with required capability.
    /// </summary>
    public async Task<bool> CanReachWithCapability(
        IVertex source, 
        IVertex target, 
        Capability required)
    {
        return await Engine.PathExistsAsync(
            source.Id, 
            target.Id,
            relation => relation is CapabilityRelation cap 
                && cap.Capabilities.Contains(required)
                && !cap.IsExpired);
    }
    
    /// <summary>
    /// Compute effective capabilities from principal to resource.
    /// Considers all paths and computes the union of capabilities.
    /// </summary>
    public async Task<CapabilitySet> EffectiveCapabilities(
        IVertex principal, 
        IVertex resource)
    {
        var paths = await Engine.AllPathsAsync(principal.Id, resource.Id);
        
        return paths
            .Select(ComputePathCapabilities)
            .Aggregate(CapabilitySet.Empty, (a, b) => a.Union(b));
    }
    
    private CapabilitySet ComputePathCapabilities(RelationPath path)
    {
        // Capabilities narrow along a path (intersection)
        // But ownership implies all capabilities
        var relations = path.Relations;
        
        if (relations.Any(r => r is OwnershipRelation))
        {
            return CapabilitySet.All;
        }
        
        return relations
            .OfType<CapabilityRelation>()
            .Select(r => r.Capabilities)
            .Aggregate((a, b) => a.Intersect(b));
    }
    
    /// <summary>
    /// Revoke a capability grant.
    /// </summary>
    public async Task RevokeAsync(RelationId grantId, IVertex revoker)
    {
        var grant = await GetAsync<CapabilityRelation>(grantId);
        if (grant == null) return;
        
        if (!grant.Revocable)
        {
            throw new InvalidOperationException("This grant is not revocable.");
        }
        
        // Verify revoker has authority (is grantor or has admin capability)
        if (grant.CreatedBy != revoker.Id)
        {
            var canRevoke = await CanReachWithCapability(
                revoker, 
                grant.Source, 
                Capability.Admin);
            
            if (!canRevoke)
            {
                throw new UnauthorizedAccessException("Insufficient authority to revoke.");
            }
        }
        
        await DissolveAsync(grantId);
    }
}
```

#### Security Relation Types

```csharp
/// <summary>
/// Represents a capability grant from source to target.
/// </summary>
[Neo4jLabel("GRANTS_CAPABILITY")]
public class CapabilityRelation : Relation<IVertex, IVertex>
{
    public override RelationKind Kind => SecurityRelations.GrantsCapability;
    
    [Neo4jProperty("caps")]
    public CapabilitySet Capabilities { get; }
    
    [Neo4jProperty("expires")]
    public DateTime? ExpiresAt { get; init; }
    
    [Neo4jProperty("revocable")]
    public bool Revocable { get; init; } = true;
    
    [Neo4jProperty("delegatable")]
    public bool Delegatable { get; init; } = false;
    
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt;
    
    public CapabilityRelation(IVertex source, IVertex target, CapabilitySet capabilities)
        : base(source, target)
    {
        Capabilities = capabilities;
    }
    
    public override ValidationResult Validate()
    {
        if (Capabilities.IsEmpty)
        {
            return ValidationResult.Error("Cannot grant empty capability set.");
        }
        
        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow)
        {
            return ValidationResult.Error("Expiration date is in the past.");
        }
        
        return ValidationResult.Success;
    }
}

/// <summary>
/// Represents ownership of a resource.
/// </summary>
[Neo4jLabel("OWNS")]
public class OwnershipRelation : Relation<IVertex, IVertex>
{
    public override RelationKind Kind => SecurityRelations.Owns;
    
    /// <summary>Ownership implies full capabilities.</summary>
    public CapabilitySet ImpliedCapabilities => CapabilitySet.All;
    
    [Neo4jProperty("transferable")]
    public bool Transferable { get; init; } = true;
    
    [Neo4jProperty("exclusive")]
    public bool Exclusive { get; init; } = true;
    
    public OwnershipRelation(IVertex owner, IVertex resource)
        : base(owner, resource)
    {
    }
}

/// <summary>
/// Options for creating capability grants.
/// </summary>
public record GrantOptions
{
    public DateTime? ExpiresAt { get; init; }
    public bool Revocable { get; init; } = true;
    public bool Delegatable { get; init; } = false;
}

/// <summary>
/// Options for establishing ownership.
/// </summary>
public record OwnershipOptions
{
    public bool Transferable { get; init; } = true;
    public bool Exclusive { get; init; } = true;
}
```

#### Supporting Types (Capabilities)

```csharp
/// <summary>
/// Individual capability.
/// </summary>
public readonly record struct Capability(string Name)
{
    public static readonly Capability Read = new("Read");
    public static readonly Capability Write = new("Write");
    public static readonly Capability Execute = new("Execute");
    public static readonly Capability Delete = new("Delete");
    public static readonly Capability Admin = new("Admin");
    public static readonly Capability Invoke = new("Invoke");
    public static readonly Capability Edit = new("Edit");
    
    public override string ToString() => Name;
}

/// <summary>
/// Immutable set of capabilities.
/// </summary>
public sealed class CapabilitySet : IEnumerable<Capability>
{
    private readonly ImmutableHashSet<Capability> _capabilities;
    
    public static readonly CapabilitySet Empty = new(ImmutableHashSet<Capability>.Empty);
    public static readonly CapabilitySet All = new(ImmutableHashSet.Create(
        Capability.Read, Capability.Write, Capability.Execute, 
        Capability.Delete, Capability.Admin, Capability.Invoke, Capability.Edit));
    
    private CapabilitySet(ImmutableHashSet<Capability> capabilities)
    {
        _capabilities = capabilities;
    }
    
    public static CapabilitySet Of(params Capability[] capabilities)
        => new(ImmutableHashSet.CreateRange(capabilities));
    
    public bool IsEmpty => _capabilities.IsEmpty;
    public bool Contains(Capability capability) => _capabilities.Contains(capability);
    
    public CapabilitySet Union(CapabilitySet other)
        => new(_capabilities.Union(other._capabilities));
    
    public CapabilitySet Intersect(CapabilitySet other)
        => new(_capabilities.Intersect(other._capabilities));
    
    public CapabilitySet Except(CapabilitySet other)
        => new(_capabilities.Except(other._capabilities));
    
    public IEnumerator<Capability> GetEnumerator() => _capabilities.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

---

### Eventing Driver

Manages subscriptions, event propagation, and routing relationships.

#### Relation Kinds

```csharp
/// <summary>
/// Eventing-related relation kinds.
/// </summary>
public static class EventingRelations
{
    public static readonly RelationKind SubscribesTo = new("Eventing", "SubscribesTo");
    public static readonly RelationKind PropagatesTo = new("Eventing", "PropagatesTo");
    public static readonly RelationKind Aggregates = new("Eventing", "Aggregates");
}
```

#### Driver Implementation

```csharp
/// <summary>
/// Driver for eventing-related relations: subscriptions, propagation, aggregation.
/// </summary>
public class EventingDriver : RelationalDriver
{
    private const int MaxPropagationDepth = 10;
    
    public override string Name => "Eventing";
    
    public override IEnumerable<RelationKind> SupportedKinds => new[]
    {
        EventingRelations.SubscribesTo,
        EventingRelations.PropagatesTo,
        EventingRelations.Aggregates
    };
    
    public override IEnumerable<IRelationRule> Rules => new IRelationRule[]
    {
        new NoCyclicSubscriptionRule(),
        new MaxPropagationDepthRule(MaxPropagationDepth)
    };
    
    public EventingDriver(IRelationalEngine engine) : base(engine) { }
    
    // ═══════════════════════════════════════════════════════════
    // Domain-Specific Operations
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Subscribe to events from a publisher.
    /// </summary>
    public async Task<SubscriptionRelation> SubscribeAsync<TEvent>(
        IVertex subscriber,
        IVertex publisher,
        EventFilter? filter = null)
        where TEvent : IEvent
    {
        var relation = new SubscriptionRelation(subscriber, publisher)
        {
            EventType = typeof(TEvent),
            Filter = filter ?? EventFilter.All,
            DeliveryGuarantee = DeliveryGuarantee.AtLeastOnce,
            CreatedBy = subscriber.Id
        };
        
        return await EstablishAsync(relation);
    }
    
    /// <summary>
    /// Subscribe with custom options.
    /// </summary>
    public async Task<SubscriptionRelation> SubscribeAsync(
        IVertex subscriber,
        IVertex publisher,
        SubscriptionOptions options)
    {
        var relation = new SubscriptionRelation(subscriber, publisher)
        {
            EventType = options.EventType,
            Filter = options.Filter ?? EventFilter.All,
            DeliveryGuarantee = options.DeliveryGuarantee,
            Priority = options.Priority,
            CreatedBy = subscriber.Id
        };
        
        return await EstablishAsync(relation);
    }
    
    // ═══════════════════════════════════════════════════════════
    // Domain-Specific Queries
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get all subscribers for an event from a publisher.
    /// </summary>
    public async Task<IEnumerable<SubscriptionRelation>> GetSubscribersAsync(
        IVertex publisher,
        IEvent @event)
    {
        var subscriptions = await Engine.Query
            .From(publisher.Id)
            .Outgoing<SubscriptionRelation>()
            .Where(s => s.Matches(@event))
            .OrderBy(s => s.Priority)
            .ToListAsync();
        
        return subscriptions;
    }
    
    /// <summary>
    /// Plan event propagation from origin.
    /// Returns a plan describing all targets and the order of delivery.
    /// </summary>
    public async Task<PropagationPlan> PlanPropagationAsync(
        IVertex origin,
        IEvent @event)
    {
        var plan = new PropagationPlan(@event);
        
        await Engine.TraverseAsync(
            origin.Id,
            relation => relation is IPropagatesEvents p && p.ShouldPropagate(@event),
            (vertex, depth, path) =>
            {
                plan.AddTarget(vertex, depth, path);
                return depth < MaxPropagationDepth;  // Continue if under max depth
            });
        
        return plan;
    }
    
    /// <summary>
    /// Unsubscribe from a publisher.
    /// </summary>
    public async Task UnsubscribeAsync(IVertex subscriber, IVertex publisher)
    {
        var subscriptions = await Engine.Query
            .Relations<SubscriptionRelation>()
            .Where(s => s.Source.Id == subscriber.Id && s.Target.Id == publisher.Id)
            .ToListAsync();
        
        foreach (var sub in subscriptions)
        {
            await DissolveAsync(sub.Id);
        }
    }
}
```

#### Eventing Relation Types

```csharp
/// <summary>
/// Interface for relations that can propagate events.
/// </summary>
public interface IPropagatesEvents
{
    /// <summary>Determines if an event should propagate through this relation.</summary>
    bool ShouldPropagate(IEvent @event);
}

/// <summary>
/// Represents a subscription from subscriber to publisher.
/// </summary>
[Neo4jLabel("SUBSCRIBES_TO")]
public class SubscriptionRelation : Relation<IVertex, IVertex>, IPropagatesEvents
{
    public override RelationKind Kind => EventingRelations.SubscribesTo;
    
    [Neo4jProperty("eventType")]
    public Type EventType { get; init; } = typeof(IEvent);
    
    [Neo4jProperty("filter")]
    public EventFilter Filter { get; init; } = EventFilter.All;
    
    [Neo4jProperty("delivery")]
    public DeliveryGuarantee DeliveryGuarantee { get; init; } = DeliveryGuarantee.AtLeastOnce;
    
    [Neo4jProperty("priority")]
    public int Priority { get; init; } = 0;
    
    public SubscriptionRelation(IVertex subscriber, IVertex publisher)
        : base(subscriber, publisher)
    {
    }
    
    /// <summary>Check if this subscription matches an event.</summary>
    public bool Matches(IEvent @event)
        => EventType.IsAssignableFrom(@event.GetType()) && Filter.Matches(@event);
    
    public bool ShouldPropagate(IEvent @event) => Matches(@event);
}

/// <summary>
/// Options for creating subscriptions.
/// </summary>
public record SubscriptionOptions
{
    public required Type EventType { get; init; }
    public EventFilter? Filter { get; init; }
    public DeliveryGuarantee DeliveryGuarantee { get; init; } = DeliveryGuarantee.AtLeastOnce;
    public int Priority { get; init; } = 0;
}

/// <summary>
/// Event delivery guarantees.
/// </summary>
public enum DeliveryGuarantee
{
    AtMostOnce,
    AtLeastOnce,
    ExactlyOnce
}
```

#### Event Filtering

```csharp
/// <summary>
/// Filter for matching events.
/// </summary>
public class EventFilter
{
    public static readonly EventFilter All = new() { MatchAll = true };
    
    public bool MatchAll { get; init; }
    public IReadOnlyList<Func<IEvent, bool>> Predicates { get; init; } 
        = Array.Empty<Func<IEvent, bool>>();
    
    public bool Matches(IEvent @event)
    {
        if (MatchAll) return true;
        return Predicates.All(p => p(@event));
    }
    
    public static EventFilter Where(Func<IEvent, bool> predicate)
        => new() { Predicates = new[] { predicate } };
    
    public EventFilter And(Func<IEvent, bool> predicate)
        => new() { Predicates = Predicates.Append(predicate).ToArray() };
}
```

#### Propagation Planning

```csharp
/// <summary>
/// Plan for propagating an event through the graph.
/// </summary>
public class PropagationPlan
{
    public IEvent Event { get; }
    
    private readonly List<PropagationTarget> _targets = new();
    public IReadOnlyList<PropagationTarget> Targets => _targets;
    
    public PropagationPlan(IEvent @event)
    {
        Event = @event;
    }
    
    public void AddTarget(IVertex vertex, int depth, RelationPath path)
    {
        _targets.Add(new PropagationTarget(vertex, depth, path));
    }
    
    /// <summary>
    /// Execute the propagation plan.
    /// </summary>
    public async Task ExecuteAsync(IEventDispatcher dispatcher)
    {
        // Group by depth for level-order delivery
        var byDepth = _targets.GroupBy(t => t.Depth).OrderBy(g => g.Key);
        
        foreach (var level in byDepth)
        {
            var tasks = level.Select(t => dispatcher.DispatchAsync(t.Vertex, Event));
            await Task.WhenAll(tasks);
        }
    }
}

/// <summary>
/// A target in a propagation plan.
/// </summary>
public record PropagationTarget(IVertex Vertex, int Depth, RelationPath Path);
```

---

## Query Layer

### LINQ Support

A LINQ provider enables querying relations like collections.

#### Query Interfaces

```csharp
/// <summary>
/// Entry point for relational queries.
/// </summary>
public interface IRelationalQuery
{
    /// <summary>Query all relations of a specific type.</summary>
    IRelationalQueryable<TRelation> Relations<TRelation>() 
        where TRelation : IRelation;
    
    /// <summary>Start a query from a specific vertex.</summary>
    IVertexQueryable From(VertexId id);
    
    /// <summary>Start a query from a vertex.</summary>
    IVertexQueryable From(IVertex vertex);
    
    /// <summary>Scope query to a specific driver's relations.</summary>
    IRelationalQuery ForDriver(IRelationalDriver driver);
}

/// <summary>
/// Queryable for relations with graph-specific operations.
/// </summary>
public interface IRelationalQueryable<TRelation> : IQueryable<TRelation>
    where TRelation : IRelation
{
    /// <summary>Filter to relations from a specific source.</summary>
    IRelationalQueryable<TRelation> FromVertex(VertexId source);
    
    /// <summary>Filter to relations to a specific target.</summary>
    IRelationalQueryable<TRelation> ToVertex(VertexId target);
    
    /// <summary>Filter to relations of a specific kind.</summary>
    IRelationalQueryable<TRelation> OfKind(RelationKind kind);
    
    /// <summary>Include relations up to a traversal depth.</summary>
    IRelationalQueryable<TRelation> Traverse(int maxDepth);
    
    /// <summary>Execute and return results.</summary>
    Task<List<TRelation>> ToListAsync();
}

/// <summary>
/// Queryable starting from a vertex.
/// </summary>
public interface IVertexQueryable
{
    /// <summary>Get outgoing relations of a specific type.</summary>
    IRelationalQueryable<TRelation> Outgoing<TRelation>() 
        where TRelation : IRelation;
    
    /// <summary>Get incoming relations of a specific type.</summary>
    IRelationalQueryable<TRelation> Incoming<TRelation>() 
        where TRelation : IRelation;
    
    /// <summary>Check if a path exists to another vertex.</summary>
    IPathQueryable CanReach(VertexId target);
}

/// <summary>
/// Queryable for path existence checks.
/// </summary>
public interface IPathQueryable
{
    /// <summary>Filter path to only include specific relation types.</summary>
    IPathQueryable Through<TRelation>(Func<TRelation, bool>? predicate = null) 
        where TRelation : IRelation;
    
    /// <summary>Execute the path check.</summary>
    Task<bool> ExecuteAsync();
    
    /// <summary>Get the actual path if it exists.</summary>
    Task<RelationPath?> GetPathAsync();
}
```

#### Usage Examples

```csharp
// Query all expired capability grants
var expiredCapabilities = await engine.Query
    .Relations<CapabilityRelation>()
    .Where(r => r.ExpiresAt < DateTime.UtcNow)
    .ToListAsync();

// Query subscriptions from a specific vertex
var aliceSubscriptions = await engine.Query
    .From(alice.Id)
    .Outgoing<SubscriptionRelation>()
    .Where(s => s.EventType == typeof(OrderCreated))
    .Select(s => new { Target = s.Target, Filter = s.Filter })
    .ToListAsync();

// Check if a path exists with specific capabilities
var canAliceInvokeBob = await engine.Query
    .From(alice.Id)
    .CanReach(bob.Id)
    .Through<CapabilityRelation>(r => r.Capabilities.Contains(Capability.Invoke))
    .ExecuteAsync();

// Complex query with traversal
var delegationChain = await engine.Query
    .From(rootPrincipal.Id)
    .Outgoing<CapabilityRelation>()
    .Where(r => r.Delegatable)
    .Traverse(maxDepth: 5)
    .ToListAsync();
```

---

### Cypher Translation

Bidirectional translation between LINQ and Cypher for Neo4j interoperability.

#### Translator Interface

```csharp
/// <summary>
/// Translates between LINQ queries and Cypher.
/// </summary>
public interface ICypherTranslator
{
    /// <summary>Translate a LINQ query to Cypher.</summary>
    string ToCypher<T>(IQueryable<T> query);
    
    /// <summary>Parse Cypher and return a queryable.</summary>
    IRelationalQueryable<IRelation> FromCypher(string cypher);
    
    /// <summary>Import relations from Neo4j using Cypher.</summary>
    Task<IEnumerable<IRelation>> ImportFromNeo4jAsync(string cypher);
    
    /// <summary>Export relations to Neo4j.</summary>
    Task ExportToNeo4jAsync(IEnumerable<IRelation> relations);
}
```

#### Translation Examples

| LINQ | Cypher |
|------|--------|
| `Query.From(alice).Outgoing<CapabilityRelation>().Where(r => r.Delegatable)` | `MATCH (a {id: 'alice'})-[r:GRANTS_CAPABILITY]->(b) WHERE r.delegatable = true RETURN r` |
| `Query.From(alice).CanReach(bob).Through<CapabilityRelation>()` | `MATCH path = (a {id: 'alice'})-[:GRANTS_CAPABILITY*]->(b {id: 'bob'}) RETURN path` |
| `Query.Relations<OwnershipRelation>().Where(r => r.Exclusive)` | `MATCH ()-[r:OWNS]->() WHERE r.exclusive = true RETURN r` |

#### Neo4j Attribute Mapping

```csharp
/// <summary>
/// Maps a relation type to a Neo4j relationship label.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class Neo4jLabelAttribute : Attribute
{
    public string Label { get; }
    
    public Neo4jLabelAttribute(string label)
    {
        Label = label;
    }
}

/// <summary>
/// Maps a property to a Neo4j property name.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class Neo4jPropertyAttribute : Attribute
{
    public string Name { get; }
    
    public Neo4jPropertyAttribute(string name)
    {
        Name = name;
    }
}
```

---

## Distribution Layer

### NewOrleans Integration

Distributed graph coordination across Orleans silos.

#### Grain Interface

```csharp
/// <summary>
/// Grain interface for distributed relational engine access.
/// One instance per silo manages the local portion of the graph.
/// </summary>
public interface IRelationalEngineGrain : IGrainWithStringKey
{
    // ═══════════════════════════════════════════════════════════
    // Local Silo Operations
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Add a relation to the local index.</summary>
    Task<TRelation> AddRelationAsync<TRelation>(TRelation relation) 
        where TRelation : IRelation;
    
    /// <summary>Get a relation by ID.</summary>
    Task<IRelation?> GetRelationAsync(RelationId id);
    
    /// <summary>Index an inbound relation (source is on another silo).</summary>
    Task IndexInboundRelation(IRelation relation);
    
    // ═══════════════════════════════════════════════════════════
    // Cross-Silo Queries
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>Check if a path exists (may fan out to other silos).</summary>
    Task<bool> PathExistsAsync(VertexId from, VertexId to, RelationFilter filter);
    
    /// <summary>Find paths (may fan out to other silos).</summary>
    Task<IEnumerable<RelationPath>> FindPathsAsync(
        VertexId from, 
        VertexId to, 
        PathQuery query);
}
```

#### Silo Index Grain

```csharp
/// <summary>
/// Per-silo index grain managing local portion of the relation graph.
/// </summary>
public class SiloRelationalIndexGrain : Grain, IRelationalEngineGrain
{
    private IRelationalEngine _localEngine;
    
    // Index: vertex -> relations where that vertex is source or target
    private readonly ConcurrentDictionary<VertexId, List<RelationId>> _vertexIndex = new();
    
    // Track which silos own which vertices
    private readonly ConcurrentDictionary<VertexId, string> _vertexSiloMap = new();
    
    public override async Task OnActivateAsync(CancellationToken ct)
    {
        _localEngine = ServiceProvider.GetRequiredService<IRelationalEngine>();
        await base.OnActivateAsync(ct);
    }
    
    public async Task<TRelation> AddRelationAsync<TRelation>(TRelation relation)
        where TRelation : IRelation
    {
        // 1. Add to local engine
        await _localEngine.AddRelationAsync(relation);
        
        // 2. Update local index
        IndexRelation(relation);
        
        // 3. If target is on different silo, notify that silo's index
        var targetSilo = await GetSiloForVertex(relation.Target);
        if (targetSilo != this.GetPrimaryKeyString())
        {
            var remoteSilo = GrainFactory.GetGrain<IRelationalEngineGrain>(targetSilo);
            await remoteSilo.IndexInboundRelation(relation);
        }
        
        return relation;
    }
    
    public Task IndexInboundRelation(IRelation relation)
    {
        // Index without storing (relation lives on source silo)
        if (!_vertexIndex.TryGetValue(relation.Target, out var list))
        {
            list = new List<RelationId>();
            _vertexIndex[relation.Target] = list;
        }
        list.Add(relation.Id);
        
        return Task.CompletedTask;
    }
    
    public async Task<bool> PathExistsAsync(
        VertexId from, 
        VertexId to, 
        RelationFilter filter)
    {
        // 1. Check local graph first
        if (await _localEngine.PathExistsAsync(from, to, filter.ToPredicate()))
            return true;
        
        // 2. Find boundary vertices (relations leaving this silo)
        var boundaryRelations = await _localEngine.Query
            .From(from)
            .Outgoing<IRelation>()
            .Traverse(maxDepth: 10)
            .Where(r => !IsLocal(r.Target))
            .ToListAsync();
        
        // 3. Fan out to other silos
        var tasks = boundaryRelations
            .GroupBy(r => GetSiloForVertex(r.Target).Result)
            .Select(async g =>
            {
                var remoteSilo = GrainFactory.GetGrain<IRelationalEngineGrain>(g.Key);
                var boundaryVertex = g.First().Target;
                return await remoteSilo.PathExistsAsync(boundaryVertex, to, filter);
            });
        
        var results = await Task.WhenAll(tasks);
        return results.Any(r => r);
    }
    
    private void IndexRelation(IRelation relation)
    {
        // Index by source
        if (!_vertexIndex.TryGetValue(relation.Source, out var sourceList))
        {
            sourceList = new List<RelationId>();
            _vertexIndex[relation.Source] = sourceList;
        }
        sourceList.Add(relation.Id);
        
        // Index by target (if local)
        if (IsLocal(relation.Target))
        {
            if (!_vertexIndex.TryGetValue(relation.Target, out var targetList))
            {
                targetList = new List<RelationId>();
                _vertexIndex[relation.Target] = targetList;
            }
            targetList.Add(relation.Id);
        }
    }
    
    private bool IsLocal(VertexId vertex)
    {
        // Check if vertex is activated on this silo
        return _vertexSiloMap.TryGetValue(vertex, out var silo) 
            && silo == this.GetPrimaryKeyString();
    }
    
    private Task<string> GetSiloForVertex(VertexId vertex)
    {
        // Resolve which silo owns this vertex
        // Could use Orleans' grain directory or custom placement
        if (_vertexSiloMap.TryGetValue(vertex, out var silo))
            return Task.FromResult(silo);
        
        // Default: hash-based assignment
        var hash = vertex.Value.GetHashCode();
        // ... resolve to silo ID
        return Task.FromResult("silo-" + (hash % 4));
    }
}
```

#### Client-Side Facade

```csharp
/// <summary>
/// Client-side facade for distributed relational operations.
/// Routes operations to appropriate silos.
/// </summary>
public class DistributedRelationalEngine : IRelationalEngine
{
    private readonly IGrainFactory _grainFactory;
    private readonly IRelationalGraph _localCache;
    
    public DistributedRelationalEngine(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
        _localCache = new InMemoryRelationalGraph();
    }
    
    public async Task<TRelation> AddRelationAsync<TRelation>(TRelation relation)
        where TRelation : IRelation
    {
        // Route to silo owning the source vertex
        var siloId = await ResolveSilo(relation.Source);
        var siloGrain = _grainFactory.GetGrain<IRelationalEngineGrain>(siloId);
        return await siloGrain.AddRelationAsync(relation);
    }
    
    public async Task<bool> PathExistsAsync(
        VertexId from, 
        VertexId to, 
        Func<IRelation, bool>? predicate = null)
    {
        var filter = predicate != null 
            ? RelationFilter.FromPredicate(predicate) 
            : RelationFilter.All;
        
        var siloId = await ResolveSilo(from);
        var siloGrain = _grainFactory.GetGrain<IRelationalEngineGrain>(siloId);
        return await siloGrain.PathExistsAsync(from, to, filter);
    }
    
    // LINQ query with distributed execution
    public IRelationalQuery Query => new DistributedRelationalQuery(this, _grainFactory);
    
    private Task<string> ResolveSilo(VertexId vertex)
    {
        // Resolve which silo owns this vertex
        var hash = vertex.Value.GetHashCode();
        return Task.FromResult("silo-" + Math.Abs(hash % 4));
    }
    
    // ... other IRelationalEngine members
}
```

---

## Persistence Layer

### Neo4j Provider

Persistence provider for Neo4j graph database.

```csharp
/// <summary>
/// Neo4j implementation of relational persistence.
/// </summary>
public class Neo4jRelationalPersistence : IRelationalPersistence
{
    private readonly IDriver _neo4jDriver;
    private readonly ICypherTranslator _translator;
    private readonly IRelationSerializer _serializer;
    
    public Neo4jRelationalPersistence(
        string connectionUri,
        string username,
        string password)
    {
        _neo4jDriver = GraphDatabase.Driver(connectionUri, AuthTokens.Basic(username, password));
        _translator = new CypherTranslator();
        _serializer = new Neo4jRelationSerializer();
    }
    
    public async Task SaveRelationAsync(IRelation relation)
    {
        var label = GetLabel(relation.Kind);
        var properties = _serializer.Serialize(relation);
        
        var cypher = $@"
            MERGE (s {{id: $sourceId}})
            MERGE (t {{id: $targetId}})
            CREATE (s)-[r:{label} $properties]->(t)
            RETURN r";
        
        await using var session = _neo4jDriver.AsyncSession();
        await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new
        {
            sourceId = relation.Source.Value,
            targetId = relation.Target.Value,
            properties = properties
        }));
    }
    
    public async Task<IRelation?> LoadRelationAsync(RelationId id)
    {
        var cypher = @"
            MATCH (s)-[r {id: $id}]->(t)
            RETURN s, r, t, type(r) as relationType";
        
        await using var session = _neo4jDriver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, new { id = id.Value.ToString() });
            return await cursor.SingleAsync();
        });
        
        return _serializer.Deserialize(result);
    }
    
    public async Task DeleteRelationAsync(RelationId id)
    {
        var cypher = @"
            MATCH ()-[r {id: $id}]->()
            DELETE r";
        
        await using var session = _neo4jDriver.AsyncSession();
        await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new { id = id.Value.ToString() }));
    }
    
    public async Task SaveBatchAsync(IEnumerable<IRelation> relations)
    {
        var batch = relations.Select(r => new
        {
            sourceId = r.Source.Value,
            targetId = r.Target.Value,
            type = GetLabel(r.Kind),
            properties = _serializer.Serialize(r)
        }).ToList();
        
        var cypher = @"
            UNWIND $batch AS rel
            MERGE (s {id: rel.sourceId})
            MERGE (t {id: rel.targetId})
            CALL apoc.create.relationship(s, rel.type, rel.properties, t) YIELD rel as r
            RETURN r";
        
        await using var session = _neo4jDriver.AsyncSession();
        await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, new { batch }));
    }
    
    public async Task<IEnumerable<IRelation>> QueryCypherAsync(
        string cypher, 
        object? parameters = null)
    {
        await using var session = _neo4jDriver.AsyncSession();
        var results = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, parameters);
            return await cursor.ToListAsync();
        });
        
        return results.Select(_serializer.Deserialize).Where(r => r != null)!;
    }
    
    private string GetLabel(RelationKind kind)
    {
        // Convert kind to Neo4j label: "Security:GrantsCapability" -> "GRANTS_CAPABILITY"
        return kind.Name.ToUpperInvariant().Replace(" ", "_");
    }
}
```

---

## Usage Examples

### Configuration and Startup

```csharp
// Startup configuration
public void ConfigureServices(IServiceCollection services)
{
    services.AddRelationalFramework(config =>
    {
        // Persistence
        config.UseNeo4jPersistence("bolt://localhost:7687", "neo4j", "password");
        
        // Distribution (for Orleans)
        config.UseNewOrleansDistribution();
        
        // Register drivers
        config.AddDriver<SecurityDriver>();
        config.AddDriver<EventingDriver>();
        config.AddDriver<WorkflowDriver>();
    });
}
```

### Using in a Grain

```csharp
public class OrderGrain : Grain, IOrderGrain
{
    private readonly IRelationalEngine _engine;
    private readonly SecurityDriver _security;
    private readonly EventingDriver _eventing;
    
    public OrderGrain(
        IRelationalEngine engine,
        SecurityDriver security,
        EventingDriver eventing)
    {
        _engine = engine;
        _security = security;
        _eventing = eventing;
    }
    
    public async Task GrantAccessAsync(UserId userId, CapabilitySet capabilities)
    {
        var user = await GetVertex(userId);
        
        await _security.GrantAsync(this.AsVertex(), user, capabilities, new GrantOptions
        {
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Delegatable = false
        });
    }
    
    public async Task<bool> CanUserEditAsync(UserId userId)
    {
        var user = await GetVertex(userId);
        return await _security.CanReachWithCapability(user, this.AsVertex(), Capability.Edit);
    }
    
    public async Task SubscribeToUpdatesAsync(IGrain subscriber, EventFilter filter)
    {
        await _eventing.SubscribeAsync<OrderUpdated>(
            subscriber.AsVertex(), 
            this.AsVertex(), 
            filter);
    }
    
    public async Task PublishUpdateAsync(OrderUpdated @event)
    {
        var plan = await _eventing.PlanPropagationAsync(this.AsVertex(), @event);
        await plan.ExecuteAsync(ServiceProvider.GetRequiredService<IEventDispatcher>());
    }
    
    private IVertex AsVertex() => new GrainVertex(this.GetPrimaryKey());
    
    private Task<IVertex> GetVertex(UserId userId) 
        => Task.FromResult<IVertex>(new UserVertex(userId));
}
```

### Complex Queries

```csharp
// Find all capability grants created in the last week
var recentGrants = await engine.Query
    .Relations<CapabilityRelation>()
    .Where(r => r.CreatedAt > DateTime.UtcNow.AddDays(-7))
    .Where(r => r.Capabilities.Contains(Capability.Admin))
    .GroupBy(r => r.Source)
    .Select(g => new { Grantor = g.Key, Count = g.Count() })
    .ToListAsync();

// Find suspicious delegation chains
var suspiciousChains = await engine.Cypher.QueryAsync(@"
    MATCH path = (u:User)-[:GRANTS_CAPABILITY*3..]->(r:Resource)
    WHERE ALL(rel IN relationships(path) WHERE rel.delegatable = true)
    RETURN path
    LIMIT 100
");

// Check authorization
var canInvoke = await engine.Query
    .From(caller.Id)
    .CanReach(target.Id)
    .Through<CapabilityRelation>(r => 
        r.Capabilities.Contains(Capability.Invoke) && !r.IsExpired)
    .ExecuteAsync();
```

---

## Benefits & Capabilities

| Concern | Solution |
|---------|----------|
| **Domain separation** | Drivers encapsulate domain logic (security, eventing, etc.) |
| **Reusable core** | Engine handles graph ops, traversal, persistence uniformly |
| **Queryability** | LINQ for C#, Cypher for external/complex queries |
| **Distribution** | NewOrleans extension handles cross-silo coordination |
| **Persistence** | Neo4j (or others) via provider pattern |
| **Type safety** | Relation types are strongly typed with domain attributes |
| **Extensibility** | New drivers, new relation types, new persistence providers |
| **Semantic computing** | Relationships carry meaning beyond structural links |

---

## Open Questions

### Consistency Model

- Eventually consistent cross-silo? Strong consistency within silo?
- How to handle partition scenarios?
- Conflict resolution for concurrent relation mutations?

### Caching Strategy

- How much graph state lives in-memory vs queried from Neo4j?
- Cache invalidation across silos?
- Warm-up strategies for frequently accessed subgraphs?

### Schema Evolution

- How do relation types evolve over time?
- Migration strategies for existing relations?
- Backward compatibility requirements?

### Indexing

- Which relation properties get indexed for fast lookup?
- Secondary indexes for common query patterns?
- Full-text search on relation attributes?

### Transactions

- Multi-relation atomic operations across silos?
- Saga patterns for distributed relation establishment?
- Compensation strategies for partial failures?

---

## Appendix: Foundational Concepts

### Association Classes in UML

In UML, association classes make relationships first-class:

```
┌────────┐         ┌─────────┐
│ Person │─────────│ Company │
└────────┘    │    └─────────┘
              │
         ┌────┴────┐
         │Employment│
         ├─────────┤
         │startDate │
         │title     │
         │salary    │
         └─────────┘
```

### QuikGraph Foundations

This framework builds on concepts from QuikGraph:

- **Typed edges** — Edges carry type information and attributes
- **Bidirectional graphs** — Navigate both directions efficiently
- **Graph algorithms** — Traversal, pathfinding, cycle detection
- **Filtered views** — Query subgraphs without copying

### Related Patterns

- **EMF (Eclipse Modeling Framework)** — Bidirectional association maintenance
- **CSLA.NET** — Parent-child state propagation
- **DCI (Data, Context, Interaction)** — Objects with roles in contexts
- **Capability-based security** — Authority through unforgeable references

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | January 2026 | Initial specification |
