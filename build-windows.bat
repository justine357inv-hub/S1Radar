@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
set "DOTNET_EXE="
set "DOTNET_ROOT_LOCAL=%cd%\.dotnet"

call :find_dotnet
if defined DOTNET_EXE goto :build

echo .NET 8 SDK was not found. Trying private S1Radar SDK...
if exist "%DOTNET_ROOT_LOCAL%\dotnet.exe" (
  set "DOTNET_EXE=%DOTNET_ROOT_LOCAL%\dotnet.exe"
  goto :build
)

set "INSTALL_SCRIPT=%TEMP%\s1radar-dotnet-install.ps1"
echo Downloading Microsoft's official .NET 8 installer...
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '%INSTALL_SCRIPT%'" || goto :download_error

set "DOTNET_ARCH=x64"
if /I "%PROCESSOR_ARCHITECTURE%"=="x86" set "DOTNET_ARCH=x86"
if defined PROCESSOR_ARCHITEW6432 set "DOTNET_ARCH=x64"
echo Installing .NET 8 SDK for %DOTNET_ARCH%...
powershell -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_SCRIPT%" -Channel 8.0 -Architecture %DOTNET_ARCH% -InstallDir "%DOTNET_ROOT_LOCAL%" || goto :install_error
if not exist "%DOTNET_ROOT_LOCAL%\dotnet.exe" goto :install_error
set "DOTNET_EXE=%DOTNET_ROOT_LOCAL%\dotnet.exe"

:build
for /f "tokens=*" %%V in ('"%DOTNET_EXE%" --version 2^>nul') do set "SDK_VERSION=%%V"
echo Using .NET SDK: !SDK_VERSION!
"%DOTNET_EXE%" --list-sdks | findstr /r /c:"^8\." >nul
if errorlevel 1 goto :install_error

echo Restoring packages...
"%DOTNET_EXE%" restore S1Radar.csproj || goto :build_error

echo Publishing S1Radar for Windows x86...
if exist publish rmdir /s /q publish
"%DOTNET_EXE%" publish S1Radar.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o publish || goto :build_error

echo.
echo ======================================================
echo BUILD COMPLETE
echo %cd%\publish\S1Radar.exe
echo .NET SDK: !SDK_VERSION!
echo Target: win-x86
echo ======================================================
echo.
pause
exit /b 0

:find_dotnet
for %%D in (
  "%ProgramFiles(x86)%\dotnet\dotnet.exe"
  "%ProgramFiles%\dotnet\dotnet.exe"
  "%USERPROFILE%\.dotnet\dotnet.exe"
) do (
  if exist %%~D (
    "%%~D" --list-sdks 2>nul | findstr /r /c:"^8\." >nul
    if not errorlevel 1 (
      set "DOTNET_EXE=%%~D"
      exit /b 0
    )
  )
)
for /f "delims=" %%D in ('where dotnet 2^>nul') do (
  "%%D" --list-sdks 2>nul | findstr /r /c:"^8\." >nul
  if not errorlevel 1 (
    set "DOTNET_EXE=%%D"
    exit /b 0
  )
)
exit /b 0

:download_error
echo Failed to download Microsoft's .NET installer. Check your internet connection.
pause
exit /b 1

:install_error
echo Failed to install or locate a usable .NET 8 SDK.
pause
exit /b 1

:build_error
echo S1Radar build failed. Review the compiler output above.
pause
exit /b 1
