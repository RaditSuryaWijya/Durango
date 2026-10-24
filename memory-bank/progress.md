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
| **Stance Auto-Counter (Space)** | ✅ Verified | Detects 1-handed weapon + `melee_outfighting` stance; executes `xd_counter`. |
| **Predictive Parry Timing** | ✅ Verified | Computes `AttackTime - ServerTime`; delays execution to 0.22s before impact. |
| **Moving Guard (2-Handed)** | ✅ Verified | 2-handed Tangkis in `melee_defense`: move freely with WASD without lock or cancel. |
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
- **Fix**:
  1. Patched `CombatSystem.<Awake>m__7` with `CombatSystem.DisableMoveCancel` to bypass `_usingAction.Clear()` on `MoveStarted`.
  2. Patched `UsingAction.Update` so `DisableAttackMoveLock` skips both `StopMove()` and `MoveLock(true)`.
  3. Added `IsGuardOrDefense` to `AutoGatherMod.cs` to prevent the post-attack cancel routine from dropping the defensive state during WASD movement.
