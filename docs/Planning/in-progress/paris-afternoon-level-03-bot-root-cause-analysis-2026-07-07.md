# Paris Afternoon Level 03 Bot Root Cause Analysis - 2026-07-07

## Scope

- Level: `02_Paris/Afternoon/level_03`.
- Failing pattern area: `medium_difficulty_3`.
- Observed by user: bot jumps the first bottom `smallNotAliveRoadAndRoof` box, then does not jump the second box and runs into the failure.
- Workflow: bug regression / analysis-only on `integration/unity-live`.
- No bot logic fix was made in this pass.

## Key Timeline

- `diagnostic_log.txt:2023`: `medium_difficulty_3` spawns bottom boxes:
  - `-84860:smallNotAliveRoadAndRoof:bottom:spawn=(56.00,-2.80)`.
  - `-84882:smallNotAliveRoadAndRoof:bottom:spawn=(60.20,-2.80)`.
  - nearby top blockers: `-85268:bigAlive`, `-85292:bigAlive`, then `-85314:smallNotAliveRoad`.
- `diagnostic_log.txt:6558`: before the failure path, graph builder proves a valid branch exists:
  `JumpOn -> PassiveCollect -> JumpOver -84860 -> JumpOver -84882 -> JumpOnRoof -> PassiveCollect`, recorded as `LEAF`.
- `diagnostic_log.txt:6601`: after an action-completed rebuild, installed plan becomes dead-end fallback:
  `hasDeadEnd=True`, `reportDepth=2`, `reportProjection=12.82`, `plan=JumpOn -> JumpOver -> PassiveAdvance`.
- `diagnostic_log.txt:6609-6611`: after `JumpOn -84816` completes, the tail rebuild still generates ordinary `JumpOver -84882`.
- `diagnostic_log.txt:6613-6620`: the `JumpOver -84882` path continues into `JumpOnRoof`, but then reaches `candidates=none`, `reasons=none`, `unresolved=True`; no `LEAF` and no `DEAD_END` are emitted for that path.
- `diagnostic_log.txt:6622-6624`: because the `JumpOver -84882` path disappeared from graph results, the selected plan is `JumpOver -84860 -> PassiveAdvance`.
- `diagnostic_log.txt:6625-6630`: executor fires `JumpOver -84860`, then immediately fires `PassiveAdvance -85292`; runtime still sees nearest bottom threat `smallNotAliveRoadAndRoof#-84882`.
- `diagnostic_log.txt:6652`: run finishes `FAIL`, lives `0`, energy `94`.

## Hypotheses

### H1: Energy starvation

Status: ruled out.

Evidence:
- Energy is `100` before the problematic sequence and `90` after firing `JumpOver -78172`.
- Final failure has energy `94`.
- The failure is not caused by inability to pay for `JumpOver` or `SuperJumpOver`.

### H2: Wrong/missing sprites or Paris fallback issue

Status: ruled out.

Evidence:
- Runtime obstacles are spawned and perceived with valid fallback sprites.
- The log reports bot planning dead-end and executed actions, not asset load failure.

### H3: The second box is not jumpable by type/spec

Status: ruled out.

Evidence:
- `ObstacleTypeEnum` maps type `3` to `smallNotAliveRoadAndRoof`.
- `ObstacleClassifier.CanJumpOverOnGround` allows `smallNotAliveRoadAndRoof`.
- `diagnostic_log.txt:5222` proves ordinary `JumpOver` for `-78194` existed: super fallback was skipped because ordinary action already covered that target.

### H4: Runtime trigger gate missed or cancelled `JumpOver -78194`

Status: ruled out.

Evidence:
- There is no `EXEC FIRE` or `EXEC CANCEL` for `targetId=-78194`.
- `JumpOverExecutor` can only gate already-planned actions; `-78194` never reached it as head action.

### H5: Level geometry alone is impossible

Status: not proven as root cause.

Evidence:
- `medium_difficulty_3` has a tight tail: bottom boxes at `x=56.0` and `x=60.2`, while top `bigAlive` blockers at `x=55.6` and `x=57.0` create a competing opposite-lane situation.
- The same pattern appears in other passing Paris runs, so the shared pattern is not proven globally invalid.
- The geometry is a trigger case, but not the direct cause of the bot choosing not to jump the second box.

### H6: Bot selected and executed a known dead-end fallback

Status: confirmed as the failure mechanism, not by itself the root cause.

