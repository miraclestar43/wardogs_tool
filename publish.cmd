@echo off
rem Builds the release folder dist\WardogsTool\:
rem   WardogsTool.exe, WardogsTool.Core.dll, WardogsTool.exe.config
rem It runs on the .NET Framework 4.8 that is part of Windows 10 1903+ and Windows 11,
rem so users install nothing. Distribute the whole folder (e.g. zipped).
setlocal
call "%~dp0tools\dotnet-env.cmd" || exit /b 1

set "OUT=%~dp0dist\WardogsTool"
rem Start from an empty folder so files from an older publish never mix in.
if exist "%OUT%" rmdir /s /q "%OUT%"

"%WARDOGS_DOTNET%" publish "%~dp0src\WardogsTool.App\WardogsTool.App.csproj" ^
  -c Release ^
  -p:DebugType=none ^
  -o "%OUT%" --nologo
if errorlevel 1 exit /b 1

echo.
echo Published to %OUT%:
dir /a-d "%OUT%" | findstr /r /c:"^[0-9]"
