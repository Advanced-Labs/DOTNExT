using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.Runtime.DynamicGrains;
using Orleans.TestingHost;
using TestExtensions;
using UnitTests.SplitGrain.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests.Runtime;

/// <summary>
/// Tests for split-assembly dynamic grain loading pattern.
/// Verifies that grain interfaces in one assembly and implementations in another
/// can be loaded and used correctly.
/// </summary>
public class SplitAssemblyDynamicGrainTests : IClassFixture<SplitAssemblyDynamicGrainTests.Fixture>, IAsyncLifetime
{
    private readonly Fixture _fixture;
    private readonly ITestOutputHelper _output;

    public SplitAssemblyDynamicGrainTests(Fixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Verifies that a grain can be called when its interface and implementation
    /// are in separate assemblies (split-assembly pattern).
    /// </summary>
    [Fact]
    public async Task SplitAssembly_CanCallGrainWithSplitInterface()
    {
        // Get the grain
        var grain = _fixture.GrainFactory.GetGrain<ITestSplitGrain>("test-key");

        // Call the grain
        var result = await grain.Echo("Hello, split assembly!");

        // Verify the result
        Assert.Equal("Echo: Hello, split assembly!", result);

        _output.WriteLine($"Successfully called split-assembly grain: {result}");
    }

    /// <summary>
    /// Verifies that grain state works correctly with split-assembly pattern.
    /// </summary>
    [Fact]
    public async Task SplitAssembly_GrainStateWorks()
    {
        // Get the grain
        var grain = _fixture.GrainFactory.GetGrain<ITestSplitGrain>("counter-test");

        // Verify initial state
        var initialCounter = await grain.GetCounter();
        Assert.Equal(0, initialCounter);

        // Increment counter
        var counter1 = await grain.IncrementCounter();
        Assert.Equal(1, counter1);

        var counter2 = await grain.IncrementCounter();
        Assert.Equal(2, counter2);

        // Verify state persistence
        var currentCounter = await grain.GetCounter();
        Assert.Equal(2, currentCounter);

        _output.WriteLine($"Split-assembly grain state works correctly: counter = {currentCounter}");
    }

    /// <summary>
    /// Verifies that multiple grain types with split interfaces work correctly.
    /// </summary>
    [Fact]
    public async Task SplitAssembly_MultipleGrainTypes()
    {
        // Test the calculator grain
        var calculator = _fixture.GrainFactory.GetGrain<ICalculatorSplitGrain>(42);

        var addResult = await calculator.Add(10, 5);
        Assert.Equal(15, addResult);

        var multiplyResult = await calculator.Multiply(7, 3);
        Assert.Equal(21, multiplyResult);

        var lastResult = await calculator.GetLastResult();
        Assert.Equal(21, lastResult);

        _output.WriteLine($"Calculator grain works correctly: last result = {lastResult}");

        // Test the test grain again to verify both types work
        var testGrain = _fixture.GrainFactory.GetGrain<ITestSplitGrain>("multi-type-test");
        var echo = await testGrain.Echo("Testing multiple types");
        Assert.Equal("Echo: Testing multiple types", echo);

        _output.WriteLine("Multiple split-assembly grain types work correctly");
    }

    /// <summary>
    /// Verifies that the plugin assembly set was correctly discovered.
    /// </summary>
    [Fact]
    public void SplitAssembly_PluginSetDiscovered()
    {
        // This test verifies that the infrastructure found both assemblies
        Assert.NotNull(_fixture.LoadResult);
        Assert.True(_fixture.LoadResult.Success, $"Load failed: {string.Join(", ", _fixture.LoadResult.Errors)}");
        Assert.NotNull(_fixture.LoadResult.Metadata);

        var metadata = _fixture.LoadResult.Metadata;

        // Should have found grain classes from the TestSplitGrains assembly
        Assert.NotEmpty(metadata.GrainClasses);
        Assert.Contains(metadata.GrainClasses, t => t.Name == "TestSplitGrain");
        Assert.Contains(metadata.GrainClasses, t => t.Name == "CalculatorSplitGrain");

        // Should have found grain interfaces from the TestSplitGrainContracts assembly
        Assert.NotEmpty(metadata.GrainInterfaces);
        Assert.Contains(metadata.GrainInterfaces, t => t.Name == "ITestSplitGrain");
        Assert.Contains(metadata.GrainInterfaces, t => t.Name == "ICalculatorSplitGrain");

        // Should have found generated code (proxies, serializers) from the contracts assembly
        Assert.True(metadata.HasGeneratedCode);

        _output.WriteLine($"Plugin set correctly discovered:");
        _output.WriteLine($"  - Grain classes: {metadata.GrainClasses.Count}");
        _output.WriteLine($"  - Grain interfaces: {metadata.GrainInterfaces.Count}");
        _output.WriteLine($"  - Serializers: {metadata.Serializers.Count}");
        _output.WriteLine($"  - Copiers: {metadata.Copiers.Count}");
        _output.WriteLine($"  - Proxies: {metadata.Proxies.Count}");
    }

    public class Fixture : BaseTestClusterFixture
    {
        public GrainLoadResult LoadResult { get; private set; }

        protected override void ConfigureTestCluster(TestClusterBuilder builder)
        {
            base.ConfigureTestCluster(builder);
            // Plugin grain loading is now enabled by default - no explicit configuration needed
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            // Find and load the TestSplitGrains assembly
            var assemblyPath = FindTestSplitGrainsAssembly();
            if (assemblyPath == null)
            {
                throw new InvalidOperationException(
                    "Could not find TestSplitGrains.dll. Make sure the test projects are built.");
            }

            // Load the assembly dynamically
            var silo = HostedCluster.Silos.First();
            var serviceProvider = HostedCluster.GetSiloServiceProvider(silo.SiloAddress);
            var grainLoader = serviceProvider.GetRequiredService<IPluginGrainLoader>();

            LoadResult = await grainLoader.LoadGrainAssemblyAsync(assemblyPath);

            if (!LoadResult.Success)
            {
                var errors = string.Join(", ", LoadResult.Errors);
                throw new InvalidOperationException($"Failed to load TestSplitGrains assembly: {errors}");
            }

            // Give the cluster time to propagate manifests
            await Task.Delay(500);
        }

        private string FindTestSplitGrainsAssembly()
        {
            // Try to find the assembly in the build output directories
            var baseDir = AppContext.BaseDirectory;

            // Navigate up to find the repository root
            var currentDir = new DirectoryInfo(baseDir);
            while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Orleans.slnx")))
            {
                currentDir = currentDir.Parent;
            }

            if (currentDir == null)
            {
                return null;
            }

            // Search for the TestSplitGrains assembly
            var testGrainsDir = Path.Combine(currentDir.FullName, "test", "Grains", "TestSplitGrains");
            if (!Directory.Exists(testGrainsDir))
            {
                return null;
            }

            // Look in bin/Debug or bin/Release directories
            var binDir = Path.Combine(testGrainsDir, "bin");
            if (Directory.Exists(binDir))
            {
                foreach (var netDir in Directory.GetDirectories(binDir, "net*", SearchOption.AllDirectories))
                {
                    var assemblyPath = Path.Combine(netDir, "TestSplitGrains.dll");
                    if (File.Exists(assemblyPath))
                    {
                        return assemblyPath;
                    }
                }
            }

            return null;
        }
    }
}
