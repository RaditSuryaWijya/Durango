@echo off
setlocal enabledelayedexpansion
title Durango AutoGather & Combat Mod Uninstaller

echo ========================================================
echo      Durango AutoGather & Combat Suite - Uninstaller
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "GAME_DIR="

:: Deteksi lokasi instalasi game
if exist "%SCRIPT_DIR%XDLands.exe" (
    set "GAME_DIR=%SCRIPT_DIR%"
) else if exist "%LOCALAPPDATA%\XDLands\PC\game\XDLands.exe" (
    set "GAME_DIR=%LOCALAPPDATA%\XDLands\PC\game\"
)

if "%GAME_DIR%"=="" (
    echo Masukkan path folder instalasi XDLands PC Anda:
    set /p "GAME_DIR=Path folder game: "
    if not exist "!GAME_DIR!\XDLands.exe" (
        echo [ERROR] XDLands.exe tidak ditemukan di folder tersebut!
        pause
        exit /b 1
    )
    if not "!GAME_DIR:~-1!"=="\" set "GAME_DIR=!GAME_DIR!\"
)

:: Cek apakah game sedang berjalan
tasklist /FI "IMAGENAME eq XDLands.exe" 2>NUL | find /I /N "XDLands.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [PERINGATAN] XDLands.exe sedang berjalan!
    echo Menutup game agar file tidak terkunci...
    taskkill /F /IM XDLands.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

set "MANAGED_DIR=!GAME_DIR!XDLands_Data\Managed\"

if exist "!MANAGED_DIR!Assembly-CSharp.dll.original" (
    copy /y "!MANAGED_DIR!Assembly-CSharp.dll.original" "!MANAGED_DIR!Assembly-CSharp.dll" >nul
    echo [OK] File original Assembly-CSharp.dll berhasil dipulihkan.
) else (
    echo [PERINGATAN] File backup Assembly-CSharp.dll.original tidak ditemukan!
)

if exist "!MANAGED_DIR!AutoGatherMod.dll" (
    del /f /q "!MANAGED_DIR!AutoGatherMod.dll" >nul 2>&1
    echo [OK] File AutoGatherMod.dll dihapus dari Managed.
)

echo.
echo ========================================================
echo   [SUKSES] Mod berhasil dicopot!
echo   Client game Anda sekarang kembali ke versi original.
echo ========================================================
echo.
pause
