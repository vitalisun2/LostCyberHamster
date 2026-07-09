# Paris Night Level 03 - Post SuperJumpOver SmallAlive Regression Analysis - 2026-07-09

## Scope

- Regression: `02_Paris/Night/level_03`, pattern sequence reaches the adjusted `shift_force_switch` / `peak_3` area.
- Expected: after successfully resolving the bottom-lane `bigAlive` with `SuperJumpOver`, the bot must keep handling the next bottom-lane `smallAlive` instead of running into it.
- Actual: the bot completes `SuperJumpOver` over `bigAlive`, then runs in `Run` state into the next bottom-lane `smallAlive`.
- Affected case: Paris Night level 03 after the user moved the lower `bigNotAlive` enough to create a valid `SuperJumpOver` window over `bigAlive`.

## Authoritative Expected Source

- User report: after the earlier `bigAlive` issue is passed, the bot "как будто бы отключился" and just runs into the dog / next `smallAlive`.
- Existing diagnostic log proves the earlier `bigAlive` resolver now fires and completes before the new collision.

## Minimal Reproduction

```powershell
.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '02_Paris/Night/level_03' -TimeoutSeconds 240
```

## Current Runtime Facts From Existing Log

Source: `LostCyberHamster/EditorLogs/diagnostic_log.txt`.

- `19:46:46.367`: applied plan is `JumpOver -> JumpOnRoof -> PassiveRoofExit -> SuperJumpOver`.
- `19:46:47.288`: `FIRE JumpOver targetId=-637016 smallNotAliveRoad`.
- `19:46:48.296`: `COMPLETE JumpOver`.
- `19:46:48.310`: old temporary diagnostic preserved current tail `JumpOnRoof -> PassiveRoofExit -> SuperJumpOver` over shorter async result `JumpOnRoof`.
- `19:46:48.383`: `FIRE JumpOnRoof targetId=-637036 bigNotAlive`.
- `19:46:49.398`: `COMPLETE JumpOnRoof state=RoofRun`.
- `19:46:49.404`: `FIRE PassiveRoofExit targetId=-637060 triggerId=-637036 desc=Passive roof exit before bigAlive`.
- `19:46:49.412`: old temporary diagnostic preserved current tail `PassiveRoofExit -> SuperJumpOver` over shorter async result `PassiveRoofExit`.
- `19:46:50.808`: `COMPLETE PassiveRoofExit state=Run`.
- `19:46:50.964`: `FIRE SuperJumpOver targetId=-637060 bigAlive`, window `[-1.06,-2.37]`.
- `19:46:52.326`: `COMPLETE SuperJumpOver state=Run`.
- `19:46:53.063`: damage from `smallAlive#-637084`, bottom lane, hamster `x=[-4.60,-2.96]`, obstacle `x=[-2.96,-1.44]`.
- `19:46:53.068`: life loss with `energy=16`, `state=Run`, `lastPattern=peak_3`.

## Initial Code Facts

- `PlanExecutor.AdvanceHead()` clears the plan when the completed action is the last action.
- `RuntimeBotController.RequestReplanForExecutionResult()` uses `RequestDeferredReplan(ActionCompleted)` for completed actions, so action-completed replan starts on the next bot tick, not in the same completion tick.
- `PlanningGraphBuilder` has `MaxSearchDepth = 6`; at depth limit it only accepts the leaf as successful if a fresh action generation at that projected state does not expose an unresolved current planning situation.
- `PlanningStateTransition.Advance()` after ground over-actions advances by `action.CompletionWorldShift + JumpPlanningConstants.PostActionReentryGuardTravel`, then rescans the next relevant obstacle.
- `JumpOn` costs 10 energy, `JumpOver` costs 10, `SuperJumpOver` costs 20. Energy at damage is 16, so a normal resolver is not excluded by energy alone at the collision moment.

## Hypotheses

