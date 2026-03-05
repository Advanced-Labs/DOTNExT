# Quick fix for corrupted RoslynDev hive
$ErrorActionPreference = "Stop"

Write-Host "=== DOTNExT Roslyn Quick Fix ===" -ForegroundColor Cyan

# Step 1: Kill VS processes
Write-Host "`n[1/4] Stopping VS processes..." -ForegroundColor Yellow
Stop-Process -Name devenv -Force -ErrorAction SilentlyContinue
Stop-Process -Name VBCSCompiler -Force -ErrorAction SilentlyContinue
Stop-Process -Name MSBuild -Force -ErrorAction SilentlyContinue
Stop-Process -Name "ServiceHub.Host.dotnet.x64" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Write-Host "  Done" -ForegroundColor Green

# Step 2: Find and remove corrupted RoslynDev hive
Write-Host "`n[2/4] Clearing corrupted RoslynDev hive..." -ForegroundColor Yellow
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
    }
}
Write-Host "  Done" -ForegroundColor Green

# Step 3: Rebuild Roslyn with -Restore and -deployExtensions
Write-Host "`n[3/4] Building Roslyn with VSIX deployment..." -ForegroundColor Yellow
Write-Host "  This may take 10-20 minutes..." -ForegroundColor Gray

$roslynRoot = "D:\Dev\DOTNExT\src\roslyn"
Push-Location $roslynRoot

try {
    # Run the build (Build.cmd already implies -build)
    & cmd.exe /c ".\Build.cmd -restore -c Release -deployExtensions"
    if ($LASTEXITCODE -ne 0) {
        throw "Roslyn build failed with exit code $LASTEXITCODE"
    }
    Write-Host "  Build succeeded" -ForegroundColor Green
} finally {
    Pop-Location
}

# Step 4: Verify VSIX files exist
Write-Host "`n[4/4] Verifying VSIX artifacts..." -ForegroundColor Yellow
$vsixPath = "D:\Dev\DOTNExT\src\roslyn\artifacts\VSSetup\Release"
$compilerVsix = Join-Path $vsixPath "Roslyn.Compilers.Extension.vsix"
$setupVsix = Join-Path $vsixPath "Roslyn.VisualStudio.Setup.vsix"

if ((Test-Path $compilerVsix) -and (Test-Path $setupVsix)) {
    Write-Host "  Compiler VSIX: OK" -ForegroundColor Green
    Write-Host "  Setup VSIX: OK" -ForegroundColor Green
} else {
    Write-Host "  VSIX files missing!" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Fix Complete ===" -ForegroundColor Cyan
Write-Host "`nNext step: Launch VS with:" -ForegroundColor White
Write-Host "  .\vsdotnext.cmd" -ForegroundColor Yellow
Write-Host "  or: devenv /rootSuffix RoslynDev" -ForegroundColor Yellow
