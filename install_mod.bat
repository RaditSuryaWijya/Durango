@echo off
setlocal enabledelayedexpansion
title Durango AutoGather Mod Installer

echo ========================================================
echo       Durango AutoGather Mod - Auto Installer
echo ========================================================
echo.

set "TARGET_DIR=%~dp0"

:: Cek apakah dijalankan langsung di dalam folder game
if not exist "%TARGET_DIR%XDLands.exe" (
    if exist "%LOCALAPPDATA%\XDLands\PC\game\XDLands.exe" (
        set "TARGET_DIR=%LOCALAPPDATA%\XDLands\PC\game\"
        echo Terdeteksi instalasi game di: !TARGET_DIR!
    ) else (
        echo [ERROR] Game XDLands tidak ditemukan di folder lokal!
        echo Silakan letakkan file installer ini langsung di dalam folder game:
        echo (Contoh: %%LOCALAPPDATA%%\XDLands\PC\game\)
        echo.
        pause
        exit /b 1
    )
)

echo Menyalin file mod ke direktori game...
if not "%~dp0"=="%TARGET_DIR%" (
    copy /y "%~dp0AutoGatherMod.dll" "%TARGET_DIR%" >nul 2>&1
    copy /y "%~dp0AutoGatherMod.cs" "%TARGET_DIR%" >nul 2>&1
    copy /y "%~dp0build_mod.bat" "%TARGET_DIR%" >nul 2>&1
    copy /y "%~dp0patch_xdlandsmark.py" "%TARGET_DIR%" >nul 2>&1
    copy /y "%~dp0doorstop_config.ini" "%TARGET_DIR%" >nul 2>&1
    copy /y "%~dp0winhttp.dll" "%TARGET_DIR%" >nul 2>&1
    copy /y "%~dp0Mono.Cecil*.dll" "%TARGET_DIR%" >nul 2>&1
)

:: Pastikan folder XDLandsMark ada dan salin AutoGatherMod.dll ke dalamnya
if not exist "%TARGET_DIR%XDLandsMark" mkdir "%TARGET_DIR%XDLandsMark"
copy /y "%~dp0AutoGatherMod.dll" "%TARGET_DIR%XDLandsMark\" >nul 2>&1

:: Menjalankan patcher XDLandsMark & Craft Limit
pushd "%TARGET_DIR%"
where python >nul 2>&1
if %errorlevel% equ 0 (
    echo Menjalankan patcher injeksi XDLandsMark...
    python patch_xdlandsmark.py
) else (
    echo [INFO] Python tidak terdeteksi. Melewati injeksi otomatis.
    echo Pastikan XDLandsMark.dll sudah ter-patch atau install Python untuk patching otomatis.
)
popd

echo.
echo ========================================================
echo   [SUKSES] Mod AutoGather berhasil dipasang!
echo   Silakan jalankan XDLands.exe untuk mulai bermain.
echo ========================================================
echo.
pause
