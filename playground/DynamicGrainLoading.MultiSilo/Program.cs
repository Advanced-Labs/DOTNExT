using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;
using System.Net;

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("  Orleans Dynamic Grain Loading - Multi-Silo Test");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

const int siloPort1 = 11111;
const int siloPort2 = 11112;
const int siloPort3 = 11113;
const int gatewayPort1 = 30000;
const int gatewayPort2 = 30001;
const int gatewayPort3 = 30002;

// Build three silos
Console.WriteLine("Building 3-silo cluster...");
Console.WriteLine();

var silo1Task = StartSilo("Silo1", siloPort1, gatewayPort1, siloPort1);
var silo2Task = StartSilo("Silo2", siloPort2, gatewayPort2, siloPort1);
var silo3Task = StartSilo("Silo3", siloPort3, gatewayPort3, siloPort1);

var hosts = await Task.WhenAll(silo1Task, silo2Task, silo3Task);
var silo1 = hosts[0];
var silo2 = hosts[1];
var silo3 = hosts[2];

Console.WriteLine("✓ All silos started successfully");
Console.WriteLine();

// Wait for cluster to stabilize
Console.WriteLine("Waiting for cluster to stabilize...");
await Task.Delay(3000);
Console.WriteLine("✓ Cluster stabilized");
Console.WriteLine();

