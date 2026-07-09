# Paris Night Level 03 Shift Force Switch Analysis - 2026-07-09

## Scope

- Location: `02_Paris/Night/level_03`.
- Pattern: `shift_force_switch`, pattern index `5`.
- Runtime symptom: hamster on bottom lane collides with bottom-lane `bigAlive`.
- Screenshot fact: overlay shows `Paris Night, shift_force_switch 5, PAUSED, Run, energy: 34, isDamaged: True`.
- Final interpretation after user correction: this is an edge/unpassable segment for current strategy constraints, not a bot logic regression.
- Reopened after geometry adjustment: user moved the bottom `bigNotAlive` farther, creating a physical `SuperJumpOver` window before the bottom `bigAlive`; the collision still reproduces.

## Authoritative Expected Source

- User-provided screenshots and runtime report from 2026-07-09.
- User correction: returning the deepest dead-end prefix is intended behavior. It is used to expose unpassable or edge-passable sections.
- The analysis question was narrowed to two hypotheses:
  - after `RunFromRoof` / `PassiveRoofExit`, no valid ground window remains for the bottom `bigAlive`;
  - `JumpOnFromRoof` / `SuperJumpOnFromRoof` might have saved the route by hitting the `bigAlive` from the roof.

## Minimal Reproduction

```powershell
.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '02_Paris/Night/level_03' -TimeoutSeconds 240
```

Observed result:

- `[TEST RESULT] FAIL`
- `CollisionController damage ... reason=RunState state=Run ... obstacle=bigAlive`
- `LifeLoss ... energy=34 state=Run`

## Level And Pattern Facts

- `LostCyberHamster/Assets/Content/locations/02_Paris/levels/Night/level_03/level_03.json` uses pattern ref `shift_force_switch` at pattern index `5`.
- `LostCyberHamster/Assets/Content/PatternsCollection.json` defines `shift_force_switch` as a forced line-switch pattern: "необходимость резко сменить линию, иначе столкновение."
- The bottom `bigAlive` is pattern obstacle id `3`, overridden in the level as `obstacle_new_york_big_alive_5_idle`.

## Runtime Facts

Target run facts from the final diagnostic pass:

- Spawned target in pattern `5`: `bigAlive#-493486`.
- Last bottom roof support before it: `bigNotAlive#-493462`.
- Runtime executes:
  - `FIRE kind=PassiveRoofExit targetId=-493486 triggerId=-493462 ... desc=Passive roof exit before bigAlive`
  - `COMPLETE kind=PassiveRoofExit targetId=-493486 ... state=Run`
- Collision then occurs:
  - `CollisionController damage ... reason=RunState state=Run ... obstacle=bigAlive#-493486`
  - `LifeLoss amount=1 lives=2 energy=34 state=Run`
- Ground recovery reason:
  - `SuperJumpOverStrategy: Нет безопасного окна для перепрыгивания: bigAlive требует дополнительный зазор, которого нет в этом участке.`

## Hypothesis 1: No Ground Window After Roof Exit

Confirmed.

Evidence:

- After `PassiveRoofExit`, hamster is in `Run`.
- Energy is `34`, so energy is not the blocker: `SuperJumpOverPolicy.EnergyCost => 20`.
- `SuperJumpOverStrategy` reaches fire-window logic, not insufficient-energy logic.
- `JumpOverChainCalculator` applies extra `bigAlive` padding; after padding the fire window collapses and returns:
  - `Нет безопасного окна для перепрыгивания: bigAlive требует дополнительный зазор, которого нет в этом участке.`

Code references:

- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverPolicy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverStrategy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverChainCalculator.cs`

Conclusion:

- Once the bot leaves the roof passively and reaches ground `Run`, the bottom `bigAlive` is no longer recoverable by ground `SuperJumpOver`.

## Hypothesis 2: Roof-To-Road Jump-On Could Hit The BigAlive

Confirmed as a geometric/runtime capability, but intentionally not selected by current strategy gates.

Diagnostic facts:

- `JumpOnFromRoof`:
  - `diagnosticWindowFound=True`
  - `diagnosticFireShift=3.93`
  - `diagnosticReason=none`
- `SuperJumpOnFromRoof`:
  - `diagnosticWindowFound=True`
  - `diagnosticFireShift=3.68`
  - `diagnosticReason=none`

This also excludes unsafe post-action/bounce as the cause, because `JumpOnFromRoofFireWindowFinder.TryFindFireShift` only returns true after `TargetRemovalPostActionSafety.IsSafeAfterCompletion` accepts the completion state.

Code references:

- `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnFromRoof/JumpOnFromRoofStrategy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnFromRoof/SuperJumpOnFromRoofStrategy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOnFromRoof/JumpOnFromRoofFireWindowFinder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/TargetRemovalPostActionSafety.cs`

## Why The Roof-To-Road Action Was Not Selected

The first filtering point is `JumpOnFromRoofActionResolver.CanPlanJumpOnFromRoof`.

Current gate:

- allow target-oriented jump-on if `JumpOnObjectiveRules.HasEnergyForJumpOnObjective(hamster)` is true;
- otherwise allow defensive jump-on only if immediate automatic roof exit is dangerous.

Runtime values:

- `energy=31`
- `JumpOnObjectiveRules.HighPriorityEnergyThreshold=40`
- `hasJumpOnObjective=False`
- `gapToFirstRoad=4.16`
- `runFromRoofTravel=1.90`
- `dangerousAutomaticRoofExit=False`

Therefore `TryResolve` returned `NotApplicable` before the normal fire-window path could produce an action.

Code references:

- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOnFromRoof/JumpOnFromRoofActionResolver.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/JumpOnObjectiveRules.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/ObstacleClassifier.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnFromRoof/JumpOnFromRoofPolicy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnFromRoof/SuperJumpOnFromRoofPolicy.cs`

## Excluded Alternatives

- Not enough energy: excluded. Ground `SuperJumpOver` costs `20`; roof `JumpOnFromRoof` costs `10`; roof `SuperJumpOnFromRoof` costs `20`.
- Wrong target type: excluded. `ObstacleClassifier.CanJumpOnFromRoofObstacle` allows `bigAlive`.
- Runtime executor cancellation: excluded. There was no `FIRE kind=JumpOnFromRoof` or `FIRE kind=SuperJumpOnFromRoof` for the target.
- Unsafe roof-to-road completion: excluded by diagnostic-only `TryFindFireShift=True` with `diagnosticReason=none`.
- Dead-end fallback as bug: excluded by user correction; returning the deepest dead-end prefix is intended.

## Final Conclusion

The bot behaved according to current strategy contracts:

- it did not spend energy on proactive roof-to-road jump-on because energy was below the high-priority objective threshold and the immediate passive roof exit was not dangerous;
- it then passively left the roof;
- the subsequent ground state was a real dead-end for `SuperJumpOver` because the bottom `bigAlive` was too close for the required padding window;
- the planner returned the deepest dead-end prefix as designed.

This segment should be treated as an unpassable or edge-passable level/pattern case under current bot strategy constraints, not as a confirmed bot logic regression.

## Reopened After Geometry Adjustment

Status: root cause proven for the adjusted geometry. This supersedes the "unpassable segment" interpretation only for the adjusted `PatternsCollection.json` state where the bottom `bigNotAlive` was moved farther and a valid ground `SuperJumpOver` window exists.

### Adjusted Runtime Facts

Minimal command stayed the same:

```powershell
.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '02_Paris/Night/level_03' -TimeoutSeconds 240
```

Latest diagnostic run:

- Result: `[TEST RESULT] FAIL`.
- Target collision: `bigAlive#-559080`, bottom lane.
- Before the collision, `SuperJumpOver` is valid:
  - `[Bot BIG_ALIVE_SUPER_JUMP_OVER] stage=action-created lane=bottom state=Run energy=32 ... target=bigAlive#-559080 x=[-0.32,0.68] ... travel=5.13 first=0.74 selected=0.74 last=2.05 obstacles=1`
  - `[Bot BIG_ALIVE_ACTION_GEN] ... actions=SuperJumpOver/target=-559080 ... complete=5.87 ... > PassiveAdvance/target=-559696 ... complete=1.75 ...`
- The plan that gets applied is not the valid `SuperJumpOver` branch:
  - `[Bot BIG_ALIVE_PASSIVE_ADVANCE_PLAN] hasDeadEnd=True reasons=ActionCompleted plan=PassiveRoofExit[target=-559080] -> PassiveAdvance[target=-559696]`
- Runtime execution follows that plan:
  - `FIRE kind=PassiveRoofExit targetId=-559080 ... desc=Passive roof exit before bigAlive`
  - `COMPLETE kind=PassiveRoofExit targetId=-559080 ... state=Run`
  - `FIRE kind=PassiveAdvance targetId=-559696 ... runtimeNearestThreat=bigAlive#-559080 lane=bottom x=[-0.50,0.50] runtimeUnsafe=[2.46,5.10] runtimeIntersects=False desc=Passive advance past smallAlive`
  - after `PassiveAdvance`, `SuperJumpOver` is no longer valid: target `bigAlive#-559080` is at `x=[-2.48,-1.48]` / `x=[-2.10,-1.10]` and the strategy reports `bigAlive требует дополнительный зазор`.
  - collision follows: `CollisionController damage ... obstacle=bigAlive#-559080 ... x=[-3.01,-2.01]`.

