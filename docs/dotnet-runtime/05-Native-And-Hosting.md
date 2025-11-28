# Native Code & Hosting Guide

Guide to native code, hosting infrastructure, and the .NET installation system.

## Overview

Native components provide:
- **Host executables** - The `dotnet` command and application hosts
- **P/Invoke implementations** - Native code called from managed libraries
- **Runtime support** - Platform-specific OS integration

**Locations:**
- Host: `src/native/corehost/`
- Native libs: `src/native/libs/`
- EventPipe: `src/native/eventpipe/`
- Installer: `src/installer/`

## Host Infrastructure (`src/native/corehost/`)

### What is the Host?

The **host** is the native executable that launches .NET applications:
- Finds and loads the correct runtime
- Resolves framework dependencies
- Starts the application

### Host Components

```
src/native/corehost/
├── apphost/           # Application host template
├── dotnet/            # dotnet CLI executable
├── fxr/               # Framework resolver
├── hostfxr/           # Host framework resolver library
├── hostpolicy/        # Host policy implementation
├── hostcommon/        # Shared code
├── nethost/           # Minimal host for embedding
├── comhost/           # COM activation host
└── ijwhost/           # IJW (C++/CLI) host
```

## The dotnet CLI (`src/native/corehost/dotnet/`)

**What it does:**
```
$ dotnet MyApp.dll
```

**Process:**
1. Parse command line
2. Find SDK (for `dotnet build`, etc.) or runtime
3. Load hostfxr.dll
4. Call into framework resolver
5. Load and start application

**Key Files:**
- **dotnet.cpp** - Main entry point
- **fxr_resolver.cpp** - Find and load hostfxr

**Outputs:**
- Linux/macOS: `dotnet` (executable)
- Windows: `dotnet.exe`

## Application Host (`src/native/corehost/apphost/`)

**What it is:**
Template for application-specific executables (e.g., `MyApp.exe`).

**When you publish:**
```bash
dotnet publish -c Release
```

Creates `MyApp.exe` (or `MyApp` on Linux/macOS) by:
1. Copying apphost template
2. Embedding app information
3. Creating self-contained exe

**Apphost contains:**
- Path to application DLL
- Path to runtime (for self-contained)
- Configuration

**Key Files:**
- **apphost.cpp** - Main entry point
- **bundle_marker.cpp** - Single-file app support

## Framework Resolver (`src/native/corehost/fxr/`)

**Purpose:** Find and load the correct runtime version.

**Process:**
```
MyApp.exe starts
    ↓
Loads hostfxr.dll
    ↓
hostfxr resolves:
  - Runtime version needed
  - Runtime location
  - Framework dependencies
    ↓
Loads hostpolicy.dll
    ↓
hostpolicy loads runtime (coreclr.dll or libmonosgen.so)
    ↓
Runtime starts app
```

**Framework Resolution:**
1. Read `MyApp.runtimeconfig.json`
   ```json
   {
     "runtimeOptions": {
       "tfm": "net8.0",
       "framework": {
         "name": "Microsoft.NETCore.App",
         "version": "8.0.0"
       }
     }
   }
   ```

2. Find framework:
   - Self-contained: App directory
   - Framework-dependent: Installed runtimes

3. Apply version roll-forward rules
4. Load found runtime

**Key Files:**
- **fx_resolver.cpp** - Framework resolution logic
- **fx_ver.cpp** - Version handling
- **roll_forward_option.cpp** - Roll-forward policy

## Host Policy (`src/native/corehost/hostpolicy/`)

**Purpose:** Implement hosting policy and load the runtime.

**Responsibilities:**
- Load runtime DLL (coreclr.dll)
- Initialize runtime
- Set up AppDomain (historical)
- Load application assembly
- Invoke entry point

**Key Files:**
- **hostpolicy.cpp** - Main implementation
- **coreclr.cpp** - CoreCLR-specific loading
- **deps_resolver.cpp** - Dependency resolution

## nethost (`src/native/corehost/nethost/`)

**Purpose:** Minimal API for embedding .NET in native apps.

**Usage:**
```c
#include <nethost.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>

// Find hostfxr
char_t buffer[PATH_MAX];
size_t buffer_size = sizeof(buffer);
get_hostfxr_path(buffer, &buffer_size, nullptr);

// Load hostfxr
void* hostfxr = dlopen(buffer, RTLD_LAZY);

// Initialize .NET
// Call managed code
```

**Scenarios:**
- Native game engines hosting .NET for scripting
- Native apps with .NET components
- C/C++ apps using .NET libraries

**API:**
- `get_hostfxr_path()` - Find hostfxr
- Small, minimal dependency

## COM Host (`src/native/corehost/comhost/`)

**Purpose:** Activate .NET classes as COM objects (Windows).

**Usage:**
```c++
// C++ code
IMyInterface* pInterface;
CoCreateInstance(CLSID_MyClass, NULL, CLSCTX_INPROC_SERVER,
                 IID_IMyInterface, (void**)&pInterface);
pInterface->MyMethod();
```

