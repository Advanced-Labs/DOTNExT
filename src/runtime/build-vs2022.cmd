@echo off
REM Force VS2022 environment for build
set "VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\"
set "VS170COMNTOOLS=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\"
call "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64

REM Now run the actual build
call build.cmd %*
