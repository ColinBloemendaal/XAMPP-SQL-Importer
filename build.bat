@echo off
echo ============================================
echo   XAMPP SQL Importer - Build Script
echo ============================================
echo.

REM ── Step 1: Check for dotnet in PATH ─────────────────────────────────────
where dotnet >nul 2>&1
if %errorlevel% neq 0 goto :not_found

REM dotnet exists — check if .NET 8 SDK is available
dotnet --list-sdks 2>nul | findstr /B "8." >nul
if %errorlevel% equ 0 goto :build

:not_found
echo [INFO] .NET 8 SDK not found on this machine.
echo [INFO] Downloading installer via PowerShell...
echo.

powershell -ExecutionPolicy Bypass -Command ^
  "Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '%TEMP%\dotnet-install.ps1'"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Could not download the installer. Check your internet connection.
    echo         Install manually from: https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo [INFO] Running .NET 8 SDK installer ^(this may take a minute^)...
powershell -ExecutionPolicy Bypass -File "%TEMP%\dotnet-install.ps1" ^
  -Channel 8.0 ^
  -InstallDir "%LOCALAPPDATA%\Microsoft\dotnet"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Installation failed.
    echo         Install manually from: https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

REM Add install dir to PATH for this session only
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

echo.
echo [OK]   .NET 8 SDK installed successfully.
echo.

:build
echo [1/2] Restoring packages...
dotnet restore SqlImporter.csproj
if %errorlevel% neq 0 (
    echo [ERROR] Restore failed.
    pause
    exit /b 1
)

echo.
echo [2/2] Publishing self-contained EXE...
dotnet publish SqlImporter.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o .\publish

echo.
if %errorlevel% equ 0 (
    echo ============================================
    echo   BUILD SUCCESSFUL
    echo   Your EXE is in: .\publish\SqlImporter.exe
    echo ============================================
) else (
    echo [ERROR] Build failed. See output above.
)
echo.
pause
