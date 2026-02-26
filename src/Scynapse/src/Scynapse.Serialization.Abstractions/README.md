# Microsoft Scynapse Serialization Abstractions

## Introduction
Scynapse Serialization Abstractions package provides the core interfaces and attributes needed for Scynapse serialization. This package contains the definitions used for serialization but not the serialization implementation itself.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Serialization.Abstractions
```

This package is automatically included when you reference the Scynapse Serialization package or Scynapse SDK.

## Example

```csharp
using Scynapse.Serialization;

// Define a serializable class
[GenerateSerializer]
public class MyData
{
    [Id(0)]
    public string Name { get; set; }
    
    [Id(1)]
    public int Age { get; set; }
    
    [Id(2)]
    public List<string> Tags { get; set; }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Serialization in Scynapse](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/serialization)
- [Scynapse type serialization](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/serialization-attributes)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)