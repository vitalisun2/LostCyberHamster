# Paris Night Level 06 BigAlive Passive Advance Analysis - 2026-07-09

## Scope

- Location: `02_Paris/Night/level_06`.
- Initial suspected symptom: bot collides with a `bigAlive` in a situation similar to Paris Night level 03.
- Refined actual symptom from runtime facts: the reproduced collision is with bottom-lane `smallAlive#-579580`, not a `bigAlive`.
- Runtime pattern at failure: pattern index `4`, `roof_bonus_run`.
- Expected source: user report that the case must be proven independently and that a physically valid handling window should exist if the bot has enough energy.
- Expected behavior for the proven case: after landing back on the bottom lane, the bot must handle the next same-lane damaging `smallAlive` with a valid resolver (`JumpOn` in the observed run), not replace it with no-input opposite-lane `PassiveAdvance`.
- Actual behavior: after a committed `JumpOn` over the previous `smallAlive`, replan projects only `3` energy, rejects same-lane jump resolvers as insufficient-energy, selects `PassiveAdvance`, and collides with `smallAlive#-579580`.

## Minimal Reproduction

```powershell
.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '02_Paris/Night/level_06' -TimeoutSeconds 240
```

## Investigation Log

- Created after proving the adjusted-geometry Paris Night level 03 root cause.
- Temporary targeted bot diagnostics from the level 03 investigation were reused and extended for this run:
  - `BIG_ALIVE_SUPER_JUMP_OVER`
  - `BIG_ALIVE_ACTION_GEN`
  - `BIG_ALIVE_BRANCH_SELECTION`
  - `BIG_ALIVE_DEAD_END_FALLBACK`
  - `BIG_ALIVE_PASSIVE_ADVANCE_PLAN`
  - `PASSIVE_ADVANCE_THREAT_GEN`
  - `SMALL_ALIVE_PASSIVE_ADVANCE_PLAN`

## Facts

### Runtime Result

- Command: `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '02_Paris/Night/level_06' -TimeoutSeconds 240`.
- Result: `[TEST RESULT] FAIL`.
- Collision line: `[CollisionController] damage ... obstacle=smallAlive#-579580 ... x=[-3.01,-1.49]`.
- Life loss line: `[LifeLoss] amount=1 lives=2 energy=16 state=Run lastPatternIndex=4 lastPattern=roof_bonus_run`.

### Energy And Action Timeline

| Time | Fact | Meaning |
|---|---|---|
| `17:05:12.824` | `[Energy] added amount=30 value=40` | The run enters the roof segment with enough energy. |
| `17:05:13.562` | `FIRE kind=SuperJumpOnFromRoof targetId=-579262 triggerId=-579238` | The roof-to-road super jump-on fires. |
| `17:05:13.565` | `[Energy] spent amount=10 value=31` | First runtime input: `RoofJumpRequest`. |
| `17:05:13.752` | `[Energy] spent amount=10 value=21` | Second runtime input: `SuperRoofJumpRequest`; total runtime cost is `20`. |
| `17:05:16.191` | `COMPLETE kind=SuperJumpOnFromRoof` | The bot lands on the road correctly. |
| `17:05:16.197` | `FIRE kind=JumpOn targetId=-579556 ... desc=Jump on smallAlive` | The bot starts resolving the previous `smallAlive`. |
| `17:05:16.198` | `[Energy] spent amount=10 value=13` | Runtime has already paid for the committed `JumpOn`. |
| `17:05:16.200` | `[Bot PASSIVE_ADVANCE_THREAT_GEN] ... energy=3 ... threat=smallAlive#-579580 ... deadEnds=... JumpOverStrategy:Недостаточно энергии ... нужно 10, доступно 3 \| JumpOnStrategy:Недостаточно энергии ... нужно 10, доступно 3 ...` | Async tail replan projects the in-progress `JumpOn` and subtracts its cost again. Same-lane resolvers for the next `smallAlive` are rejected in planning because projected energy is false-low. |
| `17:05:16.202` | `[Bot SMALL_ALIVE_PASSIVE_ADVANCE_PLAN] hasDeadEnd=True reasons=ActionCompleted plan=JumpOn[target=-579556] -> PassiveAdvance[target=-579940]` | The selected tail keeps the committed `JumpOn`, then replaces handling of `smallAlive#-579580` with `PassiveAdvance`. |
| `17:05:18.029` | `COMPLETE kind=JumpOn targetId=-579556` | The previous `smallAlive` is resolved. |
| `17:05:18.037` | `FIRE kind=PassiveAdvance targetId=-579940 ... runtimeNearestThreat=smallAlive#-579580 ... runtimeUnsafe=[3.51,6.67] runtimeIntersects=False` | `PassiveAdvance` is locally accepted because the current-lane unsafe interval starts after its short completion shift. |
| `17:05:18.923` | `CollisionController damage ... obstacle=smallAlive#-579580` | The plan never fires a resolver for the actual next same-lane threat. |

