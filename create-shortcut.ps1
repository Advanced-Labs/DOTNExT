# Create desktop shortcut for DOTNExT VS2022
$ErrorActionPreference = "Stop"

# Find VS2022
$vsPath = $null
foreach ($edition in @("Enterprise", "Professional", "Community")) {
    $path = "${env:ProgramFiles}\Microsoft Visual Studio\2022\$edition\Common7\IDE\devenv.exe"
    if (Test-Path $path) {
        $vsPath = $path
        Write-Host "Found VS 2022 $edition" -ForegroundColor Gray
        break
    }
}

if (-not $vsPath) {
    Write-Host "VS 2022 not found!" -ForegroundColor Red
    exit 1
}

# Shortcut properties
$shortcutName = "DOTNExT VS2022"
$targetPath = "D:\Dev\DOTNExT\vsdotnext.cmd"
$workingDir = "D:\Dev\DOTNExT"
$iconPath = $vsPath
$description = "Visual Studio 2022 with DOTNExT custom runtime and compiler"

# Desktop path
$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "$shortcutName.lnk"

# Create shortcut using WScript.Shell
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetPath
$shortcut.WorkingDirectory = $workingDir
$shortcut.IconLocation = "$iconPath,0"
$shortcut.Description = $description
$shortcut.Save()

Write-Host ""
Write-Host "Shortcut created: $shortcutPath" -ForegroundColor Green
Write-Host ""
Write-Host "You can now:" -ForegroundColor Cyan
Write-Host "  - Double-click it on your desktop"
Write-Host "  - Right-click > Pin to Start"
Write-Host "  - Right-click > Pin to taskbar"
Write-Host ""

# Also create one in Start Menu for easier pinning
$startMenu = [Environment]::GetFolderPath("StartMenu")
$startMenuPrograms = Join-Path $startMenu "Programs"
$startShortcutPath = Join-Path $startMenuPrograms "$shortcutName.lnk"

$shortcut2 = $shell.CreateShortcut($startShortcutPath)
$shortcut2.TargetPath = $targetPath
$shortcut2.WorkingDirectory = $workingDir
$shortcut2.IconLocation = "$iconPath,0"
$shortcut2.Description = $description
$shortcut2.Save()

Write-Host "Also added to Start Menu: $startShortcutPath" -ForegroundColor Green
Write-Host "Search 'DOTNExT' in Start Menu to find it." -ForegroundColor Cyan
