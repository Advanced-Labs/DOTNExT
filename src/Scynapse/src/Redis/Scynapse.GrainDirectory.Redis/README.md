# Microsoft Scynapse Grain Directory for Redis

## Introduction
Microsoft Scynapse Grain Directory for Redis provides a grain directory implementation using Redis. The grain directory is used to locate active grain instances across the cluster, and this package allows Scynapse to store that information in Redis.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.GrainDirectory.Redis
```

## Example - Configuring Redis Grain Directory
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure Redis as the grain directory
            .UseRedisGrainDirectoryAsDefault(options =>
            {
                options.ConnectionString = "localhost:6379";
                options.Database = 0;
            });
    });

// Run the host
await builder.RunAsync();
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Configuration Guide](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/)
- [Implementation Details](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/index)
- [Redis Documentation](https://redis.io/documentation)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)