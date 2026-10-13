@echo off
cd /d "%~dp0"
echo Mengompilasi AutoGatherMod.cs dengan .NET 3.5 compiler...
C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe /target:library /out:AutoGatherMod_new.dll /r:XDLands_Data\Managed\UnityEngine.CoreModule.dll /r:XDLands_Data\Managed\UnityEngine.IMGUIModule.dll /r:XDLands_Data\Managed\UnityEngine.TextRenderingModule.dll /r:XDLands_Data\Managed\Assembly-CSharp.dll AutoGatherMod.cs
if errorlevel 1 (
    echo [ERROR] Kompilasi gagal!
    pause
    exit /b 1
)
move /y AutoGatherMod.dll AutoGatherMod_old.dll >nul 2>&1
copy /y AutoGatherMod_new.dll AutoGatherMod.dll
copy /y AutoGatherMod_new.dll XDLandsMark\AutoGatherMod.dll
python patch_xdlandsmark.py
echo [OK] AutoGatherMod.dll berhasil di-build dan dipasang!
pause
