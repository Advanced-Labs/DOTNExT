# Quick deploy - clears hive and reinstalls existing VSIX (no rebuild)
$ErrorActionPreference = "Stop"

Write-Host "=== DOTNExT Roslyn Quick Deploy (No Rebuild) ===" -ForegroundColor Cyan

# Step 1: Kill VS processes
Write-Host "`n[1/4] Stopping VS processes..." -ForegroundColor Yellow
Stop-Process -Name devenv -Force -ErrorAction SilentlyContinue
Stop-Process -Name VBCSCompiler -Force -ErrorAction SilentlyContinue
Stop-Process -Name MSBuild -Force -ErrorAction SilentlyContinue
Stop-Process -Name "ServiceHub.Host.dotnet.x64" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
Write-Host "  Done" -ForegroundColor Green

# Step 2: Find and remove corrupted RoslynDev hive
Write-Host "`n[2/4] Clearing RoslynDev hive..." -ForegroundColor Yellow
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
$vsPath = Join-Path $localAppData "Microsoft\VisualStudio"

if (Test-Path $vsPath) {
    $roslynDevDirs = Get-ChildItem $vsPath -Directory | Where-Object { $_.Name -like "*RoslynDev*" }
    foreach ($dir in $roslynDevDirs) {
        Write-Host "  Removing: $($dir.FullName)" -ForegroundColor Gray
        Remove-Item -Recurse -Force $dir.FullName -ErrorAction SilentlyContinue
    }
    if ($roslynDevDirs.Count -eq 0) {
        Write-Host "  No RoslynDev hive found (clean state)" -ForegroundColor Gray
    } else {
        Write-Host "  Cleared $($roslynDevDirs.Count) hive folder(s)" -ForegroundColor Green
    }
}

# Step 3: Find VS and install VSIX
Write-Host "`n[3/4] Installing VSIX to experimental hive..." -ForegroundColor Yellow

# Find VS 2022
$vsInstallDir = $null
foreach ($edition in @("Enterprise", "Professional", "Community")) {
    $path = "${env:ProgramFiles}\Microsoft Visual Studio\2022\$edition"
    if (Test-Path $path) {
        $vsInstallDir = $path
        Write-Host "  Found VS 2022 $edition" -ForegroundColor Gray
        break
    }
}

if (-not $vsInstallDir) {
    Write-Host "  VS 2022 not found!" -ForegroundColor Red
    exit 1
}

$vsixInstaller = "$vsInstallDir\Common7\IDE\VSIXInstaller.exe"
$vsixPath = "D:\Dev\DOTNExT\src\roslyn\artifacts\VSSetup\Release"

# Check VSIX files exist
$compilerVsix = Join-Path $vsixPath "Roslyn.Compilers.Extension.vsix"
$setupVsix = Join-Path $vsixPath "Roslyn.VisualStudio.Setup.vsix"

if (-not (Test-Path $compilerVsix)) {
    Write-Host "  Compiler VSIX not found! Run fix-roslyn-deploy.ps1 instead." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $setupVsix)) {
    Write-Host "  Setup VSIX not found! Run fix-roslyn-deploy.ps1 instead." -ForegroundColor Red
    exit 1
}

# Install compiler extension
Write-Host "  Installing Roslyn.Compilers.Extension.vsix..." -ForegroundColor Gray
& $vsixInstaller /quiet /rootSuffix:RoslynDev $compilerVsix
$compilerResult = $LASTEXITCODE
if ($compilerResult -eq 0 -or $compilerResult -eq 1001) {
    Write-Host "  Compiler extension: OK" -ForegroundColor Green
} else {
    Write-Host "  Compiler extension: exit code $compilerResult" -ForegroundColor Yellow
}

# Install VS setup extension
Write-Host "  Installing Roslyn.VisualStudio.Setup.vsix..." -ForegroundColor Gray
& $vsixInstaller /quiet /rootSuffix:RoslynDev $setupVsix
$setupResult = $LASTEXITCODE
if ($setupResult -eq 0 -or $setupResult -eq 1001) {
    Write-Host "  Setup extension: OK" -ForegroundColor Green
} else {
    Write-Host "  Setup extension: exit code $setupResult" -ForegroundColor Yellow
}

# Step 4: Update VS configuration
Write-Host "`n[4/4] Updating VS configuration..." -ForegroundColor Yellow
$devenv = "$vsInstallDir\Common7\IDE\devenv.exe"
& $devenv /updateconfiguration
Write-Host "  Done" -ForegroundColor Green

Write-Host "`n=== Deploy Complete ===" -ForegroundColor Cyan
Write-Host "`nLaunch VS with:" -ForegroundColor White
Write-Host "  .\vsdotnext.cmd" -ForegroundColor Yellow
Write-Host "  or: devenv /rootSuffix RoslynDev" -ForegroundColor Yellow
