# Class Libraries Guide

Complete guide to the .NET Base Class Libraries (BCL) and Microsoft.Extensions framework.

## Overview

**Location:** `src/libraries/`

The libraries directory contains **218+ NuGet packages** that make up the .NET framework, including:
- **System.*** - Base Class Library (BCL)
- **Microsoft.Extensions.*** - Configuration, DI, Logging, Hosting
- **Native implementations** - P/Invoke layers for OS APIs

## Library Organization Pattern

Every library follows a consistent structure:

```
src/libraries/System.Example/
├── ref/                              # Reference assembly (API surface)
│   ├── System.Example.csproj         # Project for ref assembly
│   └── System.Example.cs             # API declarations (throw null)
├── src/                              # Implementation
│   ├── System.Example.csproj         # Project for implementation
│   ├── System/
│   │   └── Example/
│   │       ├── MyClass.cs            # Main implementation
│   │       ├── MyClass.Windows.cs    # Windows-specific
│   │       └── MyClass.Unix.cs       # Unix-specific
│   └── Resources/
│       └── Strings.resx              # Localized strings
├── tests/                            # Unit tests
│   ├── System.Example.Tests.csproj
│   └── MyClassTests.cs
└── pkg/                              # NuGet package definition (optional)
    └── System.Example.pkgproj
```

### Reference vs. Implementation Assemblies

**Reference Assembly (`ref/`):**
- Defines the public API surface
- No implementation (methods `throw null;`)
- Used at compile time
- Allows implementation to change without recompilation

**Implementation Assembly (`src/`):**
- Actual implementation
- May have internal types not in ref assembly
- Used at runtime

**Example:**

`ref/System.Example.cs`:
```csharp
namespace System.Example
{
    public class MyClass
    {
        public void MyMethod() => throw null;  // No implementation
    }
}
```

`src/System/Example/MyClass.cs`:
```csharp
namespace System.Example
{
    public class MyClass
    {
        public void MyMethod()
        {
            // Actual implementation
        }
    }
}
```

### Platform-Specific Implementations

Use partial classes and filename conventions:

- **MyClass.cs** - Shared code
- **MyClass.Windows.cs** - Windows-specific
- **MyClass.Unix.cs** - Unix-specific (Linux, macOS, etc.)
- **MyClass.Linux.cs** - Linux-specific
- **MyClass.OSX.cs** - macOS-specific
- **MyClass.iOS.cs** - iOS-specific
- **MyClass.Android.cs** - Android-specific

**Project file controls inclusion:**
```xml
<ItemGroup Condition="'$(TargetOS)' == 'windows'">
  <Compile Include="System\Example\MyClass.Windows.cs" />
</ItemGroup>
<ItemGroup Condition="'$(TargetOS)' != 'windows'">
  <Compile Include="System\Example\MyClass.Unix.cs" />
</ItemGroup>
```

## Core Libraries

### System.Private.CoreLib

**Special status:** Built with the runtime, not as a separate library.

**Location:**
- CoreCLR: `src/coreclr/System.Private.CoreLib/`
- Mono: `src/mono/System.Private.CoreLib/`
- NativeAOT: `src/coreclr/nativeaot/System.Private.CoreLib/`

**Contains:**
- `System.Object`, `System.String`, `System.Array`
- `System.Int32`, `System.Boolean`, all primitive types
- `System.Exception` and core exception types
- `System.Type`, `System.Reflection.*` core types
- `System.Threading.Thread`, `System.Threading.Tasks.Task`
- `System.Span<T>`, `System.Memory<T>`
- `System.Runtime.CompilerServices.*` compiler services
- Internal VM types (not public)

**Why special:**
- Needs deep integration with runtime (VM)
- Contains types the runtime knows about intrinsically
- No dependencies (everything depends on it)
- Different implementations for each runtime

### System.Runtime

**Location:** `src/libraries/System.Runtime/`

**Purpose:** Umbrella package that forwards types from CoreLib and other core libraries.

