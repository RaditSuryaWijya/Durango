# System Patterns & Architecture

## 1. Overall System Architecture

The mod suite operates at the core of the Unity runtime by injecting hooks into `Assembly-CSharp.dll` using Mono.Cecil, coupled with two standalone native mod assemblies:

```
[Unity Engine / XDLands Game Loop]
   │
   ├── XDLands.Mark.Ensure() (Bootloader Hook)
   │     └── AutoGather.AutoGatherLoader.Init()
   │           └── Creates GameObject "AutoGatherController" (MonoBehaviour)
   │
   ├── CombatSystem.OnAttackAlert(Messages.AttackAlerted msg, PacketHeader header)
   │     └── XDLands.CombatMods.AutoCounter.OnAttackAlert(msg.EntityId, msg.AttackTime, msg.EventAt)
   │           └── AutoCounterRunner (MonoBehaviour Queue) -> Trigger at AttackTime - 0.22s
   │                 └── TryCounter -> CombatSystem.UseBattleAction(spaceAction.Id)
   │
   ├── CombatSystem.OnDamaged(Messages.Damaged msg, PacketHeader header)
   │     └── XDLands.CombatMods.AutoCounter.OnDamaged(msg.AttackerId, msg.VictimId)
   │
   ├── UsingAction.Update()
   │     └── Intercepts Messages.UseBattleAction packets for Moving Attack
   │
   ├── LocalUnitPushArea.ProcessUnitPush()
   │     └── Bypasses pushing physics against dinosaurs (Dino Noclip)
   │
   ├── DamagedProcesser.Blow() / KnockBack()
   │     └── Checks DamagedProcesser.DisableStagger to negate stagger loss-of-control
   │
   └── PlayerController.get_MoveSpeed()
         └── Dynamically scales player vs mount move speed
```

## 2. Auto-Counter Stance Engine Design

### Key Classes
- `XDLands.CombatMods.AutoCounter`: Static coordinator that verifies stance (`XdStance.Current`), weapon class (`XdStance.WeaponClass`), identifies the Space key action (`ActionSlots[0]` or `xd_counter`), calculates impact lead times, and executes combat commands.
- `XDLands.CombatMods.AutoCounterRunner`: Persistent `MonoBehaviour` (`DontDestroyOnLoad`) managing an allocation-free timing queue of pending alerts. Runs on Unity's main thread every frame.

### Predictive Impact Timing Formula
1. When `CombatSystem.OnAttackAlert` fires:
   - Extract `AttackTime` (predicted server timestamp of damage impact) and `EventAt` (timestamp when windup began).
   - Read current predicted server time: `serverTime = Connections.Frontend.GetPredictedServerTime()`.
   - Calculate remaining time: `timeUntilImpact = (float)(AttackTime - serverTime)`.
2. Compute execution delay:
   $$\text{Delay} = \text{timeUntilImpact} - \text{LeadTime}$$
   Where $\text{LeadTime} = 0.22\text{s}$ (the sweet spot that accounts for packet travel, animation initiation, and places the active parry frame right at $t = 0$).
3. If $\text{Delay} \le 0.05\text{s}$: Execute immediately.
4. If $\text{Delay} > 0.05\text{s}$: Add/update in `AutoCounterRunner._queue`.
5. On trigger:
   - Check if attacker is alive and within plausible reach ($\le 12\text{m}$).
   - Smoothly rotate character to face the attacker: `localPlayer.TurnToYaw(yaw, true)`.
   - Select attacker target: `cs.SelectTarget(attacker)`.
   - Dispatch counteraction: `cs.UseBattleAction(spaceAction.Data.Id)`.

### Strict Exclusion of Basic Attack Fallback
Previous revisions fell back to basic weapon strikes (`onehand_default_a/b/c`) when the Space action was on cooldown or not immediately usable. This was identified as an anti-pattern because basic attacks interrupt defensive stances and leave the player vulnerable. The engine now strictly executes only the Space counter parry (`xd_counter` / `onehand_dodge`).

## 3. Mono Runtime Threading & Compatibility Pattern
- **Critical Finding**: Unity's older Mono runtime (.NET 3.5 / 2.0 subset) lacks `System.Threading.Monitor.Enter(object, ref bool)` introduced in .NET 4.0.
- **Rule**: Never use C# `lock (...)` blocks in assemblies targeting Unity Mono. Because Unity's game loop (`Update`, `OnAttackAlert`, etc.) executes exclusively on the main thread, queues can be managed synchronously without mutexes or locks.
- **Compiler**: All code is compiled using `C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe`.

## 4. Bow & Crossbow Fast Sniper Stance Architecture (Anti-Crouch & Speed Normalization)

### Core Mechanics of the Sniper Crouch Bug/Limitation
In the XDLands combat stance system, entering "Sikap Sniper" sets `XdStance.Current` to `"ranged_keep_distance"`.
1. **Movement Speed Penalty**: During combat, `PlayerController.get_MoveSpeed()` falls back to `_baseMoveSpeed.Value.BattleSpeed` (~250), which halves base movement speed from normal exploration run (`NormalSpeed` = 500) or battle run (`BattleRun` = 600).
2. **Animator State Degradation**: `LocalMotionUpdater.GetStateConditionArguments()` feeds this lower `MoveSpeed` (~250) into `PlayerAnimationStateInfo.Get(args)`. In `resources.assets`, clips with `targetSpeed` ~250 map to crouch walks (`Bow_Aim_Walk_*`, `Crossbow_Aim_Walk_*`, `Bow_Walk_N`, `Crossbow_Walk_N`) and idle crouches (`Bow_AimStand`, `Crossbow_AimStand`).
3. **Player Experience Impact**: Characters crouch down, crawl slowly, and feel sluggish when aiming/repositioning.

