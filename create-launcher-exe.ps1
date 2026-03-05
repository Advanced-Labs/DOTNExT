# Create a small .exe launcher for unique taskbar pinning
$ErrorActionPreference = "Stop"

$exePath = "D:\Dev\DOTNExT\DOTNExT.exe"

# Simple C# launcher that starts vsdotnext.cmd hidden
$code = @"
using System;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = @"D:\Dev\DOTNExT\vsdotnext.cmd",
            Arguments = string.Join(" ", args),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(startInfo);
    }
}
"@

Write-Host "Compiling DOTNExT.exe launcher..." -ForegroundColor Cyan

# Compile to exe
Add-Type -TypeDefinition $code -OutputAssembly $exePath -OutputType ConsoleApplication

if (Test-Path $exePath) {
    Write-Host "Created: $exePath" -ForegroundColor Green
} else {
    Write-Host "Failed to create exe" -ForegroundColor Red
    exit 1
}

# Find VS2022 for icon
$vsPath = $null
foreach ($edition in @("Enterprise", "Professional", "Community")) {
    $path = "${env:ProgramFiles}\Microsoft Visual Studio\2022\$edition\Common7\IDE\devenv.exe"
    if (Test-Path $path) {
        $vsPath = $path
        break
    }
}

# Update desktop shortcut to use the new .exe
$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "DOTNExT VS2022.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = "D:\Dev\DOTNExT"
$shortcut.IconLocation = "$vsPath,0"
$shortcut.Description = "Visual Studio 2022 with DOTNExT custom runtime and compiler"
$shortcut.Save()

Write-Host "Updated desktop shortcut to use DOTNExT.exe" -ForegroundColor Green

# Update Start Menu shortcut too
$startMenu = [Environment]::GetFolderPath("StartMenu")
$startShortcutPath = Join-Path $startMenu "Programs\DOTNExT VS2022.lnk"

$shortcut2 = $shell.CreateShortcut($startShortcutPath)
$shortcut2.TargetPath = $exePath
$shortcut2.WorkingDirectory = "D:\Dev\DOTNExT"
$shortcut2.IconLocation = "$vsPath,0"
$shortcut2.Description = "Visual Studio 2022 with DOTNExT custom runtime and compiler"
$shortcut2.Save()

Write-Host "Updated Start Menu shortcut" -ForegroundColor Green
Write-Host ""
Write-Host "Now you can pin 'DOTNExT VS2022' to taskbar!" -ForegroundColor Cyan
Write-Host "It will appear as a separate icon from regular VS2022." -ForegroundColor Gray
