# Microsoft Scynapse Clustering for Redis

## Introduction
Microsoft Scynapse Clustering for Redis provides cluster membership functionality for Microsoft Scynapse using Redis. This allows Scynapse silos to coordinate and form a cluster using Redis as the backing store.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Clustering.Redis
```

## Example - Configuring Redis Membership
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            // Configure Redis as the membership provider
            .UseRedisClustering(options =>
            {
                options.ConnectionString = "localhost:6379";
                options.Database = 0;
            });
    });

var host = builder.Build();
await host.StartAsync();

// Get a reference to a grain and call it
var client = host.Services.GetRequiredService<IClusterClient>();
var grain = client.GetGrain<IHelloGrain>("user123");
var response = await grain.SayHello("Redis");

// Print the result
Console.WriteLine($"Grain response: {response}");

// Keep the host running until the application is shut down
await host.WaitForShutdownAsync();
```

## Example - Client Configuration
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;
using Scynapse;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

var clientBuilder = Host.CreateApplicationBuilder(args)
    .UseScynapseClient(builder =>
    {
        builder
            // Configure Redis as the gateway provider
            .UseRedisGatewayListProvider(options =>
            {
                options.ConnectionString = "localhost:6379";
                options.Database = 0;
            });
    });

var host = clientBuilder.Build();
await host.StartAsync();
var client = host.Services.GetRequiredService<IClusterClient>();

// Get a reference to a grain and call it
var grain = client.GetGrain<IHelloGrain>("user123");
var response = await grain.SayHello("Redis Client");

// Print the result
Console.WriteLine($"Grain response: {response}");

// Keep the host running until the application is shut down
await host.WaitForShutdownAsync();
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Configuration Guide](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/)
- [Scynapse Clustering](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/cluster-management)
- [Redis Documentation](https://redis.io/documentation)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)