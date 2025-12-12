$src = "D:\Dev\DOTNExT\src\sdk\artifacts\bin\redist\Release\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.10\ref\net9.0\System.Runtime.dll"
$dest = "$env:USERPROFILE\.nuget\packages\microsoft.netcore.app.ref\9.0.10\ref\net9.0\System.Runtime.dll"

Write-Host "Source: $src"
Write-Host "Dest: $dest"
Write-Host ""

if (Test-Path $src) {
    Write-Host "Source exists, copying..."
    Copy-Item -Path $src -Destination $dest -Force
    Write-Host "Copy complete!"
} else {
    Write-Host "ERROR: Source file not found"
}

# Clean VS caches
Remove-Item "D:\Dev\DOTNExT\test-isdonext\.vs" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "D:\Dev\DOTNExT\test-isdonext\obj" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "VS caches cleared. Restart VS and reopen the project."
