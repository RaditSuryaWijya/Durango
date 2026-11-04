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


## 9. Modular Partial Class Subsystem Architecture (`Source/`)

### 1. Structural Decomposition & Motivation
Previously, the entire controller logic, UI rendering, network alert handlers, harvesting routines, and craft planning were contained within a single 14,065-line monolithic file (`AutoGatherMod.cs`). To eliminate cognitive overload, improve maintainability, and allow clean git diffs, the codebase was decomposed into a domain-driven modular structure:

```
Source/
├── Data/
│   └── AutoGatherModels.cs            # Enums & model data (Target, Filter, ESP, Craft)
├── Core/
│   ├── AutoGatherController.cs        # Entrypoint MonoBehaviour, state, Awake/Update
│   └── DoorstopEntrypoint.cs          # Loader entrypoint Doorstop
└── Features/
    ├── Combat/
    │   └── AutoGatherController.Combat.cs       # Auto Combat AI, Dodge, Burst, Counter
    ├── Gathering/
    │   └── AutoGatherController.Gathering.cs    # Engine panen, Smart Learn, node detect
    ├── Crafting/
    │   └── AutoGatherController.CraftPlanner.cs # Craft planner, resep hierarki, auto craft
    ├── ESP/
    │   └── AutoGatherController.EspRadar.cs     # Radar dino, kawah, ore, world-to-screen
    ├── Survival/
    │   └── AutoGatherController.Survival.cs     # Auto-eat stamina, loop lahar/air, stealth
    ├── Settings/
    │   └── AutoGatherController.Settings.cs     # Simpan & muat AutoGatherSettings.ini
    └── UI/
        └── AutoGatherController.UI.cs           # OnGUI window, tabs, HUD, dan tombol
```

### 2. C# Partial Class Pattern
- **Seamless State Access**: By designating `public partial class AutoGatherController`, all methods across `AutoGatherController.Combat.cs`, `AutoGatherController.Gathering.cs`, `AutoGatherController.UI.cs`, etc. retain 100% direct access to private fields, Unity lifecycle callbacks, and internal helper methods without requiring reflection, singleton accessors, or public exposure.
- **Zero Runtime Overhead**: The C# compiler combines all partial class declarations into a single unified type (`AutoGather.AutoGatherController`) at compile time. Memory layout, virtual method tables, and execution speed are 100% identical to the monolithic file.
- **Doorstop Loader Decoupling**: `Doorstop.Entrypoint` is isolated in its own file (`Source/Core/DoorstopEntrypoint.cs`), decoupling bootstrapper logic from controller logic.

### 3. Build & Compiler Integration
- **Compiler**: `C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe`.
- **Recursive Compilation**: Compiled with `/recurse:"%SCRIPT_DIR%Source\*.cs"`.
- **Backward Compatibility**: `build_mod.bat` dynamically detects if `Source/` exists and switches between `/recurse:"Source\*.cs"` and `"AutoGatherMod.cs"`.


## 10. Telegraph Hitbox Geometry & Durango Coordinate System

### 1. Root Cause of Previous Hitbox Detection Misses
Durango's engine visually renders red telegraph attack areas on client screens via `FillBorderAlert.MakeRectMesh` and `AreaOfEffectVisualizer`. Decompilation of the mesh construction revealed Durango's mathematical coordinate conventions:
- **Forward Vector**: $V_1 = (\sin(	ext{yaw}), \cos(	ext{yaw}))$. Points directly forward along the dinosaur's attack direction.
- **Lateral Vector**: $V_2 = (V_1.y, -V_1.x) = (\cos(	ext{yaw}), -\sin(	ext{yaw}))$. Points perpendicular to the right.
- **Rectangle Half-Extents (`RectSizeHalves`)**:
  - `RectSizeHalves.x` is the **Forward half-length** ($L/2$).
  - `RectSizeHalves.y` is the **Lateral half-width** ($W/2$).
Previous implementations had $x$ and $y$ swapped, treating $x$ as width and $y$ as length. Consequently, forward charge/bite rectangles were rotated 90 degrees in math, causing players standing directly in front of the dinosaur to register as "outside", while players standing at the dinosaur's flanks registered as "inside".

