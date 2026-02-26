# Roslyn Build & Integration Procedures for DOTNExT

**For TAI (AI Co-Developer)** - Follow these procedures to build, test, and integrate the modified Roslyn compiler.

---

## Part 1: Building Modified Roslyn

### Prerequisites

```bash
# Check these are installed:
dotnet --version          # Need .NET 8.0+ SDK
git --version             # Git for version control

# On Windows, also need:
# - Visual Studio 2022 with ".NET Compiler Platform SDK" workload
# - OR Build Tools for Visual Studio 2022
```

### Step 1: Navigate to Roslyn Source

```bash
cd /home/user/DOTNExT/src/roslyn
```

### Step 2: Restore Dependencies

```bash
# Restore all NuGet packages
dotnet restore Roslyn.sln
```

### Step 3: Build the C# Compiler (csc)

The modified file is in the `Microsoft.CodeAnalysis.CSharp` project.

```bash
# Build just the C# compiler (fastest for iteration)
dotnet build src/Compilers/CSharp/Portable/Microsoft.CodeAnalysis.CSharp.csproj -c Release

# OR build the full compiler toolset
dotnet build src/Compilers/CSharp/csc/csc.csproj -c Release
```

### Step 4: Locate Build Outputs

After successful build, find outputs here:

```
# Core compiler library (what we modified):
artifacts/bin/Microsoft.CodeAnalysis.CSharp/Release/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll

# Full csc.exe compiler:
artifacts/bin/csc/Release/net8.0/csc.dll
artifacts/bin/csc/Release/net472/csc.exe  (Windows only)
```

### Step 5: Verify Build Success

```bash
# Check the DLL exists and has our changes
ls -la artifacts/bin/Microsoft.CodeAnalysis.CSharp/Release/netstandard2.0/

# Optionally verify the modification is present (search for DOTNExT string)
strings artifacts/bin/Microsoft.CodeAnalysis.CSharp/Release/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll | grep -i "DOTNExT"
```

---

## Part 2: Integration Options

There are THREE ways to use the modified Roslyn:

### Option A: Programmatic Compilation (RECOMMENDED for testing)

Use Roslyn as a library to compile code at runtime. This is what our test scenarios will do.

**No installation needed** - just reference the DLLs.

```csharp
// In test project, add project reference or DLL reference to:
// - Microsoft.CodeAnalysis.CSharp.dll (our modified version)
// - Microsoft.CodeAnalysis.dll (base library)

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Compile source code programmatically
var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
var compilation = CSharpCompilation.Create("TestAssembly")
    .AddSyntaxTrees(syntaxTree)
    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
    .AddReferences(/* DOTNExT.Persistence assembly */);

using var ms = new MemoryStream();
var result = compilation.Emit(ms);
if (result.Success)
{
    ms.Seek(0, SeekOrigin.Begin);
    var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
    // Execute the compiled code
}
```

### Option B: Custom SDK (for dotnet build integration)

Create a custom .NET SDK that uses our Roslyn. More complex but allows `dotnet build` to use our compiler.

```bash
# 1. Build Roslyn SDK packages
cd /home/user/DOTNExT/src/roslyn
dotnet build src/NuGet/Microsoft.Net.Compilers.Toolset/Microsoft.Net.Compilers.Toolset.Package.csproj -c Release

# 2. Packages will be in:
# artifacts/packages/Release/Shipping/

# 3. Create local NuGet feed
mkdir -p ~/.nuget/local-feed
cp artifacts/packages/Release/Shipping/*.nupkg ~/.nuget/local-feed/

# 4. Add to NuGet.config in test project:
# <packageSources>
#   <add key="local-dotnext" value="~/.nuget/local-feed" />
# </packageSources>

# 5. Reference in test project .csproj:
# <PackageReference Include="Microsoft.Net.Compilers.Toolset" Version="X.Y.Z" />
```

### Option C: Replace Global Roslyn (NOT RECOMMENDED)

This would affect all .NET builds on the machine. Don't do this for development.

---

## Part 3: Test Scenario Architecture

### How AsyncPersistenceScenarios Will Use Modified Roslyn

