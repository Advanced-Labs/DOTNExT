# Check and fix PATH for vsdotnext
$vmrRoot = "D:\Dev\DOTNExT"

Write-Host "Checking PATH..." -ForegroundColor Cyan

# Get current user PATH
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
Write-Host "`nUser PATH entries:" -ForegroundColor Yellow
$userPath -split ";" | ForEach-Object {
    if ($_ -like "*DOTNExT*") {
        Write-Host "  [FOUND] $_" -ForegroundColor Green
    }
}

# Check if VMR root is in PATH
$inPath = $userPath -split ";" | Where-Object { $_ -eq $vmrRoot }

if (-not $inPath) {
    Write-Host "`nDOTNExT NOT in PATH. Adding now..." -ForegroundColor Yellow
    [Environment]::SetEnvironmentVariable("PATH", "$vmrRoot;$userPath", "User")
    Write-Host "Added: $vmrRoot" -ForegroundColor Green
} else {
    Write-Host "`nDOTNExT is in PATH." -ForegroundColor Green
}

# Check if vsdotnext.cmd exists
$cmdPath = Join-Path $vmrRoot "vsdotnext.cmd"
if (Test-Path $cmdPath) {
    Write-Host "`nvsdotnext.cmd exists at: $cmdPath" -ForegroundColor Green
} else {
    Write-Host "`nvsdotnext.cmd NOT FOUND at: $cmdPath" -ForegroundColor Red
}

Write-Host "`n=== To use immediately in current session ===" -ForegroundColor Cyan
Write-Host 'Run this command:' -ForegroundColor White
Write-Host '$env:PATH = [Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH","User")' -ForegroundColor Yellow
