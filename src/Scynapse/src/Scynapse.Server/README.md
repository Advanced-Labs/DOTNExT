# Microsoft Scynapse Server

## Introduction
Microsoft Scynapse Server is a metapackage that includes all the necessary components to run an Scynapse silo (server). It simplifies the process of setting up an Scynapse server by providing a single package reference rather than requiring you to reference multiple packages individually.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Server
```

## Example - Creating an Scynapse Silo Host

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

// Define a grain interface
namespace MyGrainNamespace;

public interface IMyGrain : IGrainWithStringKey
{
    Task<string> DoSomething();
}

// Implement the grain interface
public class MyGrain : Grain, IMyGrain
{
    public Task<string> DoSomething()
    {
        return Task.FromResult("Done something!");
    }
}


// Create the host
var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering();
    });

// Start the host
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
- [Scynapse server (silo) configuration](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/server-configuration)
- [Hosting Scynapse](https://learn.microsoft.com/en-us/dotnet/scynapse/host/generic-host)
- [Grain persistence](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-persistence)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)