Evidence:
- `PlanBuilder.Build` selects `bestDeadEndBranch` when no successful branch exists.
- `PlanBuilder.BuildDeadEndFallbackResult` creates a live `BotPlan` from the dead-end branch actions and attaches the dead-end report.
- `RuntimeBotController.ApplyPlanBuildResult` remembers the dead-end report but still calls `_executor.SetPlan(plan)`.
- `PlanExecutor` has no `HasDeadEnd` concept; it executes the installed `BotPlan` head action.
- Logs show this exact sequence:
  - `hasDeadEnd=True plan=JumpOver -> PassiveAdvance`.
  - `EXEC FIRE JumpOver targetId=-78172`.
  - `EXEC FIRE PassiveAdvance targetId=-78604`.
- Important nuance: this fallback path is expected only when the planner found no successful branch. Therefore the root-cause question is not "why did a dead-end fallback exist", but "why did the planner have no successful branch and then allow this tail to execute before a fresh post-action choice".

### H7: `PassiveAdvance` guard is too weak for this situation

Status: confirmed as contributing cause.

Evidence:
- `PassiveAdvancePlanner` checks only that current-lane damaging obstacle does not intersect the no-input completion shift plus short post-action guard.
- It does not check that after passive advance the next current-lane action still has a schedulable fire window.
- Runtime line before `PassiveAdvance`:
  - `runtimeNearestThreat=smallNotAliveRoadAndRoof#-78194`.
  - `runtimeUnsafe=[0.87,3.91]`.
  - `runtimeIntersects=False`.
- This means the collision-only guard lets `PassiveAdvance` run even though it consumes the useful timing window for the next jump.

### H8: Planner/runtime handoff lets a dead-end tail fire before a fresh post-action plan can replace it

Status: confirmed.

Evidence:
- `RuntimeBotController.TickBot` calls `_executor.Tick(_hamster)` before promoting deferred replan reasons and before applying/starting replans.
- When `JumpOver -78172` completes, `PlanExecutor.AdvanceHead` leaves the remaining tail `PassiveAdvance` as the current plan.
- On the next tick, executor sees that tail first and fires `PassiveAdvance`.
- Only after that does the controller promote/action-completed replan work.
- The log order matches:
  - `22:06:18.021` `COMPLETE kind=JumpOver targetId=-78172`.
  - `22:06:18.035` `FIRE kind=PassiveAdvance targetId=-78604`.
  - `22:06:18.050` `PLAN_RESULT_DIAG ... hasDeadEnd=True ... plan=PassiveAdvance`.

### H9: `JumpOver -> JumpOver` branch does not exist

Status: ruled out.

Evidence:
- `diagnostic_log.txt:6572`, `6588`, `6611`: planner generates ordinary `JumpOver` for the second box.
- `diagnostic_log.txt:6558`: a longer branch containing both jumps is recorded as `LEAF`.
- Therefore the real question is why this generated path disappears later, not whether it was generated.

### H10: Graph builder silently drops unresolved/no-reason paths

Status: proven root cause for the missing selected branch.

Evidence:
- `PlanningGraphBuilder.ExploreNode` only adds a leaf when there is no unresolved planning situation, and only adds a dead-end when dead-end reasons exist.
- If a node has `candidates.Count == 0`, `HasUnresolvedPlanningSituation == true`, and `HasDeadEndReasons == false`, the method returns without adding either a successful branch or a dead-end branch.
- The failing tail shows exactly that:
  - `diagnostic_log.txt:6611`: after committed first-box projection, `JumpOver -84882` is a candidate.
  - `diagnostic_log.txt:6613`: after `JumpOver -84882`, `JumpOnRoof -84902` is a candidate.
  - `diagnostic_log.txt:6618` and `6620`: the path reaches `candidates=none`, `reasons=none`, `unresolved=True`.
  - There is no `LEAF` or `DEAD_END` for that path before `diagnostic_log.txt:6623`.
- Meanwhile the sibling branch `JumpOver -84860 -> PassiveAdvance` does produce a dead-end report at `diagnostic_log.txt:6622-6623`, so it becomes the best available fallback.

### H11: `RunFromRoof` current-lane threat has no owning strategy

Status: proven as the concrete source of `unresolved=True / candidates=none / reasons=none`.

Evidence:
- The unresolved node is not before the second box. It is after the generated continuation:
  - `diagnostic_log.txt:6618`: prefix `JumpOver -84882 -> JumpOnRoof -84902 -> PassiveCollect -85400 -> RoofSwitchLane -85444`, `lane=top`, `state=RunFromRoof`, `candidates=none`, `reasons=none`, `unresolved=True`.
  - `diagnostic_log.txt:6620`: prefix `JumpOver -84882 -> JumpOnRoof -84902 -> RoofSwitchLane -85444`, `lane=top`, `state=RunFromRoof`, `candidates=none`, `reasons=none`, `unresolved=True`.
