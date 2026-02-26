# Microsoft Scynapse Serialization

## Introduction
Microsoft Scynapse Serialization is a fast, flexible, and version-tolerant serializer for .NET. It provides the core serialization capabilities for Scynapse, enabling efficient serialization and deserialization of data across the network and for storage.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Serialization
```

This package is automatically included when you reference the Scynapse SDK or the Scynapse client/server metapackages.

## Example

```csharp
// Creating a serializer
var services = new ServiceCollection();
services.AddSerializer();
var serviceProvider = services.BuildServiceProvider();
var serializer = serviceProvider.GetRequiredService<Serializer>();

// Serializing an object
var bytes = serializer.SerializeToArray(myObject);

// Deserializing an object
var deserializedObject = serializer.Deserialize<MyType>(bytes);
```

## Supporting your own Types

To make your types serializable in Scynapse, mark them with the `[GenerateSerializer]` attribute and mark each field/property which should be serialized with the `[Id(int)]` attribute:

```csharp
[GenerateSerializer]
public class MyClass
{
    [Id(0)]
    public string Name { get; set; }
    
    [Id(1)]
    public int Value { get; set; }
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