# Verify IsDotnext exists in our built CoreLib
$dll = "D:\Dev\DOTNExT\src\runtime\artifacts\bin\coreclr\windows.x64.Release\System.Private.CoreLib.dll"

Write-Host "Loading: $dll" -ForegroundColor Cyan
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$envType = $asm.GetType("System.Environment")
$prop = $envType.GetProperty("IsDotnext")

if ($prop) {
    Write-Host "`nSUCCESS: Environment.IsDotnext property FOUND!" -ForegroundColor Green
    Write-Host "  Type: $($prop.PropertyType)"
    Write-Host "  CanRead: $($prop.CanRead)"
} else {
    Write-Host "`nFAILED: Environment.IsDotnext NOT FOUND" -ForegroundColor Red
}
