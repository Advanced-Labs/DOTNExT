# Code Generation System

## Overview

Orleans uses **compile-time code generation** via Roslyn Source Generators to create all the infrastructure code needed for serialization and RPC. This eliminates runtime reflection, enables type safety, and provides excellent performance.

The code generator is one of Orleans' most critical components - without it, you'd need to write hundreds of lines of boilerplate per grain.

## Why Code Generation?

### The Problem

Distributed systems need infrastructure code for:

1. **Serialization**: Converting objects to bytes for network transmission
2. **RPC Proxies**: Client-side stubs that look like local method calls
3. **RPC Dispatchers**: Server-side code that invokes the actual grain method
4. **Type Discovery**: Runtime needs to know about all grain types

Writing this by hand would be:
- **Tedious**: Hundreds of lines per grain type
- **Error-prone**: Easy to make mistakes
- **Unmaintainable**: Changes to grain interfaces require updating multiple files
- **Slow**: Reflection-based alternatives have high overhead

### The Solution

Orleans generates all this code automatically at **compile-time** using a **Roslyn Source Generator**:

- **Zero runtime cost**: No reflection or dynamic code generation
- **Type-safe**: Compiler validates everything
- **Maintainable**: Just update the grain interface
- **Fast**: Generated code is optimized

## What Does CodeGen Generate?

For every grain in your application, Orleans generates:

### 1. Serializers (`FieldCodec<T>`)

Efficient serialization for:
- Grain method parameters and return values
- Grain state
- Any type marked with `[GenerateSerializer]`

**Example**:
```csharp
// Input
[GenerateSerializer]
public class UserState
{
    [Id(0)] public string Name { get; set; }
    [Id(1)] public int Age { get; set; }
}

// Generated
internal sealed class Codec_UserState : IFieldCodec<UserState>
{
    public void WriteField<TBufferWriter>(
        ref Writer<TBufferWriter> writer,
        uint fieldIdDelta,
        Type expectedType,
        UserState value)
    {
        // Write field 0 (Name)
        StringCodec.WriteField(ref writer, 0, typeof(string), value.Name);
        // Write field 1 (Age)
        Int32Codec.WriteField(ref writer, 1, typeof(int), value.Age);
    }

    public UserState ReadValue<TInput>(
        ref Reader<TInput> reader,
        Field field)
    {
        var result = new UserState();
        uint fieldId = 0;
        while (true)
        {
            var header = reader.ReadFieldHeader();
            if (header.IsEndBaseOrEndObject) break;

            fieldId += header.FieldIdDelta;
            switch (fieldId)
            {
                case 0: result.Name = StringCodec.ReadValue(ref reader, header); break;
                case 1: result.Age = Int32Codec.ReadValue(ref reader, header); break;
                default: reader.ConsumeUnknownField(header); break;
            }
        }
        return result;
    }
}
```

### 2. Copiers (`IDeepCopier<T>`)

Deep copy implementations for immutability:

```csharp
internal sealed class Copier_UserState : IDeepCopier<UserState>
{
    public UserState DeepCopy(UserState input, CopyContext context)
    {
        if (context.TryGetCopy(input, out UserState result))
            return result;

        result = new UserState
        {
            Name = input.Name, // strings are immutable
            Age = input.Age    // ints are value types
        };

        context.RecordCopy(input, result);
        return result;
    }
}
```

### 3. Grain Proxies (`GrainReference` subclass)

Client-side proxies that implement grain interfaces:

```csharp
// Input
public interface IUserGrain : IGrainWithStringKey
{
    Task<string> GetName();
    Task SetName(string name);
}

// Generated
internal sealed class UserGrainProxy : GrainReference, IUserGrain
{
    public UserGrainProxy(GrainReferenceShared shared) : base(shared) { }

    Task<string> IUserGrain.GetName()
    {
        return base.InvokeAsync<string>(
            new UserGrain_GetName_Invokable());
    }

    Task IUserGrain.SetName(string name)
    {
        return base.InvokeAsync(
            new UserGrain_SetName_Invokable { arg0 = name });
    }
}
```

