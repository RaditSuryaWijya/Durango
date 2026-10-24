@echo off
setlocal enabledelayedexpansion
title "Build Durango Mod Suite (AutoGather and AutoCounter)"

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

echo [1/3] Mengompilasi AutoCounter.cs (.NET 3.5)...
if exist "%SCRIPT_DIR%AutoCounter.cs" (
    set "COUNTER_SRC=%SCRIPT_DIR%AutoCounter.cs"
) else (
    set "COUNTER_SRC=%SCRIPT_DIR%scratch\AutoCounter.cs"
)
if not exist "!MANAGED!\Assembly-CSharp.dll.original" (
    copy /y "!MANAGED!\Assembly-CSharp.dll" "!MANAGED!\Assembly-CSharp.dll.original" >nul
)
C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe /target:library /out:"!MANAGED!\XDLands.AutoCounter.dll" /r:"!MANAGED!\UnityEngine.dll" /r:"!MANAGED!\UnityEngine.CoreModule.dll" /r:"!MANAGED!\Assembly-CSharp.dll.original" "!COUNTER_SRC!"
if %errorlevel% neq 0 (
    echo [ERROR] Kompilasi AutoCounter.cs gagal!
    pause
    exit /b 1
)
copy /y "!MANAGED!\XDLands.AutoCounter.dll" "%SCRIPT_DIR%XDLands.AutoCounter.dll" >nul 2>&1
echo [OK] XDLands.AutoCounter.dll berhasil dikompilasi!

echo.
echo [2/3] Menjalankan patcher python...
where python >nul 2>&1
if %errorlevel% equ 0 (
    python "%SCRIPT_DIR%patch_mod.py" "!GAME_DIR!"
) else (
    echo [ERROR] Python tidak ditemukan! Patcher memerlukan python.
    pause
    exit /b 1
)

echo.
echo [3/3] Mengompilasi AutoGatherMod.cs (.NET 3.5)...
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
