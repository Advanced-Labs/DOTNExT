@echo off
REM TAI TDS Test Build - Build through runtime test infrastructure
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
D:
cd D:\Dev\DOTNExT\src\runtime

REM Build just the TDS test directory
dotnet msbuild src\tests\tds\Phase1\TDSVerification.csproj /p:Configuration=Debug /p:TargetArchitecture=x64 /p:RuntimeFlavor=coreclr /p:TargetOS=windows /p:LibrariesConfiguration=Release /t:Build
