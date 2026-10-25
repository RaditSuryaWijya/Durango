# Project Progress & Changelog: Durango / XDLands PC

## 1. Feature Status & Verification

| Feature / Component | Status | Details / Verification |
| :--- | :--- | :--- |
| **AutoGather Mod Menu** | ✅ Verified | Draggable GUI, ESP 3D tags, Island Radar, hotkeys (`Insert` / `F11`) working smoothly. |
| **Smart Learning Gathering** | ✅ Verified | Learns target node on first manual gather; sweeps surrounding area by filter. |
| **Craft Quantity Bypass (999)** | ✅ Verified | IL-patched `ProductionProcessManager.OnQuantityChanged`; allows batches up to 999. |
| **Anti-Stagger & Knockback** | ✅ Verified | IL-patched `DamagedProcesser.Blow` and `KnockBack`; zero combat interruptions. |
| **Moving Attack (No MoveLock)** | ✅ Verified | Packet hook in `UsingAction.Update`; player moves freely during tebasan. |
| **Dino Noclip** | ✅ Verified | Bypasses `LocalUnitPushArea.ProcessUnitPush`; no stuck states in pens/fights. |
| **Dual Speed Multiplier** | ✅ Verified | Independent scaling for player (`1.0x` - `2.0x`) and mount (`1.8x` default). |
| **Manual Counter Cancel (Space)** | ✅ Verified | Manual 100% (No auto bot). Cancels active attack swings (Pukul/Hajar/Stab) in `melee_outfighting` when pressing Space. Validates cooldown (7s), stamina (35), and in-battle mode (`CombatMode`). Overwrites 3D model with 0.05s crossfade. |
| **Moving Guard (2-Handed)** | ✅ Verified | 2-handed Tangkis in `melee_defense`: move freely with WASD without lock or cancel. |
| **Fast Sniper Stance (Bow/Xbow)** | ✅ Verified | Anti-crouch animation sanitization + in-battle 1.5x speed multiplier in `ranged_keep_distance`. |
| **Fast Crossbow Reload**          | ✅ Verified | 1.5x accelerated cocking animation + stationary MoveLock protection (Moving Attack bypassed on reload). |
| **Mono .NET 3.5 Compatibility** | ✅ Verified | Removed `Monitor.Enter` / `lock`; clean compile with .NET 3.5 `csc.exe`. |
| **One-Click Installer / Uninstaller**| ✅ Verified | `install_mod.bat` and `uninstall_mod.bat` validated for cross-device deployment. |

---

## 2. Milestone History & Debugging Log

### Milestone 1: Mod Recovery after Client Update
- **Issue**: Client update wiped `Assembly-CSharp.dll`, removing hooks and causing mod menu to disappear.
- **Fix**: Re-ran Cecil patcher (`patch_mod.py`) targeting `XDLands.Mark.Ensure()` to re-inject `AutoGatherLoader.Init()`.

### Milestone 2: Initial Auto-Counter Implementation
- **Issue**: User requested auto-counterattack in Counter Stance with 1-handed weapons.
- **Initial Flaw**: Distance calculation used `distSqr <= 49f` which failed because Durango coordinates use 100 units = 1 meter (49 units squared = 0.49m). Counter also fell back to basic attack (`onehand_default_a/b/c`).
- **User Clarification**: "kok serangan baliknya hanya basic attack ya... serangan balik itu pake spasi" (Counter is done via Space key, not basic combo!).

### Milestone 3: Space Action Integration
- **Fix**: Routed primary action to `cs.ActionSlots[0]` (`xd_counter` / `onehand_dodge`). Removed basic attack combo fallback entirely.

### Milestone 4: Predictive Timing Calibration
- **Issue**: Counter triggered immediately upon receiving `OnAttackAlert`. For attacks with 1.5s - 2.7s windup, parry frames expired prematurely, resulting in player taking damage.
- **Fix**: Added `AutoCounterRunner` queue. Calculated `timeUntilImpact = AttackTime - ServerTime`. Trigger scheduled at `timeUntilImpact - 0.22s`.

