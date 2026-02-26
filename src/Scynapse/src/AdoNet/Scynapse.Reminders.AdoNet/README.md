# Microsoft Scynapse Reminders for ADO.NET

## Introduction
Microsoft Scynapse Reminders for ADO.NET provides persistence for Scynapse reminders using ADO.NET-compatible databases (SQL Server, MySQL, PostgreSQL, etc.). This allows your Scynapse applications to schedule persistent reminders that will be triggered even after silo restarts or grain deactivation.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Reminders.AdoNet
```

You will also need to install the appropriate ADO.NET provider for your database:

```shell
# For SQL Server
dotnet add package System.Data.SqlClient

# For MySQL
dotnet add package MySql.Data

# For PostgreSQL
dotnet add package Npgsql
```

## Example - Configuring ADO.NET Reminders
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Scynapse.Configuration;
using Scynapse.Hosting;
using Example;

// Create a host builder
var builder = Host.CreateApplicationBuilder(args);
builder.UseScynapse(siloBuilder =>
{
    siloBuilder
        .UseLocalhostClustering()
        // Configure ADO.NET as reminder storage
        .UseAdoNetReminderService(options =>
        {
            options.Invariant = "System.Data.SqlClient";  // For SQL Server
            options.ConnectionString = "Server=localhost;Database=ScynapseReminders;User ID=scynapse;******;";
        });
});

// Build and start the host
var host = builder.Build();
await host.StartAsync();

// Get a grain reference and use it
var grain = host.Services.GetRequiredService<IGrainFactory>().GetGrain<IReminderGrain>("my-reminder-grain");
await grain.StartReminder("DailyReport");
Console.WriteLine("Reminder started successfully!");

// Keep the host running until the application is shut down
await host.WaitForShutdownAsync();
```

## Example - Using Reminders in a Grain
```csharp
using System;
using System.Threading.Tasks;
using Scynapse;
using Scynapse.Runtime;

namespace Example;

public interface IReminderGrain : IGrainWithStringKey
{
    Task StartReminder(string reminderName);
    Task StopReminder();
}

public class ReminderGrain : Grain, IReminderGrain, IRemindable
{
    private string _reminderName = "MyReminder";

    public async Task StartReminder(string reminderName)
    {
        _reminderName = reminderName;
        
        // Register a persistent reminder
        await RegisterOrUpdateReminder(
            reminderName,
            TimeSpan.FromMinutes(2),  // Time to delay before the first tick (must be > 1 minute)
            TimeSpan.FromMinutes(5)); // Period of the reminder (must be > 1 minute)
    }

    public async Task StopReminder()
    {
        // Find and unregister the reminder
        var reminder = await GetReminder(_reminderName);
        if (reminder != null)
        {
            await UnregisterReminder(reminder);
        }
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        // This method is called when the reminder ticks
        Console.WriteLine($"Reminder {reminderName} triggered at {DateTime.UtcNow}. Status: {status}");
        return Task.CompletedTask;
    }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Reminders and Timers](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/timers-and-reminders)
- [Reminder Services](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/reminder-services)
- [ADO.NET Database Setup](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/adonet-configuration)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)