**C# side:**
```csharp
[ComVisible(true)]
[Guid("12345678-1234-1234-1234-123456789012")]
public class MyClass : IMyInterface
{
    public void MyMethod() { }
}
```

**Files:**
- **comhost.cpp** - COM activation entry point

## Native P/Invoke Implementations (`src/native/libs/`)

### System.Native (`src/native/libs/System.Native/`)

**Purpose:** Core OS APIs for file I/O, processes, environment, networking.

**Platform Coverage:**
- Unix: Linux, macOS, FreeBSD, iOS, Android
- Windows: Separate implementations in managed code

**Key APIs:**

**File I/O:**
- `SystemNative_Open` - open()
- `SystemNative_Read` - read()
- `SystemNative_Write` - write()
- `SystemNative_Close` - close()
- `SystemNative_Stat` - stat()

**Process Management:**
- `SystemNative_Fork` - fork()
- `SystemNative_WaitPid` - waitpid()
- `SystemNative_Kill` - kill()

**Environment:**
- `SystemNative_GetEnv` - getenv()
- `SystemNative_SetEnv` - setenv()
- `SystemNative_GetCwd` - getcwd()

**Networking (Unix sockets):**
- `SystemNative_Socket` - socket()
- `SystemNative_Bind` - bind()
- `SystemNative_Listen` - listen()
- `SystemNative_Accept` - accept()
- `SystemNative_Connect` - connect()

**Files:**
- **pal_io.c** - File I/O
- **pal_process.c** - Process management
- **pal_networking.c** - Networking
- **pal_errno.c** - Error handling

### System.Globalization.Native (`src/native/libs/System.Globalization.Native/`)

**Purpose:** Globalization using ICU (International Components for Unicode).

**ICU Bindings:**
- String collation (culture-aware sorting)
- Date/time formatting
- Number formatting
- Calendar support
- Unicode normalization

**Platform Support:**
- Linux: Uses system ICU
- macOS/iOS: Native Apple globalization APIs
- Windows: Uses Windows NLS APIs
- Android: ICU included

**Files:**
- **pal_locale.c** - Locale operations
- **pal_collation.c** - String collation
- **pal_calendarData.c** - Calendar operations
- **pal_timeZoneInfo.c** - Time zone data

### System.IO.Compression.Native

**Purpose:** Compression using zlib and brotli.

**Algorithms:**
- **zlib** - Deflate/inflate (gzip)
- **brotli** - Brotli compression

**Files:**
- Uses external zlib library
- Uses external brotli library

### System.Security.Cryptography.Native

**Platform-specific crypto implementations:**

**OpenSSL (Linux):**
`src/native/libs/System.Security.Cryptography.Native.OpenSsl/`
- AES, RSA, SHA, HMAC, etc.
- Uses OpenSSL 1.1 or 3.0

**Apple (macOS/iOS):**
`src/native/libs/System.Security.Cryptography.Native.Apple/`
- CommonCrypto framework
- Keychain integration

**Android:**
`src/native/libs/System.Security.Cryptography.Native.Android/`
- AndroidCrypto APIs

**Files:**
- **pal_evp.c** - Symmetric crypto
- **pal_rsa.c** - RSA operations
- **pal_hmac.c** - HMAC
- **pal_x509.c** - Certificate operations

### System.Net.Security.Native

**Purpose:** SSL/TLS support.

**Platform Support:**
- Linux: OpenSSL
- macOS/iOS: Secure Transport
- Windows: Schannel (in managed code)

**Files:**
- **pal_ssl.c** - SSL/TLS operations

## EventPipe (`src/native/eventpipe/`)

**Purpose:** Cross-platform event streaming for diagnostics.

**Architecture:**
```
Application fires events
    ↓
EventPipe buffers events
    ↓
IPC endpoint (named pipe/socket)
    ↓
dotnet-trace, dotnet-monitor, etc.
```

**Key Files:**
- **ep.c** - EventPipe core
- **ep-provider.c** - Event providers
- **ep-event.c** - Event definitions
- **ep-session.c** - Diagnostic sessions
- **ep-buffer.c** - Event buffering

**Event Types:**
- GC events
- JIT events
- Exception events
- Thread events
- Custom events

**Tools:**
```bash
# Collect trace
dotnet-trace collect --process-id <pid>

# View events
dotnet-trace convert trace.nettrace --format speedscope
```

## Build System for Native Code

### CMake

All native code uses CMake:

**Root CMakeLists.txt files:**
- `src/native/libs/CMakeLists.txt` - Native libs
- `src/native/corehost/CMakeLists.txt` - Host
- `src/native/eventpipe/CMakeLists.txt` - EventPipe

**Build process:**
```bash
# Configure
cmake -S src/native/libs -B artifacts/obj/native

# Build
cmake --build artifacts/obj/native

# Install
cmake --install artifacts/obj/native
```