### 4. Invokables (`IInvokable` implementation)

Server-side method dispatchers:

```csharp
[GenerateSerializer]
internal sealed class UserGrain_GetName_Invokable : IInvokable
{
    public ValueTask<Response> Invoke(ITargetHolder target)
    {
        var grain = target.GetTarget<IUserGrain>();
        return grain.GetName().ToResponse();
    }

    public MethodInfo GetMethod() => typeof(IUserGrain).GetMethod("GetName");
}

[GenerateSerializer]
internal sealed class UserGrain_SetName_Invokable : IInvokable
{
    [Id(0)] public string arg0;

    public ValueTask<Response> Invoke(ITargetHolder target)
    {
        var grain = target.GetTarget<IUserGrain>();
        return grain.SetName(arg0).ToResponse();
    }

    public MethodInfo GetMethod() => typeof(IUserGrain).GetMethod("SetName");
}
```

### 5. Activators (`IActivator<T>`)

Object construction factories:

```csharp
internal sealed class Activator_UserState : IActivator<UserState>
{
    public UserState Create() => new UserState();
}
```

### 6. Metadata (Type Manifests)

Type discovery information:

```csharp
[assembly: RegisterActivator(typeof(Activator_UserState))]
[assembly: RegisterCodec(typeof(Codec_UserState))]
[assembly: RegisterCopier(typeof(Copier_UserState))]
```

## How Code Generation Works

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                 C# Source Code                          │
│  (Grain interfaces, [GenerateSerializer] types)        │
└────────────────────┬────────────────────────────────────┘
                     │
                     ↓
┌────────────────────────────────────────────────────────┐
│              Roslyn Compiler (csc.exe)                 │
│  ┌──────────────────────────────────────────────────┐ │
│  │      Orleans Source Generator (Analyzer)         │ │
│  │                                                  │ │
│  │  1. Discovery Phase                              │ │
│  │     - Scan for grain interfaces                  │ │
│  │     - Find [GenerateSerializer] types            │ │
│  │                                                  │ │
│  │  2. Analysis Phase                               │ │
│  │     - Build type models                          │ │
│  │     - Validate grain interfaces                  │ │
│  │                                                  │ │
│  │  3. Generation Phase                             │ │
│  │     - Generate proxies, invokables               │ │
│  │     - Generate serializers, copiers              │ │
│  │     - Generate metadata                          │ │
│  │                                                  │ │
│  │  4. Output                                       │ │
│  │     - {AssemblyName}.orleans.g.cs                │ │
│  └──────────────────────────────────────────────────┘ │
└────────────────────┬────────────────────────────────────┘
                     │
                     ↓
┌────────────────────────────────────────────────────────┐
│              Compiled Assembly (.dll)                   │
│  - Your grain types                                    │
│  - Generated infrastructure code                       │
└────────────────────────────────────────────────────────┘
```

### Phase 1: Discovery

The generator scans the compilation for:

**Grain Interfaces**:
```csharp
// Found: Interfaces inheriting from IGrain
public interface IUserGrain : IGrainWithStringKey
{
    Task<string> GetName();
}
```

**Serializable Types**:
```csharp
// Found: Types with [GenerateSerializer]
[GenerateSerializer]
public class UserState
{
    [Id(0)] public string Name { get; set; }
}
```

**Method Serialization**:
```csharp
// Found: Interfaces with [GenerateMethodSerializers]
[GenerateMethodSerializers]
public interface IUserGrain : IGrain
{
    // All methods need invokables
}
```

### Phase 2: Analysis

For each discovered type, build a model:

**SerializableTypeDescription**:
```csharp
class SerializableTypeDescription
{
    INamedTypeSymbol Type;
    List<SerializableMember> Members;
    bool IsImmutable;
    bool UseActivator;
}

