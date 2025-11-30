# Roslyn Async Persistence Modification Design

**Purpose**: Document the exact changes needed to Roslyn to inject persistence calls into async state machines.

---

## Overview

We want Roslyn to generate code like this for methods marked with `[Persistable]`:

```csharp
// Generated MoveNext() pseudo-code:
void MoveNext()
{
    // NEW: Restoration check at start
    var persistenceService = DOTNExT.Persistence.AsyncPersistenceContext.Current;
    if (persistenceService != null)
    {
        var restoredState = persistenceService.TryRestore(this, "<>MethodId");
        if (restoredState >= 0)
        {
            <>1__state = restoredState;
            // Fields are restored by TryRestore
        }
    }

    // Existing: State dispatch
    switch (<>1__state)
    {
        case 0: goto Label0;
        case 1: goto Label1;
    }

    // ... method body ...

    // At each await point (MODIFIED):
    <>1__state = stateNumber;
    <>u__awaiter = awaiter;

    // NEW: Checkpoint before suspension
    if (persistenceService != null)
    {
        persistenceService.Checkpoint(this, stateNumber, "<>MethodId");
    }

    builder.AwaitOnCompleted(ref awaiter, ref this);
    return;

Label0:
    // ... resume point ...
}
```

---

## Files to Modify

### 1. `AsyncMethodToStateMachineRewriter.cs`

**Location**: `src/roslyn/src/Compilers/CSharp/Portable/Lowering/AsyncRewriter/`

#### Change 1: Add persistence-related fields

```csharp
// Add after line ~60 (after _placeholderMap declaration)

/// <summary>
/// Local variable to cache the persistence service reference.
/// </summary>
private LocalSymbol? _persistenceServiceLocal;

/// <summary>
/// Whether this method should have persistence support.
/// </summary>
private readonly bool _enablePersistence;

/// <summary>
/// The method ID used for persistence (derived from containing type + method name).
/// </summary>
private readonly string _persistenceMethodId;
```

#### Change 2: Modify constructor

```csharp
// In constructor (line ~64-96), add:

// Check for [Persistable] attribute on the method
_enablePersistence = method.GetAttributes().Any(a =>
    a.AttributeClass?.Name == "PersistableAttribute" ||
    a.AttributeClass?.ToDisplayString() == "DOTNExT.Persistence.PersistableAttribute");

if (_enablePersistence)
{
    _persistenceMethodId = $"{method.ContainingType.ToDisplayString()}.{method.Name}";
}
```

#### Change 3: Modify `GenerateMoveNext` (line 133)

```csharp
internal void GenerateMoveNext(BoundStatement body, MethodSymbol moveNextMethod)
{
    F.CurrentFunction = moveNextMethod;
    BoundStatement rewrittenBody = VisitBody(body);

    ImmutableArray<StateMachineFieldSymbol> rootScopeHoistedLocals;
    TryUnwrapBoundStateMachineScope(ref rewrittenBody, out rootScopeHoistedLocals);

    var bodyBuilder = ArrayBuilder<BoundStatement>.GetInstance();

    bodyBuilder.Add(F.HiddenSequencePoint());
    bodyBuilder.Add(F.Assignment(F.Local(cachedState), F.Field(F.This(), stateField)));
    bodyBuilder.Add(CacheThisIfNeeded());

    // NEW: Add persistence restoration check
    if (_enablePersistence)
    {
        bodyBuilder.Add(GeneratePersistenceRestorationCheck());
    }

    var exceptionLocal = F.SynthesizedLocal(F.WellKnownType(WellKnownType.System_Exception));
    // ... rest of method unchanged ...
}
```

#### Change 4: Add `GeneratePersistenceRestorationCheck` method

