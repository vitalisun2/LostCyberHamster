# Paris Night Level 03 Shift Force Switch Analysis - 2026-07-09

## Scope

- Location: `02_Paris/Night/level_03`.
- Pattern: `shift_force_switch`, pattern index `5`.
- Runtime symptom: hamster on bottom lane collides with bottom-lane `bigAlive`.
- Screenshot fact: overlay shows `Paris Night, shift_force_switch 5, PAUSED, Run, energy: 34, isDamaged: True`.
- Final interpretation after user correction: this is an edge/unpassable segment for current strategy constraints, not a bot logic regression.

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

## Temporary Diagnostics

Temporary diagnostics were added during analysis to:

- enable strategy diagnostics in automation;
- log roof-to-road jump-on resolver gating;
- run diagnostic-only fire-window checks for bottom-lane `bigAlive`;
- identify the ground `bigAlive` fire-window collapse.

All temporary diagnostics were removed before ending the analysis. Final repository code contains no `ROOF_TO_ROAD_JUMPON` markers or diagnostic helper methods from this investigation.