### Valid Resolver Facts

- The section is not physically impossible: earlier generation for the same target contains valid current-lane `JumpOn` actions for `smallAlive#-579580`.
- Diagnostic examples:
  - `[17:05:12.850] [Bot PASSIVE_ADVANCE_THREAT_GEN] ... energy=10 ... threat=smallAlive#-579580 ... actions=JumpOn/target=-579580 ... PassiveAdvance/target=-579918 ...`
  - `[17:05:13.373] [Bot PASSIVE_ADVANCE_THREAT_GEN] ... energy=11 ... threat=smallAlive#-579580 ... actions=JumpOn/target=-579580 ... PassiveAdvance ...`
- `JumpOnPolicy.EnergyCost` is `10`, so those generated actions prove the resolver was affordable and considered valid before the false-low in-progress projection.

## Code Path

- `SuperJumpOnFromRoofExecutor.TryFire` checks `hamster.Energy.Value < action.EnergyCost`, then invokes `RoofJumpRequest`; `IsCompleted` later invokes `SuperRoofJumpRequest`. `SuperJumpOnFromRoofPolicy.EnergyCost` is `20`, and `EnergyMechanics` spends `10` for each request, matching the runtime `40 -> 31 -> 21` energy facts.
- `JumpOnExecutor` fires the committed `JumpOn[target=-579556]`; `EnergyMechanics.OnJump` spends `10`, so live energy is already post-cost (`13`) when the async tail replan runs.
- `RuntimeBotController.BuildCommittedPrefix` preserves the fired/waiting execution head. `BuildTailRootState` calls `ProjectInProgressCommittedAction` for that head, and `ProjectInProgressCommittedAction` delegates to `TransitionSimulator.ProjectInProgress`.
- `JumpOnSimulator.ProjectInProgress` calls `PlanningStateTransition.ApplyRunAfterOver(planningState.Hamster, action)`.
- `PlanningStateTransition.ApplyRunAfterOver` subtracts `action.EnergyCost` from the `HamsterSnapshot`.
- `RuntimeBotController.CopyActionWithWorldShifts` preserves `action.EnergyCost` when creating projection actions, so the in-progress `JumpOn` still carries cost `10`.
- Therefore, the committed `JumpOn` cost is applied twice for the tail planning state: once by live runtime energy mechanics when the input fires, and once by the in-progress planning projection.
- `ActionGenerator.Generate` then sees projected `energy=3`, rejects `JumpOn`/`JumpOver` for the next same-lane `smallAlive` as insufficient-energy, and still adds opposite-lane `PassiveAdvance` when an opposite decision point exists.
- `PassiveAdvancePlanner.TryBuildModel` checks only that the current lane is safe until the short passive completion shift plus guard. In the failing run that local contract reports `runtimeIntersects=False`, so `PassiveAdvance` is accepted even though it does not resolve `smallAlive#-579580`.

