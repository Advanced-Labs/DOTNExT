# Suggested Commands for .NET Runtime Development

## Build Commands

### Full Build
```cmd
build.cmd                              # Default Debug build of everything
build.cmd -c Release                   # Release build of everything
```

### Subset Builds (Faster)
```cmd
build.cmd -subset clr -c Release       # CoreCLR runtime only
build.cmd -subset libs -c Release      # Libraries only
build.cmd -subset clr+libs -c Release  # CLR and libraries
build.cmd -subset mono                 # Mono runtime
build.cmd -subset host                 # Host components
```

### Configuration Options
```cmd
build.cmd -c Debug                     # Debug (default)
build.cmd -c Release                   # Release
build.cmd -c Checked                   # Checked (Debug + optimizations, CLR only)
```

### Architecture
```cmd
build.cmd -arch x64                    # 64-bit (default on x64)
build.cmd -arch x86                    # 32-bit
build.cmd -arch arm64                  # ARM64
```

### Get Help
```cmd
build.cmd -help                        # Show all build options
build.cmd -subset help                 # List available subsets
```

## Testing Commands

### Run Tests
```cmd
build.cmd -test                        # Build and run tests
build.cmd -test -testnobuild           # Run tests without building
build.cmd -test -testscope innerloop   # Run innerloop tests only
```

### Generate Test Layout (for corerun testing)
```cmd
src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=Release
```

### Test with corerun
```cmd
artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root\corerun.exe <your_app.dll>
```

## Git Commands
```cmd
git status                             # Check current status
git branch                             # List branches
git log --oneline -10                  # Recent commits
git diff                               # View changes
```

## Visual Studio
```cmd
build.cmd -vs <solution_name>          # Open solution with local SDK
build.cmd -vs CoreCLR.sln              # Open CoreCLR solution
```

## Utility Commands (Windows/PowerShell)
```powershell
Get-ChildItem (or ls, dir)             # List directory
Set-Location (or cd)                   # Change directory
Get-Content (or cat, type)             # Read file
Copy-Item (or copy)                    # Copy files
Remove-Item (or del, rm)               # Delete files
Select-String (or findstr)             # Search in files
```

## Useful Paths
- **Build outputs**: `artifacts/bin/`
- **Test outputs**: `artifacts/tests/`
- **Core_Root**: `artifacts/tests/coreclr/windows.x64.<config>/Tests/Core_Root/`
- **Packages**: `artifacts/packages/`
