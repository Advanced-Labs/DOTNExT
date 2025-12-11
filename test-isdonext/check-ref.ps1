# Check if IsDotnext is in System.Runtime.dll ref assembly
$dll = "D:\Dev\DOTNExT\src\runtime\artifacts\bin\System.Runtime\ref\Release\net9.0\System.Runtime.dll"
$bytes = [System.IO.File]::ReadAllBytes($dll)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
if ($text -match "IsDotnext") {
    Write-Host "FOUND in ref assembly!" -ForegroundColor Green
} else {
    Write-Host "NOT FOUND in ref assembly" -ForegroundColor Red
}
