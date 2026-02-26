# Microsoft Scynapse Persistence for Azure Storage

## Introduction
Microsoft Scynapse Persistence for Azure Storage provides grain persistence for Microsoft Scynapse using Azure Storage (Blob and Table). This allows your grains to persist their state in Azure Storage and reload it when they are reactivated.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Persistence.AzureStorage
```

## Example - Configuring Azure Storage Persistence
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

// Define grain interface
public interface IMyGrain : IGrainWithStringKey
{
    Task SetData(string data);
    Task<string> GetData();
}


var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure Azure Table Storage as grain storage
            .AddAzureTableGrainStorage(
                name: "tableStore",
                configureOptions: options =>
                {
                    options.ConnectionString = "YOUR_AZURE_STORAGE_CONNECTION_STRING";
                })
            // Configure Azure Blob Storage as grain storage
            .AddAzureBlobGrainStorage(
                name: "blobStore",
                configureOptions: options =>
                {
                    options.ConnectionString = "YOUR_AZURE_STORAGE_CONNECTION_STRING";
                });
    });

var host = builder.Build();
await host.StartAsync();

// Get a reference to a grain and call it
var client = host.Services.GetRequiredService<IClusterClient>();
var grain = client.GetGrain<IMyGrain>("user123");
await grain.SetData("Hello from Azure Storage!");
var response = await grain.GetData();

// Print the result
Console.WriteLine($"Grain data: {response}");

// Keep the host running until the application is shut down
await host.WaitForShutdownAsync();
```

## Example - Using Grain Storage in a Grain
```csharp
using System;
using System.Threading.Tasks;
using Scynapse;
using Scynapse.Runtime;
namespace ExampleGrains;

// Define grain state class

public class MyGrainState
{
    public string Data { get; set; }
    public int Version { get; set; }
}

// Grain implementation that uses the Azure storage
public class MyGrain : Grain, IMyGrain, IGrainWithStringKey
{
    private readonly IPersistentState<MyGrainState> _state;

    public MyGrain([PersistentState("state", "tableStore")] IPersistentState<MyGrainState> state)
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
- [Azure Storage Persistence](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-persistence/azure-storage)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)