# Active Context

## Current Status (31 Oktober 2026 - Perbaikan Node Abandonment & Lava Freeze)
1. **Perbaikan Bot Meninggalkan Node yang Masih Ada Isinya:**
   - **Penyebab:**
     - Setelah mengambil 1 item, roda interaksi ditutup. Roda interaksi slot sering kali memiliki jeda cooldown pengisian ulang (`w.Data.Timer`). Sebelumnya slot ber-timer diabaikan dan dianggap node kosong/habis sehingga bot langsung menyatakan selesai dan memasukkan node ke blacklist selama 8 menit.
     - Deteksi `IsGatheringActive()` sebelumnya mengecek nama klip animasi `player.CurrentAnimClipName`, yang dalam engine Unity sering tetap tersangkut pada nama klip terakhir (`chop`/`gather`) meskipun panen sudah selesai. Hal ini menyebabkan polling terhambat dan memicu timeout gagal buka menu.
     - Syarat `w.Data.GatheringData.IsAvailableForGathering()` sebelumnya mewajibkan `BestPerformance > 0` yang baru dihitung setelah UI selesai.
   - **Solusi:**
     - Menjadikan `GatheringSystem.IsGathering` sebagai indikator otoritatif utama untuk status aktif panen.
     - Menambahkan deteksi `slotCooldownWait`: Jika slot target masih memiliki kuantitas namun sedang dalam timer cooldown, bot akan menunggu cooldown tersebut selesai lalu memanen kembali alih-alih meninggalkan node.
     - Menghapus pengecekan redundan `IsAvailableForGathering()` dan fokus pada kuantitas stok `w.Data.GatheringData.Amount > 0`.
     - Menambahkan reset interaksi target via `InteractionSystem.SetInteractionTarget(null)` jika percobaan buka menu pertama belum muncul.

2. **Perbaikan Bot Diam Saat Mendeteksi Lahar ("Awas Lahar"):**
   - **Penyebab:**
     - Pada setiap frame saat karakter terdeteksi di bibir lahar, fungsi memanggil `nav.Stop()` dan `controller.StopMove()` 60 kali per detik. Hal ini langsung mematikan perintah `MoveToPosition` sebelum karakter sempat melangkah sehingga karakter diam mematung di tempat.
     - Fungsi `HasLavaAhead` dan `IsPathBlockedByLava` mengecek offset radius 0.7m ke belakang pada setiap titik langkah awal (0.4m), sehingga jika karakter berada di tepi lahar, langkah ke arah daratan aman tetap dianggap terpotong lahar di belakangnya.
   - **Solusi:**
     - Dibuat sistem **`HandleEmergencyLavaEscape`** dengan *Escape Lock State* (`_lavaEscapeUntil = Time.time + 1.5f`). Selama manuver darurat ini, `nav.Stop()` dan `controller.StopMove()` DILARANG dipanggil per frame.
     - Bot mengevaluasi 16 sudut arah mata angin sejauh 3.5 meter dan memilih daratan solid dengan skor keamanan tertinggi (paling jauh dari lahar).
     - Karakter otomatis berlari menjauh sejauh 3.5 meter ke daratan aman secara kontinu tanpa terhenti/membeku.
