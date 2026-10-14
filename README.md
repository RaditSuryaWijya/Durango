# Durango: Wild Lands - AutoGather & Craft Planner Mod (PC / XDLands)

Mod serbaguna untuk **Durango: Wild Lands (PC Client / XDLands)** yang menambahkan fitur otomasi panen (Auto Gathering), perencanaan resep kerajinan (Craft Planner), otomatis konsumsi makanan (Auto Eat) saat crafting, serta bypass batasan antrian craft bawaan game (15 -> 999).

---

## Fitur Utama

1. **Smart Auto Gathering**:
   - Berjalan dan memanen secara otomatis dalam radius tertentu (dapat diatur).
   - Memilih alat yang sesuai (Pickaxe, Axe, Knife, dll.) berdasarkan kebutuhan node.
   - Deteksi pintar tipe node: Pohon (Tree), Batu (Rock), Semak (Shrub), Rumput (Grass), Kerikil (Pebble), Bangkai Hewan (Corpse).
   - Filter resource: `All`, `Wood`, `Stone`, `Bush`, `Plant`, `Corpse`.
   - Mode target panen: `Auto` (habiskan node), `Daun`, `Ranting`, `Batang`, `Batu`, `Rumput`, `Daging`, `Kulit`, `Buah`, atau kunci slot tertentu (`F1` - `F6`).

2. **Craft Planner & Nested Material Hunting**:
   - Pilih resep target dan jumlah yang diinginkan.
   - Mod melacak inventaris secara *real-time* dan menghitung kekurangan bahan.
   - Jika bahan memerlukan sub-crafting (misal: butuh Tali yang dibuat dari Daun/Ranting), mod dapat memandu dan memanen bahan dasarnya secara berjenjang.
   - Opsi otomatis craft ketika seluruh bahan telah terkumpul.

3. **Auto-Eat Saat Crafting**:
   - Menghindari kehabisan stamina/energi saat melakukan antrian craft massal.
   - Otomatis memakan makanan yang tersedia di tas saat energi habis.

4. **Bypass Craft Quantity Limit (999)**:
   - Melewati batasan default game (maksimal 15 item per antrian) menjadi hingga **999 item**.

5. **Mobile Combat (Nyerang Sambil Jalan) & Animation Canceler**:
   - Membuka kuncian gerak (*MoveLock*) saat mengayunkan senjata, sehingga karakter tetap bisa bergerak bebas (WASD / Klik) saat menebas musuh.
   - Fitur *Animation Canceler* memotong jeda pemulihan animasi (*recovery lock*), membuat tebasan berikutnya siap jauh lebih cepat.
   - Sangat efektif untuk *kiting* dinosaurus predator besar atau mengejar dinosaurus herbivora yang kabur.

6. **Pengatur Kecepatan Gerak (CheatMoveSpeedMultiply)**:
   - Memanfaatkan pengali kecepatan bawaan Durango di `PlayerController` (`1.0x` s/d `2.0x`).
   - Tersedia tombol preset cepat di tab GUI: `1.0x (Normal)`, `1.2x`, `1.35x (Gesit)`, `1.5x (Cepat)`, `1.75x`, dan `2.0x (Sprint)`.

7. **ESP & Radar Dinosaurus Langka, Bangkai & Kawah Sumber Air (1 Map Penuh)**:
   - **Tactical Top-Down Edge Radar (Kompas & Penunjuk Arah Tepi Layar)**:
     - Didesain khusus untuk kamera *top-down* Durango di mana jarak pandang layar terbatas ~25 meter.
     - Objek yang berada jauh / di luar layar (>25m s/d 2km+) **tidak hilang**, melainkan di-clamp ke pinggiran bingkai layar dengan panah arah 8 penjuru (`▲, ▼, ◀, ▶, ↗, ↘, ↙, ↖`), badge kategori, nama, level, serta jarak target (`850m` / `1.4km`).
   - **Whole-Island Map Detection (Deteksi 1 Map Penuh)**:
     - Memanfaatkan sinkronisasi `MapSystem` (`_exploredPOIs` dan `_poiUpdater`) yang dikonversi langsung dari koordinat Tile pulau ke posisi 3D dunia (`Durango.Terrain.Util.TilePositionToClientPosition`).
     - Mendeteksi seluruh Kawah Vulkanik, Kubah Warphole, Retakan Tanah (Crack/Rift), dan Sumber Air di seluruh kepulauan secara utuh!
     - Dilengkapi tombol **[🗺️ Buka Peta Penuh (Reveal Map)]** untuk membuka kabut seluruh pulau secara instan (`MapSystem.PurchaseMap`).
   - **3D In-World Overlay (ESP)**:
     - Menampilkan floating tag di atas objek yang terlihat di layar:
       - **Dino Langka & Predator** (Merah Neon): T-Rex, Raptor, Smilodon, Allosaurus, Stego, Brachio, dll. atau Lv >= 50, lengkap dengan HP & jarak.
       - **Dino Jinak / Herbivora** (Hijau Neon): Zebra, Megaloceros, Babi, dll.
       - **Bangkai / Siap Loot** (Kuning Emas): Bangkai hewan siap loot di sekitar.
       - **Kawah Vulkanik & Retakan** (Oranye Menyala): Kawah vulkanik & retakan mineral artefak.
       - **Mata Air Panas & Geyser** (Cyan & Biru): Sumber belerang dan mata air alami.
       - **Snaplines Opsional**: Garis penunjuk arah vertikal dari bawah layar ke target.
   - **Radar Target List & Tombol 'Dekati'**:
     - Menampilkan seluruh objek pulau terurut dari yang paling dekat.
     - Klik tombol **[Dekati]** untuk otomatis menggerakkan karakter menghampiri target dinosaurus atau kawah di ujung pulau sekalipun!

