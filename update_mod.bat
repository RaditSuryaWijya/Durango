@echo off
title Update AutoGatherMod
cd /d "%~dp0"
echo Menunggu game XDLands ditutup...
:wait_loop
tasklist /fi "imagename eq XDLands.exe" | findstr /i "XDLands.exe" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)
echo Game sudah tertutup. Memperbarui AutoGatherMod.dll...
copy /y AutoGatherMod_new.dll AutoGatherMod.dll
copy /y AutoGatherMod_new.dll XDLandsMark\AutoGatherMod.dll

(
echo [General]
echo enabled=true
echo target_assembly=XDLandsMark\XDLandsMark.dll
echo redirect_output_log=false
echo ignore_disable_switch=true
echo.
echo [UnityMono]
echo dll_search_path_override=
echo debug_enabled=false
echo debug_address=127.0.0.1:10000
echo debug_suspend=false
) > doorstop_config.ini

if exist patch_xdlandsmark.py (
    echo Memeriksa patch XDLandsMark.dll...
    python patch_xdlandsmark.py
)

echo [OK] AutoGatherMod.dll dan XDLandsMark berhasil diperbarui!
echo Anda bisa menjalankan game kembali (dari launcher atau exe).
pause
