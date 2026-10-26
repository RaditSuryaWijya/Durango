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
├── AutoGatherMod.cs                  # Complete source code for all mod features
├── patch_mod.py                      # Cecil bytecode patcher
├── build_mod.bat                     # Master build script (.NET 3.5 csc + Python patcher)
├── install_mod.bat                   # Portable one-click installer for other PCs
├── uninstall_mod.bat                 # One-click uninstaller (restores .original)
├── AutoGatherSettings.ini            # Persistent configuration file
└── Release\                          # Pre-compiled distributable package
    ├── Assembly-CSharp.dll
    ├── AutoGatherMod.dll
    └── AutoGatherSettings.ini

c:\Users\Radit\AppData\Local\XDLands\PC\durango_git\  # Local Git repository (remote: RaditSuryaWijya/Durango)
├── AutoGatherMod.cs
├── AutoGatherMod.dll
├── AutoGatherSettings.ini
├── Release\
└── memory-bank\
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
- **Coordinate Units**: Durango world coordinates use centimeters (100 units = 1 meter). Distances like 18m translate to `1800f`, and 20m to `2000f`. Never use small values like `4.5f` for world distance checks.
- **DamageableEntity Identifier**: Access entity ID via `.GetEntityId()` method, NOT property `.EntityId`. Access current targeted entity via `.TargetId` string property.
- **World-to-Screen Mapping**:
  `Camera.main.WorldToScreenPoint(pos)` yields screen coordinates with Y=0 at screen bottom. Unity IMGUI requires Y inversion: `guiY = Screen.height - sp.y`.
- **Thread Safety**: Unity Mono runtime strictly forbids `System.Threading.Monitor.Enter(object, ref bool)`. Avoid C# `lock` blocks; synchronize via the single-threaded Unity `Update()` loop.
- **Preserve Pristine Baseline**: `Assembly-CSharp.dll.original` must be preserved at all times as the clean baseline for rebuilding patches.
