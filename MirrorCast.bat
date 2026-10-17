@echo off
setlocal
cd /d "%~dp0"

set "DOTNET_CMD="
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
    set "PATH=%USERPROFILE%\.dotnet;%PATH%"
) else if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "DOTNET_CMD=%ProgramFiles%\dotnet\dotnet.exe"
) else (
    set "DOTNET_CMD=dotnet"
)

set "EXE_RELEASE=WindowsReceiver\bin\Release\net8.0-windows10.0.22621.0\MirrorCastReceiver.exe"
set "EXE_DEBUG=WindowsReceiver\bin\Debug\net8.0-windows10.0.22621.0\MirrorCastReceiver.exe"
set "EXE_PUBLISH=WindowsReceiver\bin\Release\publish\MirrorCastReceiver.exe"

if exist "%EXE_PUBLISH%" (
    start "" "%EXE_PUBLISH%"
    exit /b 0
)

if exist "%EXE_RELEASE%" (
    start "" "%EXE_RELEASE%"
    exit /b 0
)

if exist "%EXE_DEBUG%" (
    start "" "%EXE_DEBUG%"
    exit /b 0
)

echo [*] Building and launching MirrorCast Receiver...
"%DOTNET_CMD%" run --project WindowsReceiver\WindowsReceiver.csproj -c Release