## Root Cause

Rechecked against an independent Paris Night level 06 analysis: the original "energy projection only" conclusion is too narrow. The proven root cause is the combination of a dangerous `PassiveAdvance` contract gap and execution handoff ordering; the false-low energy projection is an upstream contributor that explains why this particular run installed the dangerous tail.

Proven chain:

1. Runtime has enough energy after firing the committed `JumpOn`: live energy is `13`, then regenerates to `15` before `PassiveAdvance` and `16` by the collision.
2. The async tail planning root is built by projecting the committed in-progress `JumpOn` from a live snapshot that already reflects the runtime energy spend.
3. `JumpOnSimulator.ProjectInProgress` uses `PlanningStateTransition.ApplyRunAfterOver`, which subtracts the same `JumpOn` `EnergyCost=10` again.
4. The next planning state therefore sees `energy=3`, and current-lane resolvers for `smallAlive#-579580` are rejected with `Недостаточно энергии ... нужно 10, доступно 3`.
5. With those resolvers falsely removed, `PassiveAdvance` remains selectable and is retained as the post-`JumpOn` tail: `JumpOn[target=-579556] -> PassiveAdvance[target=-579940]`.
6. `PassiveAdvancePlanner` considers the action safe because it only checks current-lane collision until `completionWorldShift + guard`; it does not check whether the no-input interval preserves the next schedulable current-lane jump window.
7. `PlanExecutor.AdvanceHead` keeps the remaining tail after `JumpOn` completes, and `RuntimeBotController.TickBot` ticks the executor before applying the action-completed replan. Therefore `PassiveAdvance` can fire on the next tick before a fresh plan can replace the tail.
8. The fired `PassiveAdvance` waits past the valid response window for `smallAlive#-579580`; after that, `JumpOn` correctly reports no valid window and the bot collides.

So the earliest observed divergence in this run is the false-low in-progress energy projection, but the architecture-level root cause for the collision is that `PassiveAdvance` is allowed to consume a schedulable same-lane response window and execution handoff lets that retained tail fire before replan replacement.

## Excluded Alternatives

- BigAlive collision: excluded. The damaging obstacle is `smallAlive#-579580`, and the life-loss line reports `lastPatternIndex=4 lastPattern=roof_bonus_run`.
- Impossible geometry/window: excluded. The generator creates valid `JumpOn/target=-579580` actions at `energy=10` and `energy=11`.
- Real runtime energy shortage: excluded. Runtime energy is `13` immediately after `JumpOn[target=-579556]` fires, then regenerates, and the damage line has `energy=16`.
- `SuperJumpOnFromRoof` cost mismatch: excluded. Policy cost is `20`; runtime spends two `10` events, exactly matching the policy.
- PassiveAdvance collision math bug: excluded as primary cause. In the failing runtime line, `PassiveAdvance` reports `runtimeIntersects=False` because the immediate unsafe interval starts after its short completion. The bug is that this passive route setup is treated as safe even though it consumes the schedulable answer window.

## Architectural Recommendation

Fix the `PassiveAdvance` contract first: no-input route setup must prove it preserves any schedulable current-lane response window, not only that collision does not occur before passive completion. Also tighten the execution/replan boundary so an action-completed replan can replace a dangerous retained no-input tail before it fires; separately fix committed in-progress energy projection so tail planning does not reject valid resolvers from a false-low energy state.

## Cleanup

Temporary diagnostics added for this investigation:

- `RuntimeBotController`: temporary strategy/economy diagnostic enablement and `SMALL_ALIVE_PASSIVE_ADVANCE_PLAN` probe.
- `BotStrategyDiagnostics` / `ActionGenerator`: temporary `PASSIVE_ADVANCE_THREAT_GEN` probe.
- Reused level 03 `BIG_ALIVE_*` probes remained active during this run.

All temporary diagnostics were removed before final report.
