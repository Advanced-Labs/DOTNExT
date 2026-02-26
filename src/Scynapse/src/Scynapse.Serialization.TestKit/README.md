# Microsoft Scynapse Serialization Test Kit

## Introduction
Microsoft Scynapse Serialization Test Kit provides tools and utilities to help test serialization functionality in Scynapse applications. This package simplifies writing tests that verify serialization and deserialization of your custom types work correctly.

## Getting Started
To use this package, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Serialization.TestKit
```

You'll typically add this package to a test project.

## Example - Testing Serialization 
```csharp
using Scynapse.Serialization.TestKit;
using Xunit.Abstractions;

public class TimeSpanTests(ITestOutputHelper output) : FieldCodecTester<TimeSpan>(output)
{
    protected override TimeSpan CreateValue() => TimeSpan.FromMilliseconds(Guid.NewGuid().GetHashCode());
    protected override TimeSpan[] TestValues => [TimeSpan.MinValue, TimeSpan.MaxValue, TimeSpan.Zero, TimeSpan.FromSeconds(12345)];
    protected override Action<Action<TimeSpan>> ValueProvider => Gen.TimeSpan.ToValueProvider();
}

public class TimeSpanCopierTests(ITestOutputHelper output) : CopierTester<TimeSpan, IDeepCopier<TimeSpan>>(output)
{
    protected override TimeSpan CreateValue() => TimeSpan.FromMilliseconds(Guid.NewGuid().GetHashCode());
    protected override TimeSpan[] TestValues => [TimeSpan.MinValue, TimeSpan.MaxValue, TimeSpan.Zero, TimeSpan.FromSeconds(12345)];
    protected override Action<Action<TimeSpan>> ValueProvider => Gen.TimeSpan.ToValueProvider();
}

public class DateTimeOffsetTests(ITestOutputHelper output) : FieldCodecTester<DateTimeOffset, DateTimeOffsetCodec>(output)
{
    protected override DateTimeOffset CreateValue() => DateTime.UtcNow;
    protected override DateTimeOffset[] TestValues =>
    [
        DateTimeOffset.MinValue,
        DateTimeOffset.MaxValue,
        new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0), TimeSpan.FromHours(11.5)),
        new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0), TimeSpan.FromHours(-11.5)),
    ];

    protected override Action<Action<DateTimeOffset>> ValueProvider => Gen.DateTimeOffset.ToValueProvider();
}

public class DateTimeOffsetCopierTests(ITestOutputHelper output) : CopierTester<DateTimeOffset, IDeepCopier<DateTimeOffset>>(output)
{
    protected override DateTimeOffset CreateValue() => DateTime.UtcNow;
    protected override DateTimeOffset[] TestValues =>
    [
        DateTimeOffset.MinValue,
        DateTimeOffset.MaxValue,
        new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0), TimeSpan.FromHours(11.5)),
        new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0), TimeSpan.FromHours(-11.5)),
    ];

    protected override Action<Action<DateTimeOffset>> ValueProvider => Gen.DateTimeOffset.ToValueProvider();
}
```

## Additional Testing Features
The TestKit provides several utilities for testing serialization and allows you to focus on testing specific serialization components:

```csharp
// Using a specific serializer
var specificSerializer = services.GetRequiredService<Serializer<MyCustomType>>();
byte[] bytes = specificSerializer.SerializeToArray(original);
var deserialized = specificSerializer.Deserialize(bytes);
```

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Scynapse Serialization](https://learn.microsoft.com/en-us/dotnet/scynapse/host/configuration-guide/serialization)
- [Testing Scynapse Applications](https://learn.microsoft.com/en-us/dotnet/scynapse/implementation/testing)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)