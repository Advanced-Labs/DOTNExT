# Check actual assembly info
$dll = "D:\Dev\DOTNExT\src\roslyn\artifacts\bin\csc\Release\net8.0\csc.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)

Write-Host "Assembly: $($asm.FullName)" -ForegroundColor Cyan
Write-Host ""

$attrs = $asm.GetCustomAttributes($true)
foreach ($attr in $attrs) {
    $name = $attr.GetType().Name
    if ($name -match "Version|Product|Info") {
        Write-Host "$name : $($attr.ToString())"
    }
}

# Also check for DOTNExT in any attribute
$found = $attrs | Where-Object { $_.ToString() -match "DOTNExT" }
if ($found) {
    Write-Host "`nFOUND DOTNExT in attributes!" -ForegroundColor Green
} else {
    Write-Host "`nDOTNExT not in attributes" -ForegroundColor Yellow
}
