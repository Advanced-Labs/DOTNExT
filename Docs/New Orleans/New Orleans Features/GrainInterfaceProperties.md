# Enabling Properties in Orleans Grain Interfaces

## Executive Summary

This document analyzes the Orleans code generator to determine the minimum changes required to support properties in grain interfaces for NewOrleans' auto-persistence feature.

**Key Finding:** Supporting properties requires changes to 3-4 files, totaling approximately 50-100 lines of code. The main challenge is not the code generator itself, but the fundamental C# limitation that properties cannot be async.

---

## Current Architecture

### Property Blockers (2 locations)

#### 1. Analyzer: ORLEANS0008
**File:** `src/NewOrleans/src/Orleans.Analyzers/GrainInterfacePropertyDiagnosticAnalyzer.cs`

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GrainInterfacePropertyDiagnosticAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ORLEANS0008";
    public const string Title = "Grain interfaces must not contain properties";
    // Reports error for any property in an interface that extends IAddressable
}
```

**Action:** Either disable entirely for NewOrleans, or make conditional (e.g., skip for `[AllowProperties]` attribute).

#### 2. Code Generator Check
**File:** `src/NewOrleans/src/Orleans.CodeGenerator/Model/ProxyInterfaceDescription.cs:24-28`

```csharp
public ProxyInterfaceDescription(...)
{
    var prop = interfaceType.GetAllMembers<IPropertySymbol>().FirstOrDefault();
    if (prop is { })
    {
        throw new OrleansGeneratorDiagnosticAnalysisException(
            RpcInterfacePropertyDiagnostic.CreateDiagnostic(interfaceType, prop));
    }
    // ...
}
```

**Action:** Remove this check to allow properties in interfaces.

---

## Code Generation Flow

### How Methods Currently Work

```
Interface Method (IMethodSymbol)
    ↓
InvokableMethodDescription (describes the method)
    ↓
InvokableGenerator.Generate() → GeneratedInvokableDescription
    ↓
Creates "Invokable" class (the request object):
    - Extends Request<T>/TaskRequest<T>/VoidRequest
    - Has fields for each parameter (arg0, arg1, ...)
    - Has InvokeInner() that calls the target method
    ↓
ProxyGenerator.Generate() → Creates proxy class:
    - Extends GrainReference
    - Implements the grain interface
    - Each method creates an Invokable, calls base.InvokeAsync()
```

### Key Files

| File | Purpose |
|------|---------|
| `CodeGenerator.cs` | Orchestrates code generation |
| `ProxyInterfaceDescription.cs` | Describes an interface to proxy |
| `ProxyMethodDescription.cs` | Describes a method on a proxy interface |
| `InvokableMethodDescription.cs` | Describes an invokable method |
| `ProxyGenerator.cs` | Generates proxy classes |
| `InvokableGenerator.cs` | Generates invokable request classes |

### Request Base Classes

**File:** `src/NewOrleans/src/Orleans.Core.Abstractions/Runtime/GrainReference.cs`

```csharp
// Base classes for different return types:
public abstract class Request : RequestBase        // ValueTask
public abstract class Request<T> : RequestBase     // ValueTask<T>
public abstract class TaskRequest : RequestBase    // Task
public abstract class TaskRequest<T> : RequestBase // Task<T>
public abstract class VoidRequest : RequestBase    // void (one-way)
```

### Proxy Base Class

```csharp
public class GrainReference : IAddressable
{
    protected ValueTask<T> InvokeAsync<T>(IRequest methodDescription);
    protected ValueTask InvokeAsync(IRequest methodDescription);
    protected void Invoke(IRequest methodDescription);  // For one-way
}
```

---

## The C# Async Property Problem

### The Fundamental Issue

C# properties cannot be async. You cannot write:

```csharp
// INVALID C#
public async Task<int> Score { get; }
```

Orleans methods return `Task`/`ValueTask` because RPC is inherently async. A property getter/setter that performs RPC must either:

1. **Block** - Call `.Result` or `.GetAwaiter().GetResult()` (BAD: can deadlock)
2. **Return Task<T>** - Valid C# but unusual pattern
3. **Fire-and-forget** - For setters only, not reliable

### Options Analysis

#### Option A: Sync Properties with Blocking (Not Recommended)

```csharp
// Generated proxy
int IPlayerGrain.Score
{
    get => InvokeAsync<int>(new GetScoreRequest()).AsTask().GetAwaiter().GetResult();
    set => InvokeAsync(new SetScoreRequest { arg0 = value }).AsTask().Wait();
}
```

**Problems:**
- Blocks the calling thread
- Can deadlock in UI/ASP.NET contexts
- Terrible performance

#### Option B: Task<T> Property Getters (Valid but Unusual)

```csharp
// Interface
interface IPlayerGrain : IGrain
{
    Task<int> Score { get; }  // Get-only, returns Task
}

