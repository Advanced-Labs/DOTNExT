@echo off
REM TAI Full Test Build - Build all tests through runtime infrastructure
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
D:
cd D:\Dev\DOTNExT\src\runtime
call src\tests\build.cmd x64 Debug /p:LibrariesConfiguration=Release