8. **In-Game GUI & Menu Controls**:
   - Menu overlay interaktif langsung di layar game dengan 4 Tab:
     - **Tab 1: Auto Panen** (Deteksi cerdas, filter, & kontrol radius scan).
     - **Tab 2: Craft Plan** (Hierarki sub-bahan & pelacak inventaris).
     - **Tab 3: Tempur** (Moving Attack, Animation Canceler, & Speed Multiplier).
     - **Tab 4: Radar ESP** (Toggle Edge Radar, radius 150m - 1 Map Penuh, Reveal Map, & daftar objek).
   - Hotkey toggle bot (Default: `F1`).
   - Hotkey toggle tampilan menu: `Ctrl + 3 + 1` (Penuh / Mini Bar / Sembunyi).

---

## Cara Pasang di Device Lain

### Metode 1: Instalasi Otomatis (Direkomendasikan)
1. **Clone repository ini** di komputer/laptop target:
   ```bash
   git clone https://github.com/RaditSuryaWijya/Durango.git
   ```
2. Buka folder hasil clone, lalu **klik kanan dan Run as Administrator** file:
   ```text
   install_mod.bat
   ```
   *Script akan otomatis mendeteksi folder instalasi game XDLands (`%LOCALAPPDATA%\XDLands\PC\game`), menyalin file mod, dan menginjeksi XDLandsMark.*
3. Jalankan game **XDLands.exe**. Mod akan otomatis aktif saat Anda masuk ke dalam dunia game!

---

### Metode 2: Instalasi Manual
Jika folder game berada di lokasi kustom, Anda cukup menyalin file-file berikut ke folder direktori game Anda (`.../XDLands/PC/game/`):

1. Salin file-file berikut ke folder `game/`:
   - `AutoGatherMod.dll`
   - `doorstop_config.ini`
   - `winhttp.dll`
   - `patch_xdlandsmark.py`
   - `Mono.Cecil.dll`, `Mono.Cecil.Mdb.dll`, `Mono.Cecil.Pdb.dll`, `Mono.Cecil.Rocks.dll`
2. Salin juga `AutoGatherMod.dll` ke dalam sub-folder `game/XDLandsMark/`.
3. Jalankan patcher untuk menginjeksi XDLandsMark dan bypass batas craft:
   ```bash
   python patch_xdlandsmark.py
   ```
4. Selesai! Jalankan `XDLands.exe`.

---

## Cara Kompilasi / Build dari Source Code

Jika Anda melakukan perubahan pada `AutoGatherMod.cs`:
1. Buka folder game (`.../XDLands/PC/game`).
2. Jalankan:
   ```text
   build_mod.bat
   ```
   *Script akan mengompilasi kode C# menggunakan .NET 3.5 compiler (`csc.exe`) dan memperbarui file `AutoGatherMod.dll` di folder game serta `XDLandsMark/`.*

---

## Struktur File Mod

| File / Folder | Keterangan |
| :--- | :--- |
| `AutoGatherMod.cs` | Kode sumber utama C# mod AutoGather & Craft Planner |
| `AutoGatherMod.dll` | File biner DLL mod siap pakai |
| `build_mod.bat` | Batch script untuk kompilasi C# otomatis |
| `install_mod.bat` | Installer otomatis untuk device baru |
| `patch_xdlandsmark.py` | Patcher Mono.Cecil untuk injeksi `XDLandsMark.dll` & craft limit |
| `doorstop_config.ini` | Konfigurasi Unity Doorstop loader |
| `winhttp.dll` | Doorstop bootstrap proxy DLL |
| `Mono.Cecil*.dll` | Library manipulasi bytecode IL untuk patcher |

---

## Kontrol & Hotkey

- **F1**: Aktifkan / Nonaktifkan Auto Gathering (Standby / Active).
- **Ctrl + 3 + 1**: Ubah mode tampilan menu GUI (Normal -> Minimized -> Hidden).
