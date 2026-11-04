# Active Context

## Current Status (3 - 4 November 2026)

### 1. Perbaikan Akurasi Deteksi Hitbox Serangan Dino (Hitbox Area Detection Miss)
- **Penyebab Miss Sebelumnya:**
  - Dekompilasi rendering mesh Durango (`FillBorderAlert.MakeRectMesh` & `AreaOfEffectVisualizer`) membuktikan bahwa sumbu `RectSizeHalves` Durango mengkodekan:
    - `x` = **Forward half-length** sepanjang vektor hadap (`Yaw`: `(sin(yaw), cos(yaw))`).
    - `y` = **Lateral half-width** sepanjang vektor samping (`(cos(yaw), -sin(yaw))`).
  - Kode lama membalik kedua sumbu tersebut (`x` dianggap lateral dan `y` dianggap forward). Akibatnya, kotak serudukan memanjang di depan dino terotasi 90 derajat secara matematis, sehingga pemain yang berdiri tepat di depan mulut dino terklasifikasi sebagai "di luar area", sementara berdiri di samping malah terklasifikasi "di dalam area".
  - Pada busur lingkaran (*arc*), perbandingan sudut start-end tidak menangani pembungkusan lingkaran 360° secara sempurna.
- **Solusi:**
  - Menyesuaikan kalkulasi `dForward` dan `dLateral` dengan orientasi visual Durango.
  - Normalisasi rentang sudut busur (`arcSpan` & `playerOffset`) dengan toleransi tepi busur 4° agar batas tepi lingkaran tidak meleset.
  - Menambahkan buffer radius fisik kapsul tubuh karakter (`playerBodyRadius = 45f` / 0.45m) sehingga saat karakter menyentuh garis merah, ia terdeteksi valid berada di dalam area bahaya.

---

### 2. Fitur Auto Counter Attack & Auto Dodge Roll (Action Overwrite Engine)
- **Mekanisme Action Overwrite (Bypass Animasi):**
  - Mengatasi animasi tebasan/ayunan senjata yang mengunci karakter saat dino menyerang:
    1. Membersihkan aksi tempur aktif: `UsingAction.Clear()`.
    2. Mereset batas larangan cooldown: `spaceAction.ProhibitedUntil = serverTime - 1.0`.
    3. Membersihkan antrean gerak: `PlayerController.MotionUpdater.ClearReservedMotions()`.
    4. Menghentikan klip animasi serang: `localPlayer.Anim.Stop()`.
    5. Menjalankan aksi counter/dodge: `cs.UseBattleAction(...)` dan menaikkan `_state` ke 2 (Execute) pada `UsingAction` untuk memotong jeda rotasi/startup.
    6. Menimpa model 3D secara instan dengan animasi counter/dodge via `ForcePlayCounterMotion(...)`.
  - Berfungsi pada eksekusi otomatis maupun saat pemain menekan tombol **Spasi** secara manual di tengah-tengah ayunan senjata.
- **Pemisahan Timing Peringatan Mata (0.80s) vs Eksekusi Bot (0.30s / 0.35s):**
  - Alert visual banner HUD, lampu tombol Spasi, dan audio cue tetap berbunyi di detik **0.80s** (memberikan waktu bagi mata manusia).
  - Eksekusi otomatis (*bot*) menahan diri hingga detik presisi fisika animasi:
    - **Auto Counter Attack:** Dijalankan di detik **`0.30s`** sebelum benturan (*clash* instan saat cakar/gigitan dino mendarat).
    - **Auto Dodge Roll:** Dijalankan di detik **`0.35s`** sebelum benturan (saat serangan dino mendarat, karakter sedang di puncak kecepatan *roll* dan sudah bersih keluar garis merah).
  - Slider timing untuk masing-masing aksi ditambahkan di menu GUI mod (Bagian **3f** & **3g**) dan tersimpan ke `durango_settings.ini`.

---

### 3. Perbaikan Auto Gather: Isolasi Ketat Bagian Resource (Pohon Ikut Ditebang Saat Ambil Ranting)
- **Penyebab:**
  - Pada `DoesWidgetMatchTargetItem`, variabel `targetWantsAnchor` mengecek `targetActLow.Contains("pohon")`. Karena nama target adalah `"Ranting Pohon Abu"`, teks tersebut mengandung kata `"pohon"`, sehingga target salah ditandai sebagai `targetWantsAnchor = true`.
  - Ketika ranting di pohon abu tersebut sudah habis dipetik (`count = 0`), bot mengevaluasi slot lain pada pohon, yaitu slot **"Tebang Pohon Abu"** / **"Ambil Kayu Pohon Abu"**.
  - Pencocokan kata per kata (`wd`) mencocokkan kata `"pohon"` dan `"abu"` pada slot tebang. Akibatnya, slot tebang kayu dianggap cocok dengan ranting, sehingga bot menebang pohon tersebut!
- **Solusi:**
  - **Isolasi Ketat Jenis Resource (*Strict Mutual Exclusion*):**
    - Jika target mencari **Ranting**, slot yang dipilih **WAJIB** mengandung kata `ranting`/`cabang`/`branch`/`twig`. **DILARANG KERAS** menyentuh slot tebang/kayu/balok/batang (`IsAnchorSlot(w) == true`) meskipun berada di pohon yang sama.
    - Hal serupa berlaku untuk daun, kulit, buah, biji, getah, dan kerikil.
    - Kata generik spesies/tanaman (`pohon`, `tree`, `batu`, `node`) dilarang mencocokkan aksi jika jenis resource dasarnya berbeda.
  - Jika ranting di pohon abu tersebut sudah habis, bot langsung menutup menu pohon dan berjalan menuju **Pohon Abu berikutnya** di pulau tanpa menyentuh slot kayu/tebang.

---

### 4. Perbaikan Auto Gather: Navigasi Menembus Batas/Ujung Map (Jalan Terus ke Laut)
- **Penyebab:**
  - Fungsi `IsObstacleAtPosition` sebelumnya hanya mengecek dinding tebing batu (`IsCollidableMasked`).
  - Batas pulau Durango dikelilingi oleh **Lautan Lepas / Samudra** (`Biome.ColdOcean`, `Biome.WarmOcean`, serta area di luar chunk map). Karena laut bukan tebing, navigasi menganggap laut adalah jalur daratan yang kosong.
  - Sistem memori jejak langkah (*anti-looping*) memberi penalti 0 pada sektor laut yang belum pernah dikunjungi, sehingga bot tertarik berjalan lurus ke arah samudra dan tertahan di batas map tanpa bisa berputar balik.
- **Solusi:**
  - `IsObstacleAtPosition` kini mendeteksi secara menyeluruh:
    1. **Batas Chunk Pulau:** Chunk tidak ada (`terrain.GetChunkFromWorldPosition == null`).
    2. **Biome Samudra & Invalid:** Biome `ColdOcean`, `WarmOcean`, atau `Invalid`.
    3. **Kedalaman Air:** Kedalaman air `> 0.45m` (kedalaman mulai berenang/tenggelam).
    4. **Dinding Tebing:** `IsCollidableMasked`.
  - Fungsi `FindSafeDetourDirection` dan `UpdateIslandSweepExploration` kini otomatis menolak semua arah menuju samudra/luar map dan memilih arah belok menuju daratan pulau yang aman.
  - Ditambahkan **Emergency Boundary Escape**: jika karakter menyentuh air laut/batas pulau, bot seketika berbalik 180° menuju daratan solid.
