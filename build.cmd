@echo off
setlocal
call "%~dp0tools\dotnet-env.cmd" || exit /b 1
"%WARDOGS_DOTNET%" build "%~dp0WardogsTool.sln" -c Release --nologo %*
