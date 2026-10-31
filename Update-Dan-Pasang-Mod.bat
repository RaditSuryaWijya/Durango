@echo off
setlocal enabledelayedexpansion
title [Durango] Auto-Update ^& Pasang Mod Client
chcp 65001 >nul

echo ========================================================
echo       Durango AutoGather ^& Combat Mod - Auto Patcher
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "GAME_DIR="

:: 1. Deteksi lokasi instalasi game secara portabel
if exist "%SCRIPT_DIR%XDLands.exe" (
    set "GAME_DIR=%SCRIPT_DIR%"
) else if exist "%SCRIPT_DIR%..\game\XDLands.exe" (
    set "GAME_DIR=%SCRIPT_DIR%..\game\"
) else if exist "%LOCALAPPDATA%\XDLands\PC\game\XDLands.exe" (
    set "GAME_DIR=%LOCALAPPDATA%\XDLands\PC\game\"
)

if "%GAME_DIR%"=="" (
    echo [PERHATIAN] Folder instalasi default tidak terdeteksi otomatis.
    set /p "GAME_DIR=Masukkan path folder game XDLands (misal C:\Games\XDLands\PC\game): "
    if not "!GAME_DIR:~-1!"=="\" set "GAME_DIR=!GAME_DIR!\"
)

set "MANAGED_DIR=!GAME_DIR!XDLands_Data\Managed\"
set "LAUNCHER_EXE=C:\Program Files (x86)\XD Lands Launcher\XDLandsLauncher.exe"

:: 2. Cek apakah XDLands.exe sedang berjalan
tasklist /FI "IMAGENAME eq XDLands.exe" 2>NUL | find /I /N "XDLands.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [PERINGATAN] XDLands.exe sedang berjalan!
    echo Menutup game agar file tidak terkunci...
    taskkill /F /IM XDLands.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

:: 3. Tarik update mod terbaru dari GitHub jika ada repo git
echo [1/3] Memeriksa update mod terbaru dari GitHub...
cd /d "%SCRIPT_DIR%"
where git >nul 2>&1
if "%ERRORLEVEL%"=="0" (
    if exist "%SCRIPT_DIR%.git" (
        git pull origin main --quiet >nul 2>&1
        if "%ERRORLEVEL%"=="0" (
            echo [OK] Repositori mod up-to-date dengan GitHub.
        ) else (
            echo [INFO] Melewati sync Git (offline atau branch lokal).
        )
    ) else (
        echo [INFO] Folder lokal siap digunakan.
    )
) else (
    echo [INFO] Git CLI tidak ditemukan, menggunakan file lokal.
)

:: 4. Pasang mod ke client game
echo.
echo [2/3] Memasang patch mod ke client game...
if not exist "!MANAGED_DIR!" (
    echo [ERROR] Folder Managed game tidak ditemukan di:
    echo         !MANAGED_DIR!
    pause
    exit /b 1
)

:: Backup Assembly-CSharp.dll original jika belum ada
if not exist "!MANAGED_DIR!Assembly-CSharp.dll.original" (
    if exist "!MANAGED_DIR!Assembly-CSharp.dll" (
        copy /y "!MANAGED_DIR!Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll.original" >nul
        echo [BACKUP] Berhasil membuat backup Assembly-CSharp.dll.original.
    )
)

set "PATCH_SUCCESS=0"

:: Metode A: Pasang pre-patched binary dari folder Release/ (0 dependency, instan)
if exist "%SCRIPT_DIR%Release\Assembly-CSharp.dll" (
    if exist "%SCRIPT_DIR%Release\AutoGatherMod.dll" (
        copy /y "%SCRIPT_DIR%Release\Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll" >nul
        copy /y "%SCRIPT_DIR%Release\AutoGatherMod.dll" "!MANAGED_DIR!AutoGatherMod.dll" >nul
        if exist "!MANAGED_DIR!XDLands.AutoCounter.dll" del /f /q "!MANAGED_DIR!XDLands.AutoCounter.dll" >nul 2>&1
        set "PATCH_SUCCESS=1"
        echo [OK] Mod berhasil dipasang dari paket Release!
    )
)

:: Metode B: Jika paket Release tidak ada, jalankan patcher python
if "!PATCH_SUCCESS!"=="0" (
    where python >nul 2>&1
    if "%ERRORLEVEL%"=="0" (
        if exist "%SCRIPT_DIR%patch_mod.py" (
            echo Menjalankan patcher python...
            python "%SCRIPT_DIR%patch_mod.py" "!GAME_DIR!"
            if "%ERRORLEVEL%"=="0" (
                set "PATCH_SUCCESS=1"
            )
        )
    )
)

:: Salin AutoGatherSettings.ini default jika belum ada di folder game
if not exist "!GAME_DIR!AutoGatherSettings.ini" (
    if exist "%SCRIPT_DIR%Release\AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%Release\AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
        echo [CONFIG] AutoGatherSettings.ini disalin.
    ) else if exist "%SCRIPT_DIR%AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
        echo [CONFIG] AutoGatherSettings.ini disalin.
    )
)

if "!PATCH_SUCCESS!"=="0" (
    echo.
    echo [ERROR] Gagal melakukan patching mod!
    echo Pastikan folder Release/ tersedia atau Python terpasang di sistem.
    pause
    exit /b 1
)

:: 5. Verifikasi file hasil patch
echo.
echo [3/3] Memverifikasi instalasi mod...
if exist "!MANAGED_DIR!Assembly-CSharp.dll" (
    if exist "!MANAGED_DIR!AutoGatherMod.dll" (
        echo [OK] Assembly-CSharp.dll dan AutoGatherMod.dll siap digunakan!
    )
)

echo.
echo ========================================================
echo   [SUKSES] MOD TELAH SIAP UNTUK CLIENT TERBARU!
echo ========================================================
echo.
echo Pilihan:
echo  [1] Langsung Buka Launcher XDLands
echo  [2] Keluar
echo.
set /p "CHOICE=Masukkan pilihan [1/2] (Default: 1): "
if "!CHOICE!"=="" set "CHOICE=1"
if "!CHOICE!"=="1" (
    if exist "!LAUNCHER_EXE!" (
        echo Membuka Launcher XDLands...
        start "" "!LAUNCHER_EXE!"
    ) else if exist "!GAME_DIR!XDLands.exe" (
        echo Membuka Game XDLands.exe...
        start "" "!GAME_DIR!XDLands.exe"
    )
)
echo Selesai.
ping 127.0.0.1 -n 3 >nul
exit /b 0
