# Update-DOTNExT.ps1 - Script Design

## Overview

A PowerShell script to automate the full DOTNExT build and deployment cycle.

**Location:** `D:\Dev\DOTNExT\Update-DOTNExT.ps1`

---

## Updates (2025-12-05)

- Environment variables: Set for current session AND persist to user environment
- VSIX change detection: Hash comparison, skip reinstall if unchanged
- Use `RoslynDev` hive (matches upstream Roslyn docs)
- Generate `vsdotnext.cmd` - launches VS with custom runtime + compiler
- Add `-LaunchVS` switch - runs `vsdotnext.cmd`

---

## Parameters

```powershell
param(
    [switch]$SkipRuntime,      # Skip runtime build
    [switch]$SkipRoslyn,       # Skip Roslyn build
    [switch]$IncludeSDK,       # Include SDK build (optional, long build)
    [switch]$SkipDeploy,       # Skip VSIX deployment
    [switch]$SkipValidation,   # Skip validation steps
    [switch]$NoBuild,          # Skip all builds, just deploy/configure
    [switch]$LaunchVS,         # Launch VS with DOTNExT after completion
    [string]$Configuration = "Release"  # Build config
)
```

---

## Step-by-Step Implementation

### Step 1: Configuration & Paths

```powershell
$VMRRoot = "D:\Dev\DOTNExT"
$RuntimeRoot = "$VMRRoot\src\runtime"
$RoslynRoot = "$VMRRoot\src\roslyn"
$SDKRoot = "$VMRRoot\src\sdk"

# Output paths
$CoreRoot = "$RuntimeRoot\artifacts\tests\coreclr\windows.x64.$Configuration\Tests\Core_Root"
$RoslynVSIX = "$RoslynRoot\artifacts\VSSetup\$Configuration"
$SDKOutput = "$SDKRoot\artifacts\bin\redist\$Configuration\dotnet"

# VS paths (detect installed edition)
$VSEditions = @("Enterprise", "Professional", "Community")
$VSInstallDir = $null
foreach ($edition in $VSEditions) {
    $path = "${env:ProgramFiles}\Microsoft Visual Studio\2022\$edition"
    if (Test-Path $path) {
        $VSInstallDir = $path
        break
    }
}
$VSIXInstaller = "$VSInstallDir\Common7\IDE\VSIXInstaller.exe"
```

### Step 2: Process Cleanup

```powershell
function Stop-StaleProcesses {
    Write-Host "Stopping stale processes..." -ForegroundColor Cyan

    $processes = @("devenv", "VBCSCompiler", "MSBuild", "dotnet")
    foreach ($proc in $processes) {
        $running = Get-Process -Name $proc -ErrorAction SilentlyContinue
        if ($running) {
            Write-Host "  Stopping $proc..." -ForegroundColor Yellow
            Stop-Process -Name $proc -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Seconds 2
}
```

### Step 3: Build Runtime

