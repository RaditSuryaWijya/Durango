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
   - Multiplier tuned per user request: `FastSniperSpeedMultiplier = 1.7f` (+70% velocity increase), ensuring upright battle mobility with snappy repositioning.
   - Multiplies `PlayerController.ModPlayerSpeed` by `1.7f` only during battles without server desync.
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

## 6. Auto-Approach Chase & Combat Navigation Pattern

### Problem (Out-of-Range Attack Freeze Bug)
When a player initiates an attack on an enemy outside of weapon reach, Durango sets `UsingAction.State = Prepare (1)`. In this state, `UsingAction.Update()` executes `PlayerController.Instance().MoveToPosition(targetPos)` every frame to chase down the target until within hitbox range.
- `LocalMoveNavigator.MoveToPosition()` contains:
  ```csharp
  if (InputSystem.Instance().MoveLock) return;
  ```
- If `MoveLock` was asserted globally whenever an action was active, `MoveToPosition` returned immediately without pathfinding or moving, causing the character to freeze on the spot.
- Furthermore, if `DisableMoveCancel = true` was active, the vanilla WASD cancel hook (`CombatSystem.<Awake>m__7`) was suppressed, leaving the character completely paralyzed and unable to move via WASD.

### Architectural Solution
1. **Strict State Machine Separation**:
   - `State.Prepare (1)`: Target chase phase. `InputSystem.MoveLock` is strictly forced `false`, and `DisableMoveCancel` is strictly `false`.
   - `State.Ready (2)`: Arrived at attack radius; transitions on next frame.
   - `State.Using (3)`: Attack animation and packet execution. Crossbow reload lock and 1.5x speed acceleration ONLY activate here, after `_crossbowDamageTime`.
2. **Instant WASD Chase Cancellation (0ms Latency)**:
   - When in `State.Prepare (1)` (chasing), pressing any directional movement key (WASD) immediately detects player intent to abort the chase:
     ```csharp
     cs.UsingAction.Clear();
     PlayerController.Instance().CancelReserveMotion();
     ```
   - Instantly frees the character to manual control with zero delay or rubberbanding.

## 7. Counter Assist Architecture & Spatial World-to-Screen Projection

### Core Components
1. **Network Alert Hook**:
   - Intercepts incoming `Messages.AttackAlerted` packets dispatched by the server when a dinosaur begins its attack windup.
   - Enqueues into `_activeAttackAlerts` storing `EntityId`, `AttackTime` (predicted impact timestamp), `EventAt` (windup start), and local receipt time.
2. **Server-Time Synchronization**:
   - Queries `Connections.Frontend.GetPredictedServerTime()` each frame to ensure network latency compensation.
   - Computes remaining time until impact: `timeUntilHit = (float)(a.AttackTime - serverTime)`.
3. **Target-Specific Filtering (`CounterAssistTargetOnly`)**:
   - Resolves active combat target:
     ```csharp
     DamageableEntity combatTarget = (cs != null && cs.Target != null && cs.Target.Value != null && cs.Target.Value.IsAlive) 
         ? cs.Target.Value 
         : _currentCombatTarget;
     ```
   - When `CounterAssistTargetOnly == true` and `combatTarget != null`:
     - Checks `attacker == combatTarget || msg.EntityId == combatTarget.GetEntityId()`.
     - Completely suppresses alerts from un-targeted dinosaurs in the area.
4. **Spatial 3D-to-2D UI Projection (`CounterAssistUnderCharacter`)**:
   - Durango's root character transform is situated at the character's feet on the ground.
   - Converts world position into viewport coordinates:
     ```csharp
     Vector3 sp = Camera.main.WorldToScreenPoint(localPlayer.transform.position);
     ```
   - Inverts Y coordinate for Unity IMGUI (where Y = 0 is top of screen):
     ```csharp
     float charScreenX = sp.x;
     float charScreenY = Screen.height - sp.y;
     ```
   - Positions the alert banner horizontally centered at `charScreenX - bannerW * 0.5f` and vertically at `charScreenY + 18f` (floating right under feet).
   - Clamps coordinates to `[10, Screen.width - bannerW - 10]` and `[10, Screen.height - bannerH - 10]`.
5. **Two-Phase Visual & Audio Alert Progression**:
   - **Phase 1 (Windup Telegraph, > 0.38s)**: Amber banner showing dinosaur name and countdown: `⌛ Megalosaurus BERSIAP (1.5s)`.
   - **Phase 2 (Golden Counter Window, <= 0.38s)**:
     - If Counter Ready: Pulsing Gold/Cyan border `⚡ [SPASI] COUNTER! (0.35s) ⚡` with single audio cue `UISound.ClickType.ButtonBattle`.
     - If Counter on Cooldown / Low Stamina: Pulsing Orange/Red border `⚠️ [SPASI] DODGE ROLL! (CD) (0.35s) ⚠️`.
     - Bottom progress bar smoothly empties/fills with remaining window milliseconds.

## 8. Automated Resource Loop Architecture (Lava & Water)

### Core Loop State Machine (`UpdateAutoLootLava` & `UpdateAutoLootWater`)
1. **Container Inventory Scanning**:
   - Scans player inventory for eligible containers:
     - Lava: `bucket_metal`, `bucket_iron`, or custom heat-resistant containers.
     - Water: `bottle_*`, `waterskin_*`, `pot_*`, `bucket_*`, `cup_*`.
   - Checks container capacity: `curCount < maxCount`.
2. **Action Dispatch & Animation Synchronization**:
   - Triggers `InteractionSystem.Draw(targetNode, containerItem)`.
   - Listens to `InteractionSystem._drawTimer` and `TimerSystem.IsRunning()` to synchronize with the character's physical drawing animation.
   - Advances to the next container automatically when drawing completes.
3. **Safety Interlocks**:
   - Aborts loop instantly if player enters combat (`CombatSystem.CombatMode == true`).
   - Aborts loop if player moves > 4.5 meters from initial node.
   - Stops gracefully with notification when all inventory containers are 100% full.
