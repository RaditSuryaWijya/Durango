# Technical Context: Durango / XDLands PC

## 1. Technologies & Platforms
- **Game Engine**: Unity (Unity 2017 / 2018 custom build for PC).
- **Target OS**: Windows 10 / 11 (x64).
- **Runtime Environment**: Unity Mono Runtime (.NET 3.5 / 2.0 CLR subset).
- **Compilers**:
  - `C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe` (Used for compiling `AutoGatherMod.dll`).
- **IL Manipulation Library**:
  - `Mono.Cecil.dll` (v0.10+): Used in `patch_mod.py` via Python .NET / `pythonnet` (`clr.AddReference`).
- **Language**: C# 3.0 (Game mods), Python 3.12 (Patch scripts), Windows Batch (Build & Installers).

## 2. Directory Layout & Key Files

```
c:\Users\Radit\AppData\Local\XDLands\PC\game\
├── XDLands.exe                       # Main game executable
├── XDLands_Data\Managed\
│   ├── Assembly-CSharp.dll           # Core patched assembly
│   ├── Assembly-CSharp.dll.original  # Pristine backup
│   ├── AutoGatherMod.dll             # AutoGather, ESP, CraftPlanner, Combat & Counter Assist
│   └── UnityEngine*.dll              # Unity engine assemblies
└── durango_settings.ini              # Active configuration file (migrated from AutoGatherSettings.ini)

c:\Users\Radit\AppData\Local\XDLands\PC\durango_git\  # Local Git repository (remote: RaditSuryaWijya/Durango)
├── Source/                           # Modular C# source code (.NET 3.5 partial class)
│   ├── Data/
│   │   └── AutoGatherModels.cs       # Enums & model data
│   ├── Core/
│   │   ├── AutoGatherController.cs   # MonoBehaviour entrypoint & lifecycle
│   │   └── DoorstopEntrypoint.cs     # Doorstop bootstrapper
│   └── Features/
│       ├── Combat/AutoGatherController.Combat.cs
│       ├── Gathering/AutoGatherController.Gathering.cs
│       ├── Crafting/AutoGatherController.CraftPlanner.cs
│       ├── ESP/AutoGatherController.EspRadar.cs
│       ├── Survival/AutoGatherController.Survival.cs
│       ├── Settings/AutoGatherController.Settings.cs
│       └── UI/AutoGatherController.UI.cs
├── build_mod.bat                     # Master build script (compiles /recurse:Source\*.cs)
├── patch_mod.py                      # Cecil bytecode patcher
├── install_mod.bat                   # Portable one-click installer for other PCs
├── uninstall_mod.bat                 # One-click uninstaller (restores .original)
├── AutoGatherMod.dll                 # Compiled assembly
├── durango_settings.ini              # Configuration template
├── Release\                          # Pre-compiled distributable package
│   ├── Assembly-CSharp.dll
│   ├── AutoGatherMod.dll
│   └── durango_settings.ini
└── memory-bank\                      # Project documentation & context memory
```

## 3. Bytecode Injection Hooks Summary (`patch_mod.py`)
1. **Craft Limit 999**: Modifies `ProductionProcessManager.OnQuantityChanged` operand from `15` to `999`.
2. **Anti-Stagger**: Injects `ldsfld DamagedProcesser.DisableStagger` check into `DamagedProcesser.Blow` and `DamagedProcesser.KnockBack`.
3. **Lock Default Run Anim**: Patches `LocalMotionUpdater.LockDefaultRunAnim` to maintain sprint animations.
4. **Moving Attack**: Injects `OnAttackPacket` hook into `UsingAction.Update` and enables `DisableAttackMoveLock`.
5. **Dino Noclip**: Hooks `LocalUnitPushArea.ProcessUnitPush` to bypass push area resolution when targeting dinosaurs.
6. **Dual Speed Multiplier**: Patches `PlayerController.get_MoveSpeed` to apply separate multipliers for on-foot vs mounted states.
7. **Fast Sniper Stance**: Hooks `LocalMotionUpdater.TrySetMotionByStateClip` to redirect crouch idles/walks to standing battle animations (`Bow_BattleRun`, `Crossbow_BattleRun`).
8. **Mod Bootloader**: Injects `AutoGather.AutoGatherLoader.Init()` into `XDLands.Mark.Ensure()`.