### Milestone 5: Runtime MissingMethodException Resolution
- **Issue**: Runtime crashed with `Method not found: 'System.Threading.Monitor.Enter'` due to C# 4.0 `lock` keyword under Unity Mono .NET 3.5.
- **Fix**: Removed all `lock` constructs, switched compilation to .NET 3.5 `csc.exe`. Counter parry timing validated in live gameplay.

### Milestone 6: Moving Guard (2-Handed Weapons in Defensive Stance)
- **Issue**: In `melee_defense` ("Sikap Pertahanan"), using Tangkis (crossing arms to parry) froze the character (`PlayerController.StopMove` + `InputSystem.MoveLock = true`), and any player movement triggered `CombatSystem.<Awake>m__7` which called `_usingAction.Clear()`, instantly cancelling the guard.
- **Side Effect Discovered**: Bypassing `PlayerController.StopMove()` in `UsingAction.Update` caused the automated target navigator (`LocalMoveNavigator`) to continue running toward the enemy during attack windups instead of halting at `UseRange`, making it feel like the character had to be glued/colliding with the target to hit ("nempel bgt sama target").
- **Fix**:
  1. Restored `PlayerController.StopMove()` so `LocalMoveNavigator` halts at the normal weapon reach (`UseRange`), keeping melee hitbox distance 100% normal. Only `InputSystem.set_MoveLock(true)` is bypassed.
  2. Scoped `CombatSystem.DisableMoveCancel` dynamically in `AutoGatherMod.cs`: it is now set to `true` **only** when actively executing Guard (`isGuarding == true`). For normal attacks and target approach, it remains `false` so normal target tracking, range, and cancellation behave naturally.
  3. Added `IsGuardOrDefense` to `AutoGatherMod.cs` to prevent the post-attack cancel routine from dropping the defensive state during WASD movement.

### Milestone 7: Fast Sniper Stance (Bow & Crossbow Anti-Crouch and Normal Run Speed)
- **Issue**: In `ranged_keep_distance` ("Sikap Sniper"), using Bow or Crossbow forced the character into crouched aim idle (`Bow_AimStand` / `Crossbow_AimStand`) and slow crouched aim walks (`Bow_Aim_Walk_*`, `Bow_Walk_N`), moving at `BattleSpeed` (~250). The user requested removing the slow crouch so the character moves and stands upright at full speed.
- **Fix**:
  1. Engine hook via `patch_mod.py` (Section 4e): Injected `LocalMotionUpdater.DisableSniperCrouch` and `SanitizeSniperClip`, redirecting crouch walks and idles to `Bow_BattleRun`, `Crossbow_BattleRun`, `Bow_BattleStand`, and `Crossbow_BattleStand`.
  2. Mod layer in `AutoGatherMod.cs`: Dynamically scales `PlayerController.ModPlayerSpeed` by `600f / BattleSpeed` when `XdStance.Current == "ranged_keep_distance"`, restoring full run speed.
  3. Added settings persistence (`EnableFastSniperStance=true`) and GUI toggle under Combat tab (`3b. Fast Sniper Stance (Bow/Xbow)`).

### Milestone 8: Client Update & Automated Disaster Recovery Rebuild
- **Issue**: Game launcher updated the Durango/XDLands client at 20:30, completely refreshing the `game/` folder with fresh vanilla binaries.
- **Recovery & Rebuild**:
  1. Cloned repository into persistent directory outside game folder (`C:\Users\Radit\AppData\Local\XDLands\PC\durango_git`).
  2. Created backup `Assembly-CSharp.dll.original` of the fresh client assembly.
  3. Recompiled `XDLands.AutoCounter.dll` via .NET 3.5 `csc.exe`.
  4. Executed `patch_mod.py`, successfully re-injecting all 12 Cecil hooks (bootloader, 999 craft limit, anti-stagger, moving attack, moving guard, dino noclip, dual speed, fast sniper stance, and auto-counter hooks).
  5. Recompiled `AutoGatherMod.cs` against the patched assembly with zero errors.
  6. Optimized `build_mod.bat` build sequence so future automated or manual builds always run cleanly on fresh updates.
  7. Updated pre-patched `Release/` binaries and committed to GitHub.

