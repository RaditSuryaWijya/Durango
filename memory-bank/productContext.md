# Product Context: Durango / XDLands PC

## 1. Problem Statement & Motivation
Durango: Wild Lands is a complex sandbox survival MMORPG originally published by Nexon. The fan-run server project (XDLands) revived the game on PC. However:
- Gathering thousands of materials manually is repetitive and physically exhausting.
- Crafting limits (15 items per batch) hinder mass base building and tool crafting.
- Movement lock during attacks makes combat sluggish against aggressive dinosaur packs.
- Large dinosaurs and domestic pens frequently trap the player, causing stuck states.
- Recent game updates introduced combat stances (Sikap Tempur: Menyerang, Pertahanan, Melawan). The "Sikap Melawan" (Counter Stance / `melee_outfighting`) for 1-handed weapons allows countering enemy attacks by pressing Space, but human reaction time to fast dinosaur lunges often results in taking heavy damage or mistiming the counter.

## 2. Solution: Mod Suite & Auto-Counter Stance Engine
This modification suite transforms the PC client experience:
1. **Autonomous Reflex Defense**:
   Players wielding 1-handed weapons in Counter Stance no longer need to stress about millisecond-accurate parry timing against charging Raptors, Tyrannosaurs, or Carnotaurs. The client calculates the dinosaur's server attack impact timestamp and automatically engages the counter parry frame at the peak timing window.
2. **Effortless Resource Gathering**:
   The Smart Gathering engine mimics human gathering by inspecting the player's last manual interaction, caching the target parameters, and routing paths to identical nodes within a user-defined radius.
3. **Fluid Combat Mobility**:
   Combat movement locking is eliminated, allowing players to kite and circle enemies freely while maintaining telegraphed tebassan strikes and anti-stagger posture.
4. **Clean & Unobtrusive UI**:
   The mod UI renders directly via Unity IMGUI with zero external overlays, draggable windows, hotkey toggles (`Insert` / `F11`), and full persistence to `AutoGatherSettings.ini`.

## 3. User Experience & Game Mechanics
- **Weapon Classification**: One-handed weapons (`sword_onehand`, `axe_onehand`, `blunt_onehand`).
- **Stances in XDLands**:
  - `melee_cautious` (Sikap Pertahanan / Defensive)
  - `melee_reckless` (Sikap Menyerang / Aggressive)
  - `melee_outfighting` (Sikap Melawan / Counter Stance)
- **Counter Activation**:
  In `melee_outfighting`, Slot 0 on the HUD is bound to Space (`battle_dodge` / `onehand_dodge` / `xd_counter`). Activating this slot initiates the counter-evasion frame. When an enemy attack connects during this frame, the game registers `DamageResult.Countered`, displays floating cyan text `"Serangan Balik"` (*Countered*), negates damage, and triggers a retaliatory strike.
