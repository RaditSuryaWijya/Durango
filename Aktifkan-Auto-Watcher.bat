@echo off
setlocal enabledelayedexpansion
title [Durango] Aktifkan Auto-Watcher Mod
chcp 65001 >nul

echo ========================================================
echo       Durango Mod - Background Auto-Watcher Activator
echo ========================================================
echo.

set "SCRIPT_DIR=c:\Users\Radit\AppData\Local\XDLands\PC\durango_git\"
set "WATCHER_PYW=!SCRIPT_DIR!durango_watcher.pyw"
set "PID_FILE=!SCRIPT_DIR!durango_watcher.pid"
set "STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"

where pythonw >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
    echo [ERROR] pythonw.exe tidak ditemukan di sistem!
    pause
    exit /b 1
)

:: Cek apakah watcher sudah jalan
if exist "!PID_FILE!" (
    set /p OLD_PID=<"!PID_FILE!"
    tasklist /FI "PID eq !OLD_PID!" /FI "IMAGENAME eq pythonw.exe" 2>NUL | findstr /C:"pythonw.exe" >nul
    if not errorlevel 1 (
        echo [INFO] Auto-Watcher sudah berjalan di background [PID: !OLD_PID!].
        goto :ask_startup
    )
)

:: Jalankan watcher menggunakan pythonw (tanpa jendela hitam)
echo Menjalankan Durango Auto-Watcher di background...
start "" pythonw "!WATCHER_PYW!"
ping 127.0.0.1 -n 3 >nul

if exist "!PID_FILE!" (
    set /p NEW_PID=<"!PID_FILE!"
    echo [SUKSES] Auto-Watcher berhasil aktif di background [PID: !NEW_PID!].
) else (
    echo [SUKSES] Auto-Watcher telah diluncurkan.
)

:ask_startup
echo.
echo Apakah Anda ingin Auto-Watcher aktif otomatis setiap PC dinyalakan?
set /p "BOOT_CHOICE=Aktifkan saat Windows Startup? [Y/N] (Default: Y): "
if "!BOOT_CHOICE!"=="" set "BOOT_CHOICE=Y"

if /I "!BOOT_CHOICE!"=="Y" (
    echo start "" pythonw "!WATCHER_PYW!" > "!STARTUP_DIR!\Durango_Mod_Watcher.bat"
    echo [OK] Berhasil ditambahkan ke Windows Startup!
) else (
    if exist "!STARTUP_DIR!\Durango_Mod_Watcher.bat" del /f /q "!STARTUP_DIR!\Durango_Mod_Watcher.bat" >nul 2>&1
    echo [INFO] Melewati penambahan ke Windows Startup.
)

echo.
echo ========================================================
echo   Auto-Watcher kini memantau update client secara diam-diam.
echo   Setiap kali Launcher XDLands update game, mod akan
echo   langsung dipasang otomatis dalam ~2 detik!
echo ========================================================
echo.
pause
exit /b 0
