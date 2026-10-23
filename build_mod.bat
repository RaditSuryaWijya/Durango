@echo off
setlocal enabledelayedexpansion
title Build AutoGatherMod.dll (.NET 3.5)

echo ========================================================
echo       Kompilasi AutoGatherMod.cs (.NET 3.5 csc)
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

echo Mengompilasi AutoGatherMod.cs...
C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe /target:library /out:"%SCRIPT_DIR%AutoGatherMod.dll" /r:"!MANAGED!\UnityEngine.CoreModule.dll" /r:"!MANAGED!\UnityEngine.IMGUIModule.dll" /r:"!MANAGED!\UnityEngine.TextRenderingModule.dll" /r:"!MANAGED!\Assembly-CSharp.dll" "%SCRIPT_DIR%AutoGatherMod.cs"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Kompilasi C# gagal!
    pause
    exit /b 1
)

echo.
echo [OK] Kompilasi berhasil!
copy /y "%SCRIPT_DIR%AutoGatherMod.dll" "%SCRIPT_DIR%Release\AutoGatherMod.dll" >nul 2>&1
echo [OK] AutoGatherMod.dll disalin ke Release/
echo.
pause
