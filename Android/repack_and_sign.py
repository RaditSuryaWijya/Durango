import os
import zipfile
import subprocess
import shutil

BASE_DIR = r"C:\Users\Radit\AppData\Local\XDLands\PC\durango_git\Android"
SRC_APK = os.path.join(BASE_DIR, "base.apk")
MOD_DIR = os.path.join(BASE_DIR, "native_mod")
TOOLS_DIR = os.path.join(BASE_DIR, "tools")

PATCHED_MAIN_SO = os.path.join(MOD_DIR, "libmain_patched.so")
MOD_SO = os.path.join(MOD_DIR, "libdurangomod.so")
TEMP_APK = os.path.join(BASE_DIR, "base_unsigned.apk")
SIGNER_JAR = os.path.join(TOOLS_DIR, "uber-apk-signer.jar")

print(f"Reading {SRC_APK}...")

with zipfile.ZipFile(SRC_APK, 'r') as zin, zipfile.ZipFile(TEMP_APK, 'w', compression=zipfile.ZIP_DEFLATED) as zout:
    for item in zin.infolist():
        # Exclude old signature files
        if item.filename.startswith("META-INF/"):
            continue
        # Exclude old libmain.so
        if item.filename == "lib/arm64-v8a/libmain.so":
            continue
        
        # Copy original entry
        data = zin.read(item.filename)
        # Directly embed Jazzy's account token into cluster asset
        if item.filename == "assets/bin/Data/54dfbb2d471619f45b162c93ae4b16d4":
            print("Embedding Jazzy account token (sTccFu29SYZr6fnoXVo1V07n) into cluster asset...")
            data = data.replace(b"RxZ64atLqFEI0QLFD9QqA1mm", b"sTccFu29SYZr6fnoXVo1V07n")
        zout.writestr(item, data)

    # Add patched libmain.so
    print("Writing patched libmain.so...")
    with open(PATCHED_MAIN_SO, 'rb') as f:
        zout.writestr("lib/arm64-v8a/libmain.so", f.read())

    # Add libdurangomod.so
    print("Writing libdurangomod.so...")
    with open(MOD_SO, 'rb') as f:
        zout.writestr("lib/arm64-v8a/libdurangomod.so", f.read())

print("APK contents repacked successfully!")

# Sign APK using uber-apk-signer
print("Signing APK with uber-apk-signer...")
cmd = [
    "java", "-jar", SIGNER_JAR,
    "-a", TEMP_APK,
    "--overwrite"
]

res = subprocess.run(cmd, capture_output=True, text=True)
print("Signer Output:\n", res.stdout)
if res.stderr:
    print("Signer Error:\n", res.stderr)

final_apk = os.path.join(BASE_DIR, "base_modded.apk")
if os.path.exists(final_apk):
    os.remove(final_apk)

if os.path.exists(TEMP_APK):
    os.rename(TEMP_APK, final_apk)
    print(f"Modded APK successfully generated at: {final_apk}")
