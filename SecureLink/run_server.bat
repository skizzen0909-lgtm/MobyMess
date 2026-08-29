@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================
echo    SecureLink Messenger Server Launcher
echo ============================================
echo.

REM Проверка наличия dotnet
echo [1/3] Проверка установленного .NET SDK...
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo ❌ ОШИБКА: .NET SDK не найден!
    echo.
    echo Пожалуйста, установите .NET 8 SDK:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo ✅ Обнаружена версия .NET: %DOTNET_VERSION%
echo.

REM Переход в директорию с решением
cd /d "%~dp0SecureLink\Server" || (
    echo ❌ ОШИБКА: Не удалось перейти в директорию SecureLink\Server
    pause
    exit /b 1
)

echo [2/3] Сборка проекта...
dotnet build SecureLink.Server.sln --configuration Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo ❌ ОШИБКА: Сборка проекта не удалась!
    echo Проверьте логи ошибок выше.
    pause
    exit /b 1
)
echo ✅ Сборка завершена успешно.
echo.

echo [3/3] Запуск сервера...
echo ============================================
echo Сервер запущен! Нажмите Ctrl+C для остановки.
echo ============================================
echo.

dotnet run --project SecureLink.Server.Wpf/SecureLink.Server.Wpf.csproj --configuration Release

pause