### Milestone 9: Dino Red Attack Area (Hitbox Telegraph) Restoration & In-Battle Validation
- **Issue 1 (Red Dino Hitbox Telegraph Disappeared)**: In `CombatSystem.OnAttackAlert`, instructions pushing 3 parameters (`EntityId`, `AttackTime`, `EventAt`) collided with an older 1-parameter signature in `AutoCounter.OnAttackAlert`, causing an `InvalidProgramException` at runtime whenever a dinosaur started an attack. This stopped `AreaOfEffectVisualizer.ShowAttackAlerted` from being called, making the red attack alert / hitbox on the ground disappear.
- **Fix 1**: Restored the timing queue in `AutoCounter.cs` with matching 3-parameter signature (`OnAttackAlert(string, double, double)`), provided a 1-parameter fallback overload, and made `patch_mod.py` parameter-count aware. Stack height at `ret` is now 0, completely restoring `AreaOfEffectVisualizer` red attack indicators.
- **Issue 2 (Fast Sniper Stance Over-Duplicating Speed Everywhere)**: The speed multiplier (2.4x) was applied whenever `XdStance.Current == "ranged_keep_distance"`, even outside battle during normal exploration where characters don't crouch.
- **Fix 2**: Added `isInCombat` validation using `CombatSystem.Instance().CombatMode`. Fast sniper speed normalization and anti-crouch animation sanitization now strictly trigger **only** when actively in battle (`applyFastSniper = EnableFastSniperStance && isSniper && isInCombat`). Outside combat, exploration speed and animations remain untouched.

### Milestone 10: Sniper Speed Tuning (1.5x) & Stationary Accelerated Crossbow Reload (2.0x)
- **User Request**:
  1. Reduce Fast Sniper Stance combat speed multiplier to 1.5x ("jadi 1.5 aja jgn cpt cpt").
  2. Crossbow reload/cocking animation was previously cancelled by Moving Attack (which looked too blatant/suspicious). The user requested accelerating the reload animation itself, while preventing Moving Attack from cancelling or bypassing the reload animation ("moving attack tidak mempengaruhi animasi reload ini, ingat hanya untuk animasi reload ya").
- **Implementation & Verification**:
  1. **Sniper Speed Normalization**: Set `targetPlayerSpeed = MoveSpeedMultiplier * 1.5f;` during combat in `UpdateMoveSpeedMultiplier()`.
  2. **Crossbow Reload MoveLock Protection**:
     - Added `IsCrossbowAction(BattleAction)` helper to isolate Crossbow attacks (`ranged_crossbow_default`, `ranged_crossbow_quickshot`, `ranged_crossbow_aimedshot`).
     - During Crossbow actions, `_uaDisableMoveLockField` is kept `false`, and `inputSys.MoveLock = true` is maintained so the character plants their feet and does not slide.
     - WASD input during `UsingAction` state 3 explicitly skips `UsingAction.Clear()` for Crossbow actions, preventing animation cancellation.
     - `RecoveryCut` is bypassed specifically for Crossbow actions so the physical reload animation is not cut to 0.02s.
     - Melee weapons and bows are completely unaffected and retain full moving attack.
  3. **Animation & Duration Acceleration (1.5x)**:
     - When `Time.time >= _crossbowActionStartTime + _crossbowDamageTime` (bolt fired), `PlayerBehavior.Anim["Crossbow_Attack_Shoot"].speed` and `PlayerBehavior._equipmentAnim` speed are boosted to 1.5x per user tuning.
     - `_uaStateFinishTimeField` is clamped to finish synchronously at `startTime + damageTime + (normalReload / 1.5f)` (reducing default 1.8s action to ~1.27s).
     - Once 1.5x reload completes, `MoveLock` unlocks naturally and the player is free to run or shoot again.
  4. **Persistence & GUI**:
     - Added `EnableFastCrossbowReload = true` and `CrossbowReloadSpeed = 1.5f`.
     - Integrated into `AutoGatherSettings.ini` and Combat GUI tab `3c. Fast Xbow Reload`.

