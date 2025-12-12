# VAYRON SDK Design Document

> **Document Type:** Implementation Design
> **Version:** 1.0
> **Date:** 2025-12-07
> **Parent:** VAYRON-Architecture-Master.md
> **Status:** Design Phase - Priority Implementation Target

---

## 1. Overview

The VAYRON SDK is everything a developer needs to build applications on VAYRON. It hides NewOrleans, VCOM, and infrastructure complexity behind a clean developer experience.

**Philosophy:** Build real infrastructure first. No PoCs. Every investment compounds.

---

## 2. SDK Components

```
VAYRON SDK
├── VAYRON.Sdk                    # MSBuild SDK for project system
├── VAYRON.Core                   # Core runtime (wraps NewOrleans, VCOM)
├── VAYRON.Abstractions           # Interfaces and base types
├── VAYRON.Analyzers              # Roslyn analyzers for VARIA
├── VAYRON.CodeGen                # Source generators / Roslyn transforms
├── VAYRON.Tools                  # CLI tooling (vayron command)
├── VAYRON.VisualStudio           # VS2022 extension
└── Templates/                    # Project and item templates
    ├── VAYRON.Console
    ├── VAYRON.Library
    └── VAYRON.Service
```

---

## 3. Project System: VAYRON.Sdk

### 3.1 SDK Structure

```
VAYRON.Sdk/
├── Sdk/
│   ├── Sdk.props                 # Properties imported at start
│   ├── Sdk.targets               # Targets imported at end
│   └── VAYRON.Sdk.csproj         # SDK package project
├── build/
│   ├── VAYRON.Sdk.props
│   └── VAYRON.Sdk.targets
└── tools/
    └── (build tools if needed)
```

### 3.2 Sdk.props

```xml
<Project>
  <!-- VAYRON SDK Properties -->

  <PropertyGroup>
    <!-- Identify as VAYRON project -->
    <IsVayronProject>true</IsVayronProject>

    <!-- Use our Roslyn fork for compilation -->
    <UseVayronRoslyn Condition="'$(UseVayronRoslyn)' == ''">true</UseVayronRoslyn>

    <!-- Default framework -->
    <TargetFramework Condition="'$(TargetFramework)' == ''">net9.0</TargetFramework>

    <!-- Enable nullable -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- VARIA transformation -->
    <EnableVARIATransform Condition="'$(EnableVARIATransform)' == ''">true</EnableVARIATransform>

    <!-- DOTNExT targeting pack version (CRITICAL: enables DOTNExT-specific APIs) -->
    <VayronTargetingPackVersion Condition="'$(VayronTargetingPackVersion)' == ''">9.0.10</VayronTargetingPackVersion>
  </PropertyGroup>

  <!-- Override framework reference to use DOTNExT targeting pack -->
  <!-- This is what makes Environment.IsDotnext and other DOTNExT APIs visible to the compiler -->
  <ItemGroup>
    <FrameworkReference Update="Microsoft.NETCore.App" TargetingPackVersion="$(VayronTargetingPackVersion)" />
  </ItemGroup>

  <!-- Implicit package references -->
  <ItemGroup>
    <PackageReference Include="VAYRON.Core" Version="$(VayronVersion)" />
    <PackageReference Include="VAYRON.Abstractions" Version="$(VayronVersion)" />
    <PackageReference Include="VAYRON.Analyzers" Version="$(VayronVersion)" />
  </ItemGroup>

  <!-- Implicit usings for VAYRON -->
  <ItemGroup>
    <Using Include="VAYRON" />
    <Using Include="VAYRON.VCOM" />
    <Using Include="VAYRON.VNS" />
  </ItemGroup>

</Project>
```

**Note:** The `FrameworkReference Update` is critical. Without it, the compiler won't know about DOTNExT-specific APIs like `Environment.IsDotnext`. This was validated with the `test-isdonext` smoke test project.

### 3.3 Sdk.targets

