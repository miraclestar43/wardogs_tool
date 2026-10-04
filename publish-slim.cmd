@echo off
rem Optional small build: dist\WardogsTool-slim\WardogsTool.exe, framework-dependent.
rem REQUIRES the .NET 10 Desktop Runtime (x64) on the target machine:
rem   https://dotnet.microsoft.com/download/dotnet/10.0  ->  ".NET Desktop Runtime 10" for Windows x64
rem Without it Windows shows a "you must install .NET" prompt instead of starting the app.
rem For a build that needs nothing installed, use publish.cmd (dist\WardogsTool-portable).
setlocal
call "%~dp0tools\dotnet-env.cmd" || exit /b 1

set "OUT=%~dp0dist\WardogsTool-slim"
rem Start from an empty folder so files from an older publish never mix in.
if exist "%OUT%" rmdir /s /q "%OUT%"

"%WARDOGS_DOTNET%" publish "%~dp0src\WardogsTool.App\WardogsTool.App.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  -p:PublishSingleFile=true ^
  -p:DebugType=none ^
  -o "%OUT%" --nologo
if errorlevel 1 exit /b 1

echo.
echo Published (requires .NET 10 Desktop Runtime x64):
dir /b "%OUT%"
for %%F in ("%OUT%\WardogsTool.exe") do echo WardogsTool.exe: %%~zF bytes
