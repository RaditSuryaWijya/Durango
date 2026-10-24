# Technical Context: Durango / XDLands PC

## 1. Technologies & Platforms
- **Game Engine**: Unity (Unity 2017 / 2018 custom build for PC).
- **Target OS**: Windows 10 / 11 (x64).
- **Runtime Environment**: Unity Mono Runtime (.NET 3.5 / 2.0 CLR subset).
- **Compilers**:
  - `C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe` (Used for compiling both `AutoGatherMod.dll` and `XDLands.AutoCounter.dll`).
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
│   ├── AutoGatherMod.dll             # AutoGather, ESP, CraftPlanner & Combat Suite
│   ├── XDLands.AutoCounter.dll       # Stance Auto-Counter & Timing Queue
│   └── UnityEngine*.dll              # Unity engine assemblies
├── AutoGatherMod.cs                  # Source code for AutoGather & Combat suite
├── scratch\
│   └── AutoCounter.cs                # Source code for AutoCounter & AutoCounterRunner
├── patch_mod.py                      # Cecil bytecode patcher
├── build_mod.bat                     # Master build script (.NET 3.5 csc + Python patcher)
├── install_mod.bat                   # Portable one-click installer for other PCs
├── uninstall_mod.bat                 # One-click uninstaller (restores .original)
├── AutoGatherSettings.ini            # Persistent configuration file
└── scratch\durango_git\              # Local Git repository mirror (RaditSuryaWijya/Durango)
```

## 3. Bytecode Injection Hooks Summary (`patch_mod.py`)
1. **Craft Limit 999**: Modifies `ProductionProcessManager.OnQuantityChanged` operand from `15` to `999`.
2. **Anti-Stagger**: Injects `ldsfld DamagedProcesser.DisableStagger` check into `DamagedProcesser.Blow` and `DamagedProcesser.KnockBack`.
3. **Lock Default Run Anim**: Patches `LocalMotionUpdater.LockDefaultRunAnim` to maintain sprint animations.
4. **Moving Attack**: Injects `OnAttackPacket` hook into `UsingAction.Update` and enables `DisableAttackMoveLock`.
5. **Dino Noclip**: Hooks `LocalUnitPushArea.ProcessUnitPush` to bypass push area resolution when targeting dinosaurs.
6. **Dual Speed Multiplier**: Patches `PlayerController.get_MoveSpeed` to apply separate multipliers for on-foot vs mounted states.
7. **Mod Bootloader**: Injects `AutoGather.AutoGatherLoader.Init()` into `XDLands.Mark.Ensure()`.
8. **Auto-Counter Alerts**: Injects `XDLands.CombatMods.AutoCounter.OnAttackAlert(EntityId, AttackTime, EventAt)` into `CombatSystem.OnAttackAlert`.
9. **Auto-Counter Damage Hook**: Injects `XDLands.CombatMods.AutoCounter.OnDamaged(AttackerId, VictimId)` into `CombatSystem.OnDamaged`.

## 4. Dependencies & Constraints
- Assemblies in `XDLands_Data/Managed/` must strictly avoid .NET 4.0+ types (such as `System.Threading.Tasks.Task`, `ConcurrentDictionary`, or `Monitor.Enter(object, ref bool)`).
- `Assembly-CSharp.dll.original` must be preserved at all times as the clean baseline for rebuilding patches.