### Code Path

- `ActionGenerator.Generate` first collects current-lane blocker actions, then still collects opposite-lane route actions and calls `CollectPassiveAdvanceAction` whenever `hasOppositeDecisionPoint` is true (`ActionGenerator.cs`, lines 102-139).
- `PassiveAdvanceStrategy.CanConsider` is explicitly opposite-lane only and only requires ground `Run` plus required planning role on the opposite lane (`PassiveAdvanceStrategy.cs`, lines 27-38). It does not own the invariant "current blocking threat must already be resolved".
- `PassiveAdvancePlanner.TryBuildModel` computes a no-input completion shift from the opposite-lane boundary obstacle and only checks current-lane safety until that short completion shift plus post-action guard (`PassiveAdvancePlanner.cs`, lines 43-55).
- In this case `PassiveAdvance` completion is about `1.57-1.75`, while the valid `SuperJumpOver` travel over the current `bigAlive` is `5.13`. The local passive-advance safety check therefore accepts the action because `runtimeUnsafe=[2.46,5.10]` starts after the passive completion, even though this wait consumes the later jump window.
- `PlanEvaluator.SelectBestDeadEnd` selects dead-end fallback by farthest first failure projection (`PlanEvaluator.cs`, lines 39-55 and 138-163). That lets a `PassiveRoofExit -> PassiveAdvance` safe-prefix survive as a fallback branch even though it does not resolve the imminent current-lane `bigAlive`.

### Root Cause

For the adjusted geometry, the root cause is a contract gap between action generation and branch evaluation:

- `SuperJumpOver` over the current bottom `bigAlive` is valid and affordable at the decision point.
- `PassiveAdvance` is also generated from the opposite lane in the same planning state.
- `PassiveAdvance` is treated as route setup / no-input waiting, not as an action that must resolve the current `BlockingThreat`.
- The dead-end fallback plan can therefore activate `PassiveRoofExit -> PassiveAdvance`, which waits past the short opposite-lane boundary and burns the `SuperJumpOver` window. After that, `SuperJumpOver` correctly rejects because the `bigAlive` is already too close.

This is a bot logic regression in fallback/action-priority semantics, not a level geometry impossibility.

### Excluded Alternatives For Adjusted Geometry

- Insufficient energy: excluded. Valid `SuperJumpOver` is created with `energy=32`; policy cost is `20`.
- No physical window: excluded. `SuperJumpOver` reports `stage=action-created`, `first=0.74`, `selected=0.74`, `last=2.05`.
- Runtime executor cancelling `SuperJumpOver`: excluded. The activated plan contains `PassiveAdvance`, and no `FIRE kind=SuperJumpOver targetId=-559080` occurs.
- `ShouldPreserveCurrentHandoffTail` preserving a stale tail: excluded. A temporary guard log was added and did not fire in the failing run.
- Post-`PassiveAdvance` rejection as primary cause: excluded. It is a consequence: after `PassiveAdvance`, target x is already around `[-2.48,-1.48]`, so the normal `bigAlive` padding rejection is expected.

### Architectural Recommendation

The invariant should live at the planning/action-priority boundary: route-setup/passive waiting actions must not be allowed to outrank or bypass a valid current-lane `BlockingThreat` resolver. Keep this centralized in action generation or branch comparison, not as per-level thresholds or strategy-specific symptom guards; the fix should make "resolve immediate damaging blocker before passive route setup" an explicit contract.

## Temporary Diagnostics

Temporary diagnostics were added during the original analysis to:

- enable strategy diagnostics in automation;
- log roof-to-road jump-on resolver gating;
- run diagnostic-only fire-window checks for bottom-lane `bigAlive`;
- identify the ground `bigAlive` fire-window collapse.

Those original diagnostics were removed before the first analysis ended.

Temporary diagnostics added for the reopened adjusted-geometry pass:

- `RuntimeBotController`: enabled `Strategy` diagnostics for the automation run and added temporary `BIG_ALIVE_PASSIVE_ADVANCE_PLAN` / handoff-tail probes.
- `BotStrategyDiagnostics`, `ActionGenerator`, `SuperJumpOverStrategy`, `PlanBuilder`: added temporary targeted `BIG_ALIVE_*` probes for action generation, `SuperJumpOver` fire-window acceptance/rejection, branch/dead-end selection, and fallback plan contents.

These diagnostics were removed after the related Paris Night level 06 investigation was completed.
