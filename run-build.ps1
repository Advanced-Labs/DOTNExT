# Run build and log to file
$logFile = "D:\Dev\DOTNExT\build-log.txt"
"Build started at $(Get-Date)" | Out-File $logFile

try {
    & "D:\Dev\DOTNExT\Update-DOTNExT.ps1" -SkipValidation *>&1 | Tee-Object -FilePath $logFile -Append
}
catch {
    "ERROR: $_" | Out-File $logFile -Append
}

"Build finished at $(Get-Date)" | Out-File $logFile -Append
