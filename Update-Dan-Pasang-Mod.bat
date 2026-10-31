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

:: Simpan path yang valid ke game_path.txt agar tidak perlu diketik ulang di masa depan
echo !GAME_DIR!> "%SCRIPT_DIR%game_path.txt"
echo [OK] Path game terkonfirmasi: !GAME_DIR!
echo.

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

:: 4. Pilih Metode Pemasangan Mod
echo.
echo [2/3] Pilih Metode Pemasangan Mod:
echo  [1] Pasang Paket Release (Direkomendasikan: Instan, 0 dependensi, tanpa Python)
echo  [2] Jalankan Patcher Python (Mono.Cecil)
echo.
set /p "METHOD_CHOICE=Pilihan metode [1/2] (Default: 1): "
if "!METHOD_CHOICE!"=="" set "METHOD_CHOICE=1"

:: Backup Assembly-CSharp.dll original jika belum ada
if not exist "!MANAGED_DIR!Assembly-CSharp.dll.original" (
    if exist "!MANAGED_DIR!Assembly-CSharp.dll" (
        copy /y "!MANAGED_DIR!Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll.original" >nul
        echo [BACKUP] Berhasil membuat backup: Assembly-CSharp.dll.original.
    )
)

set "PATCH_SUCCESS=0"

if "!METHOD_CHOICE!"=="1" (
    echo.
    echo Memasang file mod pre-patched dari folder Release...
    if exist "%SCRIPT_DIR%Release\Assembly-CSharp.dll" (
        if exist "%SCRIPT_DIR%Release\AutoGatherMod.dll" (
            copy /y "%SCRIPT_DIR%Release\Assembly-CSharp.dll" "!MANAGED_DIR!Assembly-CSharp.dll" >nul
            copy /y "%SCRIPT_DIR%Release\AutoGatherMod.dll" "!MANAGED_DIR!AutoGatherMod.dll" >nul
            if exist "!MANAGED_DIR!XDLands.AutoCounter.dll" del /f /q "!MANAGED_DIR!XDLands.AutoCounter.dll" >nul 2>&1
            set "PATCH_SUCCESS=1"
            echo [OK] Assembly-CSharp.dll dan AutoGatherMod.dll berhasil dipasang!
        ) else (
            echo [PERINGATAN] File AutoGatherMod.dll di folder Release tidak ditemukan.
        )
    ) else (
        echo [PERINGATAN] File Assembly-CSharp.dll di folder Release tidak ditemukan.
    )
)

if "!METHOD_CHOICE!"=="2" or "!PATCH_SUCCESS!"=="0" (
    if "!PATCH_SUCCESS!"=="0" (
        echo.
        echo Menjalankan Patcher Python...
        where python >nul 2>&1
        if "%ERRORLEVEL%"=="0" (
            if exist "%SCRIPT_DIR%patch_mod.py" (
                python "%SCRIPT_DIR%patch_mod.py" "!GAME_DIR!"
                if "%ERRORLEVEL%"=="0" (
                    set "PATCH_SUCCESS=1"
                )
            ) else (
                echo [ERROR] patch_mod.py tidak ditemukan di: %SCRIPT_DIR%
            )
        ) else (
            echo [ERROR] Python tidak terdeteksi di PATH sistem!
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
    echo ========================================================
    echo [ERROR] Gagal memasang mod!
    echo Anda juga bisa menyalin file secara manual:
    echo 1. Salin file dari: %SCRIPT_DIR%Release\Assembly-CSharp.dll
    echo    Ke: !MANAGED_DIR!Assembly-CSharp.dll
    echo 2. Salin file dari: %SCRIPT_DIR%Release\AutoGatherMod.dll
    echo    Ke: !MANAGED_DIR!AutoGatherMod.dll
    echo ========================================================
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
echo  [2] Buka Game Langsung (XDLands.exe)
echo  [3] Keluar
echo.
set /p "CHOICE=Masukkan pilihan [1/2/3] (Default: 1): "
if "!CHOICE!"=="" set "CHOICE=1"
if "!CHOICE!"=="1" (
    if exist "!LAUNCHER_EXE!" (
        echo Membuka Launcher XDLands...
        start "" "!LAUNCHER_EXE!"
    ) else if exist "!GAME_DIR!XDLands.exe" (
        echo Membuka Game XDLands.exe...
        start "" "!GAME_DIR!XDLands.exe"
    )
) else if "!CHOICE!"=="2" (
    if exist "!GAME_DIR!XDLands.exe" (
        echo Membuka Game XDLands.exe...
        start "" "!GAME_DIR!XDLands.exe"
    ) else (
        echo [PERINGATAN] XDLands.exe tidak ditemukan di !GAME_DIR!
    )
)
echo Selesai.
ping 127.0.0.1 -n 3 >nul
exit /b 0
