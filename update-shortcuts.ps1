# Update shortcuts to use VSDOTNExT.exe
$exePath = "D:\Dev\DOTNExT\VSDOTNExT.exe"

# Find VS2022 for icon
$vsPath = $null
foreach ($edition in @("Enterprise", "Professional", "Community")) {
    $path = "${env:ProgramFiles}\Microsoft Visual Studio\2022\$edition\Common7\IDE\devenv.exe"
    if (Test-Path $path) {
        $vsPath = $path
        break
    }
}

$shell = New-Object -ComObject WScript.Shell

# Update desktop shortcut
$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "DOTNExT VS2022.lnk"
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = "D:\Dev\DOTNExT"
$shortcut.IconLocation = "$vsPath,0"
$shortcut.Description = "Visual Studio 2022 with DOTNExT custom runtime and compiler"
$shortcut.Save()
Write-Host "Updated desktop shortcut" -ForegroundColor Green

# Update Start Menu shortcut
$startMenu = [Environment]::GetFolderPath("StartMenu")
$startShortcutPath = Join-Path $startMenu "Programs\DOTNExT VS2022.lnk"
$shortcut2 = $shell.CreateShortcut($startShortcutPath)
$shortcut2.TargetPath = $exePath
$shortcut2.WorkingDirectory = "D:\Dev\DOTNExT"
$shortcut2.IconLocation = "$vsPath,0"
$shortcut2.Description = "Visual Studio 2022 with DOTNExT custom runtime and compiler"
$shortcut2.Save()
Write-Host "Updated Start Menu shortcut" -ForegroundColor Green

Write-Host "`nShortcuts now point to: VSDOTNExT.exe" -ForegroundColor Cyan
