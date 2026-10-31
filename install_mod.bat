@echo off
setlocal enabledelayedexpansion
title Durango AutoGather & Combat Mod Installer
chcp 65001 >nul

echo ========================================================
echo       Durango AutoGather & Combat Suite - Installer
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "GAME_DIR="

:: 1. Cek apakah path tersimpan sebelumnya di game_path.txt
if exist "%SCRIPT_DIR%game_path.txt" (
    set /p SAVED_PATH=<"%SCRIPT_DIR%game_path.txt"
    if exist "!SAVED_PATH!\XDLands_Data\Managed" (
        set "GAME_DIR=!SAVED_PATH!"
    )
)

:: Jika belum ada, coba deteksi otomatis di lokasi standar
if "!GAME_DIR!"=="" (
    if exist "%SCRIPT_DIR%XDLands.exe" (
        set "GAME_DIR=%SCRIPT_DIR%"
    ) else if exist "%SCRIPT_DIR%..\game\XDLands.exe" (
        set "GAME_DIR=%SCRIPT_DIR%..\game\"
    ) else if exist "%LOCALAPPDATA%\XDLands\PC\game\XDLands.exe" (
        set "GAME_DIR=%LOCALAPPDATA%\XDLands\PC\game\"
    )
)

:: Menu Penentuan Path Folder Game
if not "!GAME_DIR!"=="" (
    echo Lokasi folder instalasi game XDLands:
    echo  [1] Gunakan lokasi terdeteksi:
    echo      !GAME_DIR!
    echo  [2] Masukkan path folder game secara MANUAL
    echo.
    set /p "PATH_CHOICE=Pilihan [1/2] (Default: 1): "
    if "!PATH_CHOICE!"=="" set "PATH_CHOICE=1"
    if "!PATH_CHOICE!"=="2" goto :ask_manual_path
) else (
    goto :ask_manual_path
)
goto :validate_path

:ask_manual_path
echo.
echo ========================================================
echo Masukkan path folder instalasi XDLands PC Anda:
echo (Bisa ketik langsung atau Drag ^& Drop folder / file XDLands.exe ke sini)
echo Contoh: D:\Games\XDLands\PC\game
echo ========================================================
set /p "USER_INPUT_PATH=Path Game: "
if "!USER_INPUT_PATH!"=="" (
    echo [ERROR] Path tidak boleh kosong!
    goto :ask_manual_path
)

:: Bersihkan tanda kutip
set "GAME_DIR=!USER_INPUT_PATH:"=!"

:: Normalisasi jika user menginput file XDLands.exe atau subfolder Managed
if /I "!GAME_DIR:~-11!"=="XDLands.exe" set "GAME_DIR=!GAME_DIR:~0,-11!"
if /I "!GAME_DIR:~-20!"=="XDLands_Data\Managed" set "GAME_DIR=!GAME_DIR:~0,-20!"
if /I "!GAME_DIR:~-21!"=="XDLands_Data\Managed\" set "GAME_DIR=!GAME_DIR:~0,-21!"
if not "!GAME_DIR:~-1!"=="\" set "GAME_DIR=!GAME_DIR!\"

:validate_path
set "MANAGED_DIR=!GAME_DIR!XDLands_Data\Managed\"
if not exist "!MANAGED_DIR!" (
    echo.
    echo [ERROR] Folder Managed game tidak ditemukan di:
    echo         !MANAGED_DIR!
    echo Pastikan folder yang Anda pilih adalah folder tempat 'XDLands.exe' berada!
    echo.
    goto :ask_manual_path
)

:: Simpan path yang valid ke game_path.txt agar diingat
echo !GAME_DIR!> "%SCRIPT_DIR%game_path.txt"
echo [OK] Folder game terkonfirmasi:
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

:: 3. Backup file original Assembly-CSharp.dll
if not exist "!MANAGED_DIR!Assembly-CSharp.dll.original" (
    if exist "!MANAGED_DIR!Assembly-CSharp.dll" (
        copy /y "!MANAGED_DIR!Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll.original" >nul
        echo [BACKUP] Berhasil membuat backup: Assembly-CSharp.dll.original.
    )
) else (
    echo [INFO] Backup Assembly-CSharp.dll.original sudah tersedia.
)

:: 4. Pasang file mod pre-patched dari folder Release (Metode Instan, 0 Dependency)
set "INSTALLED=0"
if exist "%SCRIPT_DIR%Release\Assembly-CSharp.dll" (
    if exist "%SCRIPT_DIR%Release\AutoGatherMod.dll" (
        copy /y "%SCRIPT_DIR%Release\Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll" >nul
        copy /y "%SCRIPT_DIR%Release\AutoGatherMod.dll" "!MANAGED_DIR!AutoGatherMod.dll" >nul
        if exist "!MANAGED_DIR!XDLands.AutoCounter.dll" del /f /q "!MANAGED_DIR!XDLands.AutoCounter.dll" >nul 2>&1
        set "INSTALLED=1"
        echo [PASANG] File Assembly-CSharp.dll dan AutoGatherMod.dll berhasil dipasang dari folder Release!
    )
)

if "!INSTALLED!"=="0" (
    where python >nul 2>&1
    if !errorlevel! equ 0 (
        if exist "%SCRIPT_DIR%patch_mod.py" (
            echo Menjalankan patcher python...
            python "%SCRIPT_DIR%patch_mod.py" "!GAME_DIR!"
            set "INSTALLED=1"
        )
    )
)

if "!INSTALLED!"=="0" (
    echo [ERROR] File mod tidak ditemukan di folder Release/ maupun root!
    pause
    exit /b 1
)

:: 5. Salin AutoGatherSettings.ini jika belum ada
if not exist "!GAME_DIR!AutoGatherSettings.ini" (
    if exist "%SCRIPT_DIR%Release\AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%Release\AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
        echo [CONFIG] AutoGatherSettings.ini default disalin.
    ) else if exist "%SCRIPT_DIR%AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
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
