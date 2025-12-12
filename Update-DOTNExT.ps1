<#
.SYNOPSIS
    DOTNExT Update Script - Build and deploy custom .NET runtime, compiler, and SDK.

.DESCRIPTION
    Automates the full DOTNExT build and deployment cycle:
    - Build runtime (CLR + BCL)
    - Generate Core_Root for corerun.exe
    - Build Roslyn with VSIX
    - Deploy VSIX to VS experimental instance (RoslynDev hive)
    - Optionally build SDK
    - Set environment variables (session + persistent)
    - Generate vsdotnext.cmd launcher

.PARAMETER SkipRuntime
    Skip runtime build (clr+libs and Core_Root generation)

.PARAMETER SkipRoslyn
    Skip Roslyn build and VSIX generation

.PARAMETER IncludeSDK
    Include SDK build (optional, adds significant build time)

.PARAMETER SkipDeploy
    Skip VSIX deployment to VS experimental instance

.PARAMETER SkipValidation
    Skip validation steps after build/deploy

.PARAMETER NoBuild
    Skip all builds, just deploy and configure environment

.PARAMETER LaunchVS
    Launch Visual Studio with DOTNExT after completion (runs vsdotnext.cmd)

.PARAMETER Configuration
    Build configuration: Release (default) or Debug

.EXAMPLE
    .\Update-DOTNExT.ps1
    Full build and deploy with Release configuration

.EXAMPLE
    .\Update-DOTNExT.ps1 -SkipRuntime
    Build only Roslyn, skip runtime

.EXAMPLE
    .\Update-DOTNExT.ps1 -IncludeSDK -LaunchVS
    Full build including SDK, then launch VS

.EXAMPLE
    .\Update-DOTNExT.ps1 -NoBuild -LaunchVS
    Skip builds, just configure environment and launch VS
#>

param(
    [switch]$SkipRuntime,
    [switch]$SkipRoslyn,
    [switch]$IncludeSDK,
    [switch]$SkipDeploy,
    [switch]$SkipValidation,
    [switch]$NoBuild,
    [switch]$LaunchVS,
    [string]$Configuration = "Release"
)

#region Configuration

$ErrorActionPreference = "Stop"

$VMRRoot = "D:\Dev\DOTNExT"
$RuntimeRoot = "$VMRRoot\src\runtime"
$RoslynRoot = "$VMRRoot\src\roslyn"
$SDKRoot = "$VMRRoot\src\sdk"

# Output paths (will be set based on Configuration)
$script:CoreRoot = $null
$script:RoslynVSIX = $null
$script:SDKOutput = $null

# VS paths
$script:VSInstallDir = $null
$script:VSIXInstaller = $null

# Hash file location for VSIX change detection
$HashDir = "$VMRRoot\.dotnext"

#endregion

#region Helper Functions

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Yellow
}

function Write-Failure {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Red
}

function Initialize-Paths {
    param([string]$Config)

    # Set configuration-dependent paths
    $script:CoreRoot = "$RuntimeRoot\artifacts\tests\coreclr\windows.x64.$Config\Tests\Core_Root"
    $script:RoslynVSIX = "$RoslynRoot\artifacts\VSSetup\$Config"
    $script:SDKOutput = "$SDKRoot\artifacts\bin\redist\$Config\dotnet"

    # Find VS 2022
    $editions = @("Enterprise", "Professional", "Community")
    foreach ($edition in $editions) {
        $path = "${env:ProgramFiles}\Microsoft Visual Studio\2022\$edition"
        if (Test-Path $path) {
            $script:VSInstallDir = $path
            $script:VSIXInstaller = "$path\Common7\IDE\VSIXInstaller.exe"
            Write-Info "Found VS 2022 $edition"
            break
        }
    }

    if (-not $script:VSInstallDir) {
        throw "Visual Studio 2022 not found"
    }

    # Create hash directory if needed
    if (-not (Test-Path $HashDir)) {
        New-Item -ItemType Directory -Path $HashDir -Force | Out-Null
    }
}

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
        $storedHash = (Get-Content $HashFile -Raw).Trim()
        if ($currentHash -eq $storedHash) {
            return $false
        }
    }
    return $true
}

function Save-VSIXHash {
    param([string]$VsixPath, [string]$HashFile)
    $hash = Get-FileHashString -Path $VsixPath
    if ($hash) {
        Set-Content -Path $HashFile -Value $hash -NoNewline
    }
}

#endregion

#region Build Functions

