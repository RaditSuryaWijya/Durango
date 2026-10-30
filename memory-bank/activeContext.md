# Active Context: Durango / XDLands PC

## 1. Current State & Recent Developments
The mod suite has reached a mature, verified state. All user requests regarding client updates, gathering automation, combat enhancements, reaction timing, and codebase maintainability have been successfully implemented, tested, and tracked in Git:

1. **Manual Counter Attack Animation Cancel (Space Key)**:
   - 100% manual counter attack animation cancel.
   - Allows canceling active 1-handed attack swings (Pukul `onehand_default`, Hajar `onehand_smash`, Pukul dari Belakang `onehand_stab`) when pressing Space in Counter Stance (`melee_outfighting`).
   - Cancels `UsingAction`, resets `ProhibitedUntil`, and advances state to `_state = 2` without rotation lag.
   - Crossfades 3D character model to `Onehand_Dodge` in 0.05s via `ForcePlayCounterMotion`, completely eliminating character freeze ("diam").
2. **Strict Skill Readiness Validation (`IsCounterSkillReady`)**:
   - Validates cooldown (7s), stamina (35), player alive, not riding, crowd control/stun timer, and water depth.
   - If skill is on cooldown or out of stamina, attack animation continues naturally and model is not overridden.
3. **In-Battle Mode Validation (`CombatSystem.CombatMode`)**:
   - Prevents counter attack from triggering when exploring or walking outside combat.
   - In peace/exploration mode, Space key performs normal vanilla interactions.
4. **Auto Out Combat On Target Death**:
   - Fixed Windows Registry `BattleModeLock` setting (`HKCU\Software\XD Lands PC\XD Lands (Fan Game)\BattleModeLock_h2049228199`) reset to `0` (Unlocked).
   - Implemented `HandleAutoExitCombatOnTargetDead()`: Immediately clears dead target upon dinosaur death (`curTarget != null && !curTarget.IsAlive`), checks for remaining enemies, exits battle (`CombatSystem.ExitBattle()`), sets `CombatMode = false`, and forces `CombatGroup.SetBattleView(BattleViewMode.Normal)`.
   - Added GUI control `3d. Auto Out Combat` with persistence in `AutoGatherSettings.ini` (`AutoExitCombatOnKill=true`).
5. **Auto Loot Loop for Lava & Water**:
   - Automated loop engines (`UpdateAutoLootLava()` and `UpdateAutoLootWater()`) that loop continuously until all containers are filled or the user moves away / stops.
   - Intelligent container detection: finds empty/non-full containers in inventory (metal buckets for lava, bottles/waterskins/pots/buckets/cups for water).
   - Natural timer & animation synchronization (`IsDrawActive()` via `InteractionSystem._drawTimer` and `TimerSystem`).
   - Integrated safety stops: stops on combat, stops when moving > 4.5m away from start position, stops on full containers.
   - Dual UI card added to Tab 0 (Gathering) and Tab 2 (Tweaks) with real-time count of empty containers and one-click start/stop.
6. **Counter Assist (Visual Timing Alert) Overhaul**:
   - Solves player difficulty in judging dinosaur attack windup and landing moments.
   - Captures incoming `Messages.AttackAlerted` alerts from `CombatSystem`.
   - Tracks Phase 1 (Persiapan serang / red telegraph area) vs Phase 2 (Pukulan mendarat / strike impact at `alert.AttackTime`).
   - Calculates millisecond-precise remaining time: `float timeUntilHit = (float)(alert.AttackTime - Connections.Frontend.GetPredictedServerTime());`.
   - Resolved 5 root causes of missed alerts:
     1. Unit scale fix: Durango coordinates are centimeters (100 = 1m); replaced 4.5f with 1800f/2000f.
     2. Hitbox filter: Basic bites/strikes without explicit rect/circle hitboxes are recognized via attacker target matching.
     3. Cooldown fallback alert: When Counter skill is on CD at 0.38s, renders prominent orange alert `⚠️ [SPASI] DODGE ROLL! (CD) ⚠️` with audio cue instead of disappearing.
     4. Dynamic hook persistence: Re-hooks `CombatSystem` dynamically across island transitions, respawns, and scene reloads (`_lastHookedCombatSystem != cs`).
     5. Combat initiation: Alerts trigger even when dinosaurs initiate combat before the player enters combat mode.
