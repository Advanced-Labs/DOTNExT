# Microsoft Scynapse Stream Provider for Azure Event Hubs

## Introduction
Microsoft Scynapse Stream Provider for Azure Event Hubs enables Scynapse applications to leverage Azure Event Hubs for reliable, scalable event processing. This provider allows you to use Event Hubs as a streaming backbone for your Scynapse application to both produce and consume streams of events.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Streaming.EventHubs
```

## Example - Configuring Event Hubs Stream Provider

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;
namespace ExampleGrains;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure Azure Event Hubs as a stream provider
            .AddEventHubStreams(
                "EventHubStreamProvider",
                configurator => 
                {
                    configurator.ConfigureEventHub(builder => builder.Configure(options => 
                    {
                        options.ConnectionString = "YOUR_EVENT_HUB_CONNECTION_STRING";
                        options.ConsumerGroup = "YOUR_CONSUMER_GROUP"; // Default is "$Default"
                        options.Path = "YOUR_EVENT_HUB_NAME";
                    }));
                    
                    configurator.UseAzureTableCheckpointer(builder => builder.Configure(options => 
                    {
                        options.ConnectionString = "YOUR_STORAGE_CONNECTION_STRING";
                        options.TableName = "EventHubCheckpoints"; // Optional
                    }));
                });
    });

// Run the host
await builder.RunAsync();
```

## Example - Using Event Hub Streams in a Grain

```csharp
// Grain interface
public interface IStreamProcessingGrain : IGrainWithGuidKey
{
    Task StartProcessing();
}

// Grain implementation
public class StreamProcessingGrain : Grain, IStreamProcessingGrain
{
    private IStreamProvider _streamProvider;
    private IAsyncStream<MyEvent> _stream;
    private StreamSubscriptionHandle<MyEvent> _subscription;

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Get the stream provider
        _streamProvider = GetStreamProvider("EventHubStreamProvider");
        
        // Get a reference to a specific stream
        _stream = _streamProvider.GetStream<MyEvent>(this.GetPrimaryKey(), "MyStreamNamespace");
        
        await base.OnActivateAsync(cancellationToken);
    }

    public async Task StartProcessing()
    {
        // Subscribe to the stream to process events
        _subscription = await _stream.SubscribeAsync(OnNextAsync);
    }

    private Task OnNextAsync(MyEvent evt, StreamSequenceToken token)
    {
        Console.WriteLine($"Received event: {evt.Data}");
        return Task.CompletedTask;
    }

    // Produce an event to the stream
    public Task SendEvent(MyEvent evt)
    {
        return _stream.OnNextAsync(evt);
    }
}

// Event class

public class MyEvent
{
    public string Data { get; set; }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Scynapse Streams](https://learn.microsoft.com/en-us/dotnet/scynapse/streaming/)
- [Event Hubs integration](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/streams-implementation)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)