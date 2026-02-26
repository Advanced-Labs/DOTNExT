# Microsoft Scynapse Persistence for Azure Cosmos DB

## Introduction
Microsoft Scynapse Persistence for Azure Cosmos DB provides grain persistence for Microsoft Scynapse using Azure Cosmos DB. This allows your grains to persist their state in Azure Cosmos DB and reload it when they are reactivated, offering a globally distributed, multi-model database service for your Scynapse applications.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Persistence.Cosmos
```

## Example - Configuring Azure Cosmos DB Persistence
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure Azure Cosmos DB as grain storage
            .AddCosmosGrainStorage(
                name: "cosmosStore",
                configureOptions: options =>
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

## Example - Using Grain Storage in a Grain
```csharp
// Define grain state class

public class MyGrainState
{
    public string Data { get; set; }
    public int Version { get; set; }
}

// Grain implementation that uses the Cosmos DB storage
public class MyGrain : Grain, IMyGrain, IGrainWithStringKey
{
    private readonly IPersistentState<MyGrainState> _state;

    public MyGrain([PersistentState("state", "cosmosStore")] IPersistentState<MyGrainState> state)
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
- [Azure Storage Providers](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-persistence/azure-storage)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)