### Milestone 11: Fix Auto-Approach Chase Freeze & Add Instant WASD Chase Cancellation
- **Issue**: Attacking a dinosaur outside range caused the player to freeze in place without chasing the target, and WASD inputs could not break the player out of the stuck state.
- **Root Cause**:
  1. `isCrossbowBusy` evaluated `hasActiveAction = true` during `State.Prepare (1)` (auto-approach state), causing `InputSystem.MoveLock = true` to be set prematurely before the character was in attack range. In `LocalMoveNavigator.MoveToPosition`, having `MoveLock == true` caused it to return immediately without moving towards the dino.
  2. `_csDisableMoveCancelField` was set to `true` during `State.Prepare`, preventing `CombatSystem.<Awake>m__7` from clearing `UsingAction` when WASD was pressed.
  3. `_crossbowActionStartTime` was initialized at click time (during chase) rather than when `State.Using (3)` actually began.
- **Fix**:
  1. Restricted `isCrossbowReloading` and MoveLock enforcement strictly to `uaState == 3` (State.Using) and after `damage_time`. During `uaState == 1` (`Prepare`), `MoveLock` and `DisableMoveCancel` remain strictly `false`.
  2. `_crossbowActionStartTime` is now recorded on the exact frame `State.Using` begins.
  3. Added explicit WASD manual cancel handler for `uaState == 1`: touching WASD while chasing instantly clears `UsingAction` and gives the player 0ms manual control.

### Milestone 12: Upgrade Fast Sniper Stance Speed Multiplier to 1.7x with GUI Presets
- **User Request**: "kecepatan jalan mode sniper nya brp?masih 1,5? coba naikin ke 1,7"
- **Implementation**:
  1. Updated base sniper stance velocity calculation from 1.5x (+50%) to 1.7x (+70%):
     ```csharp
     float speedRatio = (FastSniperSpeedMultiplier > 0.1f) ? FastSniperSpeedMultiplier : 1.7f;
     targetPlayerSpeed = MoveSpeedMultiplier * speedRatio;
     ```
  2. Created dedicated `FastSniperSpeedMultiplier = 1.7f` field with full configuration persistence in `AutoGatherSettings.ini` (`FastSniperSpeedMultiplier=1.7`).
  3. Upgraded Combat GUI item `3b. Fast Sniper Stance` with dynamic badge display (`AKTIF [1.7x]`) and preset buttons (`1.5x`, `1.7x`, `2.0x`) allowing real-time switching directly from the in-game GUI.

### Milestone 13: Auto Counter Toggle System & 1-Handed Hitbox Auto-Aimlock Enhancement
- **User Request**:
  1. "kenapa saat saya main senjata 1 tangan hit box saya terasakecil bgt?"
  2. "buat agar auto counter attack bisa di matiin"
- **Analysis of 1-Handed Hitbox Feeling**:
  1. Durango's vanilla design gives 1-handed weapons (knives, short swords, 1h maces) the smallest attack reach (`UseRange` ~1.2m - 1.5m) and narrowest frontal arc (~60°-90° cone) in the game compared to 2-handed weapons (~3.0m, 180°) or spears (~4.5m linear).
  2. With Moving Attack active, moving with WASD makes the character's facing angle follow the movement direction. Strafing sideways or backing away while swinging points the tiny frontal cone away from the dinosaur, causing complete misses (whiffs).
  3. Dino Noclip allows walking inside dino colliders, where swinging forward points away from the dinosaur's center of collision.
- **Implementation**:
  1. **Auto Counter Toggle**:
     - Added `public static bool Enabled = true;` to `XDLands.CombatMods.AutoCounter`.
     - Guarded `QueueCounter`, `AutoCounterRunner.Update`, `TryCounter`, `OnAttackAlert`, and `OnDamaged` with `if (!Enabled) return;`.
     - Added `EnableAutoCounter = true;` and `SetAutoCounterEnabled(bool)` in `AutoGatherMod.cs`.
     - Added Combat GUI control: `3d. Auto Counter (Sikap Melawan)` with interactive toggle button.
     - Persisted in `AutoGatherSettings.ini` (`EnableAutoCounter=true`).
  2. **Auto-Aimlock Orientation Snapping**:
     - Connected `EnableAutoAimlock` into `HandleCombatDPSMaximizer()`. During attack execution (`uaState == 3`), the character instantly turns to face the target dinosaur (`localPlayer.TurnToYaw(targetYaw, true)`).
     - Solves 1-handed moving attack misses: swings always align directly into the target dinosaur even while strafing with WASD.
     - Added Combat GUI control: `3e. Auto Aimlock (Hadap Target)`.