| Hypothesis | Evidence For | Evidence Against | Status |
|---|---|---|---|
| H1: no physical/schedulable window for the next `smallAlive` after `SuperJumpOver` completion | Damage happens only 0.737s after `SuperJumpOver` completion. | At the final action-completed replan the generator builds both `JumpOver` and `JumpOn` for the same `smallAlive`. | Refuted |
| H2: planner had a valid `JumpOn`/`JumpOver` for `smallAlive`, but async action-completed replan applied too late | `SuperJumpOver` was last action, so runtime depends on fresh replan. | Request 92 starts at `20:25:48.941` and applies at `20:25:48.945`, before damage at `20:25:49.699`. | Refuted |
| H3: previous plan tail stopped at `SuperJumpOver` because future projection hit energy hunger on the next `smallAlive` | `ZERO_ACTION_PROBE` logs the same `smallAlive#-671866` as current target with energy `3`, `5`, `7`, `9`; normal `JumpOver`/`JumpOn` cost `10`. | This only explains the old tail. The final live replan after `SuperJumpOver` has energy `15`. | Confirmed for the old tail only |
| H4: final post-`SuperJumpOver` collision is caused by energy hunger | Root replan has energy `15`, but both valid root resolvers cost `10`; after either `JumpOver` or `JumpOn`, projected energy is `5`, and required follow-up jump/roof strategies report insufficient energy. | Energy is sufficient for the first `smallAlive` resolver. | Proven as future dead-end after the first resolver |
| H5: final post-`SuperJumpOver` collision is caused by graph/build logic losing valid candidates/dead-end | Final replan logs `rootActions=JumpOver...>JumpOn...`, then child states have dead-end reasons, but zero-cost `SwitchLane` candidates make `PlanningGraphBuilder` recurse and finally return `branches=0 deadEnds=0`. | None. This is the first code location where expected and actual diverge from a reportable dead-end. | Proven |

## Diagnostic Facts

Command:

```powershell
.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '02_Paris/Night/level_03' -TimeoutSeconds 240
```

Key runtime facts from `LostCyberHamster/EditorLogs/diagnostic_log.txt`:

- `20:25:36.475` / `20:25:38.682` / `20:25:41.012` / `20:25:42.993`: future projections see `smallAlive#-671866` as current bottom-lane `BlockingThreat|Target` with energy `3`, `5`, `7`, `9` and `deadEndReasons=6`.
- `20:25:47.578`: `SuperJumpOver` fires on `bigAlive#-671842`, window `[-1.06,-2.37]`.
- `20:25:48.935`: `SuperJumpOver` completes in `Run`.
- `20:25:48.941`: action-completed replan request 92 starts with `energy=15`, nearest current-lane threat `smallAlive#-671866`, `x=[-0.10..1.42]`.
- `20:25:48.943`: root generation for request 92 builds `JumpOver/cost=10/target=-671866` and `JumpOn/cost=10/target=-671866`; root has `branches=0 deadEnds=0`.
- `20:25:48.945`: request 92 applies `resultPlan=empty`, `hasDeadEnd=False`.
- `20:25:49.699`: damage from `smallAlive#-671866`, bottom lane, hamster `x=[-4.60,-2.96]`, obstacle `x=[-2.99,-1.47]`.
- `20:25:49.702`: life loss with `energy=16`; this is after ~0.76s of regen from the replan's `energy=15`.

## Deep Branch-Break Diagnostic

Second diagnostic pass question: after root `JumpOver` / `JumpOn` for `smallAlive`, where exactly does the graph stop producing a branch or dead-end?

Command:

```powershell
.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '02_Paris/Night/level_03' -TimeoutSeconds 240
```

Key facts from `LostCyberHamster/EditorLogs/diagnostic_log.txt`:

- `21:03:18.705`: `FIRE SuperJumpOver targetId=-678954`, window `[-1.06,-2.37]`.
- `21:03:20.076`: `COMPLETE SuperJumpOver state=Run`.
- `21:03:20.089`: root planning state after completion has `energy=15`, nearest `smallAlive#-678978`, and generated `JumpOver/cost=10` plus `JumpOn/cost=10`.
- `21:03:20.084`: after projected `JumpOver smallAlive#-678978`, child state is `Run/bottom/energy=5/next=10/projection=6.29`; nearest threat is `bigAlive#-679002`; jump/roof strategies report insufficient energy, while generated actions are zero-cost `SwitchLane` candidates.
- `21:03:20.088`: after projected `JumpOn smallAlive#-678978`, child state is `Run/bottom/energy=5/next=10/projection=7.23`; nearest threat is `bigAlive#-679002`; the same energy-hunger reasons exist, while generated actions are zero-cost `SwitchLane` candidates.
- `21:03:20.083` / `21:03:20.087`: after those `SwitchLane` projections, deeper nodes either produce more redundant `SwitchLane` ping-pong or no branch/dead-end output.
- `21:03:20.089`: root result is therefore `branches=0 deadEnds=0`; the bot applies an empty plan.
- `21:03:20.820`: damage from `smallAlive#-678978`.

Code facts:

- `JumpOverPolicy.EnergyCost => 10`; `JumpOnPolicy.EnergyCost => 10`.
- `JumpOverStrategy.CollectActions()` and `JumpOnStrategy.CollectActions()` return an action only after energy and fire-window checks pass.
- `PlanningGraphBuilder.ExploreNode()` handles `candidates.Count == 0` as either successful leaf or dead-end. With non-empty candidates it simulates each candidate and recurses, but if all candidate subtrees produce no branch/dead-end, the current node also returns no branch/dead-end.
- In the proven break, `ActionGenerationResult` at the child node contains both `Actions` (`SwitchLane`) and `DeadEndReasons` (insufficient energy from defensive jump/roof strategies). Because `candidates.Count > 0`, `PlanningGraphBuilder.ExploreNode()` does not preserve those dead-end reasons before exploring candidates.
- `PlanBuilder.Build()` selects successful branches first, then dead-end fallback branches. If both collections are empty, it returns `BotPlan.Empty(...)`.

## Root Cause

The old planned tail ending at `SuperJumpOver` is explained by energy hunger in future projection: the next `smallAlive` was visible, but projected energy was below the normal resolver cost.

The final collision is not caused by lack of a first reaction window to `smallAlive`: after `SuperJumpOver` completed, the live replan had energy `15` and generated valid `JumpOver` and `JumpOn` actions for `smallAlive`.

The proven break is one layer deeper. Projecting either first resolver spends `10` energy and leaves `energy=5`. At that child state the next current-lane threat requires a jump/roof resolver costing at least `10`, so the defensive strategies correctly emit insufficient-energy dead-end reasons. However, the same node also has zero-cost `SwitchLane` candidates. `PlanningGraphBuilder.ExploreNode()` treats non-empty candidates as the only path, recurses into those switch-lane candidates, and when they produce no branch/dead-end, it does not fall back to the dead-end reasons already present at the node. As a result the root `JumpOver`/`JumpOn` paths collapse to `branches=0 deadEnds=0`, `PlanBuilder` applies `BotPlan.Empty`, and runtime keeps running into `smallAlive`.

This corrects the intermediate "missing owner / missing concede" hypothesis: in the reproduced break the ownership and reasons exist; they are lost by graph construction under a mixed `actions + deadEndReasons` result.

## Recommendation

Fix the planning graph contract, not the level and not individual jump thresholds. If a node has generated candidates plus dead-end reasons, and all candidate subtrees fail to produce a successful branch or dead-end, `PlanningGraphBuilder` should preserve the node's existing dead-end reasons as the branch result. This keeps strategy ownership simple and makes the graph invariant explicit: an unresolved dangerous situation cannot disappear into `BotPlan.Empty`.

## Temporary Diagnostics

Temporary diagnostics were added through bot diagnostics classes for this investigation and removed after root cause was proven.
