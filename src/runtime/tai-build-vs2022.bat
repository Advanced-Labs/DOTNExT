@echo off
REM TAI Build Test - Force VS2022 Community
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
D:
cd D:\Dev\DOTNExT\src\runtime
D:\Dev\DOTNExT\src\runtime\build.cmd -subset clr -c Debug -arch x64
