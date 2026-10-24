# System Patterns & Architecture

## 1. Overall System Architecture

The mod suite operates at the core of the Unity runtime by injecting hooks into `Assembly-CSharp.dll` using Mono.Cecil, coupled with two standalone native mod assemblies:

```
[Unity Engine / XDLands Game Loop]
   │
   ├── XDLands.Mark.Ensure() (Bootloader Hook)
   │     └── AutoGather.AutoGatherLoader.Init()
   │           └── Creates GameObject "AutoGatherController" (MonoBehaviour)
   │
   ├── CombatSystem.OnAttackAlert(Messages.AttackAlerted msg, PacketHeader header)
   │     └── XDLands.CombatMods.AutoCounter.OnAttackAlert(msg.EntityId, msg.AttackTime, msg.EventAt)
   │           └── AutoCounterRunner (MonoBehaviour Queue) -> Trigger at AttackTime - 0.22s
   │                 └── TryCounter -> CombatSystem.UseBattleAction(spaceAction.Id)
   │
   ├── CombatSystem.OnDamaged(Messages.Damaged msg, PacketHeader header)
   │     └── XDLands.CombatMods.AutoCounter.OnDamaged(msg.AttackerId, msg.VictimId)
   │
   ├── UsingAction.Update()
   │     └── Intercepts Messages.UseBattleAction packets for Moving Attack
   │
   ├── LocalUnitPushArea.ProcessUnitPush()
   │     └── Bypasses pushing physics against dinosaurs (Dino Noclip)
   │
   ├── DamagedProcesser.Blow() / KnockBack()
   │     └── Checks DamagedProcesser.DisableStagger to negate stagger loss-of-control
   │
   └── PlayerController.get_MoveSpeed()
         └── Dynamically scales player vs mount move speed
```

## 2. Auto-Counter Stance Engine Design

### Key Classes
- `XDLands.CombatMods.AutoCounter`: Static coordinator that verifies stance (`XdStance.Current`), weapon class (`XdStance.WeaponClass`), identifies the Space key action (`ActionSlots[0]` or `xd_counter`), calculates impact lead times, and executes combat commands.
- `XDLands.CombatMods.AutoCounterRunner`: Persistent `MonoBehaviour` (`DontDestroyOnLoad`) managing an allocation-free timing queue of pending alerts. Runs on Unity's main thread every frame.

### Predictive Impact Timing Formula
1. When `CombatSystem.OnAttackAlert` fires:
   - Extract `AttackTime` (predicted server timestamp of damage impact) and `EventAt` (timestamp when windup began).
   - Read current predicted server time: `serverTime = Connections.Frontend.GetPredictedServerTime()`.
   - Calculate remaining time: `timeUntilImpact = (float)(AttackTime - serverTime)`.
2. Compute execution delay:
   $$\text{Delay} = \text{timeUntilImpact} - \text{LeadTime}$$
   Where $\text{LeadTime} = 0.22\text{s}$ (the sweet spot that accounts for packet travel, animation initiation, and places the active parry frame right at $t = 0$).
3. If $\text{Delay} \le 0.05\text{s}$: Execute immediately.
4. If $\text{Delay} > 0.05\text{s}$: Add/update in `AutoCounterRunner._queue`.
5. On trigger:
   - Check if attacker is alive and within plausible reach ($\le 12\text{m}$).
   - Smoothly rotate character to face the attacker: `localPlayer.TurnToYaw(yaw, true)`.
   - Select attacker target: `cs.SelectTarget(attacker)`.
   - Dispatch counteraction: `cs.UseBattleAction(spaceAction.Data.Id)`.

### Strict Exclusion of Basic Attack Fallback
Previous revisions fell back to basic weapon strikes (`onehand_default_a/b/c`) when the Space action was on cooldown or not immediately usable. This was identified as an anti-pattern because basic attacks interrupt defensive stances and leave the player vulnerable. The engine now strictly executes only the Space counter parry (`xd_counter` / `onehand_dodge`).

## 3. Mono Runtime Threading & Compatibility Pattern
- **Critical Finding**: Unity's older Mono runtime (.NET 3.5 / 2.0 subset) lacks `System.Threading.Monitor.Enter(object, ref bool)` introduced in .NET 4.0.
- **Rule**: Never use C# `lock (...)` blocks in assemblies targeting Unity Mono. Because Unity's game loop (`Update`, `OnAttackAlert`, etc.) executes exclusively on the main thread, queues can be managed synchronously without mutexes or locks.
- **Compiler**: All code is compiled using `C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe`.