```csharp
/// <summary>
/// Generates code to check for and apply persisted state restoration.
/// </summary>
private BoundStatement GeneratePersistenceRestorationCheck()
{
    // var persistenceService = AsyncPersistenceContext.Current;
    // if (persistenceService != null)
    // {
    //     var restoredState = persistenceService.TryRestore(this, methodId);
    //     if (restoredState >= 0)
    //     {
    //         <>1__state = restoredState;
    //     }
    // }

    _persistenceServiceLocal = F.SynthesizedLocal(
        GetPersistenceServiceType(),
        syntax: F.Syntax,
        kind: SynthesizedLocalKind.LoweringTemp);

    var statements = ArrayBuilder<BoundStatement>.GetInstance();

    // var persistenceService = AsyncPersistenceContext.Current;
    statements.Add(F.Assignment(
        F.Local(_persistenceServiceLocal),
        F.StaticCall(GetAsyncPersistenceContextType(), "get_Current")));

    // if (persistenceService != null) { ... }
    var restoredStateLocal = F.SynthesizedLocal(
        F.SpecialType(SpecialType.System_Int32),
        syntax: F.Syntax,
        kind: SynthesizedLocalKind.LoweringTemp);

    var restoreBlock = F.Block(
        // var restoredState = persistenceService.TryRestore(this, methodId);
        F.Assignment(
            F.Local(restoredStateLocal),
            F.Call(F.Local(_persistenceServiceLocal),
                GetTryRestoreMethod(),
                F.This(),
                F.Literal(_persistenceMethodId))),
        // if (restoredState >= 0) { <>1__state = restoredState; }
        F.If(
            F.Binary(BinaryOperatorKind.IntGreaterThanOrEqual,
                F.SpecialType(SpecialType.System_Boolean),
                F.Local(restoredStateLocal),
                F.Literal(0)),
            F.Assignment(F.Field(F.This(), stateField), F.Local(restoredStateLocal))));

    statements.Add(F.If(
        F.Binary(BinaryOperatorKind.ObjectNotEqual,
            F.SpecialType(SpecialType.System_Boolean),
            F.Local(_persistenceServiceLocal),
            F.Null(GetPersistenceServiceType())),
        restoreBlock));

    return F.Block(
        ImmutableArray.Create(_persistenceServiceLocal, restoredStateLocal),
        statements.ToImmutableAndFree());
}
```

#### Change 5: Modify `GenerateAwaitForIncompleteTask` (line 446)

```csharp
private BoundBlock GenerateAwaitForIncompleteTask(LocalSymbol awaiterTemp, BoundAwaitExpressionDebugInfo debugInfo)
{
    var awaitSyntax = awaiterTemp.GetDeclaratorSyntax();
    AddResumableState(awaitSyntax, debugInfo.AwaitId, out StateMachineState stateNumber, out GeneratedLabelSymbol resumeLabel);

    TypeSymbol awaiterFieldType = awaiterTemp.Type.IsVerifierReference()
        ? F.SpecialType(SpecialType.System_Object)
        : awaiterTemp.Type;

    FieldSymbol awaiterField = GetAwaiterField(awaiterFieldType);

    var blockBuilder = ArrayBuilder<BoundStatement>.GetInstance();

    blockBuilder.Add(
        // this.state = cachedState = stateForLabel
        GenerateSetBothStates(stateNumber));

    blockBuilder.Add(
        // Emit await yield point to be injected into PDB
        F.NoOp(NoOpStatementFlavor.AwaitYieldPoint));

    blockBuilder.Add(
        // this.<>t__awaiter = $awaiterTemp
        F.Assignment(
            F.Field(F.This(), awaiterField),
            (TypeSymbol.Equals(awaiterField.Type, awaiterTemp.Type, TypeCompareKind.ConsiderEverything2))
                ? F.Local(awaiterTemp)
                : F.Convert(awaiterFieldType, F.Local(awaiterTemp))));

    // NEW: Add checkpoint call before suspension
    if (_enablePersistence)
    {
        blockBuilder.Add(GenerateCheckpointCall(stateNumber));
    }

    blockBuilder.Add(awaiterTemp.Type.IsDynamic()
        ? GenerateAwaitOnCompletedDynamic(awaiterTemp)
        : GenerateAwaitOnCompleted(awaiterTemp.Type, awaiterTemp));

    // ... rest unchanged ...
}
```