```xml
<Project>
  <!-- VAYRON SDK Targets -->

  <!-- VARIA transformation target -->
  <Target Name="VARIATransform"
          BeforeTargets="CoreCompile"
          Condition="'$(EnableVARIATransform)' == 'true'">
    <!-- Transform VCOM types for VARIA -->
    <!-- This is where Roslyn codegen happens -->
  </Target>

  <!-- Embed VCOM type metadata -->
  <Target Name="EmbedVCOMMetadata"
          AfterTargets="CoreCompile">
    <!-- Store type info for runtime -->
  </Target>

  <!-- Package VAYRON artifacts -->
  <Target Name="PackageVayronArtifacts"
          AfterTargets="Build"
          Condition="'$(PackVayronTypes)' == 'true'">
    <!-- Package for deployment to VAYRON cluster -->
  </Target>

</Project>
```

### 3.4 Minimal .csproj

```xml
<Project Sdk="VAYRON.Sdk/1.0.0">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <!-- That's it. Everything else is implicit. -->
  </PropertyGroup>

</Project>
```

---

## 4. Project Templates

### 4.1 VAYRON Console Application

**Template ID:** `vayron.console`

**Files:**

`MyApp.csproj`:
```xml
<Project Sdk="VAYRON.Sdk/1.0.0">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

`Program.cs`:
```csharp
using VAYRON;

// Start VAYRON and run
await VayronHost.RunAsync(args, async vayron =>
{
    Console.WriteLine("Hello from VAYRON!");

    // Create a VCOM object
    var greeting = new Greeting { Message = "Hello, World!" };

    // It's already persisted. UUID assigned.
    Console.WriteLine($"Created greeting with UUID: {greeting.UUID}");

    // Find it later
    var found = await vayron.Find<Greeting>(greeting.UUID);
    Console.WriteLine($"Found: {found.Message}");
});
```

`Greeting.cs`:
```csharp
namespace MyApp;

public class Greeting
{
    public string Message { get; set; } = "";
}
```

`vayron.config.json`:
```json
{
  "node": {
    "name": "MyApp-Dev"
  },
  "persistence": {
    "provider": "memory"
  }
}
```

### 4.2 VAYRON Library

**Template ID:** `vayron.library`

**Files:**

`MyLib.csproj`:
```xml
<Project Sdk="VAYRON.Sdk/1.0.0">
  <PropertyGroup>
    <OutputType>Library</OutputType>
  </PropertyGroup>
</Project>
```

No Program.cs - just types.

### 4.3 VAYRON Service

**Template ID:** `vayron.service`

Long-running service with hosted lifetime.

`MyService.csproj`:
```xml
<Project Sdk="VAYRON.Sdk/1.0.0">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

`Program.cs`:
```csharp
using VAYRON;

await VayronHost.CreateBuilder(args)
    .ConfigureServices(services =>
    {
        // Add hosted services, etc.
    })
    .Build()
    .RunAsync();  // Runs until shutdown
```

---

## 5. CLI Tooling: vayron command

### 5.1 Commands

```bash
# Project creation
vayron new console MyApp          # Create console app
vayron new library MyLib          # Create library
vayron new service MyService      # Create service

# Build and run
vayron build                      # Build project
vayron run                        # Run project (starts VAYRON node)
vayron test                       # Run tests

# VCOM management
vayron types list                 # List types in project
vayron types info <TypeName>      # Show type details

# VNS operations
vayron find <query>               # Find objects
vayron inspect <uuid>             # Inspect object

# Node operations
vayron node status                # Show node status
vayron node connect <address>     # Connect to remote node

# Development
vayron watch                      # Watch for changes, hot reload
vayron repl                       # Interactive VAYRON REPL
```

### 5.2 Implementation

```csharp
// vayron CLI entry point
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandLineApplication
        {
            Name = "vayron",
            Description = "VAYRON Development CLI"
        };

        app.Command("new", cmd => ConfigureNewCommand(cmd));
        app.Command("build", cmd => ConfigureBuildCommand(cmd));
        app.Command("run", cmd => ConfigureRunCommand(cmd));
        app.Command("types", cmd => ConfigureTypesCommand(cmd));
        app.Command("find", cmd => ConfigureFindCommand(cmd));
        // ... etc

        return await app.ExecuteAsync(args);
    }
}
```

---

## 6. VS2022 Extension: VAYRON.VisualStudio

### 6.1 Extension Components

| Component | Purpose |
|-----------|---------|
| Project System | Support for VAYRON.Sdk projects |
| Item Templates | Add VCOM Type, Add VARIA Component |
| IntelliSense Provider | VNS-aware code completion |
| Debugger Extensions | VCOM object visualization |
| Property Pages | VAYRON project configuration UI |
| Tool Windows | VAYRON Explorer, VNS Browser |

