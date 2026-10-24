# Project Brief: Durango / XDLands PC Mod Suite

## 1. Executive Summary
Durango: Wild Lands (PC Fan Game / XDLands) Mod Suite is an advanced, production-grade modification and automation suite built specifically for the PC client of Durango. It provides automated harvesting, deep crafting calculations, movement/combat enhancement, and autonomous reaction combat mechanics including the **Auto-Counter Stance Engine**.

## 2. Core Requirements & Goals
1. **Seamless Integration**: Injected directly into the Unity IL runtime (`Assembly-CSharp.dll`) using Mono.Cecil, without requiring external cheat engines or invasive overlays.
2. **Combat Stance Autonomous Defense (Auto-Counter)**:
   - Detects when the player is wielding a 1-handed weapon (`sword_onehand`, `axe_onehand`, `blunt_onehand`).
   - Detects when the player enters "Sikap Melawan" (Counter Stance / `melee_outfighting`).
   - Automatically reacts to enemy dinosaur attack alerts (`OnAttackAlert`).
   - Computes server impact timestamps to execute the Space counteraction (`xd_counter` / `onehand_dodge`) with sub-second precision (0.22s before impact).
   - Turns character towards attacker (`TurnToYaw`) and locks target (`SelectTarget`).
   - Completely negates incoming damage while triggering the game's counterattack mechanism.
3. **Smart Learning Auto-Gather**:
   - Machine learning-inspired target acquisition: harvest any object once, and the bot learns the node, generator, and harvest action to clear the surrounding area.
   - Comprehensive resource filters (`All`, `Wood`, `Stone`, `Bush`, `Plant`, `Corpse`).
4. **Mass Production & Crafting Bypass**:
   - Bypasses the 15-item craft quantity cap to allow queues up to **999** items.
   - Automatic stamina management (Auto-Eat) during batch production.
5. **Mobility & Combat Enhancements**:
   - Independent player and mount speed multipliers (`1.0x` - `2.0x`).
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
- Custom Libraries: `AutoGatherMod.dll`, `XDLands.AutoCounter.dll`.
- Target Runtime Profile: .NET Framework 3.5 / 2.0 subset (strictly avoided .NET 4.0+ constructs like `Monitor.Enter(object, ref bool)`).
