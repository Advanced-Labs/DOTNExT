# Microsoft Scynapse Stream Provider for NATS

## Introduction
Microsoft Scynapse Stream Provider for NATS enables Scynapse applications to leverage NATS JetStream for reliable, scalable event processing. This provider allows you to use NATS JetStream as a streaming backbone for your Scynapse application to both produce and consume streams of events.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Streaming.NATS
```

## Example - Configuring NATS Stream Provider

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;
using Scynapse.Streaming.NATS.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure NATS JetStream as a stream provider
            .AddNatsStreams(
                "NatsStreamProvider",
                options =>
                {
                    options.StreamName = "scynapse-stream";
                    // Optional: Configure NATS client options
                    // options.NatsClientOptions = new NatsOpts { Url = "nats://localhost:4222" };
                    // Optional: Configure batch size (default: 100)
                    // options.BatchSize = 100;
                    // Optional: Configure partition count (default: 8)
                    // options.PartitionCount = 8;
                });
    });

// Run the host
await builder.RunAsync();
```

## Example - Configuring NATS Streams on Client

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Streaming.NATS.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapseClient(clientBuilder =>
    {
        clientBuilder
            .UseLocalhostClustering()
            .AddNatsStreams(
                "NatsStreamProvider",
                options =>
                {
                    options.StreamName = "scynapse-stream";
                });
    });

await builder.RunAsync();
```

## Example - Using NATS Streams in a Grain

```csharp
using Scynapse;
using Scynapse.Streams;

// Grain interface
public interface IStreamProcessingGrain : IGrainWithGuidKey
{
    Task StartProcessing();
    Task SendEvent(MyEvent evt);
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
        _streamProvider = this.GetStreamProvider("NatsStreamProvider");
        
        // Get a reference to a specific stream
        _stream = _streamProvider.GetStream<MyEvent>(
            StreamId.Create("MyStreamNamespace", this.GetPrimaryKey()));
        
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
- [NATS JetStream Documentation](https://docs.nats.io/nats-concepts/jetstream)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)
