# Microsoft Scynapse Event Sourcing

## Introduction
Microsoft Scynapse Event Sourcing provides support for implementing event-sourced grains. Event sourcing is a pattern where state changes are recorded as a sequence of events rather than just storing the current state. This provides a complete history of changes and allows for powerful capabilities like replaying events, temporal querying, and more robust auditing.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.EventSourcing
```

## Example - Creating an Event-Sourced Grain

```csharp
using Scynapse;
using Scynapse.EventSourcing;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

// Define grain state and events
namespace BankAccount;

public class BankAccountState
{
    public decimal Balance { get; set; }
    public string AccountHolder { get; set; }
    public int Version { get; set; }
}

public class DepositEvent
{
    public decimal Amount { get; set; }
}

public class WithdrawalEvent
{
    public decimal Amount { get; set; }
}

// Grain interface
public interface IBankAccountGrain : IGrainWithStringKey
{
    Task<decimal> GetBalance();
    Task Deposit(decimal amount);
    Task Withdraw(decimal amount);
    Task<IReadOnlyList<object>> GetHistory();
}

// Event-sourced grain implementation using JournaledGrain
public class BankAccountGrain : JournaledGrain<BankAccountState, object>, IBankAccountGrain
{
    public async Task<decimal> GetBalance()
    {
        // The state is automatically hydrated from the event log
        return State.Balance;
    }

    public async Task Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive");

        // Record the event - this will be persisted and applied to state
        RaiseEvent(new DepositEvent { Amount = amount });
        
        // Confirm the event is persisted
        await ConfirmEvents();
    }

    public async Task Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Withdrawal amount must be positive");
            
        if (State.Balance < amount)
            throw new InvalidOperationException("Insufficient funds");

        // Record the event
        RaiseEvent(new WithdrawalEvent { Amount = amount });
        
        // Confirm the event is persisted
        await ConfirmEvents();
    }

    public Task<IReadOnlyList<object>> GetHistory()
    {
        // Return the complete history of events
        return Task.FromResult<IReadOnlyList<object>>(RetrieveConfirmedEvents(0, Version).ToList());
    }

    // Event handlers to update the state based on events
    protected override void ApplyEvent(object @event)
    {
        switch (@event)
        {
            case DepositEvent deposit:
                State.Balance += deposit.Amount;
                break;
                
            case WithdrawalEvent withdrawal:
                State.Balance -= withdrawal.Amount;
                break;
        }
    }
}
```

## Example - Configuring Event Sourcing with Storage

```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;
using Scynapse.EventSourcing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure the log consistency provider for event sourcing
            .AddLogStorageBasedLogConsistencyProvider("LogStorage")
            // Configure a storage provider to store the events
            .AddMemoryGrainStorage("PubSubStore")
            .ConfigureServices(services =>
            {
                // Configure default log consistency provider
                services.Configure<JournaledGrainOptions>(options =>
                {
                    options.DefaultLogConsistencyProvider = "LogStorage";
                });
            });
    });

var host = builder.Build();
await host.StartAsync();

// Get a reference to a grain and call it
var client = host.Services.GetRequiredService<IClusterClient>();
var bankAccount = client.GetGrain<IBankAccountGrain>("account-123");

// Call grain methods
await bankAccount.Deposit(100);
await bankAccount.Withdraw(25);
var balance = await bankAccount.GetBalance();

// Print the result
Console.WriteLine($"Account balance: ${balance}");

var history = await bankAccount.GetHistory();
Console.WriteLine($"Transaction history: {history.Count} events");

// Keep the host running until the application is shut down
await host.WaitForShutdownAsync();
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Event Sourcing Overview](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/event-sourcing/overview)
- [Journaled Grains](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/event-sourcing/journaled-grains)
- [Replicated Grains](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/event-sourcing/replicated-grains)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)