### Milestone 14: Fix Auto Counter Toggle & Dynamic INI Synchronization
- **Problem**:
  - In-game GUI toggle 'Matikan' on 3d. Auto Counter did not turn off counter attacks in combat.
  - Root Cause: One-way latch in SetAutoCounterEnabled failed during game boot because XDLands.AutoCounter was not loaded yet in AppDomain, permanently nullifying reflection toggle.
- **Solution & Fix**:
  1. AutoCounter.cs now directly reads AutoGatherSettings.ini via IsEnabled() cache every 0.5s.
  2. AutoGatherMod.cs removed the one-way latch, added fallback assembly loading, and added instant SaveSettings() on button click.
  3. Recompiled XDLands.AutoCounter.dll, patched Assembly-CSharp.dll, and built AutoGatherMod.dll. Synced to Release/.

### Milestone 15: Penghapusan Total Auto Aimlock & Counter Attack Animation Cancel
- **User Requests**:
  1. Hapus fitur Auto Aim Lock karena tidak terlalu berguna dan membuat karakter terasa berbelok/bergerak sendiri saat menyerang.
  2. Buat agar serangan balik (Counter Attack / Spasi) bisa digunakan kapan saja, termasuk saat sedang berada di tengah-tengah animasi menyerang (Combo/Swing).
- **Perubahan & Implementasi**:
  1. **Penghapusan Total Auto Aimlock**:
     - Menghapus field EnableAutoAimlock, logika rotasi otomatis localP.TurnToYaw() di HandleCombatDPSMaximizer, saving/loading di AutoGatherSettings.ini, dan kontrol tombol 3e. Auto Aimlock di GUI menu.
     - Karakter kini 100% bebas diarahkan secara manual tanpa tarikan/snap rotasi otomatis saat mengayunkan senjata.
  2. **Counter Attack Animation Cancel**:
     - Mengatasi pemblokiran native Durango ProhibitedUntil dan UsingAction saat menyerang.
     - Implementasi AutoCounter.TryManualSpaceCounterCancel() saat tombol Spasi ditekan pemain:
       - Membatalkan UsingAction.Clear() aksi tebasan yang sedang berjalan.
       - Menghapus status CurrentClipInfo pada PlayerController.MotionUpdater agar tidak ditolak oleh pengecekan motion Durango.
       - Mereset ProhibitedUntil = serverTime - 1.0 pada aksi Spasi (onehand_dodge / xd_counter).
       - Memanggil seketika CombatSystem.UseBattleAction untuk langsung memainkan animasi serangan balik (parry/counter stance) tanpa jeda.
     - Mengintegrasikan pembatalan animasi serangan yang sama ke dalam AutoCounter.TryCounter() sehingga counter otomatis juga dapat meng-cancel animasi tebasan yang sedang aktif.
  3. **Build & Release**:
     - Mengompilasi XDLands.AutoCounter.dll, mem-patch Assembly-CSharp.dll, mengompilasi AutoGatherMod.dll, dan menyinkronkan seluruh binary ke direktori Release/.
### Milestone 16: Penghapusan Total Auto Counter Bot & Hook Terkait
- **User Request**: "mod auto counter di hilangin aja terus push ke git", "knp di ui masih ada?"
- **Perubahan & Pembersihan**:
  1. Menghapus berkas `AutoCounter.cs`, `XDLands.AutoCounter.dll`, dan skrip `install_auto_counter.bat` secara permanen dari repository.
  2. Membersihkan `patch_mod.py`: Menghapus Bagian 6 yang menginjeksi hook `CombatSystem.OnDamaged` dan `CombatSystem.OnAttackAlert`.
  3. Membersihkan `AutoGatherMod.cs`: Menghapus tombol toggle `3d. Auto Counter (Sikap Melawan)` dan konfigurasi `EnableAutoCounter` dari GUI menu dan `AutoGatherSettings.ini`.
  4. Karakter tidak lagi melakukan auto-parry atau counter bot otomatis saat diserang dino.