class SerializableMember
{
    string Name;
    ITypeSymbol Type;
    uint FieldId; // from [Id(N)]
    bool IsRequired;
}
```

**GrainInterfaceDescription**:
```csharp
class GrainInterfaceDescription
{
    INamedTypeSymbol InterfaceType;
    List<GrainMethodDescription> Methods;
    GrainIdKeyType KeyType; // String, Guid, Int, Compound
}

class GrainMethodDescription
{
    string Name;
    ITypeSymbol ReturnType;
    List<Parameter> Parameters;
    uint MethodId; // hash of signature
}
```

### Phase 3: Code Generation

**SerializerGenerator**:
- Creates `Codec_{TypeName}` classes
- Implements `IFieldCodec<T>` interface
- Uses `Writer<T>` and `Reader<T>` APIs
- Handles version tolerance via field IDs

**CopierGenerator**:
- Creates `Copier_{TypeName}` classes
- Implements `IDeepCopier<T>` interface
- Handles circular references via `CopyContext`

**ProxyGenerator**:
- Creates `{GrainName}Proxy` classes
- Inherits from `GrainReference`
- Implements all grain interface methods
- Marshals calls to `InvokeAsync<T>(IInvokable)`

**InvokableGenerator**:
- Creates `{GrainName}_{MethodName}_Invokable` classes
- Implements `IInvokable` interface
- Contains method parameters as fields (with `[Id]`)
- `Invoke()` method calls target grain

**ActivatorGenerator**:
- Creates `Activator_{TypeName}` classes
- Implements `IActivator<T>`
- Calls appropriate constructor

**MetadataGenerator**:
- Creates `[assembly: RegisterCodec(...)]` attributes
- Creates `[assembly: RegisterCopier(...)]` attributes
- Creates `[assembly: RegisterActivator(...)]` attributes
- Enables runtime discovery

### Phase 4: Output

All generated code goes into a single file:
```
obj/Debug/net8.0/generated/Orleans.CodeGenerator/
    Orleans.CodeGenerator.OrleansSourceGenerator/
        MyAssembly.orleans.g.cs
```

**File Structure**:
```csharp
// <auto-generated/>
#nullable enable

// Serializers
namespace Orleans.Serialization.Codecs
{
    internal sealed class Codec_UserState : IFieldCodec<UserState> { ... }
}

// Copiers
namespace Orleans.Serialization.Cloning
{
    internal sealed class Copier_UserState : IDeepCopier<UserState> { ... }
}

// Grain Proxies
namespace MyNamespace
{
    internal sealed class UserGrainProxy : GrainReference, IUserGrain { ... }
}

// Invokables
namespace MyNamespace
{
    [GenerateSerializer]
    internal sealed class UserGrain_GetName_Invokable : IInvokable { ... }
}

// Metadata
[assembly: RegisterCodec(typeof(Codec_UserState))]
[assembly: RegisterCopier(typeof(Copier_UserState))]
[assembly: RegisterActivator(typeof(Activator_UserState))]
```

## Roslyn Source Generator Integration

### Source Generator API

Orleans implements `IIncrementalGenerator`:

```csharp
[Generator]
public sealed class OrleansSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Register syntax receiver
        var types = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsCandidateType,
                transform: GetSemanticModel)
            .Where(t => t != null);

        // 2. Register source generator
        context.RegisterSourceOutput(types, GenerateSource);
    }
}
```

### Execution Model

**Incremental**:
- Only regenerates when relevant files change
- Caches analysis results
- Improves build performance

**Parallel**:
- Processes multiple types concurrently
- Roslyn manages parallelism

**Safe**:
- Cannot modify existing code
- Only adds new source files
- Cannot cause side effects

### Design-Time vs. Build-Time

**Design-Time** (Visual Studio):
- Generator is **disabled** by default
- Prevents IDE slowdowns
- Use `dotnet build` to see generated code

**Build-Time** (dotnet build):
- Generator always runs
- Output written to `obj/` directory
- Included in compilation

### Configuration

**MSBuild Properties**:
```xml
<PropertyGroup>
    <!-- Enable generator in IDE (not recommended) -->
    <Orleans_DesignTimeBuild>true</Orleans_DesignTimeBuild>

    <!-- Configure field ID generation -->
    <Orleans_GenerateFieldIds>PublicProperties</Orleans_GenerateFieldIds>

    <!-- Specify immutable attributes -->
    <Orleans_ImmutableAttributes>MyNamespace.ImmutableAttribute</Orleans_ImmutableAttributes>