7. **Target-Only Dinosaur Alert Filter (`CounterAssistTargetOnly`)**:
   - Filter `_activeAttackAlerts` so only attacks from the currently targeted dinosaur (`cs.Target.Value` or `_currentCombatTarget`) produce alerts.
   - Prevents confusing phantom alerts from ambient dinosaurs, prey animals, or other players' fights nearby.
   - Extracts and displays targeted dinosaur name in the alert banner (e.g., `Target: Megalosaurus (Benturan mendarat!)`).
   - Added toggle `🎯 Hanya Dino Target [ON]` in Combat tab and persistence in `AutoGatherSettings.ini`.
8. **Under-Character UI Alert Projection (`CounterAssistUnderCharacter`)**:
   - Projecting player character's 3D feet position to 2D screen coordinates using `Camera.main.WorldToScreenPoint(localPlayer.transform.position)`.
   - Inverted Y coordinates for Unity IMGUI (`Screen.height - sp.y`).
   - Anchors the alert banner horizontally centered at `sx - bannerW * 0.5f` and vertically at `sy + 18f` (floating right under the character's feet).
   - Clamped with screen boundaries so it never gets clipped at monitor edges.
   - Automatically follows player character movement throughout combat.
   - Added toggle `📍 Di Bawah Karakter` in Combat tab and persistence in `AutoGatherSettings.ini`.
9. **Multi-Hitbox Spatial Arbitration for Simultaneous Dinosaur Attacks (Apatosaurus)**:
   - Solved confusion when dinosaurs like Apatosaurus cast skills with 2 or more red areas (hitboxes) simultaneously.
   - Preserves multiple active hitboxes in `_activeAttackAlerts` instead of overwriting existing alerts from the same `EntityId`.
   - Real-time spatial position checking: calculates `IsPlayerInsideHitbox()` for each active red area against the player's 3D feet position.
   - Smart threat prioritization:
     - If player is inside Area 1: locks timing onto Area 1.
     - If player is inside Area 2: locks timing onto Area 2.
     - If player is in the overlap of both areas: locks onto whichever strike lands first, then seamlessly shifts to the second strike.
     - If player is outside all red areas: switches HUD to a green safe zone banner (`🛡️ POSISI AMAN (DI LUAR AREA MERAH)`) without triggering false alarms.
10. **Durango Game Patch Knowledge Base (Item Modification / Tuning)**:
    - Documented the official/private server patch note regarding item modification:
      1. Safe floor: Results never drop below the item's base level (anti-downgrade protection).
      2. Stat random roll up to max peak cap.
      3. Higher crafting skill vs recipe difficulty significantly increases the odds of rolling peak stats.
      4. Great Success (*Sukses Besar*) 100% guarantees maximum peak stats.
11. **Automated Client Update Recovery System (Zero-Friction Updates)**:
    - Eliminates the need to manually re-clone/re-patch every time the XDLands official launcher updates the client.
    - **1-Click Desktop Updater** (`[Durango] Update-Dan-Pasang-Mod.bat` on Desktop): Single-click script that pulls latest code from GitHub, dynamically patches `Assembly-CSharp.dll` with Cecil in ~2 seconds, copies `AutoGatherMod.dll`, and verifies installation.
    - **Background Auto-Watcher** (`durango_watcher.pyw`): Lightweight background process (~15MB RAM, 0% CPU, single-instance via Windows Named Mutex `Local\DurangoModWatcherMutex`) that continuously monitors `Assembly-CSharp.dll`.
    - Detects unpatched client resets within seconds of launcher recipe updates, automatically invokes dynamic patcher, plays Windows system chime notification, and logs to `durango_watcher.log`.
    - Desktop controls provided: `[Durango] Aktifkan-Auto-Watcher.bat` (with Windows Startup registration) and `[Durango] Nonaktifkan-Auto-Watcher.bat`.
12. **Codebase Modularization (Monolith Decomposition)**:
    - Decomposed the ~14,065 line monolithic `AutoGatherMod.cs` into clean, function-oriented C# `partial class` modules inside [`Source/`](file:///C:/Users/Radit/AppData/Local/XDLands/PC/durango_git/Source):
      - `Source/Data/AutoGatherModels.cs`: All Enums & data transfer objects (`ResourceFilterMode`, `GatherTargetMode`, `MenuDisplayState`, `CombatTargetMode`, `EspCategory`, `SlotRequirementInfo`, `EspTargetInfo`).
      - `Source/Core/AutoGatherController.cs`: MonoBehaviour entrypoint, state declarations, and Unity lifecycle (`Awake`, `Start`, `OnDestroy`, `Update`).
      - `Source/Core/DoorstopEntrypoint.cs`: Doorstop loader entrypoint.
      - `Source/Features/Combat/AutoGatherController.Combat.cs`: Auto Combat AI, Dodge, Burst, Counter Assist, Speed, Dino Noclip.
      - `Source/Features/Gathering/AutoGatherController.Gathering.cs`: Gathering routine, Smart Learn Target, node recognition.
      - `Source/Features/Crafting/AutoGatherController.CraftPlanner.cs`: Craft planner, sub-recipe tree, direct craft, craft limit bypass.
      - `Source/Features/ESP/AutoGatherController.EspRadar.cs`: Dinosaur, crater, water, ore radar with 3D-to-screen projection.
      - `Source/Features/Survival/AutoGatherController.Survival.cs`: Auto-eat energy food, lava & water scoop loops, stealth anti-aggro, archipelago unlocker.
      - `Source/Features/Settings/AutoGatherController.Settings.cs`: INI save/load persistence logic.
      - `Source/Features/UI/AutoGatherController.UI.cs`: OnGUI render engine, window tabs, HUD banners, draggable controls.
    - Updated [`build_mod.bat`](file:///C:/Users/Radit/AppData/Local/XDLands/PC/durango_git/build_mod.bat) to compile with `/recurse:"%SCRIPT_DIR%Source\*.cs"`.
    - Verified 100% binary & member equivalence (177 methods, 252 fields, identical types and lifecycle).
    - Removed obsolete root `AutoGatherMod.cs` from Git repository and updated `README.md`.

13. **Smart Multi-Target Gather per Specific Node Overhaul**:
    - **Initial Manual Seed**: The bot begins in learning standby until the player performs a manual gather on their desired target node.
    - **Strict Specific Node Lock**: Completely eliminated broad category matching (e.g. general wood/trees). Locks strictly onto the exact node type via `_learnedNodeEntityType`, `_learnedNodePoolName`, `_learnedNodeBaseName`, and `_learnedNodeDisplayName`.
    - **Dynamic Multi-Target Items**: If player harvests 2 or more different resources from the same node (e.g., Twigs and Leaves from an Acacia tree), all items are dynamically appended to `_learnedTargetItems`.
    - **Sequential Gathering per Node**: When harvesting a node, the bot exhausts Target Item #1 completely, then proceeds to Target Item #2, etc., on that same node before moving to the next matching node.
    - **UI & Persistence**: Displays locked node name & pool/prefab, lists all target items with individual `[X]` delete buttons, and serializes `LearnedNodeEntityType`, `LearnedNodePoolName`, and `LearnedTargetItems` to `AutoGatherSettings.ini`.

14. **Pet Immunity Filter & Mod Toggle Lifecycle Fix**:
    - **Pet/Mount/Ally Immunity**: In FindCombatTarget (Combat mode 2 & 3), animals were previously scanned indiscriminately if GetEnemies() was empty, causing player's own pet or mount to be targeted.
      - Implemented IsPetOrFriendly(DamageableEntity) and IsAnimalPetOrFriendly(AnimalBehavior).
      - Filters out PetDamageableEntity, player's active pet ID (PetManager.Instance().GetPlayerPetId()), pet components (PetAI, GrazingPetAI, VehiclePet), player character entity, and all friendly allies in CombatSystem.Instance().DamageableEntities.GetAllies().
      - Added auto-drop in UpdateCombat(): If _currentCombatTarget is ever pet/friendly, it is immediately discarded and cleared.
    - **Toggle & Restart Reliability**:
      - Fixed hotkey blocking: Removed && !_isGathering and menu open restrictions from Input.GetKeyDown(ToggleKey), allowing instant toggling at any time.
      - Mutual Exclusivity: Turning on Gather bot turns off Combat bot (IsCombatBotEnabled = false); turning on Combat bot turns off Gather bot (IsEnabled = false), preventing race conditions.
      - Clean State Reset: On toggle on, immediately clears _recentlyGathered, resets _nextScanTime = 0f, _gatherCooldownUntil = 0f, _isGathering = false, cancels all active coroutines via StopAllCoroutines(), and stops any residual navigator movement.
      - Variant Matching: In DoesNodeMatchLearnedTarget, species display name matching (cleanRaw == dispLow || immNameLow == dispLow || sprNameLow == dispLow) handles different visual/size variants of the same species cleanly.

15. **Multi-Target Sequential Harvesting & Continuous Node Navigation**:
    - **Clean Menu Triggering**: Eliminated Windows keyboard `VK_E` event simulation from `TriggerInteraction`. Directly invokes `InteractionSystem.Instance().SetInteractionTarget(new InteractionObject(target))` in-engine, preventing Durango's `OnClickCollectKey` from seeing `Target != null` and toggling the menu closed.
    - **Depleted Slot Handling**: Added zero-quantity filter `cnt <= 0 && w.Data.GatheringData != null` in `activeSlots` and `DoesWidgetMatchTargetItem`. Once Target #1 is depleted, the slot is immediately disqualified, allowing the loop to smoothly transition to Target #2 (e.g. harvesting Twigs then Leaves, or Tulang Rusuk then Tulang Kaki).
    - **Smart Learn Guard**: Wrapped `CapturePlayerManualGathering` with `!IsEnabled && !_isGathering` to avoid overriding the user's learned target list during autonomous bot operation.
    - **Continuous Island Navigation**: If no valid node is within `ScanRadiusMeters`, `FindClosestTarget` automatically targets `absoluteClosest` (up to 150m) instead of returning null and halting the character.
    - **Flexible Species Variant Matching**: Enhanced `DoesNodeMatchLearnedTarget` with substring containment and significant species noun extraction (e.g. "akasia", "tulang", "palem").

16. **Elimination of Hotkey Self-Triggering & Autonomous Combat Interference**:
    - **Self-Trigger Elimination**: Removed legacy `SimulatePressKeyRoutine((byte)(0x70 + targetSlot))` from `GatherRoutine`. This call previously injected `VK_F1` (killing Gather Bot via `ToggleMod`) or `VK_F2` (activating Combat Bot via `ToggleCombatMod`), causing the bot to die after 1 loot and combat AI to move autonomously.
    - **Separated Hotkeys**: Changed default `CombatToggleKey` to `F8` (cycling through `F8`, `F9`, `F10`, `G`, `H`) to completely prevent any collision with Durango's native `F1-F6` radial menu petals.
    - **Interaction Wheel Protection**: Added guard so that F1-F6 inputs while the game's interaction wheel is visible are treated strictly as game petal selections and will never toggle mod modes.
    - **Direct Engine Execution**: Slot actions are clicked natively through `_clickHotKeyMethod.Invoke` and `chosenWidget.SetClick()`, ensuring zero synthetic OS keyboard pollution.

## 2. Key Decisions & Rationale
- **Partial Class Modularization**: Using C# 2.0+ `partial class` enables breaking a massive 14,000-line monolith into dedicated, domain-specific files without incurring any runtime reflection overhead or breaking internal private state accessibility.
- **Target-Specific Combat Perception**: Filtering combat cues to the active target eliminates visual noise in dense jungle/savannah areas with multiple aggressive mobs.
- **Eye-Level Spatial HUD**: Anchoring timing bars beneath the player's 3D avatar drastically improves player response time compared to static screen-edge HUDs.
- **Server-Time Precision**: Uses `GetPredictedServerTime()` against `alert.AttackTime` for millisecond-exact impact timing.
- **Skill Readiness Gate**: Counter Assist alert clearly distinguishes between Counter Ready (gold/cyan) and Cooldown Dodge (orange/red).
- **Zero-Friction Client Resilience**: Decoupling the git repository (`durango_git`) from the volatile game folder and adding both a 1-click desktop shortcut and an automatic background watcher guarantees the user never loses mod functionality across launcher updates.

## 3. Active Workspace Artifacts
- Source Modules: `Source/Data/`, `Source/Core/`, `Source/Features/{Combat, Gathering, Crafting, ESP, Survival, Settings, UI}/`.
- Engine & Scripts: `patch_mod.py`, `durango_watcher.pyw`, `AutoGatherSettings.ini`.
- Automation Scripts: `[Durango] Update-Dan-Pasang-Mod.bat`, `[Durango] Aktifkan-Auto-Watcher.bat`, `[Durango] Nonaktifkan-Auto-Watcher.bat`.
- Build Files: `build_mod.bat`, `install_mod.bat`, `uninstall_mod.bat`.
- Git Repository: `C:\Users\Radit\AppData\Local\XDLands\PC\durango_git` synced to `https://github.com/RaditSuryaWijya/Durango.git`.
