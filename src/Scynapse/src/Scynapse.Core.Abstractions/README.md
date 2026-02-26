# Microsoft Scynapse Core Abstractions

## Introduction
Microsoft Scynapse Core Abstractions is the foundational library for Scynapse containing the public programming APIs for implementing grains and client code. This package defines the core abstractions that form the Scynapse programming model, including grain interfaces, grain reference interfaces, and attributes.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Core.Abstractions
```

This package is a dependency of both client and silo (server) applications and is automatically included when you reference the Scynapse SDK or the Scynapse client/server metapackages.

## Example - Defining a Grain Interface
```csharp
using Scynapse;

namespace MyGrainInterfaces;

public interface IHelloGrain : IGrainWithStringKey
{
    Task<string> SayHello(string greeting);
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Grain interfaces](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-interfaces)
- [Grain references](https://learn.microsoft.com/en-us/dotnet/scynapse/grains/grain-references)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)