@echo off
rem Builds dist\WardogsTool-portable\WardogsTool.exe: one self-contained win-x64 file.
rem No Python and no installed .NET runtime needed on the target machine.
setlocal
call "%~dp0tools\dotnet-env.cmd" || exit /b 1

set "OUT=%~dp0dist\WardogsTool-portable"
rem Start from an empty folder so files from an older publish never mix in.
if exist "%OUT%" rmdir /s /q "%OUT%"

"%WARDOGS_DOTNET%" publish "%~dp0src\WardogsTool.App\WardogsTool.App.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=none ^
  -o "%OUT%" --nologo
if errorlevel 1 exit /b 1

echo.
echo Published:
dir /b "%OUT%"
for %%F in ("%OUT%\WardogsTool.exe") do echo WardogsTool.exe: %%~zF bytes