**Pattern:** Type forwarding
```csharp
[assembly: TypeForwardedTo(typeof(System.Object))]  // → System.Private.CoreLib
[assembly: TypeForwardedTo(typeof(System.Linq.Enumerable))]  // → System.Linq
```

This allows simple package references:
```xml
<PackageReference Include="System.Runtime" />
<!-- Gets you most of the BCL -->
```

### System.Runtime.InteropServices

**Location:** `src/libraries/System.Runtime.InteropServices/`

**Contains:**
- P/Invoke attribute types (`DllImport`, `LibraryImport`)
- Marshaling attributes (`MarshalAs`, etc.)
- COM interop types (Windows)
- `Marshal` class - Memory allocation, pointer manipulation
- `GCHandle` - Pin managed objects
- Source generators for P/Invoke

## Collections

### System.Collections

**Location:** `src/libraries/System.Collections/`

**Generic collections:**
- `List<T>`, `Dictionary<TKey, TValue>`, `HashSet<T>`
- `LinkedList<T>`, `Queue<T>`, `Stack<T>`
- `SortedList<TKey, TValue>`, `SortedDictionary<TKey, TValue>`

**Non-generic (legacy):**
- `ArrayList`, `Hashtable`, `Queue`, `Stack`

### System.Collections.Concurrent

**Location:** `src/libraries/System.Collections.Concurrent/`

**Thread-safe collections:**
- `ConcurrentDictionary<TKey, TValue>` - Thread-safe dictionary
- `ConcurrentQueue<T>` - Lock-free queue
- `ConcurrentStack<T>` - Lock-free stack
- `ConcurrentBag<T>` - Unordered thread-safe collection
- `BlockingCollection<T>` - Producer-consumer scenarios

### System.Collections.Immutable

**Location:** `src/libraries/System.Collections.Immutable/`

**Immutable collections:**
- `ImmutableList<T>`, `ImmutableDictionary<TKey, TValue>`
- `ImmutableHashSet<T>`, `ImmutableArray<T>`
- Persistent data structures (structural sharing)

## I/O Libraries

### System.IO.FileSystem

**Location:** `src/libraries/System.IO.FileSystem/`

**Types:**
- `File`, `Directory` - Static helpers
- `FileInfo`, `DirectoryInfo` - Object-oriented
- `FileStream`, `DirectoryStream`
- `FileSystemWatcher` - Monitor directory changes

**Platform-specific:**
- Windows: Direct Windows API calls
- Unix: POSIX APIs via P/Invoke

**Native layer:** `src/libraries/Native/Unix/System.Native/`

### System.IO.Compression

**Location:** `src/libraries/System.IO.Compression/`

**Algorithms:**
- Deflate, GZip - `DeflateStream`, `GZipStream`
- Brotli - `BrotliStream`
- ZipArchive - ZIP file manipulation

**Native dependencies:**
- zlib (deflate/gzip)
- brotli
- Located in: `src/libraries/Native/Unix/System.IO.Compression.Native/`

## Networking Libraries

### System.Net.Http

**Location:** `src/libraries/System.Net.Http/`

**Core types:**
- `HttpClient` - Main HTTP client
- `HttpRequestMessage`, `HttpResponseMessage`
- `HttpMessageHandler` - Extensibility point

**Platform handlers:**
- Windows: WinHttp or SocketsHttpHandler
- Unix: SocketsHttpHandler (managed implementation)
- iOS/Android: Native platform handlers

### System.Net.Sockets

**Location:** `src/libraries/System.Net.Sockets/`

**Types:**
- `Socket` - Low-level BSD sockets
- `TcpClient`, `TcpListener`, `UdpClient` - Higher-level wrappers
- `SocketAsyncEventArgs` - High-performance async I/O

**Implementation:**
- Windows: Winsock
- Unix: BSD sockets
- Native layer: `src/libraries/Native/Unix/System.Native/`

### System.Net.Security

**Location:** `src/libraries/System.Net.Security/`

**Types:**
- `SslStream` - TLS/SSL encryption
- `NegotiateStream` - Windows authentication

