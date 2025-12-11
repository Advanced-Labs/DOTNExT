# Check IL for IsDotnext using strings search
$dll = "D:\Dev\DOTNExT\src\runtime\artifacts\bin\coreclr\windows.x64.Release\System.Private.CoreLib.dll"

Write-Host "Searching for 'IsDotnext' in System.Private.CoreLib.dll..." -ForegroundColor Cyan

# Use findstr on the binary (will find string if it exists)
$result = & findstr /C:"IsDotnext" $dll 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "`nSUCCESS: 'IsDotnext' string found in DLL!" -ForegroundColor Green
} else {
    Write-Host "`nSearching with Select-String..." -ForegroundColor Yellow
    $bytes = [System.IO.File]::ReadAllBytes($dll)
    $text = [System.Text.Encoding]::ASCII.GetString($bytes)
    if ($text -match "IsDotnext") {
        Write-Host "SUCCESS: 'IsDotnext' found in binary!" -ForegroundColor Green
    } else {
        Write-Host "NOT FOUND in binary" -ForegroundColor Red
    }
}