### Dual-Layer Solution
1. **Dynamic Velocity Normalization (`AutoGatherMod.UpdateMoveSpeedMultiplier`)**:
   - Detects `applyFastSniper = EnableFastSniperStance && isSniper && isInCombat`.
   - Combat state is strictly validated via `CombatSystem.Instance().CombatMode`: outside of combat, normal exploration speed (500) and animations remain untampered.
   - During combat, dynamically inspects `PlayerController._baseMoveSpeed.BattleSpeed` via reflection.
   - Multiplier tuned per user request: `speedRatio = 1.5f` (+50% velocity increase), ensuring upright battle mobility without excessive, unnatural speed.
   - Multiplies `PlayerController.ModPlayerSpeed` by `1.5f` only during battles without server desync.
2. **IL Engine Hook in `LocalMotionUpdater` (`patch_mod.py` Section 4e)**:
   - Injected `public static bool DisableSniperCrouch = false;`.
   - Injected `public static string SanitizeSniperClip(string clip)`:
     - Redirects `Bow_Aim_Walk*` and `Bow_Walk_N` to `Bow_BattleRun`.
     - Redirects `Crossbow_Aim_Walk*` and `Crossbow_Walk_N` to `Crossbow_BattleRun`.
     - Redirects `Bow_AimStand*` to `Bow_BattleStand`.
     - Redirects `Crossbow_AimStand*` to `Crossbow_BattleStand`.
   - Injected entry hook in `LocalMotionUpdater.TrySetMotionByStateClip` (`ldarg.1 -> call SanitizeSniperClip -> starg.s 1`).
   - Frame-level synchronization in `AutoGatherMod` sets `DisableSniperCrouch = applyFastSniper` dynamically during active combat in sniper stance and overrides `_currentMotionClip` if currently in crouch idle/walk.

## 5. Crossbow Accelerated Reload & MoveLock Protection Architecture

### Core Problem
1. **Crossbow Shooting & Cocking Mechanics**: In Durango, crossbow actions (`ranged_crossbow_default`, `ranged_crossbow_aimedshot`, `ranged_crossbow_quickshot`) execute a single animation clip (`Crossbow_Attack_Shoot` / `Crossbow_Attack_AimedShot`). The bolt is fired at `damage_time` (0.2s for default), and the remainder of the clip (0.2s - 1.8s) is the physical cocking/reload animation where the character pulls the bowstring with their foot/hands and slots a new bolt.
2. **Moving Attack Counter Blatancy ("Terlalu Mencolok")**: Previously, Moving Attack (`EnableMobileAttack`) forcibly set `inputSys.MoveLock = false` and allowed WASD input to clear `UsingAction`, while `RecoveryCut` truncated the action finish time to 0.02s. While this bypassed the reload delay, it made the character slide around or run immediately while shooting without ever cocking the crossbow, looking overly blatant/suspicious.

### Solution: Accelerated Stationary Reload (1.5x) + Reload MoveLock Protection
1. **Selective Moving Attack Immunity (`IsCrossbowAction`)**:
   - Moving Attack is strictly disabled **only** during active Crossbow attack/reload actions. Melee weapons and bows continue to benefit from full moving attack.
   - During crossbow actions:
     - `_uaDisableMoveLockField` is set to `false`.
     - `InputSystem.MoveLock` is maintained as `true` (locked in place).
     - WASD inputs during `UsingAction` state 3 do NOT call `Clear()`, ensuring the animation is never aborted or glitched into a slide.
     - `RecoveryCut` is bypassed for Crossbow actions.
2. **Physical Animation Acceleration (1.5x)**:
   - The moment the bolt is fired (`Time.time >= _crossbowActionStartTime + _crossbowDamageTime`), the animation playback rate on `PlayerBehavior.Anim["Crossbow_Attack_Shoot"]` and weapon equipment `PlayerBehavior._equipmentAnim` is boosted to `CrossbowReloadSpeed` (1.5f).
   - The character visibly, crisply, and rapidly cocks the bowstring in reduced duration (~1.07s instead of ~1.6s).
3. **Synchronized Action Completion**:
   - `_uaStateFinishTimeField` is clamped to `_crossbowActionStartTime + _crossbowDamageTime + (normalReloadDuration / 1.5f)`. Total duration drops from 1.8s to ~1.27s.
   - The exact moment the 1.5x reload animation completes, `UsingAction.Update` naturally finishes the action, unlocks `MoveLock`, and the player can instantly fire the next bolt or walk away.
4. **Settings & GUI Integration**:
   - Managed by `EnableFastCrossbowReload = true` and `CrossbowReloadSpeed = 1.5f`.
   - Persisted in `AutoGatherSettings.ini` and controllable via Combat GUI tab item `3c. Fast Xbow Reload`.

