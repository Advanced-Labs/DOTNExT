using Microsoft.Extensions.Logging;
using Orleans;
using DynamicGrainLoading.Contracts;

namespace DynamicGrainLoading.Implementation;

/// <summary>
/// Simple hello grain implementation.
/// Implementation is in a separate assembly from the interface (Contracts).
/// </summary>
public class HelloGrain : Grain, IHelloGrain
{
    private readonly ILogger<HelloGrain> _logger;
    private int _callCount = 0;

    public HelloGrain(ILogger<HelloGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "HelloGrain {GrainId} activated (Split Assembly - Implementation!)",
            this.GetPrimaryKeyString());
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<string> SayHello(string name)
    {
        _callCount++;
        var message = $"Hello, {name}! You are visitor #{_callCount}. (From SPLIT Implementation assembly!)";
        _logger.LogInformation("HelloGrain saying: {Message}", message);
        return Task.FromResult(message);
    }

    public Task<int> GetCallCount()
    {
        return Task.FromResult(_callCount);
    }
}

/// <summary>
/// Counter grain implementation.
/// </summary>
public class CounterGrain : Grain, ICounterGrain
{
    private readonly ILogger<CounterGrain> _logger;
    private int _count = 0;

    public CounterGrain(ILogger<CounterGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CounterGrain {GrainId} activated with count: {Count} (Split Assembly)",
            this.GetPrimaryKeyLong(),
            _count);
        return base.OnActivateAsync(cancellationToken);
    }

    public Task Increment()
    {
        _count++;
        _logger.LogInformation("Counter incremented to {Count}", _count);
        return Task.CompletedTask;
    }

    public Task<int> GetCount()
    {
        return Task.FromResult(_count);
    }

    public Task Reset()
    {
        _logger.LogInformation("Counter reset from {OldCount} to 0", _count);
        _count = 0;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Echo grain implementation for serialization testing.
/// </summary>
public class EchoGrain : Grain, IEchoGrain
{
    private readonly ILogger<EchoGrain> _logger;

    public EchoGrain(ILogger<EchoGrain> logger)
    {
        _logger = logger;
    }

    public Task<string> Echo(string message)
    {
        _logger.LogInformation("Echoing message (Split): {Message}", message);
        return Task.FromResult($"Echo (Split): {message}");
    }

    public Task<ComplexData> EchoComplex(ComplexData data)
    {
        _logger.LogInformation(
            "Echoing complex data (Split): Name={Name}, Value={Value}, TagCount={TagCount}",
            data.Name,
            data.Value,
            data.Tags.Count);

        var echo = new ComplexData
        {
            Name = $"Echo (Split) of {data.Name}",
            Value = data.Value * 2,
            Timestamp = DateTime.UtcNow,
            Tags = new List<string>(data.Tags) { "echoed", "split-assembly" }
        };

        return Task.FromResult(echo);
    }
}
