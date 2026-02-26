# Microsoft Scynapse Dashboard Core

## Introduction
Microsoft Scynapse Dashboard Core provides the foundational infrastructure and data collection services for the Scynapse Dashboard. This package contains the core grain services, metrics collection, and data models used by the dashboard UI.

## Getting Started
This package is typically referenced automatically when you install `Genesa.Scynapse.Dashboard`. You generally don't need to reference this package directly unless you're building custom monitoring solutions or extending the dashboard functionality.

To use this package directly, install it via NuGet:

```shell
dotnet add package Genesa.Scynapse.Dashboard.Abstractions
```

## What's Included
This package provides:
- **Metrics Collection Services**: Grain-based services that collect runtime statistics
- **Data Models**: Shared types for representing silo and grain statistics
- **History Tracking**: Time-series data storage for performance metrics
- **Grain Profiling**: Method-level performance tracking infrastructure

## Documentation
For more comprehensive documentation, please refer to:
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
- [Scynapse observability](https://learn.microsoft.com/en-us/dotnet/scynapse/host/monitoring/)
- [Scynapse Dashboard package](https://www.nuget.org/packages/Genesa.Scynapse.Dashboard/)

## Feedback & Contributing
- If you have any issues or would like to provide feedback, please [open an issue on GitHub](https://github.com/Scynapse/Core/issues)
- Join our community on [Discord](https://aka.ms/scynapse-discord)
- Follow the [@msftscynapse](https://twitter.com/msftscynapse) Twitter account for Scynapse announcements
- Contributions are welcome! Please review our [contribution guidelines](https://github.com/Scynapse/Core/blob/main/CONTRIBUTING.md)
- This project is licensed under the [MIT license](https://github.com/Scynapse/Core/blob/main/LICENSE)