- `diagnostic_log.txt:6617` and `6619` show the current decision point: first obstacle is `smallNotAliveRoadAndRoof`, role `BlockingThreat`, on the hamster current top lane.
- `RouteDecisionPointDetector.TryDetectCurrent` reports this as a required current-lane route decision point because the simulated hamster is no longer on a roof after `RoofSwitchLane`.
- `PlanningStateTransition.ApplyLaneSwitch` converts `RoofRun` without resulting roof support into `HamsterStateEnum.RunFromRoof`.
- `JumpOverStrategy` and `SuperJumpOverStrategy` are the semantic owners for a current-lane `BlockingThreat`, but their `CanConsider` uses `PlanningStrategyApplicability.IsGroundRunCurrentLane`.
- `PlanningStrategyApplicability.CanPlanGroundRun` requires `HamsterStateEnum.Run` exactly, while `RunFromRoof` is excluded.
- Because `ActionGenerator.CollectFromStrategy` skips `CollectActions` when `CanConsider` is false, neither jump-over strategy is called. Therefore no action and no dead-end reason can be produced.
- Runtime executors for ground jump actions already treat `RunFromRoof` as a waitable state, which indicates this transition state is expected in live action flow but not represented consistently in planning applicability.

## Corrected Root Cause

The fully proven failure chain is:

1. The route with the second jump is generated: `JumpOver -84860 -> JumpOver -84882` exists.
2. After the second jump, the path continues through roof actions and reaches a simulated `RunFromRoof` state on the top lane.
3. The detector finds a required current-lane `BlockingThreat` (`smallNotAliveRoadAndRoof`), but the ground jump strategies do not own `RunFromRoof` because `CanPlanGroundRun` only allows `Run`.
4. The action generator therefore returns `candidates=none`, `reasons=none`, `unresolved=True`.
5. `PlanningGraphBuilder` drops that node silently: it is neither a `LEAF` nor a `DEAD_END`.
6. The sibling `PassiveAdvance` path does become a dead-end branch, so `PlanBuilder` has no successful branch to choose and selects `JumpOver -84860 -> PassiveAdvance`.
7. `PlanExecutor` executes that dead-end tail before a fresh post-completion rebuild can replace it, so the bot skips the second box jump and loses a life.

Primary root: planning has no owner for a current-lane `BlockingThreat` while the simulated hamster is in `RunFromRoof`, so the generated `JumpOver -> JumpOver` route turns into an unresolved/no-reason node.

Secondary root: graph construction silently discards unresolved/no-reason branches instead of forcing them into an explainable dead-end or safe leaf/horizon outcome.

Tertiary mechanism: current execution order allows the surviving dead-end tail `PassiveAdvance` to fire immediately after `JumpOver -84860`.

## Proposed Fix Direction

1. Decide the intended planning semantics for `RunFromRoof`:
   - If ground actions may be planned while waiting for `Run`, allow ground planning ownership for `RunFromRoof` and keep executor-side waiting.
   - If no action may be planned until landing completes, add an explicit passive/transition strategy for `RunFromRoof` that either advances safely to `Run` or returns a dead-end when the landing window is unsafe.
2. Fix `PlanningGraphBuilder`: handle `candidates=none`, `unresolved=true`, `deadEndReasons=none` explicitly. This should not silently delete a path; it must become either a leaf/safe horizon result or an explainable dead-end.
3. Add/keep diagnostics for branch generation until this case is fixed, because the missing branch is otherwise invisible in result-level logs.
4. Then fix the action-completed handoff so a dead-end fallback tail cannot fire before a fresh rebuild has a chance to replace it.
5. Strengthen `PassiveAdvancePlanner`: validate that no-input advance preserves a schedulable current-lane response window, not only immediate collision safety.
6. Avoid level/content workaround here; the level exposed a real planner state-ownership bug.

## Temporary Diagnostics

The analysis relies on current temporary verbose diagnostics in `RuntimeBotController`:
- verbose bot diagnostics enabled for automation test levels;
- extra `PatternDetail` logging for `medium_difficulty_3` and `shift_jump_mix`.
- temporary `[Bot BRANCH_PROBE]` diagnostics in `PlanningGraphBuilder`.

Per analysis-only rules, these diagnostics were not removed in this pass.