**Platform TLS:**
- Windows: Schannel (SChannel)
- macOS/iOS: Secure Transport
- Linux: OpenSSL
- Android: Conscrypt
- Native: `src/libraries/Native/Unix/System.Net.Security.Native/`

## Data & Serialization

### System.Text.Json

**Location:** `src/libraries/System.Text.Json/`

**Features:**
- High-performance JSON serialization/deserialization
- Source generators for AOT scenarios
- UTF-8 based (zero allocations for bytes)
- Streaming support (`Utf8JsonReader`, `Utf8JsonWriter`)

**Key types:**
- `JsonSerializer` - Main API
- `JsonDocument` - Read-only DOM
- `JsonNode` - Mutable DOM

### System.Text.RegularExpressions

**Location:** `src/libraries/System.Text.RegularExpressions/`

**Modes:**
- Interpreted (default)
- Compiled (JIT regex to IL)
- Source generated (AOT-friendly, compile-time)

**Features:**
- Full regex support
- Backtracking limits (security)
- Span-based APIs

### System.Linq

**Location:** `src/libraries/System.Linq/`

**Query operators:**
- `Where`, `Select`, `OrderBy`, `GroupBy`
- `Join`, `Aggregate`, `Take`, `Skip`
- Deferred execution (IEnumerable<T>)

### System.Linq.Expressions

**Location:** `src/libraries/System.Linq.Expressions/`

**Expression trees:**
- Represent code as data
- Used by LINQ providers (EF Core, etc.)
- Can be compiled to delegates

## Threading & Async

### System.Threading

**Location:** `src/libraries/System.Private.CoreLib/src/System/Threading/`

**Core in CoreLib:**
- `Thread`, `ThreadPool`
- `Monitor` (lock keyword)
- `Mutex`, `Semaphore`, `EventWaitHandle`
- `Timer`

**Extended in separate libraries:**
- `System.Threading.Channels` - Producer-consumer channels
- `System.Threading.RateLimiting` - Rate limiting
- `System.Threading.Tasks.Dataflow` - TPL Dataflow

### System.Threading.Tasks

**Location:** `src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/`

**Core types:**
- `Task`, `Task<T>` - Asynchronous operations
- `ValueTask`, `ValueTask<T>` - Allocation-free for sync completion
- `TaskScheduler` - Control task execution
- `TaskCompletionSource<T>` - Manual task creation

**async/await:**
- Compiler transformation (not library code)
- State machine in `System.Runtime.CompilerServices`

## Security & Cryptography

### System.Security.Cryptography

**Location:** `src/libraries/System.Security.Cryptography/`

**Algorithms:**
- Hashing: `SHA256`, `SHA512`, `MD5`
- Symmetric: `Aes`, `TripleDES`
- Asymmetric: `RSA`, `ECDsa`, `ECDiffieHellman`
- Random: `RandomNumberGenerator`

**Platform implementations:**
- Windows: CNG (Cryptography API: Next Generation)
  - `src/libraries/System.Security.Cryptography.Cng/`
- macOS/iOS: CommonCrypto / Security framework
  - `src/libraries/Native/Unix/System.Security.Cryptography.Native.Apple/`
- Linux: OpenSSL
  - `src/libraries/Native/Unix/System.Security.Cryptography.Native.OpenSsl/`
- Android: AndroidCrypto

### System.Security.Cryptography.X509Certificates

**Location:** `src/libraries/System.Security.Cryptography.X509Certificates/`

**Certificate handling:**
- `X509Certificate2` - X.509 certificates
- `X509Chain` - Certificate chain validation
- `X509Store` - Certificate stores

## Microsoft.Extensions Framework

### Microsoft.Extensions.DependencyInjection

**Location:** `src/libraries/Microsoft.Extensions.DependencyInjection/`

**Dependency Injection container:**
```csharp
var services = new ServiceCollection();
services.AddSingleton<IMyService, MyService>();
services.AddScoped<IOtherService, OtherService>();
var provider = services.BuildServiceProvider();
var myService = provider.GetRequiredService<IMyService>();
```

