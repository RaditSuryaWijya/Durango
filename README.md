# 🦕 Durango / XDLands PC - AutoGather & Combat Suite Mod

Mod serbaguna terlengkap untuk **XD Lands (Durango: Wild Lands PC Fan Game)**. Menggabungkan sistem otomasi panen cerdas (*Smart Learning Auto-Gather*), perencana produksi multi-level (*Craft Planner*), hingga paket tempur & mobilitas (*Combat & Mobility Suite*).

---

## ⚡ Fitur Utama

### 1. 🌾 Smart Auto-Gathering (Panen Otomatis Cerdas)
- **Smart Learning System**: Dekati objek apa saja (pohon, batu, bambu, semak, bangkai) lalu panen manual 1x. Bot akan langsung mempelajari aksi dan jenis objek tersebut, kemudian otomatis berkeliling memanen semua objek sejenis di sekitar!
- **Filter Resource Lengkap**: Pilihan filter kategori `All`, `Wood`, `Stone`, `Bush`, `Plant`, dan `Corpse`.
- **Mode Target Spesifik**: Fokuskan panen pada bagian tertentu: `Auto`, `Daun`, `Ranting`, `Batang`, `Batu`, `Rumput`, `Daging`, `Kulit`, `Buah`, atau kunci slot tertentu (`F1` - `F6`).
- **Radius Scan Fleksibel**: Radius panen dapat disesuaikan langsung melalui slider di menu.

### 2. 🔨 Craft Planner & Mass Production
- **Bypass Craft Quantity Limit (999)**: Membuka batasan antrian bawaan game dari maksimal 15 item menjadi **hingga 999 item** sekaligus.
- **Nested Material Calculator**: Menghitung kebutuhan bahan dasar secara berjenjang dan melacak inventaris secara *real-time*.
- **Auto-Eat Saat Crafting**: Menjaga stamina tidak habis saat memproduksi barang dalam jumlah massal dengan otomatis memakan makanan dari tas.

- **Dual Speed Multiplier**: Pengatur kecepatan lari karakter dan dinosaurus tunggangan secara terpisah (`1.0x` s/d `2.0x`) tanpa tumpang tindih (*zero overlap*).
- **Dino Noclip**: Karakter dapat menembus badan dinosaurus (baik dino liar berukuran raksasa maupun dino peliharaan/tunggangan), mencegah terjepit saat farming di kandang atau bertarung.
- **Moving Attack (No MoveLock)**: Menyerang dan menebas sembari bebas bergerak lincah (WASD / Klik Mouse / Kiting) tanpa terkunci kaku di tempat.
- **Anti-Stagger & Anti-Stun**: Kebal terhadap efek guncangan (*stagger*), dorongan (*knockback*), dan hantaman (*blow*) dinosaurus langsung di level IL engine (`DamagedProcesser`).
- **Turbo ASPD (Recovery Cut)**: Memangkas jeda pemulihan tebasan senjata (0.02s - 0.08s) untuk ayunan serangan beruntun berkecepatan tinggi.
- **Kamuflase Semak (Anti-Aggro)**: Karakter selalu dianggap berkamuflase sehingga dinosaurus predator tidak menyerang lebih dulu (*anti-aggro*).

### 4. 🗺️ ESP 3D & Whole-Island Radar Scanner
- **Tactical Top-Down Edge Radar**: Menunjukkan panah arah dan jarak target dinosaurus langka, predator, bangkai, kawah vulkanik, dan mata air di sekeliling bingkai layar (radius dekat hingga 2 km+).
- **Deteksi 1 Map Penuh**: Membaca seluruh POI pulau dan dilengkapi tombol **Buka Peta Penuh (Reveal Map)** untuk membuka kabut pulau secara instan.
- **3D Floating Tags (ESP)**: Menandai objek penting dengan tag warna neon yang jelas (Merah: Predator/Dino Langka, Hijau: Dino Jinak, Kuning Emas: Bangkai, Oranye: Kawah Vulkanik/Mineral).

### 5. 🖥️ Antarmuka GUI Ringan & Nyaman
- **Draggable Window**: Posisi panel menu dapat digeser bebas ke mana saja dengan klik dan tahan pada header bar.
- **100% Clean Hidden Mode**: Saat menu ditutup atau disembunyikan, mod tidak meninggalkan elemen visual apa pun di layar (nol pixel overlay), membiarkan HUD game tetap bersih.
- **Dukungan Banyak Hotkey**: Akses cepat dengan tombol pilihan Anda.

---

## 🎮 Kontrol & Hotkey

| Tombol / Kombinasi | Fungsi |
| :--- | :--- |
| **Insert** atau **F11** | Buka / Tutup Menu Mod (Toggle Normal / Hidden) |
| **Ctrl + 3 + 1** | Alternatif buka / tutup Menu Mod |
| **F1** | Nyalakan / Matikan Bot Auto-Panen (Default) |
| **F2** | Nyalakan / Matikan Bot Combat Otomatis (Default) |
| **Tombol [X]** | Menutup panel menu mod seketika |