function Stop-StaleProcesses {
    Write-Step "Stopping Stale Processes"

    $processes = @("devenv", "VBCSCompiler", "MSBuild")
    foreach ($proc in $processes) {
        $running = Get-Process -Name $proc -ErrorAction SilentlyContinue
        if ($running) {
            Write-Warning "Stopping $proc..."
            Stop-Process -Name $proc -Force -ErrorAction SilentlyContinue
        }
    }

    # Give processes time to exit
    Start-Sleep -Seconds 2
    Write-Success "Process cleanup complete"
}

function Build-Runtime {
    param([string]$Config)

    Write-Step "Building Runtime (CLR + BCL)"

    Write-Info "Building clr+libs ($Config)..."
    Write-Info "This may take 15-30 minutes..."

    # Use VS Developer Command Prompt for proper build environment
    $vsDevCmd = "$script:VSInstallDir\Common7\Tools\VsDevCmd.bat"
    $vsWherePath = "C:\Program Files (x86)\Microsoft Visual Studio\Installer"
    $buildCmd = "set PATH=$vsWherePath;%PATH% && `"$vsDevCmd`" -no_logo && cd /d $RuntimeRoot && .\build.cmd -subset clr+libs -c $Config"
    & cmd.exe /c $buildCmd
    if ($LASTEXITCODE -ne 0) { throw "Runtime build failed with exit code $LASTEXITCODE" }

    Write-Info "Generating Core_Root..."
    $coreRootCmd = "set PATH=$vsWherePath;%PATH% && `"$vsDevCmd`" -no_logo && cd /d $RuntimeRoot && .\src\tests\build.cmd generatelayoutonly /p:LibrariesConfiguration=$Config"
    & cmd.exe /c $coreRootCmd
    if ($LASTEXITCODE -ne 0) { throw "Core_Root generation failed with exit code $LASTEXITCODE" }

    # Validate
    $corerun = "$script:CoreRoot\corerun.exe"
    if (-not (Test-Path $corerun)) {
        throw "corerun.exe not found at $corerun"
    }

    Write-Success "Runtime build SUCCESS"
    Write-Info "Core_Root: $script:CoreRoot"
}

