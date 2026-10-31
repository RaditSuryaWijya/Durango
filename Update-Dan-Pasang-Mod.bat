@echo off
setlocal enabledelayedexpansion
title [Durango] Pasang Mod Game Client
chcp 65001 >nul

echo ========================================================
echo       Durango AutoGather ^& Combat Mod - Installer
echo ========================================================
echo.

set "SCRIPT_DIR=%~dp0"
set "GAME_DIR="

:: 1. Cek apakah path pernah disimpan di game_path.txt
if exist "%SCRIPT_DIR%game_path.txt" (
    set /p SAVED_PATH=<"%SCRIPT_DIR%game_path.txt"
    if exist "!SAVED_PATH!\XDLands_Data\Managed\Assembly-CSharp.dll" (
        set "GAME_DIR=!SAVED_PATH!"
    ) else if exist "!SAVED_PATH!\Assembly-CSharp.dll" (
        set "GAME_DIR=!SAVED_PATH!"
    )
)

:: Jika belum ada, coba deteksi lokasi standar
if "!GAME_DIR!"=="" (
    if exist "%SCRIPT_DIR%XDLands.exe" (
        set "GAME_DIR=%SCRIPT_DIR%"
    ) else if exist "%SCRIPT_DIR%..\game\XDLands.exe" (
        set "GAME_DIR=%SCRIPT_DIR%..\game\"
    ) else if exist "%LOCALAPPDATA%\XDLands\PC\game\XDLands.exe" (
        set "GAME_DIR=%LOCALAPPDATA%\XDLands\PC\game\"
    )
)

:: Menu Penentuan Path
set "MANUAL_RETRY=0"
if not "!GAME_DIR!"=="" (
    echo Lokasi folder game XDLands terdeteksi:
    echo  [1] Gunakan lokasi ini: !GAME_DIR!
    echo  [2] Masukkan lokasi folder game secara MANUAL
    echo.
    set /p "PATH_CHOICE=Pilihan [1/2] (Default: 1): "
    if defined PATH_CHOICE set "PATH_CHOICE=!PATH_CHOICE: =!"
    if "!PATH_CHOICE!"=="" set "PATH_CHOICE=1"
    if "!PATH_CHOICE:~0,1!"=="2" goto :ask_manual_path
) else (
    goto :ask_manual_path
)
goto :validate_path

:ask_manual_path
set /a "MANUAL_RETRY+=1"
if !MANUAL_RETRY! gtr 5 (
    echo.
    echo [ERROR] Gagal mendapatkan path valid setelah beberapa percobaan.
    echo Tekan tombol apa saja untuk keluar...
    pause >nul
    exit /b 1
)

echo.
echo ========================================================
echo Masukkan lokasi folder game XDLands Anda:
echo (Ketik path atau Drag ^& Drop folder game / file XDLands.exe ke sini)
echo Contoh: D:\Games\XDLands\PC\game
echo ========================================================
set "USER_INPUT_PATH="
set /p "USER_INPUT_PATH=Path Game: "
if "!USER_INPUT_PATH!"=="" (
    if not "!GAME_DIR!"=="" (
        echo [INFO] Menggunakan path terdeteksi sebelumnya: !GAME_DIR!
        goto :validate_path
    )
    echo [ERROR] Path tidak boleh kosong!
    goto :ask_manual_path
)

:: Bersihkan tanda kutip ganda dan spasi di ujung
set "GAME_DIR=!USER_INPUT_PATH:"=!"
for /f "tokens=* delims= " %%a in ("!GAME_DIR!") do set "GAME_DIR=%%a"

:: Deteksi jika user men-drag file langsung
if /I "!GAME_DIR:~-19!"=="Assembly-CSharp.dll" (
    set "GAME_DIR=!GAME_DIR:~0,-19!"
    if /I "!GAME_DIR:~-20!"=="XDLands_Data\Managed" set "GAME_DIR=!GAME_DIR:~0,-20!"
    if /I "!GAME_DIR:~-21!"=="XDLands_Data\Managed\" set "GAME_DIR=!GAME_DIR:~0,-21!"
)
if /I "!GAME_DIR:~-11!"=="XDLands.exe" set "GAME_DIR=!GAME_DIR:~0,-11!"
if /I "!GAME_DIR:~-20!"=="XDLands_Data\Managed" set "GAME_DIR=!GAME_DIR:~0,-20!"
if /I "!GAME_DIR:~-21!"=="XDLands_Data\Managed\" set "GAME_DIR=!GAME_DIR:~0,-21!"
if not "!GAME_DIR:~-1!"=="\" set "GAME_DIR=!GAME_DIR!\"

:validate_path
set "MANAGED_DIR=!GAME_DIR!XDLands_Data\Managed\"

:: Cek apakah file target Assembly-CSharp.dll ada di Managed
if not exist "!MANAGED_DIR!Assembly-CSharp.dll" (
    :: Cek apakah user langsung menunjuk ke folder Managed
    if exist "!GAME_DIR!Assembly-CSharp.dll" (
        set "MANAGED_DIR=!GAME_DIR!"
        goto :path_ok
    )
    echo.
    echo ========================================================
    echo [ERROR] File 'Assembly-CSharp.dll' TIDAK DITEMUKAN di:
    echo         !MANAGED_DIR!
    echo Pastikan folder yang Anda pilih adalah folder instalasi game
    echo yang berisi file 'XDLands.exe' dan subfolder 'XDLands_Data'!
    echo ========================================================
    echo.
    set "GAME_DIR="
    goto :ask_manual_path
)

