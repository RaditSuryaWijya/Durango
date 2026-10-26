# Active Context: Durango / XDLands PC

## 1. Current State & Recent Developments
The mod suite has reached a mature, verified state. All user requests regarding the recent client update and combat handling have been successfully implemented and tested:

1. **Manual Counter Attack Animation Cancel (Space Key)**:
   - Replaced previous auto-counter parry bot with 100% manual counter attack animation cancel.
   - Allows canceling active 1-handed attack swings (Pukul `onehand_default`, Hajar `onehand_smash`, Pukul dari Belakang `onehand_stab`) when pressing Space in Counter Stance (`melee_outfighting`).
   - Cancels `UsingAction`, resets `ProhibitedUntil`, and advances state to `_state = 2` without rotation lag.
   - Crossfades 3D character model to `Onehand_Dodge` in 0.05s via `ForcePlayCounterMotion`, completely eliminating character freeze ("diam").
2. **Strict Skill Readiness Validation (`IsCounterSkillReady`)**:
   - Skill is only canceled if ready: validates cooldown (7s), stamina (35), player alive, not riding, crowd control/stun timer, and water depth.
   - If skill is on cooldown or out of stamina, attack animation continues naturally and model is not overridden.
3. **In-Battle Mode Validation (`CombatSystem.CombatMode`)**:
   - Added `if (!cs.CombatMode) return;` to prevent counter attack from triggering when exploring or walking outside combat.
   - In peace/exploration mode, Space key performs normal vanilla interactions (no counter attacks triggered).
4. **Complete Auto Counter Bot Removal**:
   - `AutoCounter.cs`, `XDLands.AutoCounter.dll`, and `install_auto_counter.bat` removed.
   - Cleaned `patch_mod.py` (removed hooks into `OnDamaged` & `OnAttackAlert`).
   - Cleaned `AutoGatherMod.cs` GUI menu and settings INI.
5. **Fresh Client Update & Disaster Recovery**:
   - Successfully adapted to official client update (`Assembly-CSharp.dll` 6.79MB).
   - Enhanced `patch_mod.py` with automatic detection of fresh official client assemblies and auto-backup refresh.
   - Built and deployed clean binaries to `XDLands_Data/Managed/` and `Release/`.
   - Full repository synchronized with remote GitHub `origin/main`.
6. **Auto Out Combat On Target Death**:
   - Identified root cause of not auto-exiting combat: Durango vanilla has a combat HUD padlock toggle `BattleModeLock` saved in Windows Registry (`HKCU\Software\XD Lands PC\XD Lands (Fan Game)\BattleModeLock_h2049228199`). On this PC it was set to `1` (Locked, preventing exiting battle view). On user's other laptop it was `0` (Unlocked).
   - Reset registry key to `0` (Unlocked).
   - Implemented `HandleAutoExitCombatOnTargetDead()` in `AutoGatherMod.cs`: Immediately clears dead target upon dinosaur death (`curTarget != null && !curTarget.IsAlive`), checks for remaining enemies, exits battle (`CombatSystem.ExitBattle()`), sets `CombatMode = false`, and forces `CombatGroup.SetBattleView(BattleViewMode.Normal)` to guarantee instant return to peace mode regardless of game lock settings.
   - Added GUI control `3d. Auto Out Combat` with persistence in `AutoGatherSettings.ini` (`AutoExitCombatOnKill=true`).
7. **Auto Loot Loop for Lava & Water**:
   - Built automated loop engines (`UpdateAutoLootLava()` and `UpdateAutoLootWater()`) that loop continuously until all containers are filled or the user moves away / stops.
   - Intelligent container detection: finds empty/non-full containers in inventory (metal buckets for lava, bottles/waterskins/pots/buckets/cups for water).
   - Natural timer & animation synchronization (`IsDrawActive()` via `InteractionSystem._drawTimer` and `TimerSystem`).
   - Integrated safety stops: stops on combat, stops when moving > 4.5m away from start position, stops on full containers.
   - Dual UI card added to Tab 0 (Gathering) and Tab 2 (Tweaks) with real-time count of empty containers and one-click start/stop.
8. **Counter Assist (Visual Timing Alert)**:
   - Implemented Counter Assist feature solving player difficulty in timing parries/counters against various dinosaur species and attack types.
   - Captures incoming `Messages.AttackAlerted` alerts from CombatSystem even in manual combat mode.
   - Tracks Phase 1 (Persiapan serang / red telegraph area) vs Phase 2 (Pukulan mendarat / strike impact at `alert.AttackTime`).
   - Calculates precise remaining time until impact: `float timeUntilHit = (float)(alert.AttackTime - Connections.Frontend.GetPredictedServerTime());`.
   - Triggers visual and audio cues when entering the Golden Counter Window (default: `0.05s <= timeUntilHit <= 0.38s`).
   - Dynamically finds and highlights the Counter Action Button (Button 0) on the HUD with pulsating neon borders, inner tint, and floating badge `⚡ [SPASI] COUNTER! (0.35s) ⚡`.
   - Displays Center HUD Combat Alert banner with animated countdown and progress timing bar.
   - Plays crisp audio cue (`UISound.ClickType.ButtonBattle`) on window opening.
   - Validates skill readiness (`IsCounterSkillReady`); adapts to Dodge Roll when using 2-Hand weapons.
   - Added GUI control `3e. Counter Assist Alert` in Combat tab with timing presets and settings persistence (`EnableCounterAssist=true`).

## 2. Key Decisions & Rationale
- **Human Reflex Assistance over Automated Bots**: Player retains 100% manual control over pressing Space. The mod provides precise timing guidance instead of bot automation.
- **Server-Time Precision**: Uses `GetPredictedServerTime()` against `alert.AttackTime` for millisecond-exact impact timing, eliminating guessing across different dinosaur attack speeds.
- **Skill Readiness Gate**: Counter Assist alert only prompts when Counter skill is ready (not on cooldown, sufficient stamina).
- **CombatMode Gate**: Restricting combat interception to `CombatSystem.CombatMode == true` preserves peaceful roaming.

## 3. Active Workspace Artifacts
- Source Files: `AutoGatherMod.cs`, `patch_mod.py`, `AutoGatherSettings.ini`.
- Build Files: `build_mod.bat`, `install_mod.bat`, `uninstall_mod.bat`.
- Git Repository: `C:\Users\Radit\AppData\Local\XDLands\PC\durango_git` synced to `https://github.com/RaditSuryaWijya/Durango.git`.