### 2. Precise Hitbox Math Implementation
- **Oriented Bounding Box (OBB)**:
  $$\Delta = 	ext{playerPos} - 	ext{center}$$
  $$d_{	ext{Forward}} = \Delta.x \cdot \sin(	ext{yaw}) + \Delta.z \cdot \cos(	ext{yaw})$$
  $$d_{	ext{Lateral}} = \Delta.x \cdot \cos(	ext{yaw}) - \Delta.z \cdot \sin(	ext{yaw})$$
  $$	ext{IsInside} = (|d_{	ext{Forward}}| \le 	ext{halfForward} + r_{	ext{body}}) \land (|d_{	ext{Lateral}}| \le 	ext{halfLateral} + r_{	ext{body}})$$
  Where $r_{	ext{body}} = 45	ext{ cm}$ (0.45m character collision capsule radius) prevents false "outside" boundaries when touching the red line.
- **Circular Arc Telegraphs**:
  $$	ext{arcSpan} = 	ext{Repeat}(	ext{endAngle} - 	ext{startAngle}, 360^\circ)$$
  $$	ext{playerOffset} = 	ext{Repeat}(	ext{playerAngle} - 	ext{startAngle}, 360^\circ)$$
  $$	ext{InArc} = (	ext{playerOffset} \le 	ext{arcSpan} + 4^\circ) \lor (	ext{playerOffset} \ge 360^\circ - 4^\circ)$$
  A $4^\circ$ angular tolerance ensures perimeter sweep attacks are never dropped due to floating-point truncation.


## 11. Action Overwrite (Bypass Animasi) & Combat Cancel Mechanics

### 1. Problem: Animation Lock During Enemy Attacks
In vanilla Durango, using any battle action sets `CombatSystem.UsingAction`. While an attack swing is playing, `CombatSystem.IsUsableAction` returns `false` for any other action, locking the character in the swing animation until `ProhibitedUntil` expires. If a dinosaur launches a sudden attack during this window, pressing Space or triggering defense fails, resulting in unavoidable damage.

### 2. Action Overwrite Pipeline
To allow immediate evasive maneuvers (Counter Attack or Dodge Roll) without animation lock or freezing, the Action Overwrite engine executes:
1. **Clear Active Action**: `cs.UsingAction.Clear()` to wipe out the current attack.
2. **Reset Execution Prohibitions**: `spaceAction.ProhibitedUntil = serverTime - 1.0` so that `IsUsableAction` evaluates to `true` instantly.
3. **Flush Motion Queue**: `PlayerController.MotionUpdater.ClearReservedMotions()` to prevent stale root motion packets.
4. **Halt Active Clip**: `localPlayer.Anim.Stop()` to release the animator lock.
5. **Dispatch Target Action**: `cs.UseBattleAction(actionId)`.
6. **Force State 2 (Execute)**: Via reflection on `UsingAction._state`, set value to `2` (Execute). This bypasses the pre-attack rotation lock and startup pause.
7. **Instant Visual Crossfade**: `ForcePlayCounterMotion(localPlayer, action)` to crossfade the 3D character mesh directly into the counter/roll animation without idle freeze frames.


## 12. Split Alert Window vs Automated Execution Timing

### 1. Human Visual Window vs Bot Execution Physics
- **Human Reflex Timing (0.80s)**:
  - Human eyes and motor reaction require ~0.25s - 0.35s to process visual flashing/audio cues and press Space.
  - Setting the HUD banner, button glow, and audio cue at **0.80s** before damage impact provides the optimal reaction window for manual play.
- **Bot Execution Timing (0.30s - 0.35s)**:
  - A software bot reacts in 0ms (instantaneously on frame).
  - If a bot executes Dodge Roll at 0.80s: The roll animation lasts ~0.65s. The roll finishes at $t = -0.15	ext{s}$, leaving the character standing completely still right as the dinosaur's attack lands!
  - If a bot executes Counter Attack at 0.80s: The active parry window (~0.35s) expires before the dinosaur's claws make contact.
  - Furthermore, acting at 0.80s wastes ~0.5s of free attack time where the player could have landed another hit.

### 2. Implementation: Decoupled Timing Channels
1. **Peringatan Visual/Audio (`CounterAssistWindowMax`, Default: 0.80s)**:
   Controls HUD banner, button glow, and `PlayCounterAssistSound()`. 100% preserved for human awareness.
2. **Eksekusi Otomatis (`AutoCounterTriggerTime`, Default: 0.30s)**:
   Bot holds until $t \le 0.30	ext{s}$, ensuring the weapon raises directly into the impact clash for a perfect stagger.