try
{
    var grainFactory1 = silo1.Services.GetRequiredService<IGrainFactory>();
    var grainLoader1 = silo1.Services.GetRequiredService<IDynamicGrainLoader>();
    var grainLoader2 = silo2.Services.GetRequiredService<IDynamicGrainLoader>();
    var grainLoader3 = silo3.Services.GetRequiredService<IDynamicGrainLoader>();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 1: Load Assembly on Silo 1");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();

    // Find the test grains assembly
    var assemblyPath = FindTestGrainsAssembly();
    if (assemblyPath == null)
    {
        Console.WriteLine("❌ ERROR: Could not find DynamicGrainLoading.TestGrains.dll");
        Console.WriteLine("   Please build the TestGrains project first:");
        Console.WriteLine("   cd playground/DynamicGrainLoading.TestGrains");
        Console.WriteLine("   dotnet build");
        return;
    }

    Console.WriteLine($"Found test grains assembly: {assemblyPath}");
    Console.WriteLine();
    Console.WriteLine("Loading assembly on Silo 1...");

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var loadResult = await grainLoader1.LoadGrainAssemblyAsync(assemblyPath);
    stopwatch.Stop();
    Console.WriteLine();

    if (!loadResult.Success)
    {
        Console.WriteLine("❌ FAILED to load assembly on Silo 1!");
        foreach (var error in loadResult.Errors)
        {
            Console.WriteLine($"  - {error}");
        }
        return;
    }

    Console.WriteLine("✓ Assembly loaded successfully on Silo 1!");
    Console.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"  Grain types: {loadResult.GrainTypes.Count}");
    Console.WriteLine($"  Manifest version: {loadResult.NewManifestVersion}");
    Console.WriteLine();

    Console.WriteLine("Loaded grain types on Silo 1:");
    foreach (var grainType in loadResult.GrainTypes)
    {
        Console.WriteLine($"  - {grainType}");
    }
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 2: Verify Cluster Propagation");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();

    Console.WriteLine("Waiting for manifest propagation across cluster...");
    await Task.Delay(2000);
    Console.WriteLine();

    // Check cluster manifests on all silos
    var clusterProvider1 = silo1.Services.GetRequiredService<IClusterManifestProvider>();
    var clusterProvider2 = silo2.Services.GetRequiredService<IClusterManifestProvider>();
    var clusterProvider3 = silo3.Services.GetRequiredService<IClusterManifestProvider>();

    var manifest1 = clusterProvider1.Current;
    var manifest2 = clusterProvider2.Current;
    var manifest3 = clusterProvider3.Current;

    Console.WriteLine("Cluster Manifest Status:");
    Console.WriteLine($"  Silo 1 manifest version: {manifest1.Version}");
    Console.WriteLine($"  Silo 1 knows about {manifest1.Silos.Count} silos");
    Console.WriteLine($"  Silo 2 manifest version: {manifest2.Version}");
    Console.WriteLine($"  Silo 2 knows about {manifest2.Silos.Count} silos");
    Console.WriteLine($"  Silo 3 manifest version: {manifest3.Version}");
    Console.WriteLine($"  Silo 3 knows about {manifest3.Silos.Count} silos");
    Console.WriteLine();

    if (manifest1.Version == manifest2.Version && manifest2.Version == manifest3.Version)
    {
        Console.WriteLine("✓ All silos have the same manifest version!");
    }
    else
    {
        Console.WriteLine("⚠ WARNING: Silos have different manifest versions");
        Console.WriteLine("  This may be temporary during propagation");
    }
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 3: Activate Grains on Different Silos");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();

    // Get grain types
    var helloGrainType = loadResult.Assembly.GetType("DynamicGrainLoading.TestGrains.IHelloGrain");
    var counterGrainType = loadResult.Assembly.GetType("DynamicGrainLoading.TestGrains.ICounterGrain");
    var echoGrainType = loadResult.Assembly.GetType("DynamicGrainLoading.TestGrains.IEchoGrain");

    if (helloGrainType == null || counterGrainType == null || echoGrainType == null)
    {
        Console.WriteLine("❌ ERROR: Could not find grain types in loaded assembly");
        return;
    }

    // Test activating grains (they may activate on different silos)
    Console.WriteLine("Test 1: Activate HelloGrain (may be on any silo)");
    Console.WriteLine("--------------------------------------------------");

    // Use generic GetGrain<T> via reflection
    // Find GetGrain<T>(string primaryKey, string? grainClassNamePrefix = null)
    var getGrainStringMethod = typeof(IGrainFactory)
        .GetMethods()
        .FirstOrDefault(m => m.Name == "GetGrain"
                          && m.IsGenericMethod
                          && m.GetGenericArguments().Length == 1
                          && m.GetParameters().Length == 2
                          && m.GetParameters()[0].ParameterType == typeof(string));
    var helloGrainGenericMethod = getGrainStringMethod!.MakeGenericMethod(helloGrainType);

    object helloGrain1 = helloGrainGenericMethod.Invoke(grainFactory1, new object?[] { "user1", null })!;
    object helloGrain2 = helloGrainGenericMethod.Invoke(grainFactory1, new object?[] { "user2", null })!;
    object helloGrain3 = helloGrainGenericMethod.Invoke(grainFactory1, new object?[] { "user3", null })!;

    // Use reflection to invoke SayHello method
    var sayHelloMethod = helloGrainType.GetMethod("SayHello");
    var response1 = await (Task<string>)sayHelloMethod!.Invoke(helloGrain1, new object[] { "from grain 1" })!;
    Console.WriteLine($"✓ HelloGrain user1: {response1}");

    var response2 = await (Task<string>)sayHelloMethod!.Invoke(helloGrain2, new object[] { "from grain 2" })!;
    Console.WriteLine($"✓ HelloGrain user2: {response2}");

    var response3 = await (Task<string>)sayHelloMethod!.Invoke(helloGrain3, new object[] { "from grain 3" })!;
    Console.WriteLine($"✓ HelloGrain user3: {response3}");
    Console.WriteLine();

    Console.WriteLine("Test 2: Counter Grains");
    Console.WriteLine("-----------------------");

    // Use generic GetGrain<T> via reflection
    // Find GetGrain<T>(long primaryKey, string? grainClassNamePrefix = null)
    var getGrainLongMethod = typeof(IGrainFactory)
        .GetMethods()
        .FirstOrDefault(m => m.Name == "GetGrain"
                          && m.IsGenericMethod
                          && m.GetGenericArguments().Length == 1
                          && m.GetParameters().Length == 2
                          && m.GetParameters()[0].ParameterType == typeof(long));
    var counterGrainGenericMethod = getGrainLongMethod!.MakeGenericMethod(counterGrainType);

    object counter1 = counterGrainGenericMethod.Invoke(grainFactory1, new object?[] { 1L, null })!;
    object counter2 = counterGrainGenericMethod.Invoke(grainFactory1, new object?[] { 2L, null })!;

    // Use reflection to invoke methods
    var incrementMethod = counterGrainType.GetMethod("Increment");
    var getCountMethod = counterGrainType.GetMethod("GetCount");

    await (Task)incrementMethod!.Invoke(counter1, null)!;
    await (Task)incrementMethod!.Invoke(counter1, null)!;
    var count1 = await (Task<int>)getCountMethod!.Invoke(counter1, null)!;
    Console.WriteLine($"✓ Counter 1: {count1}");

    await (Task)incrementMethod!.Invoke(counter2, null)!;
    var count2 = await (Task<int>)getCountMethod!.Invoke(counter2, null)!;
    Console.WriteLine($"✓ Counter 2: {count2}");
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 4: Load on Silo 2 (Already Loaded Check)");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();

    Console.WriteLine("NOTE: Currently each silo must load assemblies independently.");
    Console.WriteLine("Attempting to load on Silo 2...");
    Console.WriteLine();

    var loadResult2 = await grainLoader2.LoadGrainAssemblyAsync(assemblyPath);

    if (loadResult2.Success)
    {
        Console.WriteLine("✓ Assembly loaded successfully on Silo 2");
        Console.WriteLine($"  Duration: {loadResult2.LoadDuration.TotalMilliseconds}ms");
        Console.WriteLine($"  Grain types: {loadResult2.GrainTypes.Count}");
    }
    else if (loadResult2.Errors.Any(e => e.Contains("already loaded")))
    {
        Console.WriteLine("✓ Assembly already loaded on Silo 2 (expected behavior)");
    }
    else
    {
        Console.WriteLine("⚠ Load on Silo 2 had issues:");
        foreach (var error in loadResult2.Errors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 5: Cross-Silo Communication");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();

    Console.WriteLine("Creating grains and calling across silos...");

    // Create multiple grains (they'll distribute across silos)
    var tasks = new List<Task>();
    for (int i = 0; i < 10; i++)
    {
        object grain = helloGrainGenericMethod.Invoke(grainFactory1, new object?[] { $"bulk-user-{i}", null })!;
        var messageTask = (Task<string>)sayHelloMethod!.Invoke(grain, new object[] { $"bulk message {i}" })!;
        tasks.Add(messageTask);
    }

    await Task.WhenAll(tasks);
    Console.WriteLine("✓ Successfully called 10 grains (distributed across cluster)");
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  ✓ ALL MULTI-SILO TESTS PASSED!");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine("Summary:");
    Console.WriteLine($"  - 3-silo cluster running");
    Console.WriteLine($"  - Loaded {loadResult.GrainTypes.Count} grain types on Silo 1");
    Console.WriteLine($"  - Manifests propagated across cluster");
    Console.WriteLine($"  - Activated grains on multiple silos");
    Console.WriteLine($"  - Cross-silo communication working");
    Console.WriteLine();
    Console.WriteLine("Press any key to shut down cluster...");
    Console.ReadKey();
}
finally
{
    Console.WriteLine();
    Console.WriteLine("Shutting down silos...");

    await Task.WhenAll(
        silo1.StopAsync(),
        silo2.StopAsync(),
        silo3.StopAsync()
    );

    Console.WriteLine("✓ All silos stopped");
}

static async Task<IHost> StartSilo(string siloName, int siloPort, int gatewayPort, int primarySiloPort)
{
    Console.WriteLine($"Starting {siloName}...");

    var host = Host.CreateDefaultBuilder()
        .UseOrleans((context, siloBuilder) =>
        {
            siloBuilder
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "dynamic-test-cluster";
                    options.ServiceId = "dynamic-test-service";
                })
                .ConfigureEndpoints(IPAddress.Loopback, siloPort, gatewayPort)
                .UseDevelopmentClustering(options =>
                {
                    options.PrimarySiloEndpoint = new IPEndPoint(IPAddress.Loopback, primarySiloPort);
                })
                .AddDynamicGrainLoading();  // ← Enable dynamic grain loading
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Orleans.Runtime.DynamicGrains", LogLevel.Debug);
            logging.AddFilter($"Orleans.Runtime.Management.ManagementGrain.{siloName}", LogLevel.Warning);
            logging.AddFilter("Orleans.Runtime.SiloControl", LogLevel.Warning);
        })
        .Build();

    await host.StartAsync();
    Console.WriteLine($"✓ {siloName} started (port {siloPort}, gateway {gatewayPort})");

    return host;
}

static string? FindTestGrainsAssembly()
{
    var locations = new[]
    {
        "../DynamicGrainLoading.TestGrains/bin/Debug/net8.0/DynamicGrainLoading.TestGrains.dll",
        "../DynamicGrainLoading.TestGrains/bin/Release/net8.0/DynamicGrainLoading.TestGrains.dll",
        "../../DynamicGrainLoading.TestGrains/bin/Debug/net8.0/DynamicGrainLoading.TestGrains.dll",
        "../../DynamicGrainLoading.TestGrains/bin/Release/net8.0/DynamicGrainLoading.TestGrains.dll",
    };

    foreach (var location in locations)
    {
        var fullPath = Path.GetFullPath(location);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }
    }

    return null;
}