```
┌─────────────────────────────────────────────────────────────────┐
│                    AsyncPersistenceScenarios                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐    ┌──────────────────────────────────┐  │
│  │ Test Workflow    │    │  PersistableAsyncCompiler        │  │
│  │ Source Code      │───▶│  (uses modified Roslyn DLLs)     │  │
│  │ [Persistable]    │    │                                   │  │
│  │ async Task Foo() │    │  CSharpCompilation.Create()      │  │
│  └──────────────────┘    │  .Emit() to MemoryStream         │  │
│                          │  Load into AssemblyLoadContext    │  │
│                          └──────────────────────────────────┘  │
│                                       │                         │
│                                       ▼                         │
│                          ┌──────────────────────────────────┐  │
│                          │  Compiled Assembly (in memory)   │  │
│                          │  - Has auto-injected checkpoint  │  │
│                          │  - Has auto-injected restore     │  │
│                          └──────────────────────────────────┘  │
│                                       │                         │
│                                       ▼                         │
│                          ┌──────────────────────────────────┐  │
│                          │  Execute with Persistence        │  │
│                          │  AsyncPersistenceContext.Current │  │
│                          │  = InMemoryAsyncPersistenceService│  │
│                          └──────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Required Project Structure

```
AsyncPersistenceScenarios/
├── AsyncPersistenceScenarios.csproj
├── Services/
│   ├── PersistableAsyncCompiler.cs      # NEW: Uses modified Roslyn
│   ├── IAsyncPersistenceService.cs
│   ├── InMemoryAsyncPersistenceService.cs
│   └── AsyncPersistenceContext.cs
├── TestWorkflows/
│   ├── BasicWorkflows.cs                 # Manual checkpoint (existing)
│   ├── InstrumentedWorkflow.cs           # Hand-written demo (existing)
│   └── PersistableWorkflowSource.cs      # NEW: Source code strings
└── Program.cs
```

---

## Part 4: Implementation Steps for TAI

### Step 1: Build Modified Roslyn

```bash
cd /home/user/DOTNExT/src/roslyn
dotnet restore Roslyn.sln
dotnet build src/Compilers/CSharp/Portable/Microsoft.CodeAnalysis.CSharp.csproj -c Release

# Report back:
# - Did the build succeed?
# - List any errors or warnings
# - Confirm artifacts/bin/Microsoft.CodeAnalysis.CSharp/Release/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll exists
```

### Step 2: Create PersistableAsyncCompiler Service

Create this file in the test project:

**File**: `/home/user/DOTNExT/src/Scynapse/playground/AsyncPersistenceScenarios/Services/PersistableAsyncCompiler.cs`

```csharp
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AsyncPersistenceScenarios.Services;

/// <summary>
/// Compiles C# source code using the modified Roslyn compiler
/// that injects persistence calls into [Persistable] async methods.
/// </summary>
public class PersistableAsyncCompiler
{
    private readonly List<MetadataReference> _references;

    public PersistableAsyncCompiler()
    {
        // Add standard references
        _references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            // Add DOTNExT.Persistence types
            MetadataReference.CreateFromFile(typeof(DOTNExT.Persistence.AsyncPersistenceContext).Assembly.Location),
        };

        // Add runtime assemblies
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        _references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")));
        _references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Threading.Tasks.dll")));
    }

    public Assembly? CompileAndLoad(string sourceCode, out IEnumerable<Diagnostic> diagnostics)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var compilation = CSharpCompilation.Create(
            assemblyName: $"DynamicAssembly_{Guid.NewGuid():N}",
            syntaxTrees: new[] { syntaxTree },
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        diagnostics = result.Diagnostics;

        if (!result.Success)
        {
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);
        return AssemblyLoadContext.Default.LoadFromStream(ms);
    }
}
```

### Step 3: Update Project References

**File**: `/home/user/DOTNExT/src/Scynapse/playground/AsyncPersistenceScenarios/AsyncPersistenceScenarios.csproj`

Add reference to modified Roslyn:

```xml
<ItemGroup>
  <!-- Reference our modified Roslyn compiler -->
  <ProjectReference Include="../../../roslyn/src/Compilers/CSharp/Portable/Microsoft.CodeAnalysis.CSharp.csproj" />
  <!-- OR use DLL reference if project reference doesn't work -->
  <!-- <Reference Include="Microsoft.CodeAnalysis.CSharp">
    <HintPath>../../../roslyn/artifacts/bin/Microsoft.CodeAnalysis.CSharp/Release/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll</HintPath>
  </Reference> -->
