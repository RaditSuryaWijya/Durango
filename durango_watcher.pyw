"""
Durango Mod Background Auto-Watcher
===================================
Script ini berjalan diam di background (tanpa jendela command prompt).
Memantau file Assembly-CSharp.dll di folder game XDLands.
Jika launcher XDLands mengunduh update baru (client reset ke vanilla),
watcher ini secara otomatis dan instan (dalam ~2 detik) menambal kembali
mod Durango (AutoGather, Combat Suite, Counter Assist, dll.) sebelum Anda membuka game.
"""

import os
import sys
import time
import subprocess
import datetime
import winsound
import ctypes

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LOCALAPPDATA = os.environ.get("LOCALAPPDATA", "")
GAME_DIR = os.path.join(LOCALAPPDATA, r"XDLands\PC\game")
MANAGED_DIR = os.path.join(GAME_DIR, r"XDLands_Data\Managed")
ASM_PATH = os.path.join(MANAGED_DIR, "Assembly-CSharp.dll")
PATCH_SCRIPT = os.path.join(SCRIPT_DIR, "patch_mod.py")
PID_FILE = os.path.join(SCRIPT_DIR, "durango_watcher.pid")
LOG_FILE = os.path.join(SCRIPT_DIR, "durango_watcher.log")

def log(message):
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    line = f"[{now}] {message}"
    try:
        with open(LOG_FILE, "a", encoding="utf-8") as f:
            f.write(line + "\n")
    except:
        pass

def is_game_running():
    try:
        # Gunakan tasklist untuk mengecek apakah XDLands.exe sedang aktif
        out = subprocess.check_output(
            'tasklist /FI "IMAGENAME eq XDLands.exe" /NH',
            shell=True,
            text=True,
            creationflags=subprocess.CREATE_NO_WINDOW
        )
        return "XDLands.exe" in out
    except:
        return False

def is_assembly_patched(file_path):
    if not os.path.exists(file_path):
        return True # File belum ada, tunggu
    try:
        # Cek apakah hook LoadAutoGather dan DisableStagger ada di dalam DLL
        with open(file_path, "rb") as f:
            content = f.read()
        return (b"LoadAutoGather" in content) and (b"DisableStagger" in content)
    except Exception:
        # File mungkin sedang di-lock oleh launcher yang sedang menulis
        return True # Anggap belum siap, tunggu siklus berikutnya

def apply_patch():
    log("MEMULAI AUTO-PATCH: Client game baru terdeteksi (belum ada hook mod)!")
    try:
        # Jalankan patch_mod.py
        result = subprocess.run(
            [sys.executable, PATCH_SCRIPT, GAME_DIR],
            capture_output=True,
            text=True,
            creationflags=subprocess.CREATE_NO_WINDOW
        )
        if result.returncode == 0:
            log("SUKSES: Mod Durango berhasil dipasang kembali ke versi client terbaru!")
            # Bunyikan notifikasi sistem Windows
            winsound.MessageBeep(winsound.MB_ICONASTERISK)
            return True
        else:
            log(f"GAGAL: Patch error - {result.stderr.strip() or result.stdout.strip()}")
            return False
    except Exception as e:
        log(f"EXCEPTION saat patching: {e}")
        return False

ERROR_ALREADY_EXISTS = 183
_mutex_handle = None

def acquire_mutex():
    global _mutex_handle
    _mutex_handle = ctypes.windll.kernel32.CreateMutexW(None, False, "Local\\DurangoModWatcherMutex")
    last_error = ctypes.windll.kernel32.GetLastError()
    if last_error == ERROR_ALREADY_EXISTS:
        return False
    try:
        with open(PID_FILE, "w") as f:
            f.write(str(os.getpid()))
    except:
        pass
    return True

def cleanup():
    global _mutex_handle
    if _mutex_handle:
        try:
            ctypes.windll.kernel32.CloseHandle(_mutex_handle)
            _mutex_handle = None
        except:
            pass
    if os.path.exists(PID_FILE):
        try:
            with open(PID_FILE, "r") as f:
                saved_pid = int(f.read().strip())
            if saved_pid == os.getpid():
                os.remove(PID_FILE)
        except:
            pass

def main():
    if not acquire_mutex():
        # Instance lain sudah berjalan
        return

    log("Durango Mod Background Auto-Watcher DIAKTIFKAN.")
    
    last_mtime = 0
    last_size = 0

    try:
        while True:
            # Jika game XDLands sedang berjalan, jangan ganggu file
            if not is_game_running() and os.path.exists(ASM_PATH):
                try:
                    stat = os.stat(ASM_PATH)
                    current_mtime = stat.st_mtime
                    current_size = stat.st_size

                    # Jika file termodifikasi atau ukurannya berubah
                    if current_mtime != last_mtime or current_size != last_size:
                        last_mtime = current_mtime
                        last_size = current_size

                        # Beri jeda 1.5 detik agar launcher selesai menulis file secara komplit
                        time.sleep(1.5)

                        # Cek apakah file ini versi vanilla (belum dipatch)
                        if not is_assembly_patched(ASM_PATH):
                            log(f"Perubahan terdeteksi pada Assembly-CSharp.dll ({current_size} bytes). Memeriksa patch...")
                            success = apply_patch()
                            if success:
                                # Update stat setelah dipatch
                                try:
                                    new_stat = os.stat(ASM_PATH)
                                    last_mtime = new_stat.st_mtime
                                    last_size = new_stat.st_size
                                except:
                                    pass
                except Exception as e:
                    pass

            time.sleep(3)
    except (KeyboardInterrupt, SystemExit):
        log("Durango Mod Background Auto-Watcher DIMATIKAN.")
    except Exception as e:
        import traceback
        log(f"FATAL ERROR di watcher: {traceback.format_exc()}")
    finally:
        cleanup()

if __name__ == "__main__":
    main()
