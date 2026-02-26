# Microsoft Scynapse Runtime

## Introduction
Microsoft Scynapse Runtime is the core server-side component of Scynapse. It hosts and executes grains, manages grain lifecycles, and provides all the runtime services necessary for a functioning Scynapse server (silo).

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Runtime
```

This package is automatically included when you reference the Scynapse Server metapackage.

## Example - Configuring a Silo

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering();
    });

await builder.Build().RunAsync();
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Server configuration](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/server-configuration)
- [Silo lifecycle](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/silo-lifecycle)
- [Clustering](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/cluster-management)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)