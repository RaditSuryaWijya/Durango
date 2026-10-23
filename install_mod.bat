@echo off
setlocal enabledelayedexpansion
title Durango AutoGather & Combat Mod Installer

echo ========================================================
echo       Durango AutoGather & Combat Suite - Installer
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "GAME_DIR="

:: 1. Deteksi lokasi instalasi XDLands
if exist "%SCRIPT_DIR%XDLands.exe" (
    set "GAME_DIR=%SCRIPT_DIR%"
) else if exist "%LOCALAPPDATA%\XDLands\PC\game\XDLands.exe" (
    set "GAME_DIR=%LOCALAPPDATA%\XDLands\PC\game\"
)

if "%GAME_DIR%"=="" (
    echo [PERHATIAN] Folder instalasi default tidak terdeteksi otomatis.
    echo Masukkan path folder instalasi XDLands PC Anda:
    echo (Contoh: C:\Games\XDLands\PC\game)
    set /p "GAME_DIR=Path folder game: "
    if not exist "!GAME_DIR!\XDLands.exe" (
        echo [ERROR] XDLands.exe tidak ditemukan di folder tersebut!
        echo.
        pause
        exit /b 1
    )
    if not "!GAME_DIR:~-1!"=="\" set "GAME_DIR=!GAME_DIR!\"
)

echo [OK] Folder game terdeteksi di:
echo      !GAME_DIR!
echo.

:: 2. Cek apakah game sedang berjalan
tasklist /FI "IMAGENAME eq XDLands.exe" 2>NUL | find /I /N "XDLands.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [PERINGATAN] XDLands.exe sedang berjalan!
    echo Menutup game agar file tidak terkunci...
    taskkill /F /IM XDLands.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

set "MANAGED_DIR=!GAME_DIR!XDLands_Data\Managed\"
if not exist "!MANAGED_DIR!" (
    echo [ERROR] Folder Managed game tidak ditemukan di:
    echo         !MANAGED_DIR!
    pause
    exit /b 1
)

:: 3. Backup file original Assembly-CSharp.dll
if not exist "!MANAGED_DIR!Assembly-CSharp.dll.original" (
    if exist "!MANAGED_DIR!Assembly-CSharp.dll" (
        copy /y "!MANAGED_DIR!Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll.original" >nul
        echo [BACKUP] Berhasil membuat backup:
        echo          Assembly-CSharp.dll.original
    )
) else (
    echo [INFO] Backup Assembly-CSharp.dll.original sudah tersedia.
)

:: 4. Pasang file mod pre-patched dari folder Release (Metode Instan, 0 Dependency)
set "INSTALLED=0"
if exist "%SCRIPT_DIR%Release\Assembly-CSharp.dll" (
    copy /y "%SCRIPT_DIR%Release\Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll" >nul
    copy /y "%SCRIPT_DIR%Release\AutoGatherMod.dll" "!MANAGED_DIR!AutoGatherMod.dll" >nul
    set "INSTALLED=1"
    echo [PASANG] File Assembly-CSharp.dll dan AutoGatherMod.dll berhasil dipasang!
) else if exist "%SCRIPT_DIR%AutoGatherMod.dll" (
    copy /y "%SCRIPT_DIR%AutoGatherMod.dll" "!MANAGED_DIR!AutoGatherMod.dll" >nul
    echo [PASANG] AutoGatherMod.dll disalin.
    :: Jalankan patcher python jika ada
    where python >nul 2>&1
    if !errorlevel! equ 0 (
        echo Menjalankan patcher python...
        python "%SCRIPT_DIR%patch_mod.py" "!GAME_DIR!"
        set "INSTALLED=1"
    )
)

if "!INSTALLED!"=="0" (
    echo [ERROR] File mod tidak ditemukan di folder Release/ maupun root!
    pause
    exit /b 1
)

:: 5. Salin AutoGatherSettings.ini jika belum ada
if not exist "!GAME_DIR!AutoGatherSettings.ini" (
    if exist "%SCRIPT_DIR%AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
        echo [CONFIG] AutoGatherSettings.ini default disalin.
    ) else if exist "%SCRIPT_DIR%Release\AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%Release\AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
        echo [CONFIG] AutoGatherSettings.ini default disalin.
    )
)

echo.
echo ========================================================
echo      [SUKSES] MOD BERHASIL DIPASANG DI PERANGKAT INI!
echo ========================================================
echo.
echo PANDUAN PENGGUNAAN:
echo 1. Jalankan game XDLands.exe.
echo 2. Masuk ke dalam dunia/pulau.
echo 3. Tekan [Insert] atau [F11] atau [Ctrl + 3 + 1] untuk
echo    membuka / menutup Menu Mod!
echo 4. Tekan [F1] untuk aktifkan Auto-Panen Cerdas.
echo 5. Buka Tab 'Tempur' di menu mod untuk mengatur:
echo    - Dual Speed Multiplier (Kecepatan Lari Player & Dino)
echo    - Dino Noclip (Tembus badan dinosaurus)
echo    - Moving Attack (Menyerang bebas sambil jalan)
echo    - Anti-Stagger (Kebal guncangan dinosaurus)
echo    - Turbo ASPD / Recovery Cut
echo 6. Untuk mencopot mod kapan saja, jalankan: uninstall_mod.bat
echo ========================================================
echo.
pause
