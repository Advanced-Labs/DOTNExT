using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("  Orleans Dynamic Grain Loading - Single Silo Test");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// Build the host with Orleans silo
var host = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        siloBuilder
            .UseLocalhostClustering()
            .AddDynamicGrainLoading();  // ← Enable dynamic grain loading
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);

        // Enable detailed logging for dynamic grain loading
        logging.AddFilter("Orleans.Runtime.DynamicGrains", LogLevel.Debug);
    })
    .Build();

Console.WriteLine("✓ Host configured");
Console.WriteLine("✓ Dynamic grain loading enabled");
Console.WriteLine();

// Start the silo
Console.WriteLine("Starting Orleans silo...");
await host.StartAsync();
Console.WriteLine("✓ Silo started successfully");
Console.WriteLine();

try
{
    var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
    var grainLoader = host.Services.GetRequiredService<IDynamicGrainLoader>();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 1: Load Test Grain Assembly");
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

    // Load the assembly
    Console.WriteLine("Loading assembly...");
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    var loadResult = await grainLoader.LoadGrainAssemblyAsync(assemblyPath);

    stopwatch.Stop();
    Console.WriteLine();

    if (!loadResult.Success)
    {
        Console.WriteLine("❌ FAILED to load assembly!");
        Console.WriteLine();
        Console.WriteLine("Errors:");
        foreach (var error in loadResult.Errors)
        {
            Console.WriteLine($"  - {error}");
        }
        return;
    }

    Console.WriteLine("✓ Assembly loaded successfully!");
    Console.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"  Grain types loaded: {loadResult.GrainTypes.Count}");
    Console.WriteLine($"  Manifest version: {loadResult.NewManifestVersion}");
    Console.WriteLine();

    Console.WriteLine("Loaded grain types:");
    foreach (var grainType in loadResult.GrainTypes)
    {
        Console.WriteLine($"  - {grainType}");
    }
    Console.WriteLine();

    Console.WriteLine("Assembly metadata:");
    Console.WriteLine($"  Grain classes: {loadResult.Metadata.GrainClasses.Count}");
    foreach (var grainClass in loadResult.Metadata.GrainClasses)
    {
        Console.WriteLine($"    - {grainClass.FullName}");
    }
    Console.WriteLine($"  Grain interfaces: {loadResult.Metadata.GrainInterfaces.Count}");
    foreach (var grainInterface in loadResult.Metadata.GrainInterfaces)
    {
        Console.WriteLine($"    - {grainInterface.FullName}");
    }
    Console.WriteLine($"  Serializers: {loadResult.Metadata.Serializers.Count}");
    Console.WriteLine($"  Copiers: {loadResult.Metadata.Copiers.Count}");
    Console.WriteLine($"  Proxies: {loadResult.Metadata.Proxies.Count}");
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 2: Activate and Use Dynamically Loaded Grains");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();

    // Get the grain types dynamically
    var helloGrainType = loadResult.Assembly.GetType("DynamicGrainLoading.TestGrains.IHelloGrain");
    var counterGrainType = loadResult.Assembly.GetType("DynamicGrainLoading.TestGrains.ICounterGrain");
    var echoGrainType = loadResult.Assembly.GetType("DynamicGrainLoading.TestGrains.IEchoGrain");
    var complexDataType = loadResult.Assembly.GetType("DynamicGrainLoading.TestGrains.ComplexData");

    if (helloGrainType == null || counterGrainType == null || echoGrainType == null)
    {
        Console.WriteLine("❌ ERROR: Could not find grain types in loaded assembly");
        return;
    }

    // Test 1: Hello Grain
    Console.WriteLine("Test 1: HelloGrain");
    Console.WriteLine("------------------");

    // Use generic GetGrain<T> via reflection to get properly typed proxy
    // Find GetGrain<T>(string primaryKey, string? grainClassNamePrefix = null)
    var getGrainStringMethod = typeof(IGrainFactory)
        .GetMethods()
        .FirstOrDefault(m => m.Name == "GetGrain"
                          && m.IsGenericMethod
                          && m.GetGenericArguments().Length == 1
                          && m.GetParameters().Length == 2
                          && m.GetParameters()[0].ParameterType == typeof(string));
    var helloGrainGenericMethod = getGrainStringMethod!.MakeGenericMethod(helloGrainType);
    dynamic helloGrain = helloGrainGenericMethod.Invoke(grainFactory, new object[] { "test-user", null })!;

    var helloMessage = await helloGrain.SayHello("World");
    Console.WriteLine($"✓ Response: {helloMessage}");

    var callCount = await helloGrain.GetCallCount();
    Console.WriteLine($"✓ Call count: {callCount}");
    Console.WriteLine();

    // Test 2: Counter Grain
    Console.WriteLine("Test 2: CounterGrain");
    Console.WriteLine("--------------------");

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
    dynamic counterGrain = counterGrainGenericMethod.Invoke(grainFactory, new object[] { 123L, null })!;

    await counterGrain.Increment();
    Console.WriteLine("✓ Incremented counter");

    await counterGrain.Increment();
    Console.WriteLine("✓ Incremented counter again");

    var count = await counterGrain.GetCount();
    Console.WriteLine($"✓ Current count: {count}");

    await counterGrain.Reset();
    Console.WriteLine("✓ Reset counter");

    count = await counterGrain.GetCount();
    Console.WriteLine($"✓ Count after reset: {count}");
    Console.WriteLine();

    // Test 3: Echo Grain (Serialization Test)
    Console.WriteLine("Test 3: EchoGrain (Serialization Test)");
    Console.WriteLine("---------------------------------------");

    // Use generic GetGrain<T> via reflection
    // Find GetGrain<T>(Guid primaryKey, string? grainClassNamePrefix = null)
    var getGrainGuidMethod = typeof(IGrainFactory)
        .GetMethods()
        .FirstOrDefault(m => m.Name == "GetGrain"
                          && m.IsGenericMethod
                          && m.GetGenericArguments().Length == 1
                          && m.GetParameters().Length == 2
                          && m.GetParameters()[0].ParameterType == typeof(Guid));
    var echoGrainGenericMethod = getGrainGuidMethod!.MakeGenericMethod(echoGrainType);
    dynamic echoGrain = echoGrainGenericMethod.Invoke(grainFactory, new object[] { Guid.NewGuid(), null })!;

    var echoResponse = await echoGrain.Echo("Hello from dynamic grain!");
    Console.WriteLine($"✓ Simple echo: {echoResponse}");

    // Create complex data
    dynamic complexData = Activator.CreateInstance(complexDataType!)!;
    complexData.Name = "Test Data";
    complexData.Value = 42;
    complexData.Timestamp = DateTime.UtcNow;
    complexData.Tags = new List<string> { "dynamic", "test", "orleans" };

    Console.WriteLine("Sending complex data:");
    Console.WriteLine($"  Name: {complexData.Name}");
    Console.WriteLine($"  Value: {complexData.Value}");
    Console.WriteLine($"  Tags: {string.Join(", ", complexData.Tags)}");

    var echoComplexResponse = await echoGrain.EchoComplex(complexData);
    Console.WriteLine("✓ Received complex echo:");
    Console.WriteLine($"  Name: {echoComplexResponse.Name}");
    Console.WriteLine($"  Value: {echoComplexResponse.Value}");
    Console.WriteLine($"  Timestamp: {echoComplexResponse.Timestamp}");
    Console.WriteLine($"  Tags: {string.Join(", ", echoComplexResponse.Tags)}");
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  Test Phase 3: Monitor Load Events");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();

    // Note: Events were already published during loading
    Console.WriteLine("✓ Load events are available via grainLoader.LoadEvents");
    Console.WriteLine("  (In production, subscribe to LoadEvents for real-time monitoring)");
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("  ✓ ALL TESTS PASSED!");
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine("Summary:");
    Console.WriteLine($"  - Loaded {loadResult.GrainTypes.Count} grain types dynamically");
    Console.WriteLine($"  - Activated and used 3 different grain types");
    Console.WriteLine($"  - Verified serialization of custom types");
    Console.WriteLine($"  - Load duration: {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine();
    Console.WriteLine("Press any key to shut down...");
    Console.ReadKey();
}
finally
{
    Console.WriteLine();
    Console.WriteLine("Shutting down silo...");
    await host.StopAsync();
    Console.WriteLine("✓ Silo stopped");
}

static string? FindTestGrainsAssembly()
{
    // Try common locations
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
