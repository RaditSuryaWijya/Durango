@echo off
setlocal enabledelayedexpansion
title [Durango] Auto-Update ^& Pasang Mod Client
chcp 65001 >nul

echo ========================================================
echo       Durango AutoGather ^& Combat Mod - Auto Patcher
echo ========================================================
echo.

set "SCRIPT_DIR=c:\Users\Radit\AppData\Local\XDLands\PC\durango_git\"
set "GAME_DIR=c:\Users\Radit\AppData\Local\XDLands\PC\game\"
set "MANAGED_DIR=!GAME_DIR!XDLands_Data\Managed\"
set "LAUNCHER_EXE=C:\Program Files (x86)\XD Lands Launcher\XDLandsLauncher.exe"

:: 1. Cek apakah XDLands.exe sedang berjalan
tasklist /FI "IMAGENAME eq XDLands.exe" 2>NUL | find /I /N "XDLands.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [PERINGATAN] XDLands.exe sedang berjalan!
    echo Menutup game agar file tidak terkunci...
    taskkill /F /IM XDLands.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

:: 2. Tarik update mod terbaru dari GitHub (jika ada koneksi internet)
echo [1/3] Memeriksa update mod terbaru dari GitHub...
cd /d "!SCRIPT_DIR!"
where git >nul 2>&1
if "%ERRORLEVEL%"=="0" (
    git pull origin main --quiet >nul 2>&1
    if "%ERRORLEVEL%"=="0" (
        echo [OK] Repositori mod up-to-date dengan GitHub.
    ) else (
        echo [INFO] Melewati sync Git [offline atau ada branch lokal].
    )
) else (
    echo [INFO] Git CLI tidak ditemukan, menggunakan file lokal.
)

:: 3. Jalankan patcher python
echo.
echo [2/3] Memasang patch mod ke client game...
where python >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
    echo [ERROR] Python tidak terdeteksi di PATH!
    pause
    exit /b 1
)

python "!SCRIPT_DIR!patch_mod.py" "!GAME_DIR!"
if not "%ERRORLEVEL%"=="0" (
    echo.
    echo [ERROR] Gagal melakukan patching! Silakan cek log di atas.
    pause
    exit /b 1
)

:: 4. Verifikasi file hasil patch
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
    ) else (
        echo Membuka Game XDLands.exe...
        start "" "!GAME_DIR!XDLands.exe"
    )
)
echo Selesai.
ping 127.0.0.1 -n 3 >nul
exit /b 0