// Proxy
Task<int> IPlayerGrain.Score => InvokeAsync<int>(new GetScoreRequest()).AsTask();
```

**Limitations:**
- Only getters (setters would need `Task Score { set; }` which is weird)
- Unusual API for consumers: `var score = await grain.Score;`

#### Option C: Generate Methods Instead of Properties in Proxy

```csharp
// Interface has properties
interface IPlayerGrain : IGrain
{
    int Score { get; set; }
}

// But proxy generates METHODS that satisfy the interface via explicit implementation
// Wait - this doesn't work! Explicit implementation must match the interface signature.
```

**This doesn't work** - the proxy must implement the interface exactly as declared.

#### Option D: Separate Client/Server Interfaces (Recommended for Most Cases)

```csharp
// Server-side (grain implementation)
interface IPlayerGrainInternal
{
    int Score { get; set; }  // Sync property - no RPC
}

// Client-side (for RPC)
interface IPlayerGrain : IGrain
{
    Task<int> GetScore();
    Task SetScore(int value);
}

// Grain implements both
class PlayerGrain : Grain, IPlayerGrain, IPlayerGrainInternal
{
    private int _score;

    int IPlayerGrainInternal.Score
    {
        get => _score;
        set { _score = value; PersistAsync(); }
    }

    Task<int> IPlayerGrain.GetScore() => Task.FromResult(_score);
    Task IPlayerGrain.SetScore(int value) { _score = value; return PersistAsync(); }
}
```

---

## Recommended Implementation for NewOrleans

Given your auto-persistence use case, here's the recommended approach:

### Design: Properties Work Differently on Server vs Client

**Server-side (Grain):** Properties provide direct field access + persistence trigger
**Client-side (Proxy):** Properties translate to async method invocations

### Implementation Strategy

#### Step 1: Remove Property Blockers

```csharp
// In GrainInterfacePropertyDiagnosticAnalyzer.cs
// Option A: Disable entirely
// Option B: Add check for [AllowInterfaceProperties] attribute

// In ProxyInterfaceDescription.cs:24-28
// Remove or make conditional:
var prop = interfaceType.GetAllMembers<IPropertySymbol>().FirstOrDefault();
if (prop is { })
{
    throw ...  // REMOVE THIS
}
```

#### Step 2: Extend ProxyInterfaceDescription.GetMethods()

**File:** `ProxyInterfaceDescription.cs:75-110`

Current code only iterates `IMethodSymbol`. Extend to handle `IPropertySymbol`:

```csharp
private List<ProxyMethodDescription> GetMethods()
{
    var result = new List<ProxyMethodDescription>();
    foreach (var iface in GetAllInterfaces(InterfaceType))
    {
        // Existing: Process methods
        foreach (var method in iface.GetDeclaredInstanceMembers<IMethodSymbol>())
        {
            if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                continue;
            if (method.MethodKind == MethodKind.PropertyGet ||
                method.MethodKind == MethodKind.PropertySet)
                continue;  // Skip - handled below via properties

            result.Add(CodeGenerator.GetProxyMethodDescription(InterfaceType, method));
        }

        // NEW: Process properties
        foreach (var property in iface.GetDeclaredInstanceMembers<IPropertySymbol>())
        {
            // Create method descriptions for getter and setter
            if (property.GetMethod != null)
            {
                result.Add(CodeGenerator.GetProxyMethodDescription(InterfaceType, property.GetMethod));
            }
            if (property.SetMethod != null)
            {
                result.Add(CodeGenerator.GetProxyMethodDescription(InterfaceType, property.SetMethod));
            }
        }
    }
    return result;
}
```

#### Step 3: Extend ProxyGenerator to Handle Properties

**File:** `ProxyGenerator.cs`

Add property generation alongside method generation:

```csharp
private MemberDeclarationSyntax[] CreateProxyMethods(
    List<GeneratedFieldDescription> fieldDescriptions,
    ProxyInterfaceDescription interfaceDescription)
{
    var res = new List<MemberDeclarationSyntax>();

    // Group methods by their property (if any)
    var propertyMethods = new Dictionary<IPropertySymbol, (ProxyMethodDescription Getter, ProxyMethodDescription Setter)>();
    var regularMethods = new List<ProxyMethodDescription>();

    foreach (var methodDescription in interfaceDescription.Methods)
    {
        var method = methodDescription.Method;
        if (method.AssociatedSymbol is IPropertySymbol property)
        {
            if (!propertyMethods.TryGetValue(property, out var existing))
            {
                existing = (null, null);
            }

            if (method.MethodKind == MethodKind.PropertyGet)
                propertyMethods[property] = (methodDescription, existing.Setter);
            else if (method.MethodKind == MethodKind.PropertySet)
                propertyMethods[property] = (existing.Getter, methodDescription);
        }
        else
        {
            regularMethods.Add(methodDescription);
        }
    }

    // Generate regular methods
    foreach (var methodDescription in regularMethods)
    {
        res.Add(CreateProxyMethod(methodDescription));
    }

    // Generate properties
    foreach (var (property, (getter, setter)) in propertyMethods)
    {
        res.Add(CreateProxyProperty(property, getter, setter, fieldDescriptions));
    }

    return res.ToArray();
}

