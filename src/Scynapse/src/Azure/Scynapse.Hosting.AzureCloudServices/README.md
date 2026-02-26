# Microsoft Scynapse Hosting for Azure Cloud Services

## Introduction
Microsoft Scynapse Hosting for Azure Cloud Services provides support for hosting Scynapse silos in Azure Cloud Services. This package integrates Scynapse with the Azure Cloud Services lifecycle, allowing your silos to properly start, stop, and take advantage of Azure Cloud Services features.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Hosting.AzureCloudServices
```

## Example - Configuring Scynapse with Azure Cloud Services
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;

// In your CloudService WorkerRole entry point
public class WorkerRole : RoleEntryPoint
{
    private ISiloHost _silo;

    public override bool OnStart()
    {
        // Create the silo host
        _silo = Host.CreateApplicationBuilder(args)
            .UseScynapse(builder =>
            {
                // Configure Scynapse for Azure Cloud Services
                builder.UseAzureStorageClustering(options =>
                {
                    options.ConnectionString = "YOUR_AZURE_STORAGE_CONNECTION_STRING";
                });

                // Add other Scynapse configurations as needed
            })
            .Build();

        // Start the silo
        _silo.StartAsync().GetAwaiter().GetResult();
        
        return base.OnStart();
    }

    public override void OnStop()
    {
        // Properly shutdown the silo
        _silo.StopAsync().GetAwaiter().GetResult();
        
        base.OnStop();
    }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Hosting in Azure App Service](https://learn.microsoft.com/en-us/dotnet/scynapse/deployment/deploy-to-azure-app-service)
- [Hosting in Azure Container Apps](https://learn.microsoft.com/en-us/dotnet/scynapse/deployment/deploy-to-azure-container-apps)
- [Hosting in Service Fabric](https://learn.microsoft.com/en-us/dotnet/scynapse/deployment/service-fabric)
- [Silo Configuration](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/typical-configurations)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)