3. **Eksekusi Otomatis Roll (`AutoDodgeTriggerTime`, Default: 0.35s)**:
   Bot holds until $t \le 0.35	ext{s}$. The character executes roll, covering 2-3 meters outward with active iframes exactly when the attack hits at $t = 0.0	ext{s}$.


## 13. Resource Node Interaction & Slot Isolation Architecture

### 1. Problem: Accidental Tree Chopping on Twigs
Harvestable nodes in Durango (such as Ash Trees / "Pohon Abu") contain multiple interaction slots on a single radial menu wheel:
- Slot A: "Ambil Ranting Pohon Abu" (Twigs / Secondary)
- Slot B: "Petik Daun Pohon Abu" (Leaves / Secondary)
- Slot C: "Tebang Pohon Abu" / "Ambil Kayu Pohon Abu" (Wood / Anchor)
Previously, the word `"pohon"` was included in anchor search keywords. Because the target was `"Ranting Pohon Abu"`, it was incorrectly flagged as wanting an anchor (`targetWantsAnchor = true`). When twigs ran out, word-level matching matched `"pohon"` and `"abu"` on the wood slot, causing the bot to chop down the tree instead of walking to the next Ash Tree!

### 2. Strict Mutual Exclusion Rules
`DoesWidgetMatchTargetItem` enforces strict part categorization:
1. **Secondary vs Anchor Mutual Exclusion**:
   - If target wants **Ranting** (`ranting`, `cabang`, `branch`, `twig`, `stick`, `dahan`), slot **MUST** be Ranting and is **STRICTLY FORBIDDEN** from selecting Anchor slots (`IsAnchorSlot(w)`).
   - If target wants **Daun**, slot **MUST** be Daun and is **STRICTLY FORBIDDEN** from selecting Anchor slots.
   - If target wants **Kulit** / **Buah** / **Biji** / **Getah** / **Bunga** / **Kerikil**, Anchor slots are strictly rejected.
2. **Anchor Strict Protection**:
   - If slot is an Anchor (`tebang`, `kayu`, `balok`, `batang`, `quarry`, `tambang`), it **CANNOT** be matched unless the target specifically wants an Anchor item.
3. **Generic Species Name Blacklist**:
   - Generic category words (`pohon`, `tree`, `batu`, `rock`, `node`, `tanaman`, `semak`, `besar`, `kecil`, `alam`) are barred from single-word substring matches. A match on the species name (e.g. `"abu"`) alone is insufficient without matching the harvestable part.
4. **No Fallback to Arbitrary Slots**:
   - When a targeted secondary resource is depleted on a node, the bot halts interaction on that node immediately (`break;`) and navigates to the next node in the world.


## 14. Terrain, Island Chunks, Biome Map Boundaries, and Navigation

### 1. Problem: Endless Walking into the Ocean / Map Boundary
Durango islands are finite chunks surrounded by ocean water. Previously, `IsObstacleAtPosition` only checked `terrain.IsCollidableMasked(worldPos)`. Because ocean water and empty ocean chunks are not solid cliffs, `HasObstacleAhead` returned `false`.
When the player cleared nearby land nodes, the anti-looping breadcrumb system (`GetNavTrailPenalty`) scored unexplored ocean sectors with a penalty of `0`. Consequently, the sweep engine consistently steered into the sea, getting stuck against the water barrier.

### 2. Multi-Layered Island Boundary & Terrain Detection
1. **Chunk Boundaries**:
   `TerrainBase.GetChunkFromWorldPosition(worldPos) == null` detects positions beyond the loaded island boundaries.
2. **Biome Validation**:
   `TerrainBase.GetTileBiome(worldPos)` checks for:
   - `Shared.Region.Biome.ColdOcean`
   - `Shared.Region.Biome.WarmOcean`
   - `Shared.Region.Biome.Invalid`
   - `(int)biome == -1`
   All of these represent off-island waters and are flagged as impassable obstacles.
3. **Water Depth Threshold**:
   `TerrainBase.GetWaterDepth(worldPos) > 0.45f` detects water deep enough to trigger swimming or drowning.
4. **Emergency Boundary Escape**:
   If character coordinates touch ocean or boundary tiles, `HandleEmergencyBoundaryEscape` executes an immediate 180° inland detour.
5. **Inland-Biased Detour**:
   `FindSafeDetourDirection` rejects all candidate angles facing the sea, directing the player exclusively towards dry land sectors.
