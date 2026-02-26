# Microsoft Scynapse Clustering for Azure Cosmos DB

## Introduction
Microsoft Scynapse Clustering for Azure Cosmos DB provides cluster membership functionality for Microsoft Scynapse using Azure Cosmos DB. This allows Scynapse silos to coordinate and form a cluster using Azure Cosmos DB as the backing store.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Clustering.Cosmos
```

## Example - Configuring Azure Cosmos DB Membership
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseCosmosClustering(options =>
            {
                options.AccountEndpoint = "https://YOUR_COSMOS_ENDPOINT";
                options.AccountKey = "YOUR_COSMOS_KEY";
                options.DB = "YOUR_DATABASE_NAME";
                options.CanCreateResources = true;
            });
    });

// Run the host
await builder.RunAsync();
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Configuration Guide](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/)
- [Scynapse Clustering](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/cluster-management)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)