### 6.2 Project System Integration

VS2022 uses the Common Project System (CPS). VAYRON.Sdk projects should "just work" because they're MSBuild-based.

What we need to add:
- Project flavor GUID for VAYRON-specific UI
- Custom property pages
- Item templates in Add New Item

### 6.3 IntelliSense Provider

The key differentiator: VNS-aware IntelliSense.

```csharp
// When developer types:
var order = vayron.Find<Order>("

// Our IntelliSense provider:
// 1. Queries local VNS (or simulated from project types)
// 2. Returns completion items for Order instances/patterns
// 3. Shows documentation from VCOM metadata

// For dynamic:
dynamic x = vayron.Find("

// IntelliSense shows:
// - All addressable types
// - Known instance patterns
// - "Search..." option for semantic
```

**Implementation approach:**

```csharp
[Export(typeof(IAsyncCompletionSource))]
[ContentType("CSharp")]
[Name("VNSCompletionSource")]
public class VNSCompletionSource : IAsyncCompletionSource
{
    public async Task<CompletionContext> GetCompletionContextAsync(
        IAsyncCompletionSession session,
        CompletionTrigger trigger,
        SnapshotPoint triggerLocation,
        SnapshotSpan applicableToSpan,
        CancellationToken token)
    {
        // Detect if we're in a VNS context (vayron.Find, etc.)
        // Query VNS for completions
        // Return completion items
    }
}
```

### 6.4 VAYRON Explorer Tool Window

A tool window showing:
- VCOM types in solution
- Active VAYRON node status
- VNS namespace browser
- Object inspector

```
┌─────────────────────────────────────────────────────────────┐
│ VAYRON Explorer                                      [─][□][×] │
├─────────────────────────────────────────────────────────────┤
│ ▼ Types                                                      │
│   ├── MyApp.Order                                            │
│   ├── MyApp.Customer                                         │
│   └── MyApp.OrderItem                                        │
│ ▼ Node: MyApp-Dev (Running)                                  │
│   ├── Status: Active                                         │
│   ├── Objects: 42                                            │
│   └── Memory: 128 MB                                         │
│ ▼ VNS Browser                                                │
│   └── [Search...]                                            │
└─────────────────────────────────────────────────────────────┘
```

### 6.5 Debugger Visualizer

When inspecting VCOM objects in debugger:

```
order (Order)                                              ▼
├── UUID: 3fa85f64-5717-4562-b3fc-2c963f66afa6
├── State: Active
├── Customer → (Customer) UUID: ...                   [Inspect]
├── Items: VCOMList<OrderItem> (3 items)                    ▼
│   ├── [0]: (OrderItem) UUID: ...
│   ├── [1]: (OrderItem) UUID: ...
│   └── [2]: (OrderItem) UUID: ...
├── Status: Pending
└── [VCOM Metadata]                                         ▼
    ├── TypeUUID: ...
    ├── BackingGrain: VCOMPodGrain/...
    └── Persisted: Yes
```

---

## 7. VAYRON.Core: The Runtime

### 7.1 What It Wraps

VAYRON.Core contains:
- NewOrleans (completely hidden)
- VCOM infrastructure
- VAYRON Kernel grain types
- VNS implementation
- Persistence configuration

### 7.2 VayronHost

The entry point for all VAYRON applications:

```csharp
public static class VayronHost
{
    /// <summary>
    /// Simple run with action.
    /// </summary>
    public static async Task RunAsync(
        string[] args,
        Func<IVayronContext, Task> action)
    {
        var host = CreateBuilder(args).Build();
        await host.StartAsync();
        try
        {
            await action(host.Context);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// Create builder for advanced configuration.
    /// </summary>
    public static IVayronHostBuilder CreateBuilder(string[] args)
    {
        return new VayronHostBuilder(args);
    }

    /// <summary>
    /// Simple run until shutdown (for services).
    /// </summary>
    public static async Task RunAsync(string[] args)
    {
        await CreateBuilder(args).Build().RunAsync();
    }
}
```

### 7.3 IVayronContext

What developers interact with:

```csharp
public interface IVayronContext
{
    /// <summary>
    /// Find objects by VNS query.
    /// </summary>
    Task<IReadOnlyList<T>> Find<T>(string query) where T : class;

    /// <summary>
    /// Find single object by UUID.
    /// </summary>
    Task<T> Find<T>(Guid uuid) where T : class;

    /// <summary>
    /// Semantic search.
    /// </summary>
    Task<IReadOnlyList<T>> Search<T>(string naturalLanguage) where T : class;

    /// <summary>
    /// Access to types.
    /// </summary>
    IVTypeRegistry Types { get; }

    /// <summary>
    /// Access to namespaces (VNS).
    /// </summary>
    IVNamespaceRegistry Namespaces { get; }

    /// <summary>
    /// Current node information.
    /// </summary>
    IVayronNode Node { get; }
}
```

### 7.4 Configuration

`vayron.config.json`:
```json
{
  "node": {
    "name": "MyNode",
    "clusterId": "my-cluster",
    "serviceId": "my-service"
  },
  "persistence": {
    "provider": "ravendb",
    "connectionString": "http://localhost:8080",
    "database": "MyVayronDB"
  },
  "graph": {
    "provider": "neo4j",
    "connectionString": "bolt://localhost:7687"
  },
  "vns": {
    "rootNamespace": "MyApp"
  }
}
```

Developer never sees Orleans config. It's derived from this.

---

## 8. Implementation Plan

### Phase 1: Foundation (Week 1-2)

1. **VAYRON.Sdk skeleton**
   - Basic Sdk.props and Sdk.targets
   - Project builds with `dotnet build`

2. **Console template**
   - Minimal working template
   - `vayron new console` works

3. **VAYRON.Core minimal**
   - VayronHost.RunAsync works
   - Single-node NewOrleans hidden inside
   - In-memory persistence only

### Phase 2: VCOM Basics (Week 3-4)

4. **VObject base type**
   - UUID generation
   - Basic lifecycle

5. **VCOMPodGrain**
   - Hosts VObject instances
   - State persistence

6. **VARIA minimal transform**
   - Simple types work
   - Properties persist

### Phase 3: Developer Experience (Week 5-6)

7. **VS2022 extension skeleton**
   - Project recognition
   - Basic item templates

8. **CLI tooling**
   - `vayron` command basics
   - build, run, new

9. **Debugging support**
   - Object visualization

### Phase 4: VNS & Polish (Week 7-8)

10. **VNS basic**
    - Named resolution
    - Query resolution

11. **IntelliSense**
    - VNS-aware completion

12. **Documentation**
    - Getting started guide
    - API reference

---

## 9. Open Questions

### 9.1 SDK Distribution

**Question:** How do developers get the SDK?

**Options:**
1. NuGet feed (public or private)
2. dotnet tool install
3. VS extension includes SDK
4. Manual download

**Recommendation:** Start with private NuGet feed. Move to public when ready.

### 9.2 Roslyn Fork Integration

**Question:** How do we use our Roslyn fork for VARIA transforms?

**Options:**
1. SDK includes forked compiler
2. SDK downloads compiler on first build
3. VS extension replaces compiler

**Recommendation:** SDK includes compiler. Simplest distribution.

### 9.3 NewOrleans Packaging

**Question:** How is NewOrleans packaged?

**Options:**
1. VAYRON.Core includes NewOrleans DLLs directly
2. VAYRON.Core references private NewOrleans packages
3. IL-merge NewOrleans into VAYRON.Core

**Recommendation:** Private NewOrleans packages. Cleaner dependency management.

---

## 10. Success Criteria

**Milestone 1: Developer can create and run VAYRON app**
- [ ] `vayron new console MyApp` creates project
- [ ] `vayron run` starts app
- [ ] Simple VCOM type works
- [ ] State persists across restarts

**Milestone 2: VS2022 integration works**
- [ ] Project opens in VS2022
- [ ] Build works (F6)
- [ ] Debug works (F5)
- [ ] IntelliSense works for basic scenarios

**Milestone 3: VNS works**
- [ ] `vayron.Find<T>()` resolves objects
- [ ] IntelliSense shows VNS completions
- [ ] Types discoverable across solution

---

*This document defines the VAYRON SDK implementation. It's the priority target because good tooling compounds.*

*Version 1.0 - 2025-12-07*
