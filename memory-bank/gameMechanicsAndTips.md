# Game Mechanics, Reverse Engineering Insights, & Tips & Tricks
## Durango: Wild Lands (PC / XDLands) Mod Suite

Dokumen ini mendokumentasikan seluruh pengetahuan arsitektur mesin game Durango (XDLands PC), mekanisme internal game, hubungan antara mod dan sistem game, formula matematis, serta tips dan trik reverse engineering yang telah dipelajari selama pengembangan mod ini.

---

## 1. Arsitektur Mesin Game & Sinkronisasi Server-Client

### Client vs Server Authority
- **Durango Wild Lands** adalah game hybrid server-client:
  - Posisi karakter, input gerak, rotasi hadap (*yaw*), dan animasi dieksekusi secara lokal di client (`PlayerBehavior`, `PlayerController`, `LocalMoveNavigator`).
  - Efek pertarungan, status damage, durasi interaksi panen, dan validitas stok node diputuskan secara otoritatif oleh server backend via paket pesan TCP/UDP.
- **Predicted Server Time (`Connections.Frontend.GetPredictedServerTime()`):**
  - Jam lokal komputer (`Time.time`) tidak sama dengan jam server. Server memiliki timestamp tersendiri dalam detik dengan presisi desimal ganda (`double`).
  - Durango menyediakan `GetPredictedServerTime()` yang mengompensasi latensi ping bolak-balik (RTT).
  - **Trik Penting**: Semua validasi cooldown skill (`action.ProhibitedUntil`), jadwal pendaratan serangan dino (`msg.AttackTime`), dan timing counter/dodge **WAJIB** dihitung terhadap `serverTime`, BUKAN `Time.time` lokal!

---

## 2. Mekanisme Tempur, Hitbox, & Action Overwrite

### Koordinat Hitbox Telegraph Durango (`FillBorderAlert.MakeRectMesh`)
- Ketika dino melakukan serangan area (kotak tebasan depan, serudukan, kibasan ekor, dll), paket `Messages.AttackAlerted` dikirim dari server ke client.
- **Sistem Koordinat 2D Durango:**
  - **Panjang ke Depan (Forward):** Diukur sepanjang vektor arah hadap dino (`Yaw`).
    $$\vec{V}_{\text{Forward}} = (\sin(\text{yaw}), \cos(\text{yaw}))$$
  - **Lebar ke Samping (Lateral Right):**
    $$\vec{V}_{\text{Lateral}} = (\cos(\text{yaw}), -\sin(\text{yaw}))$$
  - **Ukuran Persegi (`RectSizeHalves`):**
    - `RectSizeHalves.x` = **Setengah panjang ke depan (Forward)**.
    - `RectSizeHalves.y` = **Setengah lebar ke samping (Lateral)**.
  - **Tips Reverse Engineering:** Jangan pernah menukar `x` dan `y` pada `RectSizeHalves` Durango! Jika tertukar, kotak serudukan dino akan berputar 90 derajat secara matematis sehingga pemain yang berdiri di depan dino akan dianggap "di luar area".
  - **Buffer Radius Tubuh Karakter:** Tambahkan `playerBodyRadius = 45f` (0.45 meter) pada batas setengah extents untuk memperhitungkan kapsul tabrakan karakter pemain.

### Mengapa Animasi Menyerang Mengunci Karakter (*Animation Lock*)?
- Di game Durango asli, saat karakter mengayunkan senjata, sistem membuat objek `CombatSystem.UsingAction`.
- Selama `UsingAction` memiliki nilai (`HasValue() == true`), fungsi `CombatSystem.IsUsableAction` akan menolak (`return false`) semua aksi lain. Pemain tidak bisa guling, tidak bisa counter, dan tidak bisa berpindah stance sampai ayunan selesai.
- **Trik "Action Overwrite" (Bypass Animasi Instan):**
  Untuk membatalkan tebasan senjata secara instan saat dino mendadak menyerang:
  1. Panggil `cs.UsingAction.Clear()`.
  2. Paksa mundurkan larangan cooldown: `action.ProhibitedUntil = serverTime - 1.0`.
  3. Bersihkan antrean gerak root motion: `PlayerController.MotionUpdater.ClearReservedMotions()`.
  4. Hentikan klip animasi serang: `localPlayer.Anim.Stop()`.
  5. Panggil `cs.UseBattleAction(action.Data.Id)`.
  6. Naikkan state internal aksi: Via refleksi private field, set `UsingAction._state = 2` (State 2 = *Execute*). Ini menghilangkan jeda rotasi pre-cast dan langsung mengeksekusi serangan/guling di frame yang sama!
  7. Crossfade animasi model 3D: `localPlayer.Anim.CrossFade(clipName, 0.08f)`.

