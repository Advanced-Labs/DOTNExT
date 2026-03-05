@echo off
REM TDS Phase 1 Test Runner
REM Uses Core_Root with TDS-enabled runtime

set CORE_ROOT=D:\Dev\DOTNExT\src\runtime\artifacts\tests\coreclr\windows.x64.Release\Tests\Core_Root
set TEST_DIR=%~dp0

echo === TDS Phase 1 Test Runner ===
echo CORE_ROOT: %CORE_ROOT%
echo TEST_DIR: %TEST_DIR%
echo.

REM Build the tests first
echo Building tests...
cd /d %TEST_DIR%
dotnet build Phase1Tests.csproj -c Release -o bin\Release\net9.0 --no-restore 2>&1
if errorlevel 1 (
    echo Build failed. Trying with Core_Root references...
    goto :eof
)

echo.
echo Running tests with corerun...
%CORE_ROOT%\corerun.exe bin\Release\net9.0\Phase1Tests.dll