</PropertyGroup>
```

## Detailed Generator Deep-Dive

### Serializer Generation

**Input Type**:
```csharp
[GenerateSerializer]
public class Order
{
    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public string CustomerName { get; set; }
    [Id(2)] public decimal Total { get; set; }
    [Id(3)] public List<string> Items { get; set; }
}
```

**Generated Codec**:
```csharp
internal sealed class Codec_Order : IFieldCodec<Order>
{
    private static readonly Type CodecFieldType = typeof(Order);

    public void WriteField<TBufferWriter>(
        ref Writer<TBufferWriter> writer,
        uint fieldIdDelta,
        Type expectedType,
        Order value)
        where TBufferWriter : IBufferWriter<byte>
    {
        if (value is null)
        {
            ReferenceCodec.WriteNullReference(ref writer, fieldIdDelta, expectedType);
            return;
        }

        ReferenceCodec.MarkValueField(writer.Session);
        writer.WriteStartObject(fieldIdDelta, expectedType, CodecFieldType);

        // Field 0: OrderId
        GuidCodec.WriteField(ref writer, 0, typeof(Guid), value.OrderId);

        // Field 1: CustomerName
        StringCodec.WriteField(ref writer, 1, typeof(string), value.CustomerName);

        // Field 2: Total
        DecimalCodec.WriteField(ref writer, 2, typeof(decimal), value.Total);

        // Field 3: Items
        ListCodec<string>.WriteField(ref writer, 3, typeof(List<string>), value.Items);

        writer.WriteEndObject();
    }

    public Order ReadValue<TInput>(ref Reader<TInput> reader, Field field)
    {
        if (field.WireType == WireType.Reference)
            return ReferenceCodec.ReadReference<Order>(ref reader, field);

        field.EnsureWireType(WireType.TagDelimited);

        var result = new Order();
        ReferenceCodec.RecordObject(reader.Session, result);

        uint fieldId = 0;
        while (true)
        {
            var header = reader.ReadFieldHeader();
            if (header.IsEndBaseOrEndObject) break;

            fieldId += header.FieldIdDelta;
            switch (fieldId)
            {
                case 0:
                    result.OrderId = GuidCodec.ReadValue(ref reader, header);
                    break;
                case 1:
                    result.CustomerName = StringCodec.ReadValue(ref reader, header);
                    break;
                case 2:
                    result.Total = DecimalCodec.ReadValue(ref reader, header);
                    break;
                case 3:
                    result.Items = ListCodec<string>.ReadValue(ref reader, header);
                    break;
                default:
                    reader.ConsumeUnknownField(header);
                    break;
            }
        }

        return result;
    }
}
```

**Key Features**:
1. **Field IDs**: Enable adding/removing fields without breaking compatibility
2. **Wire Types**: Optimize encoding (varint, fixed, length-prefixed)
3. **Unknown Field Handling**: `ConsumeUnknownField()` for forward compatibility
4. **Reference Tracking**: Handles circular references via `ReferenceCodec`
5. **Null Handling**: Special encoding for null references

### Proxy and Invokable Generation

**Complete Example**:

```csharp
// Input Interface
public interface ICalculatorGrain : IGrainWithIntegerKey
{
    Task<int> Add(int a, int b);
    Task<int> Multiply(int a, int b);
}

// Generated Proxy
internal sealed class CalculatorGrainProxy : GrainReference, ICalculatorGrain
{
    public CalculatorGrainProxy(GrainReferenceShared shared) : base(shared) { }

    Task<int> ICalculatorGrain.Add(int a, int b)
    {
        var invokable = new CalculatorGrain_Add_Invokable
        {
            arg0 = a,
            arg1 = b
        };
        return base.InvokeAsync<int>(invokable);
    }

