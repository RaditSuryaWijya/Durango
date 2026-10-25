@echo off
setlocal enabledelayedexpansion
title "Build Durango AutoGather Mod"

echo ========================================================
echo       Kompilasi Durango Mod Suite (.NET csc)
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "GAME_DIR="

if exist "%SCRIPT_DIR%XDLands.exe" (
    set "GAME_DIR=%SCRIPT_DIR%"
) else if exist "%LOCALAPPDATA%\XDLands\PC\game\XDLands.exe" (
    set "GAME_DIR=%LOCALAPPDATA%\XDLands\PC\game\"
)

if "%GAME_DIR%"=="" (
    echo [ERROR] Folder game XDLands tidak ditemukan.
    pause
    exit /b 1
)

set "MANAGED=!GAME_DIR!XDLands_Data\Managed"

if not exist "!MANAGED!\Assembly-CSharp.dll.original" (
    copy /y "!MANAGED!\Assembly-CSharp.dll" "!MANAGED!\Assembly-CSharp.dll.original" >nul
)

echo [1/2] Menjalankan patcher python...
where python >nul 2>&1
if %errorlevel% equ 0 (
    python "%SCRIPT_DIR%patch_mod.py" "!GAME_DIR!"
) else (
    echo [ERROR] Python tidak ditemukan! Patcher memerlukan python.
    pause
    exit /b 1
)

echo.
echo [2/2] Mengompilasi AutoGatherMod.cs (.NET 3.5)...
C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe /target:library /out:"%SCRIPT_DIR%AutoGatherMod.dll" /r:"!MANAGED!\UnityEngine.CoreModule.dll" /r:"!MANAGED!\UnityEngine.AnimationModule.dll" /r:"!MANAGED!\UnityEngine.IMGUIModule.dll" /r:"!MANAGED!\UnityEngine.TextRenderingModule.dll" /r:"!MANAGED!\Assembly-CSharp.dll" "%SCRIPT_DIR%AutoGatherMod.cs"
if %errorlevel% neq 0 (
    echo [ERROR] Kompilasi AutoGatherMod.cs gagal!
    pause
    exit /b 1
)
copy /y "%SCRIPT_DIR%AutoGatherMod.dll" "!MANAGED!\AutoGatherMod.dll" >nul 2>&1
echo [OK] AutoGatherMod.dll berhasil dikompilasi!

echo.
echo ========================================================
echo       [SELESAI] Seluruh mod berhasil di-build!
echo ========================================================
pause