#### Change 6: Add `GenerateCheckpointCall` method

```csharp
/// <summary>
/// Generates: if (persistenceService != null) persistenceService.Checkpoint(this, stateNumber, methodId);
/// </summary>
private BoundStatement GenerateCheckpointCall(StateMachineState stateNumber)
{
    Debug.Assert(_persistenceServiceLocal != null);

    return F.If(
        F.Binary(BinaryOperatorKind.ObjectNotEqual,
            F.SpecialType(SpecialType.System_Boolean),
            F.Local(_persistenceServiceLocal),
            F.Null(GetPersistenceServiceType())),
        F.ExpressionStatement(
            F.Call(F.Local(_persistenceServiceLocal),
                GetCheckpointMethod(),
                F.This(),
                F.Literal((int)stateNumber),
                F.Literal(_persistenceMethodId))));
}
```

#### Change 7: Add helper methods for type/method resolution

```csharp
private NamedTypeSymbol? _asyncPersistenceContextType;
private NamedTypeSymbol? _persistenceServiceType;

private NamedTypeSymbol GetAsyncPersistenceContextType()
{
    return _asyncPersistenceContextType ??= F.Compilation.GetTypeByMetadataName(
        "DOTNExT.Persistence.AsyncPersistenceContext")!;
}

private NamedTypeSymbol GetPersistenceServiceType()
{
    return _persistenceServiceType ??= F.Compilation.GetTypeByMetadataName(
        "DOTNExT.Persistence.IAsyncPersistenceService")!;
}

private MethodSymbol GetTryRestoreMethod()
{
    return (MethodSymbol)GetPersistenceServiceType()
        .GetMembers("TryRestore").First();
}

private MethodSymbol GetCheckpointMethod()
{
    return (MethodSymbol)GetPersistenceServiceType()
        .GetMembers("Checkpoint").First();
}
```

---

## Required Runtime Types

The generated code depends on these types existing at runtime:

```csharp
namespace DOTNExT.Persistence
{
    public static class AsyncPersistenceContext
    {
        public static IAsyncPersistenceService? Current { get; }
        public static IDisposable SetCurrent(IAsyncPersistenceService? service);
    }

    public interface IAsyncPersistenceService
    {
        void Checkpoint(object stateMachine, int stateNumber, string methodId);
        int TryRestore(object stateMachine, string methodId);
        void Complete(string methodId, object? result);
        void Fault(string methodId, Exception exception);
    }
}
```

---

## Challenges and Solutions

### Challenge 1: Type Resolution

The compiler needs to resolve `DOTNExT.Persistence.AsyncPersistenceContext` at compile time.

**Solution**: Make persistence opt-in via:
1. A compiler flag (`/persistence+`)
2. Or presence of the `[Persistable]` attribute
3. Only attempt type resolution if persistence is requested

### Challenge 2: State Machine is a Struct

Most async state machines are structs. Boxing may be needed for the `Checkpoint` call.

**Solution**: The `Checkpoint(object stateMachine, ...)` signature accepts boxed struct. The persistence service uses reflection to read/write fields.

### Challenge 3: Method ID Uniqueness

For distributed scenarios, the method ID must be globally unique.

**Solution**: Use fully-qualified type name + method name + parameter hash. This can be computed at compile time.

---

## Testing Strategy

1. Create a small test project with `[Persistable]` methods
2. Compile with modified Roslyn
3. Decompile the result to verify generated code
4. Run with `InMemoryAsyncPersistenceService` and verify checkpoints

---

## Alternative: Source Generator Approach

Instead of modifying Roslyn core, we could use a **Source Generator** that:
1. Finds methods with `[Persistable]` attribute
2. Generates wrapper implementations

This is faster to implement and test, though less integrated.

See `PersistableAsyncSourceGenerator` for this approach.

---

*This document describes the design. Implementation is tracked in CURRENT-WORK.md.*