function Build-Roslyn {
    param([string]$Config)

    Write-Step "Building Roslyn"

    Write-Info "Building with VSIX ($Config)..."
    Write-Info "This may take 10-20 minutes..."

    $vsDevCmd = "$script:VSInstallDir\Common7\Tools\VsDevCmd.bat"
    $vsWherePath = "C:\Program Files (x86)\Microsoft Visual Studio\Installer"
    $roslynCmd = "set PATH=$vsWherePath;%PATH% && `"$vsDevCmd`" -no_logo && cd /d $RoslynRoot && .\Build.cmd -restore -build -c $Config -deployExtensions"
    & cmd.exe /c $roslynCmd
    if ($LASTEXITCODE -ne 0) { throw "Roslyn build failed with exit code $LASTEXITCODE" }

    # Validate VSIX files
    $compilerVsix = "$script:RoslynVSIX\Roslyn.Compilers.Extension.vsix"
    $setupVsix = "$script:RoslynVSIX\Roslyn.VisualStudio.Setup.vsix"

    if (-not (Test-Path $compilerVsix)) {
        throw "Compiler VSIX not found at $compilerVsix"
    }
    if (-not (Test-Path $setupVsix)) {
        throw "VS Setup VSIX not found at $setupVsix"
    }

    Write-Success "Roslyn build SUCCESS"
    Write-Info "VSIX location: $script:RoslynVSIX"
}

function Build-SDK {
    param([string]$Config)

    Write-Step "Building SDK"

    Write-Info "Building SDK ($Config)..."
    Write-Info "This may take 20-40 minutes..."

    $sdkCmd = "cd /d $SDKRoot && .\build.cmd -c $Config"
    & cmd.exe /c $sdkCmd
    if ($LASTEXITCODE -ne 0) { throw "SDK build failed with exit code $LASTEXITCODE" }

    $dotnetExe = "$script:SDKOutput\dotnet.exe"
    if (-not (Test-Path $dotnetExe)) {
        throw "dotnet.exe not found at $dotnetExe"
    }

    Write-Success "SDK build SUCCESS"
    Write-Info "SDK location: $script:SDKOutput"
}

#endregion

#region Deploy Functions

function Deploy-RoslynVSIX {
    Write-Step "Deploying Roslyn VSIX"

    $compilerVsix = "$script:RoslynVSIX\Roslyn.Compilers.Extension.vsix"
    $setupVsix = "$script:RoslynVSIX\Roslyn.VisualStudio.Setup.vsix"

    $compilerHashFile = "$HashDir\compiler-vsix.hash"
    $setupHashFile = "$HashDir\setup-vsix.hash"

    # Check if compiler VSIX changed
    if (Test-VSIXChanged -VsixPath $compilerVsix -HashFile $compilerHashFile) {
        Write-Info "Installing Compiler Extension..."
        & $script:VSIXInstaller /quiet /rootSuffix:RoslynDev $compilerVsix
        if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 1001) {
            # 1001 = already installed (we'll update anyway)
            Save-VSIXHash -VsixPath $compilerVsix -HashFile $compilerHashFile
            Write-Success "Compiler Extension installed"
        } else {
            Write-Warning "Compiler Extension install returned: $LASTEXITCODE"
        }
    } else {
        Write-Info "Compiler Extension unchanged, skipping"
    }

    # Check if setup VSIX changed
    if (Test-VSIXChanged -VsixPath $setupVsix -HashFile $setupHashFile) {
        Write-Info "Installing VS Setup Extension..."
        & $script:VSIXInstaller /quiet /rootSuffix:RoslynDev $setupVsix
        if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 1001) {
            Save-VSIXHash -VsixPath $setupVsix -HashFile $setupHashFile
            Write-Success "VS Setup Extension installed"
        } else {
            Write-Warning "VS Setup Extension install returned: $LASTEXITCODE"
        }
    } else {
        Write-Info "VS Setup Extension unchanged, skipping"
    }

    Write-Success "VSIX deployment complete"
}

#endregion

#region Environment Functions

function Set-DOTNExTEnvironment {
    Write-Step "Setting Environment Variables"

    # === Session Variables ===
    Write-Info "Setting session variables..."

    $env:CORE_ROOT = $script:CoreRoot
    Write-Info "CORE_ROOT = $script:CoreRoot"

    # Add Core_Root to PATH for session
    if ($env:PATH -notlike "*$script:CoreRoot*") {
        $env:PATH = "$script:CoreRoot;$env:PATH"
    }

    $env:DOTNET_MULTILEVEL_LOOKUP = "0"

    if (Test-Path $script:SDKOutput) {
        $env:DOTNET_ROOT = $script:SDKOutput
        Write-Info "DOTNET_ROOT = $script:SDKOutput"
    }

    # === Persistent Variables (User level) ===
    Write-Info "Persisting to user environment..."

    [Environment]::SetEnvironmentVariable("CORE_ROOT", $script:CoreRoot, "User")
    [Environment]::SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0", "User")

    if (Test-Path $script:SDKOutput) {
        [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $script:SDKOutput, "User")
    }

    # Update user PATH if Core_Root not present
    $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($userPath -and $userPath -notlike "*$script:CoreRoot*") {
        [Environment]::SetEnvironmentVariable("PATH", "$script:CoreRoot;$userPath", "User")
        Write-Info "Added Core_Root to user PATH"
    }

    Write-Success "Environment configured (session + persistent)"
}

function New-VSDotNextLauncher {
    Write-Step "Generating vsdotnext.cmd"

    $launcherPath = "$VMRRoot\vsdotnext.cmd"
    $devenvPath = "$script:VSInstallDir\Common7\IDE\devenv.exe"

    # Build the content with actual paths baked in
    $content = @"
@echo off
REM ============================================
REM DOTNExT Visual Studio Launcher
REM Launches VS 2022 with custom runtime + compiler
REM Generated by Update-DOTNExT.ps1
REM ============================================

SET DOTNET_ROOT=$script:SDKOutput
SET DOTNET_MULTILEVEL_LOOKUP=0
SET CORE_ROOT=$script:CoreRoot
SET PATH=%DOTNET_ROOT%;%CORE_ROOT%;%PATH%

echo Starting Visual Studio with DOTNExT...
echo   DOTNET_ROOT=%DOTNET_ROOT%
echo   CORE_ROOT=%CORE_ROOT%
echo   Hive: RoslynDev (experimental)
echo.

start "" "$devenvPath" /rootSuffix RoslynDev %*
"@

    Set-Content -Path $launcherPath -Value $content -Encoding ASCII
    Write-Success "Generated: $launcherPath"
    Write-Info "Run 'vsdotnext.cmd' or 'vsdotnext.cmd MySolution.sln' to launch"
}

#endregion

#region Validation Functions

function Test-DOTNExTSetup {
    Write-Step "Validating Setup"

    $allPassed = $true

    # Test corerun
    Write-Info "Testing corerun..."
    $corerunPath = "$script:CoreRoot\corerun.exe"
    if (Test-Path $corerunPath) {
        try {
            $output = & $corerunPath --help 2>&1
            Write-Success "corerun: OK"
        } catch {
            Write-Failure "corerun: FAILED - $_"
            $allPassed = $false
        }
    } else {
        Write-Failure "corerun: NOT FOUND"
        $allPassed = $false
    }

    # Test custom dotnet if SDK was built
    if (Test-Path $script:SDKOutput) {
        Write-Info "Testing custom dotnet..."
        $dotnetPath = "$script:SDKOutput\dotnet.exe"
        if (Test-Path $dotnetPath) {
            try {
                $output = & $dotnetPath --version 2>&1
                Write-Success "dotnet: OK ($output)"
            } catch {
                Write-Failure "dotnet: FAILED - $_"
                $allPassed = $false
            }
        }
    }

    # Test vsdotnext.cmd exists
    Write-Info "Testing vsdotnext.cmd..."
    if (Test-Path "$VMRRoot\vsdotnext.cmd") {
        Write-Success "vsdotnext.cmd: OK"
    } else {
        Write-Warning "vsdotnext.cmd: NOT FOUND (generate with full run)"
    }

    # Check environment persistence
    Write-Info "Testing persisted environment..."
    $persistedCoreRoot = [Environment]::GetEnvironmentVariable("CORE_ROOT", "User")
    if ($persistedCoreRoot -eq $script:CoreRoot) {
        Write-Success "CORE_ROOT persisted: OK"
    } else {
        Write-Warning "CORE_ROOT not persisted"
    }

    if ($allPassed) {
        Write-Success "All validations passed"
    }
}

#endregion

#region Main Execution

try {
    $startTime = Get-Date

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host "       DOTNExT Update Script" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
    Write-Host "Skip Runtime:  $SkipRuntime" -ForegroundColor Cyan
    Write-Host "Skip Roslyn:   $SkipRoslyn" -ForegroundColor Cyan
    Write-Host "Include SDK:   $IncludeSDK" -ForegroundColor Cyan
    Write-Host "Skip Deploy:   $SkipDeploy" -ForegroundColor Cyan
    Write-Host "No Build:      $NoBuild" -ForegroundColor Cyan
    Write-Host "Launch VS:     $LaunchVS" -ForegroundColor Cyan
    Write-Host ""

    # Initialize paths based on configuration
    Initialize-Paths -Config $Configuration

    # Stop stale processes before build/deploy
    Stop-StaleProcesses

    # Build phase
    if (-not $NoBuild) {
        if (-not $SkipRuntime) {
            Build-Runtime -Config $Configuration
        } else {
            Write-Info "Skipping runtime build"
        }

        if (-not $SkipRoslyn) {
            Build-Roslyn -Config $Configuration
        } else {
            Write-Info "Skipping Roslyn build"
        }

        if ($IncludeSDK) {
            Build-SDK -Config $Configuration
        }
    } else {
        Write-Info "Skipping all builds (NoBuild specified)"
    }

    # Deploy phase
    if (-not $SkipDeploy -and -not $SkipRoslyn) {
        if (Test-Path "$script:RoslynVSIX\Roslyn.Compilers.Extension.vsix") {
            Deploy-RoslynVSIX
        } else {
            Write-Warning "VSIX not found, skipping deployment"
        }
    }

    # Environment setup
    Set-DOTNExTEnvironment

    # Generate launcher
    New-VSDotNextLauncher

    # Validation
    if (-not $SkipValidation) {
        Test-DOTNExTSetup
    }

    # Calculate elapsed time
    $elapsed = (Get-Date) - $startTime
    $elapsedStr = "{0:mm}m {0:ss}s" -f $elapsed

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "       DOTNExT Update COMPLETE" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Elapsed time: $elapsedStr" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  Terminal:  corerun.exe <app.dll>" -ForegroundColor White
    Write-Host "  VS (cmd):  vsdotnext.cmd [solution.sln]" -ForegroundColor White
    Write-Host "  VS (PS):   .\vsdotnext.cmd" -ForegroundColor White
    Write-Host ""

    # Launch VS if requested
    if ($LaunchVS) {
        Write-Host "Launching Visual Studio..." -ForegroundColor Cyan
        & "$VMRRoot\vsdotnext.cmd"
    }
}
catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "       DOTNExT Update FAILED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
    exit 1
}

#endregion
