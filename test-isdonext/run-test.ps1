# Compile and run with DOTNExT runtime

$coreRoot = "D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root"
$refAsm = "D:\Dev\DOTNExT\src\runtime\artifacts\bin\ref\net9.0"

# Ensure Core_Root exists
if (-not (Test-Path "$coreRoot\corerun.exe")) {
    Write-Host "Setting up Core_Root..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $coreRoot -Force | Out-Null
    Copy-Item "D:\Dev\DOTNExT\src\runtime\artifacts\bin\coreclr\windows.x64.Release\*" $coreRoot -Force
    Copy-Item "D:\Dev\DOTNExT\src\runtime\artifacts\bin\runtime\net9.0-windows-Release-x64\*" $coreRoot -Force
}

# Find csc.exe from .NET SDK
$csc = (Get-ChildItem "C:\Program Files\dotnet\sdk" -Recurse -Filter "csc.dll" | Select-Object -First 1).FullName

Write-Host "Compiling with DOTNExT ref assemblies..." -ForegroundColor Cyan
Push-Location "D:\Dev\DOTNExT\test-isdonext"

# Compile using dotnet exec csc.dll
dotnet exec $csc /nologo /out:test.dll /target:exe /reference:"$refAsm\System.Runtime.dll" /reference:"$refAsm\System.Console.dll" Program.cs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nRunning with DOTNExT corerun:" -ForegroundColor Green
    & "$coreRoot\corerun.exe" test.dll
} else {
    Write-Host "Compilation failed" -ForegroundColor Red
}

Pop-Location