**Lifetimes:**
- Singleton - One instance per container
- Scoped - One instance per scope
- Transient - New instance every time

### Microsoft.Extensions.Configuration

**Location:** `src/libraries/Microsoft.Extensions.Configuration/`

**Configuration sources:**
- JSON files - `Microsoft.Extensions.Configuration.Json`
- XML files - `Microsoft.Extensions.Configuration.Xml`
- Environment variables - `Microsoft.Extensions.Configuration.EnvironmentVariables`
- Command line - `Microsoft.Extensions.Configuration.CommandLine`
- In-memory - `Microsoft.Extensions.Configuration.InMemory`

**Usage:**
```csharp
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

string value = config["MyKey"];
var section = config.GetSection("MySection");
```

### Microsoft.Extensions.Logging

**Location:** `src/libraries/Microsoft.Extensions.Logging/`

**Logging abstraction:**
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
});

logger.LogInformation("Hello {Name}", name);
logger.LogError(exception, "Error occurred");
```

**Providers:**
- Console, Debug, EventSource, EventLog
- Third-party: Serilog, NLog, etc.

### Microsoft.Extensions.Hosting

**Location:** `src/libraries/Microsoft.Extensions.Hosting/`

**Generic host for console apps:**
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<MyBackgroundService>();
    })
    .Build();

await host.RunAsync();
```

**Features:**
- Dependency injection
- Configuration
- Logging
- Lifetime management

### Microsoft.Extensions.Options

**Location:** `src/libraries/Microsoft.Extensions.Options/`

**Options pattern:**
```csharp
public class MyOptions
{
    public string Setting1 { get; set; }
    public int Setting2 { get; set; }
}

// Registration
services.Configure<MyOptions>(configuration.GetSection("MyOptions"));

// Usage
public class MyService
{
    public MyService(IOptions<MyOptions> options)
    {
        var settings = options.Value;
    }
}
```

## Native Library Implementations

### System.Native

**Location:** `src/libraries/Native/Unix/System.Native/`

**OS APIs:**
- File I/O: `SystemNative_Open`, `SystemNative_Read`, `SystemNative_Write`
- Processes: `SystemNative_Fork`, `SystemNative_WaitPid`
- Networking: `SystemNative_Socket`, `SystemNative_Bind`
- Environment: `SystemNative_GetEnv`, `SystemNative_GetCwd`

### System.Globalization.Native

**Location:** `src/libraries/Native/Unix/System.Globalization.Native/`

**ICU (International Components for Unicode) bindings:**
- Locale handling
- String comparison / collation
- Date/time formatting
- Number formatting
- Calendar calculations

### System.Security.Cryptography.Native

**Platform-specific crypto:**
- OpenSSL (Linux): `System.Security.Cryptography.Native.OpenSsl/`
- Apple (macOS/iOS): `System.Security.Cryptography.Native.Apple/`

## Common Patterns

### Platform Detection

**Managed code:**
```csharp
if (OperatingSystem.IsWindows())
{
    // Windows-specific
}
else if (OperatingSystem.IsLinux())
{
    // Linux-specific
}
else if (OperatingSystem.IsMacOS())
{
    // macOS-specific
}
```

**Build-time (project file):**
```xml
<ItemGroup Condition="'$(TargetOS)' == 'windows'">
  <Compile Include="*.Windows.cs" />
</ItemGroup>
```

### P/Invoke Patterns

**Location:** `src/libraries/Common/src/Interop/`

**Shared P/Invoke declarations:**
```
src/libraries/Common/src/Interop/
├── Windows/
│   ├── Kernel32/
│   │   └── Interop.ReadFile.cs
│   └── User32/
│       └── Interop.MessageBox.cs
└── Unix/
    └── System.Native/
        └── Interop.Read.cs
```

