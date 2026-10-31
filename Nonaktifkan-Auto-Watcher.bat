@echo off
setlocal enabledelayedexpansion
title [Durango] Nonaktifkan Auto-Watcher Mod
chcp 65001 >nul

echo ========================================================
echo       Durango Mod - Matikan Background Auto-Watcher
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "PID_FILE=!SCRIPT_DIR!durango_watcher.pid"
set "STARTUP_BAT=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Durango_Mod_Watcher.bat"

if exist "!PID_FILE!" (
    set /p OLD_PID=<"!PID_FILE!"
    echo Menghentikan proses Auto-Watcher [PID: !OLD_PID!]...
    taskkill /F /PID !OLD_PID! /FI "IMAGENAME eq pythonw.exe" >nul 2>&1
    del /f /q "!PID_FILE!" >nul 2>&1
    echo [OK] Auto-Watcher berhasil dihentikan.
) else (
    echo [INFO] Auto-Watcher saat ini tidak sedang berjalan.
)

if exist "!STARTUP_BAT!" (
    del /f /q "!STARTUP_BAT!" >nul 2>&1
    echo [OK] Startup otomatis Windows berhasil dihapus.
)

echo.
echo Auto-Watcher telah sepenuhnya dimatikan.
echo.
pause
exit /b 0
