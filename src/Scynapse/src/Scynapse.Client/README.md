# Microsoft Scynapse Client

## Introduction
Microsoft Scynapse Client is a metapackage that includes all the necessary components to connect to an Scynapse cluster from a client application. It provides a simplified way to set up an Scynapse client by providing a single package reference rather than requiring you to reference multiple packages individually.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Client
```

## Example - Creating an Scynapse Client

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse;
using Scynapse.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
namespace ExampleGrains;

// Define a grain interface
public interface IMyGrain : IGrainWithStringKey
{
    Task<string> DoSomething();
}


// Create a client
var builder = Host.CreateApplicationBuilder(args)
    .UseScynapseClient(client =>
    {
        client.UseLocalhostClustering();
    });

var host = builder.Build();
await host.StartAsync();

// Get a reference to a grain and call it
var client = host.Services.GetRequiredService<IClusterClient>();
var grain = client.GetGrain<IMyGrain>("my-grain-id");
var result = await grain.DoSomething();

// Print the result
Console.WriteLine($"Result: {result}");

// Keep the host running until the application is shut down
await host.WaitForShutdownAsync();
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Scynapse client configuration](https://learn.microsoft.com/en-us/dotnet/scynapse/host/client)
- [Grain references](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-references)
- [Scynapse request context](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/request-context)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)