private PropertyDeclarationSyntax CreateProxyProperty(
    IPropertySymbol property,
    ProxyMethodDescription getter,
    ProxyMethodDescription setter,
    List<GeneratedFieldDescription> fieldDescriptions)
{
    var accessors = new List<AccessorDeclarationSyntax>();

    if (getter != null)
    {
        // For sync property: var request = new GetterRequest(); return InvokeAsync<T>(request).AsTask().GetAwaiter().GetResult();
        // For Task<T> property: var request = new GetterRequest(); return InvokeAsync<T>(request).AsTask();
        var (_, body) = CreateAsyncProxyMethodBody(fieldDescriptions, getter);
        accessors.Add(
            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(ConvertToPropertyAccessorBody(body, isGetter: true, property.Type)));
    }

    if (setter != null)
    {
        var (_, body) = CreateAsyncProxyMethodBody(fieldDescriptions, setter);
        accessors.Add(
            AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithBody(ConvertToPropertyAccessorBody(body, isGetter: false, null)));
    }

    return PropertyDeclaration(property.Type.ToTypeSyntax(), property.Name)
        .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(property.ContainingType.ToNameSyntax()))
        .WithAccessorList(AccessorList(List(accessors)));
}
```

---

## Minimum Changes Summary

### Files to Modify

| File | Change |
|------|--------|
| `GrainInterfacePropertyDiagnosticAnalyzer.cs` | Disable or add conditional |
| `ProxyInterfaceDescription.cs` | Remove property block (lines 24-28), extend GetMethods() |
| `ProxyGenerator.cs` | Add CreateProxyProperty(), modify CreateProxyMethods() |
| `RpcInterfacePropertyDiagnostic.cs` | (Optional) Remove or keep for conditional use |

### Estimated Scope

- **Lines of code:** ~50-100 lines added/modified
- **Risk:** Medium - touches core code generation
- **Testing:** Need proxy tests with property access

---

## Alternative: The "Treat Properties as Methods" Approach

If you want to avoid the async property complexity entirely, you could:

1. Allow properties in interfaces (remove blockers)
2. Generate getter/setter METHODS in the proxy, not properties
3. Map property.GetMethod → generated GetPropertyName() method
4. Map property.SetMethod → generated SetPropertyName(value) method

**Result:** Interface has properties, proxy has methods. But this breaks the interface contract - the proxy wouldn't actually implement the interface property!

**Solution:** Don't have the proxy implement the interface directly for properties. Instead, use a wrapper or different approach... but this gets complicated.

---

## Conclusion

The cleanest solution for NewOrleans auto-persistence is:

1. **For grain implementation (server-side):** Use sync properties with explicit interface implementation
2. **For client proxies:** Accept the blocking behavior OR use `Task<T>` property return types

If blocking is acceptable for your use case (e.g., internal services, not UI-bound), the implementation is straightforward. If not, consider:
- Using `Task<T>` property return types
- Generating methods instead of properties for the client interface
- Using a code-first approach where the client interface is auto-generated with methods

---

## Next Steps

1. Decide on the async property strategy (blocking vs Task<T> vs methods)
2. Implement the property blocker removal
3. Extend GetMethods() to include property accessors
4. Add property generation to ProxyGenerator
5. Add tests for property-based grain interfaces
