using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AsyncPersistenceScenarios.Services;

/// <summary>
/// Compiles C# source code using the Roslyn compiler.
///
/// IMPORTANT: This service currently uses the STOCK Roslyn compiler from NuGet.
/// To test automatic persistence injection, you must replace the Roslyn NuGet
/// packages with references to our modified Roslyn DLLs:
///
/// Option 1: Reference modified Roslyn DLLs directly
///   - Build src/roslyn with 'dotnet build'
///   - Replace Microsoft.CodeAnalysis.CSharp NuGet with the built DLLs
///   - Located in: src/roslyn/artifacts/bin/Microsoft.CodeAnalysis.CSharp/Debug/netstandard2.0/
///
/// Option 2: Create local NuGet package
///   - Build Roslyn with 'dotnet pack'
///   - Add local NuGet source pointing to artifacts
///   - Update package reference versions
///
/// Until modified Roslyn is integrated, Challenge 7 shows "No checkpoints created"
/// because the stock compiler doesn't know about [Persistable].
///
/// This service allows us to:
/// 1. Compile source code with [Persistable] methods at runtime
/// 2. Load the compiled assembly into memory
/// 3. Execute the methods with persistence context active
/// 4. Verify that checkpoint/restore was automatically injected
/// </summary>
public class PersistableAsyncCompiler
{
    private readonly List<MetadataReference> _references;
    private readonly List<Diagnostic> _lastDiagnostics = new();

    /// <summary>
    /// Gets the diagnostics from the last compilation attempt.
    /// </summary>
    public IReadOnlyList<Diagnostic> LastDiagnostics => _lastDiagnostics;

    public PersistableAsyncCompiler()
    {
        _references = new List<MetadataReference>();

        // Add core runtime references
        AddRuntimeReferences();

        // Add DOTNExT.Persistence types (from our test project)
        AddPersistenceReferences();
    }

    private void AddRuntimeReferences()
    {
        // Core types
        _references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        _references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        _references.Add(MetadataReference.CreateFromFile(typeof(Task).Assembly.Location));
        _references.Add(MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location));
        _references.Add(MetadataReference.CreateFromFile(typeof(IAsyncStateMachine).Assembly.Location));

        // Get runtime directory for additional assemblies
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        // Essential runtime assemblies
        var runtimeAssemblies = new[]
        {
            "System.Runtime.dll",
            "System.Threading.Tasks.dll",
            "System.Threading.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "netstandard.dll"
        };

        foreach (var assembly in runtimeAssemblies)
        {
            var path = Path.Combine(runtimeDir, assembly);
            if (File.Exists(path))
            {
                _references.Add(MetadataReference.CreateFromFile(path));
            }
        }
    }

    private void AddPersistenceReferences()
    {
        // Add reference to our persistence types
        // These are in the current assembly (AsyncPersistenceScenarios)
        var persistenceAssembly = typeof(DOTNExT.Persistence.AsyncPersistenceContext).Assembly;
        var persistenceLocation = persistenceAssembly.Location;

        Console.WriteLine($"[Compiler] Persistence assembly: {persistenceAssembly.FullName}");
        Console.WriteLine($"[Compiler] Persistence location: {(string.IsNullOrEmpty(persistenceLocation) ? "<empty>" : persistenceLocation)}");

        if (!string.IsNullOrEmpty(persistenceLocation))
        {
            _references.Add(MetadataReference.CreateFromFile(persistenceLocation));
            Console.WriteLine($"[Compiler] Added persistence reference from: {persistenceLocation}");
        }
        else
        {
            // Fallback: try to add from memory if file location is empty
            Console.WriteLine("[Compiler] WARNING: Persistence assembly has no file location!");
        }
    }

    /// <summary>
    /// Compiles source code and loads the resulting assembly.
    /// The modified Roslyn compiler will inject persistence calls
    /// for methods marked with [Persistable].
    /// </summary>
    /// <param name="sourceCode">C# source code to compile</param>
    /// <param name="assemblyName">Optional assembly name</param>
    /// <returns>Loaded assembly, or null if compilation failed</returns>
    public Assembly? CompileAndLoad(string sourceCode, string? assemblyName = null)
    {
        _lastDiagnostics.Clear();

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName ?? $"DynamicPersistable_{Guid.NewGuid():N}",
            syntaxTrees: new[] { syntaxTree },
            references: _references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        _lastDiagnostics.AddRange(result.Diagnostics);

        if (!result.Success)
        {
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);

        // Load into a collectible context so we can unload later if needed
        var context = new AssemblyLoadContext(null, isCollectible: true);
        return context.LoadFromStream(ms);
    }

    /// <summary>
    /// Compiles source code and saves to a file for inspection.
    /// Useful for decompiling and verifying the injected persistence calls.
    /// </summary>
    /// <param name="sourceCode">C# source code to compile</param>
    /// <param name="outputPath">Path to save the compiled DLL</param>
    /// <returns>True if compilation succeeded</returns>
    public bool CompileToFile(string sourceCode, string outputPath)
    {
        _lastDiagnostics.Clear();

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetFileNameWithoutExtension(outputPath),
            syntaxTrees: new[] { syntaxTree },
            references: _references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug));

        var result = compilation.Emit(outputPath);

        _lastDiagnostics.AddRange(result.Diagnostics);

        return result.Success;
    }

    /// <summary>
    /// Gets a formatted string of compilation errors.
    /// </summary>
    public string GetErrorsString()
    {
        var errors = _lastDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"  {d.Id}: {d.GetMessage()}")
            .ToList();

        return errors.Count == 0
            ? "No errors"
            : string.Join(Environment.NewLine, errors);
    }

    /// <summary>
    /// Gets a formatted string of all diagnostics.
    /// </summary>
    public string GetDiagnosticsString()
    {
        return string.Join(Environment.NewLine,
            _lastDiagnostics.Select(d => $"[{d.Severity}] {d.Id}: {d.GetMessage()}"));
    }
}