    Task<int> ICalculatorGrain.Multiply(int a, int b)
    {
        var invokable = new CalculatorGrain_Multiply_Invokable
        {
            arg0 = a,
            arg1 = b
        };
        return base.InvokeAsync<int>(invokable);
    }
}

// Generated Invokables
[GenerateSerializer]
internal sealed class CalculatorGrain_Add_Invokable : IInvokable
{
    [Id(0)] public int arg0;
    [Id(1)] public int arg1;

    public ValueTask<Response> Invoke(ITargetHolder target)
    {
        var grain = target.GetTarget<ICalculatorGrain>();
        var task = grain.Add(arg0, arg1);
        return task.ToResponse();
    }

    public MethodInfo GetMethod() =>
        typeof(ICalculatorGrain).GetMethod(nameof(ICalculatorGrain.Add));

    public int GetArgumentCount() => 2;
    public object GetArgument(int index) => index switch
    {
        0 => arg0,
        1 => arg1,
        _ => throw new IndexOutOfRangeException()
    };
}

[GenerateSerializer]
internal sealed class CalculatorGrain_Multiply_Invokable : IInvokable
{
    [Id(0)] public int arg0;
    [Id(1)] public int arg1;

    public ValueTask<Response> Invoke(ITargetHolder target)
    {
        var grain = target.GetTarget<ICalculatorGrain>();
        var task = grain.Multiply(arg0, arg1);
        return task.ToResponse();
    }

    public MethodInfo GetMethod() =>
        typeof(ICalculatorGrain).GetMethod(nameof(ICalculatorGrain.Multiply));

    public int GetArgumentCount() => 2;
    public object GetArgument(int index) => index switch
    {
        0 => arg0,
        1 => arg1,
        _ => throw new IndexOutOfRangeException()
    };
}

// Serializers for Invokables (also generated)
internal sealed class Codec_CalculatorGrain_Add_Invokable
    : IFieldCodec<CalculatorGrain_Add_Invokable>
{
    // ... WriteField, ReadValue methods
}
```

### Method ID Hashing

Each grain method gets a unique ID based on its signature:

```csharp
uint ComputeMethodId(IMethodSymbol method)
{
    var signature = $"{method.ContainingType.ToDisplayString()}.{method.Name}";
    foreach (var param in method.Parameters)
        signature += $"_{param.Type.ToDisplayString()}";

    return Hash(signature);
}
```

**Example**:
```
ICalculatorGrain.Add(int, int) → hash("ICalculatorGrain.Add_int_int") → 0x12AB34CD
```

Used for method dispatch and versioning.

## Advanced Features

### Immutability Detection

Types marked immutable skip deep copying:

```csharp
[Immutable]
[GenerateSerializer]
public class ImmutableData
{
    [Id(0)] public string Value { get; init; }
}

// Generated copier just returns input
internal sealed class Copier_ImmutableData : IDeepCopier<ImmutableData>
{
    public ImmutableData DeepCopy(ImmutableData input, CopyContext context)
    {
        return input; // No need to copy
    }
}
```

### Converters

Custom serialization for types you don't control:

```csharp
[RegisterConverter]
public sealed class DateTimeOffsetConverter :
    IConverter<DateTimeOffset, DateTimeOffsetSurrogate>
{
    public DateTimeOffset ConvertFromSurrogate(
        in DateTimeOffsetSurrogate surrogate)
    {
        return new DateTimeOffset(
            surrogate.Ticks,
            TimeSpan.FromMinutes(surrogate.OffsetMinutes));
    }

    public DateTimeOffsetSurrogate ConvertToSurrogate(
        in DateTimeOffset value)
    {
        return new DateTimeOffsetSurrogate
        {
            Ticks = value.Ticks,
            OffsetMinutes = (short)value.Offset.TotalMinutes
        };
    }
}

