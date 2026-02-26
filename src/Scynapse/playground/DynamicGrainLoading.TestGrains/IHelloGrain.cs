using Scynapse;

namespace DynamicGrainLoading.TestGrains;

/// <summary>
/// Simple grain interface for testing dynamic loading.
/// </summary>
public interface IHelloGrain : IGrainWithStringKey
{
    Task<string> SayHello(string name);
    Task<int> GetCallCount();
}

/// <summary>
/// Counter grain for testing state management.
/// </summary>
public interface ICounterGrain : IGrainWithIntegerKey
{
    Task Increment();
    Task<int> GetCount();
    Task Reset();
}

/// <summary>
/// Echo grain for testing serialization.
/// </summary>
public interface IEchoGrain : IGrainWithGuidKey
{
    Task<string> Echo(string message);
    Task<ComplexData> EchoComplex(ComplexData data);
}

/// <summary>
/// Complex data type for testing serialization of custom types.
/// </summary>
[GenerateSerializer]
public class ComplexData
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public int Value { get; set; }

    [Id(2)]
    public DateTime Timestamp { get; set; }

    [Id(3)]
    public List<string> Tags { get; set; } = new();
}
