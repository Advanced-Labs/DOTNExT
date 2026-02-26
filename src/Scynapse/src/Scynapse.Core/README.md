# Microsoft Scynapse Core Library

## Introduction
Microsoft Scynapse Core is the primary library used by both client and server applications. It provides the runtime components necessary for Scynapse applications, including serialization, communication, and the core hosting infrastructure.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Core
```

This package is automatically included when you reference the Scynapse SDK or the Scynapse client/server metapackages.

## Example - Configuring a Client

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Scynapse;
using Scynapse.Configuration;
using System;
using System.Threading.Tasks;

// Define a grain interface
namespace MyGrainNamespace;

public interface IHelloGrain : IGrainWithStringKey
{
    Task<string> SayHello(string greeting);
}

// Implement the grain interface
public class HelloGrain : Grain, IHelloGrain
{
    public Task<string> SayHello(string greeting)
    {
        return Task.FromResult($"Hello! I got: {greeting}");
    }
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
var grain = host.Services.GetRequiredService<IClusterClient>().GetGrain<IHelloGrain>("grain-id");
var response = await grain.SayHello("Hello from client!");

// Print the result
Console.WriteLine($"Response: {response}");

// Keep the host running until the application is shut down
await host.WaitForShutdownAsync();
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Client Configuration](https://learn.microsoft.com/en-us/dotnet/scynapse/host/client)
- [Dependency Injection](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/dependency-injection)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)