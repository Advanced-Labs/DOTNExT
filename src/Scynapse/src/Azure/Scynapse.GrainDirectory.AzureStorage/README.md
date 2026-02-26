# Microsoft Scynapse Grain Directory for Azure Storage

## Introduction
Microsoft Scynapse Grain Directory for Azure Storage provides a grain directory implementation using Azure Storage. The grain directory is used to locate active grain instances across the cluster, and this package allows Scynapse to store that information in Azure Storage.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.GrainDirectory.AzureStorage
```

## Example - Configuring Azure Storage Grain Directory
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure Azure Storage as grain directory
            .UseAzureStorageGrainDirectoryAsDefault(options =>
            {
                options.ConnectionString = "YOUR_AZURE_STORAGE_CONNECTION_STRING";
                options.TableName = "GrainDirectory";
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

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)