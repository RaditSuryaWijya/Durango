# Active Context

## Current Status (31 Oktober 2026 - Update Navigasi & Auto Eat)
1. **Perbaikan Navigasi Looping / Berputar di 1 Area:**
   - **Penyebab:**
     - `FindSafeDetourDirection` sebelumnya menggunakan variabel belok `_detourTurnSign` statis (+1.0f) sehingga karakter selalu berbelok searah jarum jam saat membentur lahar (wall-following effect).
     - Tidak ada memori jejak kaki (breadcrumb trail) sehingga area yang sudah dilewati tidak memiliki penalti.
     - `FindClosestTarget` dan `Update()` menolak secara instan target apa pun yang garis lurusnya memotong lahar, bahkan jika berjarak jauh dan dapat dikelilingi melalui daratan aman.
   - **Solusi yang Diterapkan:**
     - **Breadcrumb Trail Memory (`_navVisitedTrail`):** Merekam koordinat posisi berjalan karakter setiap detik. Posisi yang sering dilewati diberi penalti repulsi (repulsion field) dinamis hingga 3 menit.
     - **Symmetrical Scoring Detour:** Menguji kedua arah sudut belok (-35°, +35°, -65°, +65°, dst.) dan memilih sudut aman yang memiliki jejak kunjungan paling minim (unexplored terrain).
     - **8-Sector Compass Island Sweep:** Mode jelajah pulau mengevaluasi 8 arah mata angin dengan jarak jangkauan 12 meter dan memilih sektor yang paling belum pernah dijelajahi.
     - **Relaksasi Seleksi Target:** Jarak bahaya lahar diturunkan ke 1.5 meter (`IsTargetInOrNearLava`). Garis lurus ke target hanya dibatalkan jika sangat dekat (< 5-6m), sedangkan node jauh diberi penalti skor jarak agar bot tetap bisa memutari sungai lahar tanpa stuck/looping.

2. **Perbaikan Inisiasi Makanan (Smart Food Learn):**
   - **Penyebab:** Durango memanggil event `UseItemSucceed` (bukan `UseItemStarted`) untuk item konsumsi instan seperti makanan saat menerima `Messages.OK` dari server.
   - **Solusi yang Diterapkan:**
     - Memasang hook ganda pada `InventorySystem.UseItemSucceed` dan `InventorySystem.UseItemStarted`.
     - Ditambahkan auto-rehook otomatis jika player berpindah pulau/scene.
     - Ditambahkan antarmuka **1-Click Food Picker** di Menu Mod (Tab 0 - Status & Pengaturan -> Bagian Survival): Menampilkan tombol cepat `[Pilih: Nama Makanan]` untuk semua item makanan yang ada di dalam tas, sehingga pemain bisa langsung memilih makanan tanpa harus memakannya terlebih dahulu.
