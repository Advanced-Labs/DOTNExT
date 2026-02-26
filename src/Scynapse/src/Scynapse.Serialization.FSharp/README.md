# Microsoft Scynapse Serialization for F#

## Introduction
Microsoft Scynapse Serialization for F# provides serialization support for F# specific types in Microsoft Scynapse. This package enables seamless integration of F# types like discriminated unions, records, and other F# specific constructs with Scynapse serialization system.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Serialization.FSharp
```

## Example - Configuring F# Serialization
```fsharp
open Microsoft.Extensions.Hosting
open Scynapse.Hosting

let builder = 
    Host.CreateApplicationBuilder(args)
        .UseScynapse(fun siloBuilder ->
            siloBuilder
                .UseLocalhostClustering()
                // F# serialization is automatically configured when the package is referenced
                |> ignore)

// Run the host
await builder.RunAsync()
```

## Example - Using F# Types with Scynapse
```fsharp
// Define F# discriminated union and record types
[<Scynapse.GenerateSerializer>]
type UserRole =
    | [<Id(0u)>] Admin
    | [<Id(1u)>] Moderator
    | [<Id(2u)>] User of level:int

[<Scynapse.GenerateSerializer>]
type UserRecord = {
    [<Id(0u)>] Id: string
    [<Id(1u)>] Name: string
    [<Id(2u)>] Role: UserRole
    [<Id(3u)>] Tags: string list
}

// Define a grain interface
type IFSharpGrain =
    inherit Scynapse.IGrainWithStringKey
    abstract member GetUser: unit -> Task<UserRecord>
    abstract member UpdateUser: UserRecord -> Task

// Grain implementation
type FSharpGrain() =
    inherit Scynapse.Grain()
    let mutable userData = Unchecked.defaultof<UserRecord>

    interface IFSharpGrain with
        member this.GetUser() =
            Task.FromResult(userData)
            
        member this.UpdateUser(user) =
            userData <- user
            Task.CompletedTask
```

## Example - Client Code Using F# Grain
```fsharp
// Client-side code
let client = clientBuilder.Build()
let grain = client.GetGrain<IFSharpGrain>("user1")

// Create an F# record with discriminated union
let user = {
    Id = "user1"
    Name = "F# User"
    Role = UserRole.Admin
    Tags = ["functional"; "programming"]
}

// Call the grain with F# types
grain.UpdateUser(user) |> Async.AwaitTask |> ignore
let retrievedUser = grain.GetUser() |> Async.AwaitTask |> Async.RunSynchronously
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Scynapse Serialization](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/serialization)
- [F# Documentation](https://learn.microsoft.com/en-us/dotnet/fsharp/)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)