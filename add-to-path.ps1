# Add VMR root to user PATH for global access to vsdotnext command
$vmrRoot = "D:\Dev\DOTNExT"
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")

if ($userPath -notlike "*$vmrRoot*") {
    [Environment]::SetEnvironmentVariable("PATH", "$vmrRoot;$userPath", "User")
    Write-Host "Added to user PATH: $vmrRoot" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run from anywhere:" -ForegroundColor Cyan
    Write-Host "  vsdotnext              - Launch VS with DOTNExT"
    Write-Host "  vsdotnext solution.sln - Launch VS with specific solution"
    Write-Host ""
    Write-Host "NOTE: Restart your terminal for PATH changes to take effect." -ForegroundColor Yellow
} else {
    Write-Host "Already in PATH: $vmrRoot" -ForegroundColor Gray
}
