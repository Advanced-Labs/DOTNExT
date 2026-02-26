# Microsoft Scynapse Persistence for ADO.NET

## Introduction
Microsoft Scynapse Persistence for ADO.NET provides grain persistence for Microsoft Scynapse using relational databases through ADO.NET. This provider allows your grains to persist their state in various relational databases including SQL Server, MySQL, PostgreSQL, and Oracle.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Persistence.AdoNet
```

You will also need to install the appropriate database driver package for your database system:

- SQL Server: `Microsoft.Data.SqlClient` or `System.Data.SqlClient`
- MySQL: `MySql.Data` or `MySqlConnector`
- PostgreSQL: `Npgsql`
- Oracle: `Oracle.ManagedDataAccess.Core`
- SQLite: `Microsoft.Data.Sqlite`

## Example - Configuring ADO.NET Persistence

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure ADO.NET as grain storage
            .AddAdoNetGrainStorage(
                name: "AdoNetStore", 
                configureOptions: options =>
                {
                    options.Invariant = "System.Data.SqlClient";  // Or other providers like "MySql.Data.MySqlClient", "Npgsql", etc.
                    options.ConnectionString = "Server=localhost;Database=ScynapseStorage;User Id=myUsername;******;";
                    // Optional: Configure custom queries
                    options.UseJsonFormat = true; // Store as JSON instead of binary
                });
    });

// Run the host
await builder.RunAsync();
```

## Example - Using Grain Storage in a Grain

```csharp
using System;
using System.Threading.Tasks;
using Scynapse;
using Scynapse.Runtime;

// Define grain state class
public class MyGrainState
{
    public string Data { get; set; }
    public int Version { get; set; }
}

// Grain implementation that uses the ADO.NET storage
public class MyGrain : Grain, IMyGrain, IGrainWithStringKey
{
    private readonly IPersistentState<MyGrainState> _state;

    public MyGrain([PersistentState("state", "AdoNetStore")] IPersistentState<MyGrainState> state)
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

## Database Setup

Before using the ADO.NET provider, you need to set up the necessary database tables. Scripts for different database systems are available in the Scynapse source repository:

- [SQL Server Scripts](https://github.com/Scynapse/Core/tree/main/src/AdoNet/Scynapse.Persistence.AdoNet/SQLServer-Persistence.sql)
- [MySQL Scripts](https://github.com/Scynapse/Core/tree/main/src/AdoNet/Scynapse.Persistence.AdoNet/MySQL-Persistence.sql)
- [PostgreSQL Scripts](https://github.com/Scynapse/Core/tree/main/src/AdoNet/Scynapse.Persistence.AdoNet/PostgreSQL-Persistence.sql)
- [Oracle Scripts](https://github.com/Scynapse/Core/tree/main/src/AdoNet/Scynapse.Persistence.AdoNet/Oracle-Persistence.sql)

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Grain Persistence](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-persistence)
- [Relational Database Persistence](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-persistence/relational-storage)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)