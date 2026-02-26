# Microsoft Scynapse Persistence for Redis

## Introduction
Microsoft Scynapse Persistence for Redis provides grain persistence for Microsoft Scynapse using Redis. This allows your grains to persist their state in Redis and reload it when they are reactivated, leveraging Redis's in-memory data store for fast access.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Persistence.Redis
```

## Example - Configuring Redis Persistence
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure Redis as grain storage
            .AddRedisGrainStorage(
                name: "redisStore",
                configureOptions: options =>
                {
                    options.ConnectionString = "localhost:6379";
                    options.Database = 0;
                    options.UseJson = true; // Serializes grain state as JSON
                    options.KeyPrefix = "grain-"; // Optional prefix for Redis keys
                });
    });

// Run the host
await builder.RunAsync();
```

## Example - Using Grain Storage in a Grain
```csharp
// Define grain state class

public class MyGrainState
{
    public string Data { get; set; }
    public int Version { get; set; }
}

// Grain implementation that uses Redis storage
public class MyGrain : Grain, IMyGrain, IGrainWithStringKey
{
    private readonly IPersistentState<MyGrainState> _state;

    public MyGrain([PersistentState("state", "redisStore")] IPersistentState<MyGrainState> state)
    {
        _state = state;
    }

    public async Task SetData(string data)
    {
        _state.State.Data = data;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<string> GetData()
    {
        return Task.FromResult(_state.State.Data);
    }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Grain Persistence](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-persistence)
- [Redis Documentation](https://redis.io/documentation)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)