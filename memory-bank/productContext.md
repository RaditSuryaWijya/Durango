# Product Context: Durango / XDLands PC

## 1. Problem Statement & Motivation
Durango: Wild Lands is a complex sandbox survival MMORPG originally published by Nexon. The fan-run server project (XDLands) revived the game on PC. However:
- Gathering thousands of materials manually is repetitive and physically exhausting.
- Gathering liquids (lava and water) requires clicking dozens of times repeatedly per container.
- Crafting limits (15 items per batch) hinder mass base building and tool crafting.
- Movement lock during attacks makes combat sluggish against aggressive dinosaur packs.
- Large dinosaurs and domestic pens frequently trap the player, causing stuck states.
- Dinosaur attack windups vary wildly across species (from snappy 0.6s raptor bites to 2.5s tyrannosaur tail sweeps). Human reaction time struggles to accurately judge the exact frame of landing, leading to missed parries/counters or premature dodges.
- When fighting among packs or herds, ambient dinosaur attacks create visual chaos and false alerts.

## 2. Solution: Mod Suite & Combat Assistance
This modification suite transforms the PC client experience:
1. **Intelligent Counter Assist (Visual Timing Alert)**:
   - Eliminates guesswork by tracking the dinosaur's server attack timestamp in real-time.
   - Calculates the exact millisecond remaining until impact and displays a sleek timing banner directly underneath the character's feet.
   - Filters alerts exclusively to the actively targeted dinosaur (`CounterAssistTargetOnly`), eliminating confusing phantom alerts from other animals in the vicinity.
   - Distinguishes clearly between Counter Ready (gold/cyan neon flash) and Skill Cooldown (orange dodge alert).
2. **Effortless Automated Gathering & Liquid Loops**:
   - Smart Gathering engine mimics human gathering by learning from the player's last manual interaction and auto-sweeping nearby nodes.
   - Auto Lava & Water Loops continuously fill all inventory containers (buckets, bottles, waterskins, pots) with zero manual clicking until all containers are filled.
3. **Fluid Combat Mobility & Stance Overhauls**:
   - Attack movement locking is eliminated, allowing players to kite and circle enemies freely during attack swings.
   - Fast Sniper Stance allows bow/crossbow users to stand and run at full speed without forced crouch walks.
   - Accelerated 1.5x Crossbow reload with natural stationary animation.
4. **Clean & Unobtrusive UI**:
   - The mod UI renders directly via Unity IMGUI with zero external overlays, draggable windows, hotkey toggles (`Insert` / `F11`), and full persistence to `AutoGatherSettings.ini`.

## 3. User Experience & Game Mechanics
- **Weapon Classification**: One-handed weapons (`sword_onehand`, `axe_onehand`, `blunt_onehand`), Two-handed weapons (`sword_twohand`, `axe_twohand`, `blunt_twohand`), Bows, Crossbows.
- **Stances in XDLands**:
  - `melee_cautious` (Sikap Pertahanan / Defensive)
  - `melee_reckless` (Sikap Menyerang / Aggressive)
  - `melee_outfighting` (Sikap Melawan / Counter Stance)
  - `ranged_keep_distance` (Sikap Sniper)
- **Counter Activation Mechanics**:
  In `melee_outfighting`, Slot 0 on the HUD is bound to Space (`battle_dodge` / `onehand_dodge` / `xd_counter`). Activating this slot initiates the counter-evasion frame. When an enemy attack connects during this frame, the game registers `DamageResult.Countered`, displays floating cyan text `"Serangan Balik"` (*Countered*), negates damage, and triggers a retaliatory strike.
