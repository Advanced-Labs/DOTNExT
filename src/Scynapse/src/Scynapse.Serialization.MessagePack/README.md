# Microsoft Scynapse Serialization for MessagePack

## Introduction
Microsoft Scynapse Serialization for MessagePack provides MessagePack serialization support for Microsoft Scynapse using the MessagePack format. This high-performance binary serialization format is ideal for scenarios requiring efficient serialization and deserialization.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Serialization.MessagePack
```

## Example - Configuring MessagePack Serialization
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scynapse.Hosting;
using Scynapse.Serialization;

var builder = Host.CreateApplicationBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            // Configure MessagePack as a serializer
            .AddSerializer(serializerBuilder => serializerBuilder.AddMessagePackSerializer());
    });

// Run the host
await builder.RunAsync();
```

## Example - Using MessagePack with a Custom Type
```csharp
using Scynapse;
using Scynapse.Serialization.Cloning;
using Scynapse.Serialization.Codecs;
using Scynapse.Serialization.Configuration;
using Scynapse.Serialization.Serializers;
using MessagePack;
namespace ExampleGrains;

// Define a class with MessagePack attributes
[MessagePackObject]
public class MyMessagePackClass
{
    [Key(0)]
    public string Name { get; set; }
    
    [Key(1)]
    public int Age { get; set; }
    
    [Key(2)]
    public List<string> Tags { get; set; }
}

// You can use it directly in your grain interfaces and implementation
public interface IMyGrain : IGrainWithStringKey
{
    Task<MyMessagePackClass> GetData();
    Task SetData(MyMessagePackClass data);
}

public class MyGrain : Grain, IMyGrain
{
    private MyMessagePackClass _data;

    public Task<MyMessagePackClass> GetData()
    {
        return Task.FromResult(_data);
    }

    public Task SetData(MyMessagePackClass data)
    {
        _data = data;
        return Task.CompletedTask;
    }
}
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Scynapse Serialization](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/serialization)
- [MessagePack for C#](https://github.com/neuecc/MessagePack-CSharp)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/dotnet/scynapse/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/dotnet/scynapse/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/dotnet/scynapse/blob/main/LICENSE)