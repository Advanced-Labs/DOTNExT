$logFile = "D:\Dev\DOTNExT\build-log.txt"
if (Test-Path $logFile) {
    $content = Get-Content $logFile
    $keyLines = $content | Where-Object { $_ -match '(Build|error|Error|succeeded|failed|===|Elapsed|COMPLETE|SUCCESS|FAILED)' }
    $keyLines | Select-Object -Last 30
} else {
    "Log file not found"
}
