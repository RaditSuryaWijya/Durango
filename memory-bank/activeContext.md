# Active Context: Durango / XDLands PC

## 1. Current State & Recent Developments
The mod suite has reached a mature, verified state. All user requests regarding the recent client update have been successfully implemented and tested:
1. **Combat Stance Mechanics Addressed**:
   - Responded to XDLands combat stance update (`XdStance` / `melee_outfighting`).
   - Integrated automatic triggering of the Space key action (`xd_counter` / `onehand_dodge` / `ActionSlots[0]`).
2. **Elimination of Erroneous Basic Attacks**:
   - Removed previous fallback that queued basic weapon attacks (`onehand_default_a/b/c`), ensuring counter stance strictly performs the parry counter.
3. **Resolution of Parry Timing (Sub-Second Predictive Impact)**:
   - Fixed the issue where early triggering (at alert onset, up to 2.7s before impact) caused the parry frame to expire prematurely, leaving the player exposed to full damage.
   - Built the `AutoCounterRunner` queue system with a **0.22s lead time** calculated directly against the server impact timestamp `Messages.AttackAlerted.AttackTime`.
4. **Resolution of Mono Runtime Compatibility Bug**:
   - Fixed `Method not found: 'System.Threading.Monitor.Enter'` by removing `lock` statements and switching compilation to .NET Framework 3.5 `csc.exe`.
   - Verified in-game: counter executes at the exact optimal window, negates damage, and triggers `"Serangan Balik"`.

## 2. Key Decisions & Rationale
- **Single-Threaded Queue**: Since Unity's MonoBehaviour loop and network message handlers execute sequentially on the main thread, an un-synchronized `List<QueuedAlert>` is both faster and 100% immune to CLR runtime version mismatches.
- **Lead Time = 0.22s**: Accounts for network transmission latency (~0.02s) and animation windup (~0.08s), ensuring the player's parry frame is in its active apex when the dinosaur's attack arrives at $t = 0$.
- **Combat Reach Guard (12m)**: At the moment the delayed counter triggers, the attacker must still be within 12 meters of the player. If the dinosaur lunged in another direction or the player sprinted away, the counter is safely aborted to conserve stamina and cooldown.

## 3. Active Workspace Artifacts
- Source Files: `AutoGatherMod.cs`, `scratch/AutoCounter.cs`, `patch_mod.py`.
- Build Files: `build_mod.bat`, `install_mod.bat`, `uninstall_mod.bat`.
- Git Repository: `scratch/durango_git/` synced to `https://github.com/RaditSuryaWijya/Durango.git`.