</ItemGroup>
```

### Step 4: Create Test Source Code

**File**: `/home/user/DOTNExT/src/Scynapse/playground/AsyncPersistenceScenarios/TestWorkflows/PersistableWorkflowSource.cs`

```csharp
namespace AsyncPersistenceScenarios.TestWorkflows;

/// <summary>
/// Source code strings for testing the modified Roslyn compiler.
/// These will be compiled at runtime using our modified Roslyn.
/// </summary>
public static class PersistableWorkflowSource
{
    public const string SimpleWorkflow = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace DynamicWorkflows
{
    [Persistable]
    public class TestWorkflow
    {
        public async Task<int> SimpleCalculation(int input)
        {
            Console.WriteLine($""Step 1: input = {input}"");
            var step1 = await Task.Delay(100).ContinueWith(_ => input * 2);

            Console.WriteLine($""Step 2: step1 = {step1}"");
            var step2 = await Task.Delay(100).ContinueWith(_ => step1 + 10);

            Console.WriteLine($""Result: {step2}"");
            return step2;
        }
    }
}
";

    public const string PersistableAttribute = @"
namespace DOTNExT.Persistence
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class PersistableAttribute : Attribute { }
}
";
}
```

### Step 5: Add Challenge 7 to Program.cs

Add new menu option for testing with modified Roslyn compiler.

---

## Part 5: VS2022 Integration

### Will VS2022 Use the Modified Roslyn?

**NO, not automatically.** VS2022 has its own embedded Roslyn. To use modified Roslyn in VS:

1. **For IntelliSense**: Would need to replace VS components (not recommended)
2. **For Build**: Can configure project to use custom compiler

### Using Modified Roslyn for Builds in VS2022

Add to your project file:

```xml
<PropertyGroup>
  <!-- Use custom Roslyn toolset -->
  <CscToolPath>/path/to/DOTNExT/src/roslyn/artifacts/bin/csc/Release/net8.0/</CscToolPath>
  <CscToolExe>csc.dll</CscToolExe>
</PropertyGroup>
```

Or use the NuGet package approach from Option B above.

---

## Part 6: Verification Checklist for TAI

After completing the steps, verify:

```
[ ] Roslyn builds successfully (no errors)
[ ] Microsoft.CodeAnalysis.CSharp.dll exists in artifacts
[ ] AsyncPersistenceScenarios project compiles
[ ] PersistableAsyncCompiler can compile simple source code
[ ] Compiled code contains persistence calls (decompile to verify)
[ ] Checkpoint/restore works with dynamically compiled code
```

### How to Verify Persistence Code Was Injected

Use a decompiler (ILSpy, dnSpy, or dotPeek) to examine the compiled assembly:

```bash
# Install ILSpy CLI
dotnet tool install -g ilspycmd

# Decompile the dynamic assembly (save it first)
ilspycmd DynamicAssembly.dll -o decompiled/

# Look for:
# - AsyncPersistenceContext.Current
# - persistenceService.TryRestore
# - persistenceService.Checkpoint
```

---

## Quick Reference Commands for TAI

```bash
# Build Roslyn
cd /home/user/DOTNExT/src/roslyn
dotnet build src/Compilers/CSharp/Portable/Microsoft.CodeAnalysis.CSharp.csproj -c Release

# Build test project
cd /home/user/DOTNExT/src/Scynapse/playground/AsyncPersistenceScenarios
dotnet build

# Run test project
dotnet run

# Check for our modifications in compiled DLL
strings artifacts/bin/Microsoft.CodeAnalysis.CSharp/Release/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll | grep "DOTNExT"
```

---

*Last updated: 2025-11-30*