### Timing Sempurna: Peringatan Manusia vs Eksekusi Bot
- **Manusia (0.80s):** Butuh waktu ~0.25 – 0.35 detik untuk melihat kedipan HUD / mendengar audio dan menekan Spasi.
- **Bot (0.30s – 0.35s):** Bereaksi instan dalam 0 ms.
  - **Auto Counter Attack (`0.30s`):** Senjata diangkat kokoh dalam ~0.08 detik. Di detik 0.0s (benturan serangan dino), senjata berada tepat di posisi tangkisan (*clash frame*), memicu stagger/stun dino seketika.
  - **Auto Dodge Roll (`0.35s`):** Animasi roll berdurasi ~0.65s. Berguling di detik 0.35s menjamin bahwa saat serangan dino mendarat (0.0s), karakter sedang meluncur kencang di udara dengan *invulnerability frame (iframe)* aktif dan tubuh sudah bersih di luar area merah.

---

## 3. Mekanisme Roda Aksi (Radial Interaction Wheel) & Panen

### Anatomi Node Resource Durango
- Setiap objek alam (pohon, batu, bangkai, semak) memiliki menu radial dengan hingga 6 slot (`Durango.UI.InteractionMenuWidget_PC`).
- **Jenis Item pada Node:**
  1. **Item Sekunder (Non-Anchor):** Ranting, daun, kulit kayu, buah, biji, getah, bunga, kerikil. Mengambil item ini **TIDAK MENGHANCURKAN** node.
  2. **Item Utama (Anchor):** Tebang pohon (kayu/balok/batang), quarry batu besar (batu/tambang). Mengambil item ini **MENGHANCURKAN / MENEBANG** node secara permanen dari map!

### Mengapa Pohon Ikut Ditebang Saat Mengambil Ranting? (Pelajaran Berharga)
- Pada pohon abu, nama teks interaksi adalah:
  - "Ambil Ranting Pohon Abu"
  - "Tebang Pohon Abu"
- Kata `"pohon"` dan `"abu"` terdapat di KEDUA slot tersebut!
- Jika sistem pencocokan mencocokkan kata perkata secara longgar, ketika ranting habis, slot tebang akan cocok dengan kata `"pohon"` atau `"abu"`. Akibatnya bot menebang pohon tersebut.
- **Aturan Isolasi Bagian Resource (Strict Part Isolation):**
  - Jika target mencari **Ranting**, slot yang dipilih **WAJIB** mengandung kata `ranting`/`cabang`/`branch`/`twig`. **DILARANG KERAS** menyentuh slot tebang/kayu/balok/batang (`IsAnchorSlot == true`).
  - Kata generik spesies seperti `"pohon"`, `"tree"`, `"batu"`, `"rock"` dimasukkan ke dalam daftar hitam (*blacklist*) pencocokan kata tunggal.

### Timer Cooldown Slot & Stok
- Dalam Durango, slot pada roda aksi terkadang memiliki timer pendinginan lokal (`w.Data.Timer`). Jika timer ini aktif, jumlah stok sementara bernilai 0.
- **Trik Penting**: Bot harus mengecek `slotCooldownWait`. Jika slot masih memiliki sisa stok tetapi sedang cooldown beberapa detik, bot **harus menunggu cooldown tersebut** alih-alih langsung menyatakan node kosong dan pergi.

---

## 4. Mekanisme Terrain, Biome, & Navigasi Batas Map

### Struktur Chunk & Biome Durango
- Setiap pulau di Durango dibagi menjadi kisi-kisi chunk 3D (`TerrainChunkBase`) dengan ukuran $3200 \times 3200\text{ unit}$ ($32 \times 32\text{ meter}$).
- Setiap chunk memiliki matriks ubin (tile) $16 \times 16$, dengan ukuran ubin $200 \times 200\text{ unit}$ ($2 \times 2\text{ meter}$).
- **Biome Enum (`Shared.Region.Biome`):**
  - **Daratan Valid:** `TemperateForest`, `TropicalForest`, `Desert`, `Tundra`, `SnowField`, `Grassland`, `SwampMud`, `Volcanic`, `SandBeach`, `PebbleBeach`.
  - **Batas Map & Bahaya:** `ColdOcean` (10), `WarmOcean` (11), `Lava` (14), `Invalid` (15 atau -1).

