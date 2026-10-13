@echo off
title XD Lands - Auto-Run Mod
cd /d "%~dp0"
echo Memulai Auto-Run Mod untuk XD Lands...
if exist "C:\Users\Radit\AppData\Local\Programs\Python\Python312\python.exe" (
    "C:\Users\Radit\AppData\Local\Programs\Python\Python312\python.exe" autorun_mod.py
) else (
    python autorun_mod.py
)
pause