[GenerateSerializer]
public struct DateTimeOffsetSurrogate
{
    [Id(0)] public long Ticks;
    [Id(1)] public short OffsetMinutes;
}
```

### Aliasing

Stable type names for versioning:

```csharp
[GenerateSerializer]
[Alias("app.UserProfile.v2")]
public class UserProfile
{
    // Type name in serialized form is "app.UserProfile.v2"
    // Can rename C# type without breaking compatibility
}
```

### Compound Keys

Support for multi-part grain keys:

```csharp
public interface IUserSessionGrain : IGrainWithGuidCompoundKey
{
    // Key is (Guid userId, string sessionId)
}

// Usage
var grain = grainFactory.GetGrain<IUserSessionGrain>(
    userId,
    keyExtension: sessionId);
```

## Performance Characteristics

### Serialization Performance

**Generated Codecs**:
- **Zero allocation** for value types
- Minimal allocation for reference types (only the object itself)
- No reflection, no expression trees
- Direct field access
- Inlining-friendly code

**Benchmark** (vs. JSON.NET):
```
| Method           | Mean     | Allocated |
|------------------|----------|-----------|
| Orleans          | 120 ns   | 0 B       |
| JsonNet          | 2,400 ns | 1,024 B   |
```

### RPC Performance

**Generated Proxies/Invokables**:
- **One allocation** per call (the `Invokable` object)
- Type-safe dispatch (no boxing)
- No reflection
- Direct method invocation

**Overhead**:
- Proxy creation: ~5 ns (cached)
- Invokable creation: ~10 ns
- Serialization: ~100-500 ns (depends on payload)
- Network: dominant factor

## Troubleshooting Code Generation

### Viewing Generated Code

**Location**:
```
{ProjectDir}/obj/{Configuration}/{TargetFramework}/generated/
    Orleans.CodeGenerator/
        Orleans.CodeGenerator.OrleansSourceGenerator/
            {AssemblyName}.orleans.g.cs
```

**Command**:
```bash
dotnet build
cat obj/Debug/net8.0/generated/Orleans.CodeGenerator/*/MyApp.orleans.g.cs
```

### Common Issues

**Issue**: No code generated

**Causes**:
- Missing `[GenerateSerializer]` attribute
- Grain interface doesn't inherit from `IGrain`
- Build didn't run (IDE using cached build)

**Fix**:
```bash
dotnet clean
dotnet build
```

---

**Issue**: `Could not find serializer for type X`

**Causes**:
- Type not marked with `[GenerateSerializer]`
- Type in different assembly without reference

**Fix**:
- Add `[GenerateSerializer]` to type
- Or use `[GenerateSerializer(IncludeBaseTypes = true)]`
- Or create custom codec

---

**Issue**: Performance degradation

**Causes**:
- Large number of fields (>50)
- Deep inheritance hierarchy
- Complex generic types

**Fix**:
- Flatten type hierarchy
- Split into smaller types
- Use custom codec for optimization

### Diagnostics

**MSBuild Verbosity**:
```bash
dotnet build /v:detailed
```

**Generator Logging**:
```xml
<PropertyGroup>
    <Orleans_CodeGeneratorLogLevel>Trace</Orleans_CodeGeneratorLogLevel>
</PropertyGroup>
```

## Summary

Orleans' code generation system:

1. **Runs at compile-time** via Roslyn Source Generators
2. **Generates** serializers, proxies, invokables, and metadata
3. **Eliminates** runtime reflection and overhead
4. **Provides** type safety and IDE support
5. **Enables** high performance RPC and serialization
6. **Supports** schema evolution and versioning

### Key Takeaways

- **Automatic**: Works transparently after adding attributes
- **Fast**: Zero runtime overhead
- **Safe**: Compile-time validation
- **Extensible**: Custom codecs and converters supported
- **Maintainable**: Generated code is readable and debuggable

### For Contributors

When working on codegen:
- Location: `src/Orleans.CodeGenerator/`
- Entry point: `OrleansSourceGenerator.cs`
- Tests: `test/CodeGenerator.Tests/`
- Use Roslyn APIs for syntax generation
- Maintain incremental generation support

---

**Next**: [Runtime and Activation System](05-runtime-activation.md)
