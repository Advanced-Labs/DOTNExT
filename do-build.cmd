@echo off
set PATH=C:\Program Files (x86)\Microsoft Visual Studio\Installer;%PATH%
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -no_logo
cd /d D:\Dev\DOTNExT\src\runtime
.\build.cmd -subset clr+libs -c Release
