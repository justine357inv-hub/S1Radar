@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
set "DOTNET_ROOT_LOCAL=%cd%\.dotnet"

call :find_dotnet
if defined DOTNET_EXE goto :build

echo .NET 8 SDK was not found on PATH. Trying private S1Radar SDK...
if exist "%DOTNET_ROOT_LOCAL%\dotnet.exe" (
  set "DOTNET_EXE=%DOTNET_ROOT_LOCAL%\dotnet.exe"
  goto :build
)

echo Downloading Microsoft's .NET 8 SDK installer...
set "INSTALL_SCRIPT=%TEMP%\s1radar-dotnet-install.ps1"
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '%INSTALL_SCRIPT%'" || goto :download_error
powershell -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_SCRIPT%" -Channel 8.0 -Architecture x64 -InstallDir "%DOTNET_ROOT_LOCAL%" || goto :install_error
if not exist "%DOTNET_ROOT_LOCAL%\dotnet.exe" goto :install_error
set "DOTNET_EXE=%DOTNET_ROOT_LOCAL%\dotnet.exe"

:build
for /f "tokens=*" %%V in ('"%DOTNET_EXE%" --version 2^>nul') do set "SDK_VERSION=%%V"
echo Using .NET SDK: !SDK_VERSION!
"%DOTNET_EXE%" --list-sdks | findstr /r /c:"^8\." >nul
if errorlevel 1 (
  echo No .NET 8 SDK is available to the selected dotnet host.
  goto :install_error
)

echo Restoring packages...
"%DOTNET_EXE%" restore || goto :build_error

echo Publishing S1Radar for Windows x64...
if exist publish rmdir /s /q publish
"%DOTNET_EXE%" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o publish || goto :build_error

echo.
echo ======================================================
echo BUILD COMPLETE
echo %cd%\publish\S1Radar.exe
echo .NET SDK: !SDK_VERSION!
echo ======================================================
echo.
pause
exit /b 0

:find_dotnet
set "DOTNET_EXE="
where dotnet >nul 2>nul
if errorlevel 1 exit /b 0
for /f "delims=" %%D in ('where dotnet') do (
  set "CANDIDATE=%%D"
  for /f "tokens=*" %%S in ('"!CANDIDATE!" --list-sdks 2^>nul ^| findstr /r /c:"^8\."') do (
    set "DOTNET_EXE=!CANDIDATE!"
    exit /b 0
  )
)
exit /b 0

:download_error
echo.
echo Failed to download the official .NET installer.
echo Check your internet connection and rerun this builder.
pause
exit /b 1

:install_error
echo.
echo Failed to install or locate a usable .NET 8 SDK.
echo Existing .NET 8 installation may be damaged or inaccessible.
pause
exit /b 1

:build_error
echo.
echo S1Radar build failed. The full compiler output above contains the error.
pause
exit /b 1