```powershell
function Build-Runtime {
    param([string]$Config)

    Write-Host "`n=== Building Runtime ===" -ForegroundColor Green
    Push-Location $RuntimeRoot

    try {
        # Build CLR + Libs
        Write-Host "Building clr+libs..." -ForegroundColor Cyan
        & .\build.cmd -subset clr+libs -c $Config
        if ($LASTEXITCODE -ne 0) { throw "Runtime build failed" }

        # Generate Core_Root
        Write-Host "Generating Core_Root..." -ForegroundColor Cyan
        & .\src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=$Config
        if ($LASTEXITCODE -ne 0) { throw "Core_Root generation failed" }

        # Validate
        $corerun = "$CoreRoot\corerun.exe"
        if (-not (Test-Path $corerun)) {
            throw "corerun.exe not found at $corerun"
        }
        Write-Host "Runtime build SUCCESS" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
```

### Step 4: Build Roslyn

```powershell
function Build-Roslyn {
    param([string]$Config)

    Write-Host "`n=== Building Roslyn ===" -ForegroundColor Green
    Push-Location $RoslynRoot

    try {
        Write-Host "Building with VSIX..." -ForegroundColor Cyan
        & .\Build.cmd -restore -build -c $Config -deployExtensions
        if ($LASTEXITCODE -ne 0) { throw "Roslyn build failed" }

        # Validate VSIX files exist
        $compilerVsix = "$RoslynVSIX\Roslyn.Compilers.Extension.vsix"
        $setupVsix = "$RoslynVSIX\Roslyn.VisualStudio.Setup.vsix"

        if (-not (Test-Path $compilerVsix)) {
            throw "Compiler VSIX not found"
        }
        if (-not (Test-Path $setupVsix)) {
            throw "VS Setup VSIX not found"
        }
        Write-Host "Roslyn build SUCCESS" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
```

### Step 5: Build SDK (Optional)

```powershell
function Build-SDK {
    param([string]$Config)

    Write-Host "`n=== Building SDK ===" -ForegroundColor Green
    Push-Location $SDKRoot

    try {
        & .\build.cmd -c $Config
        if ($LASTEXITCODE -ne 0) { throw "SDK build failed" }

        $dotnetExe = "$SDKOutput\dotnet.exe"
        if (-not (Test-Path $dotnetExe)) {
            throw "dotnet.exe not found at $dotnetExe"
        }
        Write-Host "SDK build SUCCESS" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
```

### Step 6: Deploy VSIX

```powershell
function Deploy-RoslynVSIX {
    Write-Host "`n=== Deploying Roslyn VSIX ===" -ForegroundColor Green

    $compilerVsix = "$RoslynVSIX\Roslyn.Compilers.Extension.vsix"
    $setupVsix = "$RoslynVSIX\Roslyn.VisualStudio.Setup.vsix"

    Write-Host "Installing Compiler Extension..." -ForegroundColor Cyan
    & $VSIXInstaller /quiet /experimental $compilerVsix

    Write-Host "Installing VS Setup..." -ForegroundColor Cyan
    & $VSIXInstaller /quiet /experimental $setupVsix

    Write-Host "VSIX deployment SUCCESS" -ForegroundColor Green
    Write-Host "Launch VS with: devenv.exe /rootSuffix Exp" -ForegroundColor Yellow
}
```

### Step 7: Set Environment

```powershell
function Set-DOTNExTEnvironment {
    Write-Host "`n=== Setting Environment ===" -ForegroundColor Green

    # Core_Root for corerun
    $env:CORE_ROOT = $CoreRoot
    Write-Host "CORE_ROOT = $CoreRoot" -ForegroundColor Cyan

    # Add to PATH
    if ($env:PATH -notlike "*$CoreRoot*") {
        $env:PATH = "$CoreRoot;$env:PATH"
    }

    # If SDK built
    if (Test-Path $SDKOutput) {
        $env:DOTNET_ROOT = $SDKOutput
        $env:DOTNET_MULTILEVEL_LOOKUP = "0"
        Write-Host "DOTNET_ROOT = $SDKOutput" -ForegroundColor Cyan
    }

    Write-Host "Environment configured" -ForegroundColor Green
}
```

### Step 8: Validation

```powershell
function Test-DOTNExTSetup {
    Write-Host "`n=== Validating Setup ===" -ForegroundColor Green

    # Test corerun
    Write-Host "Testing corerun..." -ForegroundColor Cyan
    $corerunHelp = & "$CoreRoot\corerun.exe" --help 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  corerun: OK" -ForegroundColor Green
    } else {
        Write-Host "  corerun: FAILED" -ForegroundColor Red
    }

    # Test custom dotnet if available
    if ($env:DOTNET_ROOT -and (Test-Path "$env:DOTNET_ROOT\dotnet.exe")) {
        Write-Host "Testing custom dotnet..." -ForegroundColor Cyan
        $dotnetInfo = & "$env:DOTNET_ROOT\dotnet.exe" --info 2>&1
        Write-Host "  dotnet: OK" -ForegroundColor Green
    }

    Write-Host "`nValidation complete" -ForegroundColor Green
}
```

### Step 9: Main Script

```powershell
# Main execution
try {
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host "    DOTNExT Update Script" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta

    Stop-StaleProcesses

    if (-not $NoBuild) {
        if (-not $SkipRuntime) { Build-Runtime -Config $Configuration }
        if (-not $SkipRoslyn) { Build-Roslyn -Config $Configuration }
        if ($IncludeSDK -and -not $SkipSDK) { Build-SDK -Config $Configuration }
    }

    if (-not $SkipDeploy) { Deploy-RoslynVSIX }

    Set-DOTNExTEnvironment

    if (-not $SkipValidation) { Test-DOTNExTSetup }

    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "    DOTNExT Update COMPLETE" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "`nNext steps:"
    Write-Host "  - Terminal: corerun.exe <app.dll>"
    Write-Host "  - VS: devenv.exe /rootSuffix Exp"
}
catch {
    Write-Host "`nERROR: $_" -ForegroundColor Red
    exit 1
}
```

---

## Usage Examples

```powershell
# Full build + deploy
.\Update-DOTNExT.ps1

# Skip runtime, only Roslyn
.\Update-DOTNExT.ps1 -SkipRuntime

# Just deploy (no builds)
.\Update-DOTNExT.ps1 -NoBuild

# Include SDK build
.\Update-DOTNExT.ps1 -IncludeSDK

# Debug configuration
.\Update-DOTNExT.ps1 -Configuration Debug
```

---

## Additional Features

### Generate vsdotnext.cmd

```powershell
function New-VSDotNextLauncher {
    $launcherPath = "$VMRRoot\vsdotnext.cmd"

    $content = @"
@echo off
REM DOTNExT Visual Studio Launcher
REM Launches VS 2022 with custom runtime + compiler

SET DOTNET_ROOT=$SDKOutput
SET DOTNET_MULTILEVEL_LOOKUP=0
SET PATH=%DOTNET_ROOT%;%PATH%
SET CORE_ROOT=$CoreRoot

start "" "$VSInstallDir\Common7\IDE\devenv.exe" /rootSuffix RoslynDev %*
"@

    Set-Content -Path $launcherPath -Value $content -Encoding ASCII
    Write-Host "Generated: vsdotnext.cmd" -ForegroundColor Green
}
```

### VSIX Change Detection

```powershell
function Get-FileHashString {
    param([string]$Path)
    if (Test-Path $Path) {
        return (Get-FileHash -Path $Path -Algorithm MD5).Hash
    }
    return $null
}

function Test-VSIXChanged {
    param([string]$VsixPath, [string]$HashFile)

    $currentHash = Get-FileHashString -Path $VsixPath
    if (-not $currentHash) { return $true }

    if (Test-Path $HashFile) {
        $storedHash = Get-Content $HashFile -Raw
        if ($currentHash -eq $storedHash.Trim()) {
            return $false  # Not changed
        }
    }
    return $true  # Changed or no hash stored
}

function Save-VSIXHash {
    param([string]$VsixPath, [string]$HashFile)
    $hash = Get-FileHashString -Path $VsixPath
    Set-Content -Path $HashFile -Value $hash
}
```

### Environment Persistence

```powershell
function Set-DOTNExTEnvironment {
    param([switch]$Persist)

    Write-Host "`n=== Setting Environment ===" -ForegroundColor Green

    # Session variables
    $env:CORE_ROOT = $CoreRoot
    if ($env:PATH -notlike "*$CoreRoot*") {
        $env:PATH = "$CoreRoot;$env:PATH"
    }

    if (Test-Path $SDKOutput) {
        $env:DOTNET_ROOT = $SDKOutput
        $env:DOTNET_MULTILEVEL_LOOKUP = "0"
    }

    # Persist to user environment
    if ($Persist) {
        Write-Host "Persisting to user environment..." -ForegroundColor Cyan
        [Environment]::SetEnvironmentVariable("CORE_ROOT", $CoreRoot, "User")
        [Environment]::SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0", "User")

        if (Test-Path $SDKOutput) {
            [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $SDKOutput, "User")
        }

        # Update PATH (add if not present)
        $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        if ($userPath -notlike "*$CoreRoot*") {
            [Environment]::SetEnvironmentVariable("PATH", "$CoreRoot;$userPath", "User")
        }

        Write-Host "Environment persisted (survives reboot)" -ForegroundColor Green
    }
}
```

---

## Notes

- Script assumes VS 2022 is installed
- SDK build is optional (skip by default) as it's long and often not needed
- VSIX installs to RoslynDev hive (experimental instance, safe)
- Environment variables set for session AND persisted to user environment
- `vsdotnext.cmd` generated for easy VS launching with custom runtime + compiler
- VSIX hashes stored to skip reinstall if unchanged