/// <summary>
/// Source code templates for testing [Persistable] compilation.
/// </summary>
public static class PersistableSourceTemplates
{
    /// <summary>
    /// Simple workflow with [Persistable] attribute.
    /// When compiled with modified Roslyn, should have checkpoint/restore injected.
    /// </summary>
    public const string SimpleWorkflow = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace DynamicWorkflows
{
    public class TestWorkflow
    {
        [Persistable]
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

    /// <summary>
    /// Workflow WITHOUT [Persistable] - should NOT have persistence injected.
    /// Use as control comparison.
    /// </summary>
    public const string NonPersistableWorkflow = @"
using System;
using System.Threading.Tasks;

namespace DynamicWorkflows
{
    public class NonPersistableWorkflow
    {
        public async Task<int> NormalCalculation(int input)
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

    /// <summary>
    /// Class-level [Persistable] - all async methods should have persistence.
    /// </summary>
    public const string ClassLevelPersistable = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace DynamicWorkflows
{
    [Persistable]
    public class FullyPersistableWorkflow
    {
        public async Task<int> Method1(int x)
        {
            var result = await Task.Delay(50).ContinueWith(_ => x * 2);
            return result;
        }

        public async Task<string> Method2(string s)
        {
            var result = await Task.Delay(50).ContinueWith(_ => s.ToUpper());
            return result;
        }
    }
}
";

    /// <summary>
    /// Workflow with multiple await points - tests multiple checkpoint injection.
    /// </summary>
    public const string MultiAwaitWorkflow = @"
using System;
using System.Threading.Tasks;
using DOTNExT.Persistence;

namespace DynamicWorkflows
{
    public class MultiAwaitWorkflow
    {
        [Persistable]
        public async Task<int> FiveStepCalculation(int input)
        {
            Console.WriteLine($""Starting with {input}"");

            var s1 = await Step1Async(input);     // Checkpoint 0
            var s2 = await Step2Async(s1);        // Checkpoint 1
            var s3 = await Step3Async(s2);        // Checkpoint 2
            var s4 = await Step4Async(s3);        // Checkpoint 3
            var s5 = await Step5Async(s4);        // Checkpoint 4

            Console.WriteLine($""Final result: {s5}"");
            return s5;
        }

        private Task<int> Step1Async(int x) => Task.FromResult(x + 1);
        private Task<int> Step2Async(int x) => Task.FromResult(x * 2);
        private Task<int> Step3Async(int x) => Task.FromResult(x - 3);
        private Task<int> Step4Async(int x) => Task.FromResult(x * x);
        private Task<int> Step5Async(int x) => Task.FromResult(x / 2);
    }
}
";
}
