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

## 2. Key Decisions & Rationale
- **100% Manual vs Auto Parry**: User requested removing all automated parry bots. The combat suite now strictly focuses on responsiveness and animation canceling under player command.
- **Skill Readiness Gate**: Preventing animation cancels when skills are on cooldown prevents wasted inputs, visual glitches, and vulnerable lockouts.
- **CombatMode Gate**: Restricting counter attack interception to `CombatSystem.CombatMode == true` preserves standard peaceful movement (jumping, roaming) without accidental combat posture triggering.

## 3. Active Workspace Artifacts
- Source Files: `AutoGatherMod.cs`, `patch_mod.py`, `AutoGatherSettings.ini`.
- Build Files: `build_mod.bat`, `install_mod.bat`, `uninstall_mod.bat`.
- Git Repository: `C:\Users\Radit\AppData\Local\XDLands\PC\durango_git` synced to `https://github.com/RaditSuryaWijya/Durango.git`.
