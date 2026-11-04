# Project Brief: Durango / XDLands PC Mod Suite

## 1. Executive Summary
Durango: Wild Lands (PC Fan Game / XDLands) Mod Suite is an advanced, production-grade modification and automation suite built specifically for the PC client of Durango. It provides automated harvesting, liquid loop engines (lava & water), deep crafting calculations, movement/combat enhancement, manual & automated animation cancels, precision reaction timing through the **Counter Assist Engine**, **Auto Counter Attack**, **Auto Dodge Roll**, and intelligent terrain/island navigation.

## 2. Core Requirements & Goals
1. **Seamless Integration**: Injected directly into the Unity IL runtime (`Assembly-CSharp.dll`) using Mono.Cecil, without requiring external cheat engines or invasive overlays.
2. **Combat Stance Defense, Counter Assist, & Auto Roll**:
   - Detects when the player enters "Sikap Melawan" (Counter Stance / `melee_outfighting`) with 1-handed weapons.
   - **Action Overwrite (Bypass Animasi)**: Allows instant manual or automated animation cancel into Counter parry (`xd_counter` / `onehand_dodge`) or Dodge Roll during active attack swings, completely eliminating animation lock.
   - **Real-Time Counter Assist Timing Engine**: predicts exact dinosaur landing frames, alerts the player within a customizable golden window (0.80s human visual/audio cue), and anchors a neon countdown HUD directly underneath the character's feet.
   - **Auto Counter Attack**: Automatically executes parry counter attack at the exact clash frame (**0.30s** before impact) while swinging weapon.
   - **Auto Dodge Roll**: Automatically breaks attack animation and rolls out to the nearest safe exit vector (**0.35s** before impact) when Counter is on cooldown or when using 2-handed weapons.
   - **Presisi Hitbox Telegraph**: Reconstructed Durango's forward/lateral rectangle orientation and normalized arc math with character body collision buffer (45 units / 0.45m), eliminating false "outside" misses.
   - Filters alerts exclusively to the targeted dinosaur (`CounterAssistTargetOnly`) to eliminate visual clutter in dense mobs.
3. **Automated Resource & Liquid Loops**:
   - **Smart Learning Auto-Gather**: Clears surrounding nodes based on player's manual harvest pattern.
   - **Strict Resource Part Isolation**: Enforces strict mutual exclusion between harvestable parts (Ranting vs Daun vs Kulit vs Tebang Kayu Anchor), preventing trees from being chopped down when only gathering twigs or leaves.
   - **Map Boundary & Ocean Navigation**: Comprehensively identifies island boundaries via chunk loading status, ocean biomes (`ColdOcean`, `WarmOcean`), and deep water, seamlessly detouring inland and never walking endlessly into the ocean.
   - **Continuous Lava & Water Loops**: Autonomously fills all inventory containers until filled or combat starts.
4. **Mass Production & Crafting Bypass**:
   - Bypasses the 15-item craft quantity cap to allow queues up to **999** items.
   - Automatic stamina management (Auto-Eat) during batch production.
5. **Mobility & Combat Enhancements**:
   - Independent player and mount speed multipliers (`1.0x` - `2.0x`).
   - Fast Sniper Stance (removes crouch crawls, full run speed for bows & crossbows).
   - Fast Crossbow Reload (1.5x speed cocking animation with stationary protection).
   - Full collision passthrough against dinosaurs (Dino Noclip).
   - Moving Attack (attack without animation movement lock).
   - Immunity to stagger, blow, and knockback (`DamagedProcesser.DisableStagger`).
   - Attack recovery cut (turbo ASPD).
   - Stealth anti-aggro camouflage.
6. **Cross-Platform Portable Installer**:
   - Single-click batch installation (`install_mod.bat`) and uninstallation (`uninstall_mod.bat`).
   - Automated compilation via .NET Framework 3.5 `csc.exe` (`build_mod.bat`).

## 3. Scope & Boundaries
- Target Client: `XDLands.exe` (Unity 2017 / 2018 Mono runtime on Windows x64).
- Target Assembly: `XDLands_Data/Managed/Assembly-CSharp.dll`.
- Custom Library: `AutoGatherMod.dll` (modular C# partial-class architecture under `Source/` compiled into a single high-performance assembly).
- Target Runtime Profile: .NET Framework 3.5 / 2.0 subset (strictly avoiding .NET 4.0+ constructs like `Monitor.Enter(object, ref bool)`).
