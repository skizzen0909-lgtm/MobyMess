@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================
echo    SecureLink Messenger Server Launcher
echo ============================================
echo.

REM Check for .NET SDK
echo [1/3] Checking for .NET SDK...
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET SDK not found!
    echo.
    echo Please install .NET 8 SDK:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo [INFO] .NET version: %DOTNET_VERSION%
echo.

REM Change to Server directory
cd /d "%~dp0Server" || (
    echo [ERROR] Failed to navigate to Server directory
    pause
    exit /b 1
)

echo [2/3] Building project...
dotnet build SecureLink.Server.sln --configuration Release --no-incremental
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed!
    echo Check error logs above.
    pause
    exit /b 1
)
echo [SUCCESS] Build completed.
echo.

echo [3/3] Starting server...
echo ============================================
echo Server is running! Press Ctrl+C to stop.
echo ============================================
echo.

dotnet run --project SecureLink.Server.Wpf/SecureLink.Server.Wpf.csproj --configuration Release

pause