**LibraryImport pattern:**
```csharp
internal static partial class Interop
{
    internal static partial class Kernel32
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ReadFile(
            SafeHandle hFile,
            byte[] lpBuffer,
            int nNumberOfBytesToRead,
            out int lpNumberOfBytesRead,
            IntPtr lpOverlapped);
    }
}
```

### Resource Strings

**Pattern:** .resx files for localization

`Strings.resx`:
```xml
<data name="ArgumentNull_FileName">
  <value>File name cannot be null.</value>
</data>
```

Generated code:
```csharp
throw new ArgumentNullException(SR.ArgumentNull_FileName);
```

## Building Libraries

### Build a Single Library

```bash
cd src/libraries/System.IO.FileSystem
dotnet build
dotnet test
```

### Build All Libraries

```bash
./build.sh -subset libs
```

### Build with Tests

```bash
./build.sh -subset libs -test
```

## Testing Libraries

### Test Structure

Each library has:
- Unit tests in `tests/` directory
- Tests use xUnit framework
- Theory-based tests for parameterization

**Example:**
```csharp
public class FileTests
{
    [Fact]
    public void ReadAllText_FileExists_ReturnsContent()
    {
        string path = GetTestFilePath();
        File.WriteAllText(path, "Hello");
        Assert.Equal("Hello", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("path1")]
    [InlineData("path2")]
    public void MultipleInputs(string path)
    {
        // Test with various inputs
    }
}
```

### Run Tests

```bash
# Single library
cd src/libraries/System.IO.FileSystem/tests
dotnet test

# All libraries
./build.sh -subset libs.tests

# Specific test
dotnet test --filter "FullyQualifiedName~ReadAllText"
```

## Adding a New Library

### Steps

1. **Create directory structure:**
   ```bash
   mkdir -p src/libraries/System.MyNew/src
   mkdir -p src/libraries/System.MyNew/ref
   mkdir -p src/libraries/System.MyNew/tests
   ```

2. **Create projects:**
   - `ref/System.MyNew.csproj` - Reference assembly
   - `src/System.MyNew.csproj` - Implementation
   - `tests/System.MyNew.Tests.csproj` - Tests

3. **Add to solution:**
   - Update `src/libraries/libs.proj`

4. **Define API (ref):**
   ```csharp
   namespace System.MyNew
   {
       public class MyClass
       {
           public void MyMethod() => throw null;
       }
   }
   ```

5. **Implement (src):**
   ```csharp
   namespace System.MyNew
   {
       public class MyClass
       {
           public void MyMethod()
           {
               // Implementation
           }
       }
   }
   ```

6. **Add tests:**
   ```csharp
   public class MyClassTests
   {
       [Fact]
       public void MyMethod_Works()
       {
           var instance = new MyClass();
           instance.MyMethod();
       }
   }
   ```

7. **Build and test:**
   ```bash
   ./build.sh -subset libs -projects src/libraries/System.MyNew/**/*.csproj
   ```

## API Review Process

For new public APIs:

1. **Mark as preview:**
   ```csharp
   [EditorBrowsable(EditorBrowsableState.Never)]
   public void MyNewAPI() { }
   ```

2. **Create API proposal** - GitHub issue with `api-suggestion` label

3. **API review meeting** - Present to API review board

4. **Get approval** - Remove `EditorBrowsable` attribute

5. **Ship** - API is now public contract (can't break)

## Summary

Libraries organization:
- **218+ packages** in consistent structure
- **ref/** for API surface, **src/** for implementation
- **Platform-specific code** via partial classes
- **Native P/Invoke** in `src/libraries/Native/`
- **Microsoft.Extensions** for modern app patterns

Key libraries:
- **System.Private.CoreLib** - Core types, built with runtime
- **Collections** - Generic, concurrent, immutable
- **I/O** - File system, compression
- **Networking** - HTTP, sockets, security
- **Data** - JSON, XML, LINQ
- **Threading** - Tasks, parallel, async
- **Cryptography** - Platform-abstracted crypto

---

**Next:** See [05-Native-And-Hosting.md](05-Native-And-Hosting.md) for native code and hosting infrastructure.
