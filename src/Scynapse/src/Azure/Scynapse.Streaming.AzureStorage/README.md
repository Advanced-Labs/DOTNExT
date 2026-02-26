# Microsoft Scynapse Streaming for Azure Storage Queues

## Introduction
Microsoft Scynapse Streaming for Azure Storage provides a stream provider implementation for Scynapse using Azure Storage Queues. This allows for publishing and subscribing to streams of events with Azure Storage Queues as the underlying messaging infrastructure.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Streaming.AzureStorage
```

## Example - Configuring Azure Storage Queues Streaming
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;
using Scynapse.Streams;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure Azure Storage Queues as a stream provider
            .AddAzureQueueStreams(
                name: "AzureQueueStreamProvider", 
                b => b.ConfigureAzureQueue(ob => ob.Configure((options, dep) =>
                {
                    options.ConfigureTestDefaults();
                    options.QueueNames = Enumerable.Range(0, 8).Select(num => $"{dep.Value.ClusterId}-{num}").ToList();
                })));
    });

// Run the host
await builder.RunAsync();
```

## Example - Using Azure Storage Queue Streams in a Grain
```csharp
// Producer grain
public class ProducerGrain : Grain, IProducerGrain
{
    private IAsyncStream<string> _stream;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Get a reference to a stream
        var streamProvider = GetStreamProvider("AzureQueueStreamProvider");
        _stream = streamProvider.GetStream<string>(Guid.NewGuid(), "MyStreamNamespace");
        
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SendMessage(string message)
    {
        // Send a message to the stream
        await _stream.OnNextAsync(message);
    }
}

// Consumer grain
public class ConsumerGrain : Grain, IConsumerGrain, IAsyncObserver<string>
{
    private StreamSubscriptionHandle<string> _subscription;

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Get a reference to a stream
        var streamProvider = GetStreamProvider("AzureQueueStreamProvider");
        var stream = streamProvider.GetStream<string>(this.GetPrimaryKey(), "MyStreamNamespace");
        
        // Subscribe to the stream
        _subscription = await stream.SubscribeAsync(this);
        
        await base.OnActivateAsync(cancellationToken);
    }

    public Task OnNextAsync(string item, StreamSequenceToken token = null)
    {
        Console.WriteLine($"Received message: {item}");
        return Task.CompletedTask;
    }

    public Task OnCompletedAsync()
    {
        Console.WriteLine("Stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        Console.WriteLine($"Stream error: {ex.Message}");
        return Task.CompletedTask;
    }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Scynapse Streams](https://learn.microsoft.com/en-us/dotnet/scynapse/streaming/index)
- [Stream Providers](https://learn.microsoft.com/en-us/dotnet/scynapse/streaming/stream-providers)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)