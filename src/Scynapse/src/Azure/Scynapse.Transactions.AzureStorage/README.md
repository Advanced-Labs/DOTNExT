# Microsoft Scynapse Transactions for Azure Storage

## Introduction
Microsoft Scynapse Transactions for Azure Storage provides the infrastructure to store Scynapse transaction logs in Azure Storage. This package allows Scynapse applications to use ACID transactions across multiple grain calls with Azure Storage as the backing transaction log store.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Transactions.AzureStorage
```

## Example - Configuring Azure Storage for Transactions
```csharp
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;
using Scynapse.Transactions;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Enable transactions
            .AddAzureTableTransactionalStateStorage(
                name: "TransactionStore",
                configureOptions: options =>
                {
                    options.ConnectionString = "YOUR_AZURE_STORAGE_CONNECTION_STRING";
                })
            .UseTransactions();
    });

// Run the host
await builder.RunAsync();
```

## Example - Using Transactions in Grains
```csharp
// A grain with transactional state
public class MyTransactionalGrain : Grain, IMyTransactionalGrain
{
    private readonly ITransactionalState<MyState> _state;

    // Inject the transactional state
    public MyTransactionalGrain(
        [TransactionalState("state", "TransactionStore")]
        ITransactionalState<MyState> state)
    {
        _state = state;
    }

    // Method that performs a transaction
    [Transaction(TransactionOption.Create)]
    public async Task Transfer(string otherGrainKey, int amount)
    {
        // Read our state within the transaction
        var myState = await _state.PerformRead(state => state);
        
        // Ensure we have enough balance
        if (myState.Balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        // Update our state within the transaction
        await _state.PerformUpdate(s => s.Balance -= amount);
        
        // Call another grain within the same transaction
        var otherGrain = GrainFactory.GetGrain<IMyTransactionalGrain>(otherGrainKey);
        await otherGrain.Deposit(amount);
    }

    // Method that participates in a transaction
    [Transaction(TransactionOption.Join)]
    public Task Deposit(int amount)
    {
        // Update state within the joined transaction
        return _state.PerformUpdate(s => s.Balance += amount);
    }

    // Read operation within a transaction
    [Transaction(TransactionOption.CreateOrJoin)]
    public Task<int> GetBalance()
    {
        return _state.PerformRead(s => s.Balance);
    }
}

// State class

public class MyState
{
    public int Balance { get; set; }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Transactions](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/transactions)
- [Distributed ACID Transactions](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/transactions/acid-transactions)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)