## 4. Key Engine API Patterns & Nuances

### Coordinate Units & Hitbox Math
- **World Coordinate Scale**: Durango uses centimeters (100 units = 1 meter). Distances like 18m translate to `1800f`, and 20m to `2000f`. Never use small values like `4.5f` for world distance checks.
- **Hitbox Orientation (`FillBorderAlert.MakeRectMesh`)**:
  - `RectSizeHalves.x` = Forward half-extent ($L/2$) along Yaw.
  - `RectSizeHalves.y` = Lateral half-extent ($W/2$) perpendicular to Yaw.
  - Forward 2D vector: `Vector2(sin(yaw * Deg2Rad), cos(yaw * Deg2Rad))`.
  - Lateral 2D vector: `Vector2(fwd.y, -fwd.x)`.
- **Character Capsule Buffer**: Player body radius is ~45 units (0.45m). Must be added to half-extents when testing boundaries.

### Entity & Damage Identification
- **DamageableEntity Identifier**: Access entity ID via `.GetEntityId()` method, NOT property `.EntityId`. Access current targeted entity via `.TargetId` string property.
- **Combat Target Selection**: `CombatSystem.Instance().Target.Value` represents the locked combat target.

### Action State Machine & Action Overwrite
- **State Machine (`UsingAction._state`)**:
  - `_state = 0`: Idle / Uninitialized.
  - `_state = 1`: Windup / Startup / Rotation Lock.
  - `_state = 2`: Execute / Active Action.
- Setting `UsingAction._state = 2` via reflection allows bypassing the startup lock and executing instant combat cancels.

### Terrain, Chunks, and Biome Architecture
- **Terrain Instance**: `Durango.Utils.Singleton<Durango.Terrain.TerrainBase>.Instance()` or `UnityEngine.Object.FindObjectOfType<TerrainBase>()`.
- **Chunk Lookup**: `terrain.GetChunkFromWorldPosition(worldPos)` returns `TerrainChunkBase`. Returns `null` if the position is outside the loaded island boundaries.
- **Biome Retrieval**: `terrain.GetTileBiome(worldPos)` returns `Shared.Region.Biome`.
  - Ocean values: `Biome.ColdOcean` (10), `Biome.WarmOcean` (11), `Biome.Invalid` (-1 or 15).
  - Beach values: `Biome.SandBeach`, `Biome.PebbleBeach`.
- **Water Depth**: `terrain.GetWaterDepth(worldPos)` returns water depth in meters. Depths $> 0.45\text{m}$ cause character swimming or drowning.
- **Obstacle Mask**: `terrain.IsCollidableMasked(worldPos)` returns true if a terrain tile contains collidable cliffs/boulders.

### Radial Interaction Wheel
- **Widget Representation**: `Durango.UI.InteractionMenuWidget_PC`.
- **Action Texts**: `GetSlotActionText(w)` returns the localized action name.
- **Item Count**: `(w.Data.GatheringData != null) ? w.Data.GatheringData.Amount : w.Data.Count`. Must be $> 0$ for a valid gatherable slot.
- **Action Execution**: `InteractionMenu_PC._clickHotKeyMethod.Invoke(wheel, new object[] { slotIndex, Trigger.Up })` or `w.SetClick()`.

### Thread Safety & Mono Constraints
- Unity's older Mono runtime strictly forbids `System.Threading.Monitor.Enter(object, ref bool)`.
- Avoid C# `lock (...)` blocks; synchronize via the single-threaded Unity `Update()` loop.
- Pristine baseline: `Assembly-CSharp.dll.original` must be preserved at all times as the clean baseline for rebuilding patches.
