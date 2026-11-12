@echo off
setlocal
echo ========================================================
echo   Durango Wild Lands Android Combat Mod Build Script
echo ========================================================

set NDK_CLANG=tools\ndk\android-ndk-r25c\toolchains\llvm\prebuilt\windows-x86_64\bin\aarch64-linux-android24-clang++.cmd
set SRC_DIR=native_mod
set OUT_SO=native_mod\libdurangomod.so

if not exist "%NDK_CLANG%" (
    echo [ERROR] Android NDK clang not found at %NDK_CLANG%!
    pause
    exit /b 1
)

echo [1/3] Compiling C++ ARM64 Native Mod...
call "%NDK_CLANG%" -shared -fPIC -O2 -static-libstdc++ -std=c++14 -llog "%SRC_DIR%\main.cpp" "%SRC_DIR%\And64InlineHook.cpp" -o "%OUT_SO%"
if not exist "%OUT_SO%" (
    echo [ERROR] Native compilation failed!
    pause
    exit /b 1
)
echo      -> libdurangomod.so built successfully!

echo [2/3] Patching libmain.so dependencies and repacking APK...
python repack_and_sign.py
if errorlevel 1 (
    echo [ERROR] Repack and sign failed!
    pause
    exit /b 1
)

echo [3/3] Done!
echo ========================================================
echo   Output APK: base_modded.apk
echo   Ready to install on Android / Emulator!
echo ========================================================
pause