**Compiler selection:**
- Linux: gcc or clang
- macOS: clang (from Xcode)
- Windows: MSVC

### Cross-Compilation

**Build ARM64 on x64:**
```bash
# Set up cross-compiler
./eng/common/cross/build-rootfs.sh arm64

# Cross-compile
./build.sh -subset libs.native -arch arm64 -cross
```

## Installer (`src/installer/`)

**Purpose:** Package .NET runtime, SDK, and host for distribution.

### Package Structure

```
src/installer/
├── pkg/                      # Package definitions
│   ├── sfx/                  # Shared framework
│   │   └── Microsoft.NETCore.App/
│   ├── projects/             # Individual packages
│   └── installers/           # Platform installers (MSI, PKG, DEB, RPM)
└── managed/                  # Managed components
    ├── Microsoft.NETCore.DotNetHost/
    └── Microsoft.NETCore.App/
```

### Shared Framework

**Microsoft.NETCore.App** - Runtime shared framework

Contains:
- Runtime (coreclr.dll or libmonosgen.so)
- System.Private.CoreLib.dll
- Framework libraries
- Host policy

**Location after install:**
```
Windows: C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.0\
Linux:   /usr/share/dotnet/shared/Microsoft.NETCore.App/8.0.0/
macOS:   /usr/local/share/dotnet/shared/Microsoft.NETCore.App/8.0.0/
```

### Runtime Packs

**Microsoft.NETCore.App.Runtime.{RID}** - Runtime for specific platform

RID examples:
- win-x64, win-arm64
- linux-x64, linux-arm64
- osx-x64, osx-arm64

### Building Installers

```bash
# Build installer packages
./build.sh -subset installer

# Outputs in artifacts/packages/
```

**Package formats:**
- Windows: MSI
- macOS: PKG
- Linux: DEB (Debian/Ubuntu), RPM (Red Hat/Fedora)

## Runtime Configuration

### runtimeconfig.json

**Purpose:** Configure runtime behavior for an application.

**Example:**
```json
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "8.0.0"
    },
    "configProperties": {
      "System.GC.Server": true,
      "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false
    },
    "rollForward": "LatestMinor"
  }
}
```

**Roll-forward policies:**
- **Minor** - Allow newer minor versions (8.0 → 8.1)
- **Major** - Allow newer major versions (8.0 → 9.0)
- **LatestMinor** - Use latest minor
- **LatestMajor** - Use latest major
- **Disable** - Exact version only

## Debugging Native Code

### Windows (Visual Studio)

1. Open `corehost.sln`
2. Set startup project (e.g., `corerun`)
3. Set breakpoints in C++ code
4. F5 to debug

### Linux (GDB)

```bash
gdb --args dotnet MyApp.dll
(gdb) break hostfxr_main_startupinfo
(gdb) run
(gdb) backtrace
```

### macOS (LLDB)

```bash
lldb -- dotnet MyApp.dll
(lldb) breakpoint set --name hostfxr_main_startupinfo
(lldb) run
(lldb) bt
```

## Common Development Tasks

### Modify Host Behavior

1. Edit `src/native/corehost/fxr/*.cpp`
2. Rebuild:
   ```bash
   ./build.sh -subset host.native
   ```
3. Test with rebuilt dotnet:
   ```bash
   artifacts/bin/dotnet/Debug/dotnet MyApp.dll
   ```

### Add P/Invoke Function

1. Add native function in `src/native/libs/System.Native/`:
   ```c
   int32_t SystemNative_MyFunction(const char* arg)
   {
       // Implementation
       return 0;
   }
   ```

2. Add declaration in header:
   ```c
   PALEXPORT int32_t SystemNative_MyFunction(const char* arg);
   ```

3. Add P/Invoke in managed code:
   ```csharp
   [LibraryImport(Libraries.SystemNative, EntryPoint = "SystemNative_MyFunction")]
   internal static partial int MyFunction(string arg);
   ```

4. Rebuild native libs:
   ```bash
   ./build.sh -subset libs.native
   ```

### Test Native Changes

```bash
# Rebuild native components
./build.sh -subset libs.native+host.native

# Run tests
./build.sh -subset libs.tests
```

## Summary

**Host components:**
- **dotnet** - CLI executable
- **apphost** - Application-specific exe
- **hostfxr** - Framework resolver
- **hostpolicy** - Runtime loader

**Native libraries:**
- **System.Native** - Core OS APIs
- **System.Globalization.Native** - ICU bindings
- **System.Security.Cryptography.Native** - Platform crypto
- **System.Net.Security.Native** - SSL/TLS

**EventPipe:**
- Cross-platform diagnostics
- Event streaming
- Used by dotnet-trace, dotnet-counters

**Installer:**
- Packages runtime for distribution
- MSI, PKG, DEB, RPM formats
- Shared framework layout

---

**Next:** See [06-Build-System.md](06-Build-System.md) for build infrastructure details.