:path_ok
:: Simpan path yang valid ke game_path.txt
echo !GAME_DIR!> "%SCRIPT_DIR%game_path.txt"
echo.
echo [OK] Folder game terkonfirmasi:
echo      !GAME_DIR!
echo.

:: 2. Cek apakah game XDLands.exe sedang berjalan
tasklist /FI "IMAGENAME eq XDLands.exe" 2>NUL | find /I /N "XDLands.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [PERINGATAN] XDLands.exe sedang berjalan!
    echo Menutup game agar file tidak terkunci...
    taskkill /F /IM XDLands.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

:: 3. Backup file original Assembly-CSharp.dll jika belum ada
if not exist "!MANAGED_DIR!Assembly-CSharp.dll.original" (
    if exist "!MANAGED_DIR!Assembly-CSharp.dll" (
        copy /y "!MANAGED_DIR!Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll.original" >nul
        echo [BACKUP] Berhasil membuat backup: Assembly-CSharp.dll.original
    )
) else (
    echo [INFO] Backup Assembly-CSharp.dll.original sudah ada.
)

:: 4. Pasang mod dari folder Release
echo.
echo ========================================================
echo Memasang file mod ke folder game...
echo ========================================================
set "COPY_FAIL=0"

if not exist "%SCRIPT_DIR%Release\Assembly-CSharp.dll" (
    echo [ERROR] File '%SCRIPT_DIR%Release\Assembly-CSharp.dll' tidak ditemukan!
    set "COPY_FAIL=1"
) else (
    echo [1/3] Menyalin Assembly-CSharp.dll [pre-patched]...
    copy /y "%SCRIPT_DIR%Release\Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll"
    if errorlevel 1 (
        echo [ERROR] Gagal menyalin Assembly-CSharp.dll! Pastikan game ditutup dan jalankan script sebagai Administrator.
        set "COPY_FAIL=1"
    )
)

if not exist "%SCRIPT_DIR%Release\AutoGatherMod.dll" (
    echo [ERROR] File '%SCRIPT_DIR%Release\AutoGatherMod.dll' tidak ditemukan!
    set "COPY_FAIL=1"
) else (
    echo [2/3] Menyalin AutoGatherMod.dll...
    copy /y "%SCRIPT_DIR%Release\AutoGatherMod.dll" "!MANAGED_DIR!AutoGatherMod.dll"
    if errorlevel 1 (
        echo [ERROR] Gagal menyalin AutoGatherMod.dll!
        set "COPY_FAIL=1"
    )
)

:: Salin konfigurasi default jika belum ada
if not exist "!GAME_DIR!AutoGatherSettings.ini" (
    echo [3/3] Menyalin AutoGatherSettings.ini ke folder game...
    if exist "%SCRIPT_DIR%Release\AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%Release\AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
    ) else if exist "%SCRIPT_DIR%AutoGatherSettings.ini" (
        copy /y "%SCRIPT_DIR%AutoGatherSettings.ini" "!GAME_DIR!AutoGatherSettings.ini" >nul
    )
) else (
    echo [3/3] AutoGatherSettings.ini sudah ada di folder game [tidak ditimpa].
)

:: Hapus file mod lama jika ada
if exist "!MANAGED_DIR!XDLands.AutoCounter.dll" del /f /q "!MANAGED_DIR!XDLands.AutoCounter.dll" >nul 2>&1

echo.
if "!COPY_FAIL!"=="0" (
    echo ========================================================
    echo      [SUKSES] MOD TELAH BERHASIL DIPASANG!
    echo ========================================================
    echo Lokasi terpasang:
    echo  - !MANAGED_DIR!Assembly-CSharp.dll
    echo  - !MANAGED_DIR!AutoGatherMod.dll
    echo  - !GAME_DIR!AutoGatherSettings.ini
    echo.
    echo PANDUAN:
    echo 1. Jalankan XDLands.exe secara langsung.
    echo 2. Masuk ke dalam dunia/pulau.
    echo 3. Tekan [Insert] atau [Ctrl+3+1] untuk membuka menu mod!
    echo 4. Tekan [F1] untuk aktifkan Auto-Panen.
    echo ========================================================
) else (
    echo ========================================================
    echo   [GAGAL] Pemasangan mod gagal disalin.
    echo   Silakan periksa pesan error di atas.
    echo ========================================================
)

echo.
echo Pilihan:
echo  [1] Buka Game Langsung (XDLands.exe)
echo  [2] Buka Launcher Game (XDLandsLauncher.exe)
echo  [3] Keluar
echo.
set /p "FINAL_CHOICE=Pilihan [1/2/3] (Default: 3): "
if "!FINAL_CHOICE!"=="" set "FINAL_CHOICE=3"

if "!FINAL_CHOICE!"=="1" (
    if exist "!GAME_DIR!XDLands.exe" (
        echo Membuka !GAME_DIR!XDLands.exe...
        start "" "!GAME_DIR!XDLands.exe"
    ) else (
        echo XDLands.exe tidak ditemukan di !GAME_DIR!
    )
) else if "!FINAL_CHOICE!"=="2" (
    set "LAUNCHER_EXE=C:\Program Files (x86)\XD Lands Launcher\XDLandsLauncher.exe"
    if exist "!LAUNCHER_EXE!" (
        echo Membuka Launcher...
        start "" "!LAUNCHER_EXE!"
    ) else (
        echo Launcher tidak ditemukan di lokasi standar.
    )
)

echo.
echo Tekan tombol apa saja untuk menutup jendela ini...
pause >nul
exit /b 0
