@echo off
rem Shared by build.cmd / test.cmd / publish.cmd.
rem Uses the .NET 10 SDK installed user-locally in C:\dotnet10 (override with WARDOGS_DOTNET).
rem A bare "dotnet" on this machine resolves to C:\Program Files\dotnet, which has only
rem the .NET 6 runtime and no SDK, so the scripts never fall back to it.

if "%WARDOGS_DOTNET%"=="" set "WARDOGS_DOTNET=C:\dotnet10\dotnet.exe"
if not exist "%WARDOGS_DOTNET%" (
    echo [ERROR] .NET 10 SDK not found: %WARDOGS_DOTNET%
    echo         Install it with dotnet-install.ps1 -Channel 10.0 -InstallDir C:\dotnet10
    echo         or set WARDOGS_DOTNET to another dotnet.exe that has a 10.0 SDK.
    exit /b 1
)
for %%I in ("%WARDOGS_DOTNET%") do set "DOTNET_ROOT=%%~dpI"
set "DOTNET_MULTILEVEL_LOOKUP=0"
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
exit /b 0
