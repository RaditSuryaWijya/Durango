"""
XD Lands PC - Auto-Run Mod (Python External Assistant)
Fitur:
- Tekan hotkey (Default: 'Q') untuk toggle Auto-Run (jalan maju terus menerus).
- Tekan tombol 'S' (mundur) atau hotkey lagi untuk otomatis berhenti.
- Tetap bisa belok kiri ('A') atau kanan ('D') saat Auto-Run aktif.
- Failsafe: Otomatis berhenti jika Alt+Tab keluar dari game.
- Suara bip notifikasi saat aktif / nonaktif.
"""

import sys
import time
import threading
import ctypes
import winsound
from pynput import keyboard

# ================= KONFIGURASI =================
# Tombol toggle (pilihan yang tidak dipakai game: 'q', 'caps_lock', '`')
TOGGLE_KEY = 'q'

# Nama jendela game (case-insensitive substring)
GAME_WINDOW_SUBSTRING = "XD Lands"

# Scancode DirectX untuk tombol 'W' (Maju)
SCANCODE_W = 0x11
KEYEVENTF_SCANCODE = 0x0008
KEYEVENTF_KEYUP = 0x0002

# ================= STRUKTUR SENDINPUT =================
PUL = ctypes.POINTER(ctypes.c_ulong)

class KeyBdInput(ctypes.Structure):
    _fields_ = [
        ("wVk", ctypes.c_ushort),
        ("wScan", ctypes.c_ushort),
        ("dwFlags", ctypes.c_ulong),
        ("time", ctypes.c_ulong),
        ("dwExtraInfo", PUL)
    ]

class HardwareInput(ctypes.Structure):
    _fields_ = [
        ("uMsg", ctypes.c_ulong),
        ("wParamLow", ctypes.c_ushort),
        ("wParamHigh", ctypes.c_ushort)
    ]

class MouseInput(ctypes.Structure):
    _fields_ = [
        ("dx", ctypes.c_long),
        ("dy", ctypes.c_long),
        ("mouseData", ctypes.c_ulong),
        ("dwFlags", ctypes.c_ulong),
        ("time", ctypes.c_ulong),
        ("dwExtraInfo", PUL)
    ]

class Input_I(ctypes.Union):
    _fields_ = [("ki", KeyBdInput), ("mi", MouseInput), ("hi", HardwareInput)]

class Input(ctypes.Structure):
    _fields_ = [("type", ctypes.c_ulong), ("ii", Input_I)]

def press_scancode(scancode):
    extra = ctypes.c_ulong(0)
    ii_ = Input_I()
    ii_.ki = KeyBdInput(0, scancode, KEYEVENTF_SCANCODE, 0, ctypes.pointer(extra))
    x = Input(ctypes.c_ulong(1), ii_)
    ctypes.windll.user32.SendInput(1, ctypes.pointer(x), ctypes.sizeof(x))

def release_scancode(scancode):
    extra = ctypes.c_ulong(0)
    ii_ = Input_I()
    ii_.ki = KeyBdInput(0, scancode, KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP, 0, ctypes.pointer(extra))
    x = Input(ctypes.c_ulong(1), ii_)
    ctypes.windll.user32.SendInput(1, ctypes.pointer(x), ctypes.sizeof(x))

# ================= STATUS & KONTROL =================
is_running = False
lock = threading.Lock()

def is_game_focused():
    """Memeriksa apakah jendela XD Lands sedang aktif/fokus."""
    hwnd = ctypes.windll.user32.GetForegroundWindow()
    if not hwnd:
        return False
    length = ctypes.windll.user32.GetWindowTextLengthW(hwnd)
    if length == 0:
        return False
    buf = ctypes.create_unicode_buffer(length + 1)
    ctypes.windll.user32.GetWindowTextW(hwnd, buf, length + 1)
    title = buf.value
    return GAME_WINDOW_SUBSTRING.lower() in title.lower()

def sound_feedback(state):
    """Memberikan suara beep pelan untuk konfirmasi."""
    try:
        if state:
            winsound.Beep(1200, 100) # Nada tinggi = ON
        else:
            winsound.Beep(600, 100)  # Nada rendah = OFF
    except Exception:
        pass

def set_autorun(state):
    global is_running
    with lock:
        if is_running == state:
            return
        is_running = state

        if is_running:
            press_scancode(SCANCODE_W)
            print("\n[MOD] >>> AUTO-RUN: AKTIF (Karakter Berjalan Maju) <<<")
            threading.Thread(target=sound_feedback, args=(True,), daemon=True).start()
        else:
            release_scancode(SCANCODE_W)
            print("\n[MOD] >>> AUTO-RUN: BERHENTI <<<")
            threading.Thread(target=sound_feedback, args=(False,), daemon=True).start()

def focus_watcher():
    """Failsafe thread: lepas tombol jika pemain Alt+Tab keluar game."""
    while True:
        time.sleep(0.3)
        if is_running:
            if not is_game_focused():
                # Game tidak fokus, matikan autorun demi keamanan
                set_autorun(False)

def on_press(key):
    global is_running
    try:
        # Cek tombol toggle
        char_match = False
        if hasattr(key, 'char') and key.char:
            if key.char.lower() == TOGGLE_KEY.lower():
                char_match = True
        elif hasattr(key, 'name'):
            if key.name.lower() == TOGGLE_KEY.lower():
                char_match = True

        if char_match:
            # Pastikan hanya aktif ketika game sedang difokuskan
            if not is_running and not is_game_focused():
                return
            set_autorun(not is_running)
            return

        # Jika tombol S (mundur) ditekan saat autorun aktif, matikan autorun
        if is_running:
            if hasattr(key, 'char') and key.char and key.char.lower() == 's':
                set_autorun(False)
    except Exception:
        pass

def main():
    print("=" * 60)
    print("       XD LANDS PC - AUTO-RUN MOD (EXTERNAL SCRIPT)")
    print("=" * 60)
    print(f"[*] Tombol Toggle : '{TOGGLE_KEY.upper()}'")
    print("[*] Tombol Stop   : Tekan 'S' (Mundur) atau tekan Toggle lagi")
    print("[*] Status Jendela: Hanya aktif saat jendela XD Lands fokus")
    print("[*] Audio Cue     : Bip nada tinggi (ON), Bip nada rendah (OFF)")
    print("-" * 60)
    print("[INFO] Mod siap dan mendengarkan input di background...")
    print("[INFO] Tekan Ctrl+C di terminal ini untuk menutup mod.")
    print("=" * 60)

    # Jalankan watcher thread
    watcher = threading.Thread(target=focus_watcher, daemon=True)
    watcher.start()

    # Listener keyboard global
    with keyboard.Listener(on_press=on_press) as listener:
        try:
            listener.join()
        except KeyboardInterrupt:
            set_autorun(False)
            print("\n[MOD] Dimatikan. Selamat bermain!")

if __name__ == "__main__":
    main()