### Mengapa Bot Sebelumnya Berjalan Lurus Menembus Batas Map?
1. Fungsi pengecekan rintangan lama hanya mengecek rintangan tebing bertopeng (`IsCollidableMasked`).
2. Lautan lepas bukan tebing bertopeng, sehingga bot menganggap laut adalah jalan daratan yang bisa dilewati.
3. Sektor laut yang belum pernah dikunjungi memiliki skor jejak langkah (*nav trail penalty*) sebesar `0`, sehingga algoritma pencari rute secara keliru memilih laut sebagai arah jelajah pulau berikutnya!
4. **Solusi Multi-Lapisan:**
   - Cek `terrain.GetChunkFromWorldPosition(worldPos) == null` $\rightarrow$ Luar batas chunk pulau.
   - Cek `biome == ColdOcean || biome == WarmOcean || biome == Invalid` $\rightarrow$ Samudra lepas.
   - Cek `terrain.GetWaterDepth(worldPos) > 0.45f` $\rightarrow$ Air laut dalam.
   - Memilih arah jelajah dengan memutar (*sweep heading*) menjauhi laut kembali ke arah pusat pulau.

---

## 5. Bytecode Patching & Trik IL Engine (`patch_mod.py`)

### 1. Batas Crafting 15 $\rightarrow$ 999
- Secara default, slider crafting Durango dibatasi maksimal 15 item di `ProductionProcessManager.OnQuantityChanged`.
- Bytecode patching mengubah instruksi `ldc.i4.s 15` menjadi `ldc.i4 999`, membuka kemampuan antrean produksi massal hingga 999 item tanpa batasan UI.

### 2. Anti-Stagger & Super Armor (`DamagedProcesser`)
- Saat dino menabrak atau mencakar, game memanggil `DamagedProcesser.Blow` dan `DamagedProcesser.KnockBack`.
- Patch menyisipkan flag statis `DamagedProcesser.DisableStagger`. Ketika bernilai `true`, kedua metode tersebut langsung melakukan `ret` tanpa menjatuhkan atau membuat karakter terhuyung-huyung (*super armor* permanen).

### 3. Dino Noclip (`LocalUnitPushArea.ProcessUnitPush`)
- Karakter dan dinosaurus memiliki area pendorong fisik kapsul (`LocalUnitPushArea`).
- Patch memotong `ProcessUnitPush`: saat berhadapan dengan dino, resolusi tabrakan dilewati (`ret`), memungkinkan karakter menembus tubuh dinosaurus untuk memukul dari belakang atau meloloskan diri dari kepungan tanpa tersangkut.

### 4. Fast Sniper Stance & Speed Normalization
- Saat menggunakan busur atau crossbow dalam "Sikap Sniper", Durango menurunkan kecepatan lari menjadi kecepatan jalan tempur (`BattleSpeed` ~250). Animator game memetakan kecepatan ini ke klip merangkak/merunduk jongkok (`Bow_Aim_Walk_*`, `Crossbow_Aim_Walk_*`).
- Patch menyisipkan sanitizer klip di `LocalMotionUpdater.TrySetMotionByStateClip` yang membelokkan animasi jongkok kembali ke animasi tempur berdiri tegak (`Bow_BattleRun`, `Crossbow_BattleRun`), serta mengalikan `PlayerController.ModPlayerSpeed` sebesar $1.7\times$ hanya selama bertarung.

---

## 6. Tips & Trik Developer / Modder Durango

1. **Kompilasi Tanpa Menutup Launcher:**
   - Anda bisa mengompilasi mod saat launcher terbuka. Namun saat game `XDLands.exe` sedang aktif berjalan di RAM, file `AutoGatherMod.dll` di folder Managed akan terkunci oleh sistem operasi Windows.
   - Tutup game terlebih dahulu sebelum menjalankan `build_mod.bat`.
2. **Hindari `System.Threading.Monitor.Enter(object, ref bool)`:**
   - Runtime Unity Mono (.NET 3.5) tidak mendukung konstruksi `lock (obj)` generasi baru C#.
   - Selalu jalankan logika antrean dan state di dalam loop utama Unity (`Update()`) yang secara alami berjalan single-threaded di main thread.
3. **Format Pengaturan INI:**
   - Simpan semua konfigurasi ke file `durango_settings.ini`.
   - Gunakan `CultureInfo.InvariantCulture` saat melakukan `float.TryParse` dan `ToString("F2")` agar tanda desimal titik (`.`) tidak tertukar dengan koma (`,`) pada komputer dengan regional Indonesia.
4. **Verifikasi Hash DLL Setelah Build:**
   - Gunakan PowerShell `Get-FileHash` untuk membandingkan hash SHA256 antara `durango_git\AutoGatherMod.dll` dan `game\XDLands_Data\Managed\AutoGatherMod.dll` untuk memastikan file yang dimainkan adalah versi terbaru.