### Milestone 17: Integrasi Manual Counter Attack Animation Cancel & Validasi Kesiapan Skill
- **User Requests**:
  1. "kenapa saat saya mengguankan skil pukul hajar atau pukul dari belakang saat animasi ketiga skil tsb saya tidak bisa menggunakan counter attack" -> "terapkan".
  2. "oke counter attacknya bisa namun animasi nya diam bisa ga langsung di timpa sama animasi counter attack".
  3. "jangan Begitu tombol Spasi ditekan langsung overide model kasih validasi juga apakah skilnya ready atau engga".
- **Implementasi**:
  1. **Integrasi ke AutoGatherMod.cs**: Menambahkan `HandleManualCounterCancel()` langsung ke dalam loop `Update()`.
  2. **Validasi Kesiapan Skill (`IsCounterSkillReady`)**:
     - Memeriksa Cooldown skill (7 detik): `serverTime < spaceAction.CooldownUntil && serverTime > spaceAction.CooldownSince`.
     - Memeriksa Stamina (membutuhkan stamina minimal ~35).
     - Memeriksa status pemain: `localPlayer.IsAlive`, `!localPlayer.IsRiding`, tidak ter-stun (`DamagedProcesser.ControllLostUntil`), dan kedalaman air `WaterDepthLevel < 2`.
     - **Prinsip**: Jika skill belum siap, animasi pukulan dibiarkan berjalan dan TIDAK di-cancel, serta model 3D tidak di-override.
  3. **Mengatasi Animasi Freeze / Diam (`ForcePlayCounterMotion`)**:
     - Membatalkan aksi lama (`ua.Clear()`), mereset `spaceAction.ProhibitedUntil = serverTime - 1.0`, dan menghentikan animasi lama (`localPlayer.Anim.Stop()`).
     - Mengubah state ke eksekusi seketika (`UsingAction._state = 2`) via reflection agar tidak menunggu jeda rotasi.
     - Meng-crossfade animasi model 3D langsung (`Onehand_Dodge`) dengan durasi transisi 0,05s sehingga pose bertransisi mulus tanpa karakter membeku berdiri.

### Milestone 18: Client Update Recovery & Deteksi Otomatis Patcher
- **Kondisi**: Game launcher memperbarui client game ke versi baru (`Assembly-CSharp.dll` 6.79 MB).
- **Peningkatan Patcher (`patch_mod.py` & `build_mod.bat`)**:
  1. `patch_mod.py` kini mendeteksi otomatis jika `Assembly-CSharp.dll` adalah versi official yang baru di-download oleh launcher (belum terinjeksi `LoadAutoGather` / `DisableStagger`).
  2. Otomatis memperbarui backup `Assembly-CSharp.dll.original` dari versi client baru.
  3. Seluruh 11 patch engine sukses diterapkan ke client baru.
  4. `AutoGatherMod.cs` berhasil dikompilasi ulang (.NET 3.5) terhadap client baru dan otomatis menyinkronkan folder `Release/`.

### Milestone 19: Validasi Battle Mode (`CombatSystem.CombatMode`)
- **User Request**: "untuk Manual Counter Attack Animation Cancel tambahin validasi lagi agar membaca kondisi sedang batle mode atau engga soalnya saya sedang tidak dibatle mod menekan spasi malah counter attack".
- **Penyebab**: Sebelum validasi ini, pemain yang berjalan di luar pertempuran dan menekan Spasi tetap memicu pemanggilan `UseBattleAction` jika sedang memegang senjata 1-Hand dalam stance `melee_outfighting`.
- **Solusi**:
  1. Menambahkan validasi `if (!cs.CombatMode) return;` di baris awal `HandleManualCounterCancel()`.
  2. Menambahkan validasi `if (!cs.CombatMode) return false;` di dalam `IsCounterSkillReady()`.
  3. Saat berada di luar mode tempur (eksplorasi/gathering biasa), penekanan tombol Spasi diabaikan oleh mod sehingga kontrol damai (roaming/jumping) berjalan normal seperti game vanilla.
