# Project Brief: Durango / XDLands PC Mod Suite

## 1. Executive Summary
Durango: Wild Lands (PC Fan Game / XDLands) Mod Suite is an advanced, production-grade modification and automation suite built specifically for the PC client of Durango. It provides automated harvesting, liquid loop engines (lava & water), deep crafting calculations, movement/combat enhancement, manual animation cancels, and precision reaction timing through the **Counter Assist Engine**.

## 2. Core Requirements & Goals
1. **Seamless Integration**: Injected directly into the Unity IL runtime (`Assembly-CSharp.dll`) using Mono.Cecil, without requiring external cheat engines or invasive overlays.
2. **Combat Stance Defense & Counter Assist**:
   - Detects when the player enters "Sikap Melawan" (Counter Stance / `melee_outfighting`) with 1-handed weapons.
   - Allows instant manual animation cancel into Counter parry (`xd_counter` / `onehand_dodge`) when pressing Space during active attack swings.
   - Real-time **Counter Assist Timing Engine**: predicts exact dinosaur landing frames, alerts the player within a customizable golden window (0.38s), and anchors a neon countdown HUD directly underneath the character's feet.
   - Filters alerts exclusively to the targeted dinosaur (`CounterAssistTargetOnly`) to eliminate visual clutter in dense mobs.
   - Automatically adapts to Dodge Roll when using 2-handed weapons or when Counter is on cooldown.
3. **Automated Resource & Liquid Loops**:
   - Smart Learning Auto-Gather: clears surrounding nodes based on player's manual harvest pattern.
   - Continuous Lava & Water Loops: autonomously fills all inventory containers until filled or combat starts.
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
