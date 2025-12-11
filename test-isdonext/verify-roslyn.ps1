# Verify Roslyn DOTNExT version
$csc = "D:\Dev\DOTNExT\src\roslyn\artifacts\bin\csc\Release\net9.0\csc.dll"

Write-Host "Checking Roslyn compiler version..." -ForegroundColor Cyan
dotnet exec $csc /version
