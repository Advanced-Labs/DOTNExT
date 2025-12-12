# Test DOTNExT Environment.IsDotnext

$coreRoot = "D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root"

Write-Host "Setting up Core_Root..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $coreRoot -Force | Out-Null

Write-Host "Copying coreclr binaries..." -ForegroundColor Cyan
Copy-Item "D:\Dev\DOTNExT\src\runtime\artifacts\bin\coreclr\windows.x64.Release\*" $coreRoot -Force

Write-Host "Copying BCL libraries..." -ForegroundColor Cyan
Copy-Item "D:\Dev\DOTNExT\src\runtime\artifacts\bin\runtime\net9.0-windows-Release-x64\*" $coreRoot -Force

Write-Host "Building test app..." -ForegroundColor Cyan
Push-Location "D:\Dev\DOTNExT\test-isdonext"
dotnet build -c Release --nologo -v q
Pop-Location

Write-Host "`nRunning with DOTNExT corerun:" -ForegroundColor Green
$env:CORE_ROOT = $coreRoot
& "$coreRoot\corerun.exe" "D:\Dev\DOTNExT\test-isdonext\bin\Release\net9.0\test-isdonext.dll"