*(Tombol toggle bot dapat diganti langsung melalui menu mod ke tombol lain seperti F4, F7, F8, F9, F12, atau tombol petik `~`)*.

---

## 🚀 Cara Pemasangan di Komputer / Laptop Lain

### Metode 1: One-Click Auto Installer (Sangat Mudah & Direkomendasikan)
Tidak memerlukan instalasi Python atau compiler apa pun:

1. **Unduh (Download ZIP)** repository ini atau clone menggunakan Git:
   ```bash
   git clone https://github.com/RaditSuryaWijya/Durango.git
   ```
2. Buka folder hasil unduhan, lalu **klik dua kali (double-click)** file:
   ```text
   install_mod.bat
   ```
3. Installer akan otomatis:
   - Mendeteksi folder instalasi XDLands PC (`%LOCALAPPDATA%\XDLands\PC\game`).
   - Membuat file backup original `Assembly-CSharp.dll.original` secara otomatis.
   - Memasang file mod yang sudah teruji (*pre-patched*).
4. Jalankan **XDLands.exe** dan masuk ke permainan.
5. Tekan **Insert** atau **F11** untuk membuka Menu Mod!

---

### Metode 2: Pemasangan Manual
Jika folder game berada di lokasi kustom:
1. Buka folder instalasi game Anda (folder tempat `XDLands.exe` berada).
2. Masuk ke sub-folder:
   ```text
   XDLands_Data\Managed\
   ```
3. Buat backup file `Assembly-CSharp.dll` (misal ubah nama menjadi `Assembly-CSharp.dll.original`).
4. Salin file dari folder `Release/` mod ini:
   - Salin `Release\Assembly-CSharp.dll` ke `XDLands_Data\Managed\`
   - Salin `Release\AutoGatherMod.dll` ke `XDLands_Data\Managed\`
5. Salin `AutoGatherSettings.ini` ke folder utama tempat `XDLands.exe` berada.
6. Selesai! Jalankan `XDLands.exe`.

---

## 🗑️ Cara Mencopot Mod (Uninstall)

Jika Anda ingin mengembalikan game ke kondisi original 100%:
1. Buka folder repository ini.
2. Klik dua kali file:
   ```text
   uninstall_mod.bat
   ```
3. Script akan otomatis memulihkan `Assembly-CSharp.dll.original` dan menghapus `AutoGatherMod.dll`. Game kembali 100% bawaan pabrik.

---

## 🛠️ Kompilasi dari Source Code (Untuk Pengembang)

Jika Anda ingin mengedit `AutoGatherMod.cs`:
1. Pastikan Anda berada di direktori game atau repository.
2. Jalankan:
   ```text
   build_mod.bat
   ```
   *Script akan mengompilasi kode C# menggunakan compiler .NET Framework (`csc.exe`) dan memperbarui file biner di folder `Release/`.*
3. Jika ingin melakukan re-patching IL pada file original game secara manual dari source code:
   ```bash
   python patch_mod.py
   ```

---

## 📂 Struktur File Repository

```text
Durango/
├── README.md               # Dokumentasi lengkap & panduan penggunaan
├── .gitignore              # Konfigurasi filter Git
├── AutoGatherMod.cs        # Kode sumber utama C# mod
├── AutoGatherMod.dll       # Biner mod terkompilasi
├── AutoGatherSettings.ini  # Template konfigurasi bawaan mod
├── install_mod.bat         # Installer 1-klik otomatis untuk komputer lain
├── uninstall_mod.bat       # Uninstaller 1-klik untuk mengembalikan game original
├── build_mod.bat           # Script kompilasi C# otomatis (.NET 3.5 csc)
├── patch_mod.py            # Patcher mandiri berbasis Python & Mono.Cecil
├── MarkHelper.dll          # Assembly pembantu injeksi auto-loader
├── MarkHelper.cs           # Kode sumber pembantu auto-loader
├── Mono.Cecil*.dll         # Library manipulasi bytecode IL untuk patcher
└── Release/                # Paket siap pakai tanpa perlu instalasi tambahan
    ├── Assembly-CSharp.dll # Engine game ter-patch
    ├── AutoGatherMod.dll   # Controller mod
    └── AutoGatherSettings.ini # Pengaturan default
```

---

## ⚠️ Catatan Keamanan & Kompatibilitas
- Mod ini kompatibel dengan versi terbaru **XD Lands (Fan Game) PC Client**.
- Seluruh konfigurasi tombol dan fitur tersimpan secara persisten di file `AutoGatherSettings.ini` sehingga pengaturan Anda tidak akan hilang saat keluar game.
