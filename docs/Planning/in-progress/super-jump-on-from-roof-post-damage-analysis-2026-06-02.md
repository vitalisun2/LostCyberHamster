# SuperJumpOnFromRoof post-damage analysis 2026-06-02

## Scope

Регресс: `01_New_York/Morning/test_super_jump_on_from_roof`, последний паттерн `test_super_jump_on_from_roof_04`.

Фактически бот выполняет `SuperJumpOnFromRoof` на `smallAlive`, после возврата в `Run` сразу получает damage от следующего `smallNotAliveRoad`.

Ожидаемо planner должен отсеять такую ветку, если после target removal нет безопасного ground re-entry.

## Sources

- Логи: `EditorLogs/diagnostic_log.txt`, каналы `BOT` и `STAB`.
- Уровень: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_super_jump_on_from_roof/test_super_jump_on_from_roof.json`.
- Паттерны: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Код:
  - `SnapshotBuilder`
  - `DecisionPointDetector`
  - `JumpOnFromRoofTargetChainComposer`
  - `JumpOnFromRoofStrategy`
  - `SuperJumpOnFromRoofStrategy`
  - `JumpOnFromRoofRetainedActionValidator`
  - `TargetRemovalPostActionSafety`
  - `PlanBuilder`
  - `CollisionController`
  - `SuperJumpOnFromRoofExecutor`

## Log Facts

- `00:24:12.764` fire `JumpOnRoof bigNotAlive`.
- `00:24:13.778` complete `JumpOnRoof`, state `RoofRun`.
- `00:24:15.030` fire `SuperJumpOnFromRoof smallAlive`, `triggerX=-3.82`, `renderX=4.75`, `obstacleLeftX=-3.83`.
- `00:24:17.648` complete `SuperJumpOnFromRoof`, state `Run`.
- `00:24:17.668` `CollisionController damage source=Stay reason=RunState`, hamster `[-4.60,-2.96]`, obstacle `smallNotAliveRoad` `[-3.46,-2.06]`.
- STAB around the last pattern reports `obstacles=6`, matching all obstacles in `test_super_jump_on_from_roof_04`.

## Asset Facts

`test_super_jump_on_from_roof_04` has 6 obstacles:

- `bigNotAlive` at `x=-5.2`.
- `smallNotAliveRoad` at `x=-2.4`, `x=-0.8`, `x=0.8`.
- target `smallAlive` at `x=2.4`.
- post-target `smallNotAliveRoad` at `x=8.0`.

## Code Facts

- `SnapshotBuilder` collects obstacles from `screenLeftEdgeX` to `visionRightEdgeX`.
- `visionRightEdgeX = screenRightEdgeX + fullScreenWidth`.
- `DecisionPointDetector.TryDetectRequiredDecisionPoint` uses:
  - first obstacle horizon: `ScreenRightEdgeX`;
  - target horizon: `VisionRightEdgeX`.
- `JumpOnFromRoofTargetChainComposer` builds the roof-exit road target chain up to `maxTargetLeftX`.
- Both `JumpOnFromRoofStrategy` and `SuperJumpOnFromRoofStrategy` call `TargetRemovalPostActionSafety.IsSafeAfterCompletion` before adding the action.
- `TargetRemovalPostActionSafety` checks same-lane damaging obstacles at exactly `completionWorldShift`; it does not check a re-entry interval after `Run` or the next controllable frame.
- `CollisionController` damage is `Stay/RunState`, so the damage happens after state changes to `Run`, not during roof jump resolver hit detection.
- `PlanBuilder` uses `InProgressExecutionHandoffActionCount = 2`; while a head action is in progress, both the in-progress head and the next action are retained without retained validation.

## Hypotheses

### H1: planner does not look beyond screen edge

Status: mostly disproved.

Evidence:
- Snapshot has a separate `VisionRightEdgeX`.
- Required roof jump-on target detection uses `VisionRightEdgeX`.
- Existing STAB logs show `obstacles=6` around the last pattern, matching the whole pattern including the post-target `smallNotAliveRoad`.

Remaining uncertainty:
- Existing logs do not print exact `screenRightEdgeX`, `visionRightEdgeX` and each obstacle bound at the original candidate creation frame.

### H2: post-action safety is too local

Status: supported.

Evidence:
- Damage occurs on `RunState` immediately after action completion.
- `TargetRemovalPostActionSafety` only checks instantaneous overlap at `completionWorldShift`.
- It does not reserve any distance/time for the first physics/bot tick after `Run` or for the next action fire window.

### H3: retained handoff can preserve stale next action

Status: supported by code, not fully proven by current logs.

Evidence:
- During an in-progress head action, `PlanBuilder` skips validation for action indices `0` and `1`.
- The second retained action can be a not-yet-fired `SuperJumpOnFromRoof`.
- If a post-target obstacle becomes visible or changes the safety result while `JumpOnRoof` is in progress, the next action can survive until fire without `JumpOnFromRoofRetainedActionValidator`.

## Root Cause

Confirmed root cause class: the planner's target-removal safety model is not strict enough for ground re-entry after long roof-to-road jump-on actions.

The user's screen-edge hypothesis is directionally plausible as a symptom, but current code/logs show the planner already has a vision horizon and the last pattern was visible as 6 obstacles. The more precise issue is that a previously planned long `SuperJumpOnFromRoof` can remain valid even when the post-target `smallNotAliveRoad` makes the `Run` re-entry unsafe.

## Proposed Solution

1. Replace point-only `TargetRemovalPostActionSafety` with a stricter shared post-run re-entry safety policy.
   - Keep the same shared call site for `JumpOn`, `SuperJumpOn`, `JumpOnFromRoof`, `SuperJumpOnFromRoof`.
   - Check same-lane damaging obstacles after target removal over a short interval `[completionWorldShift, completionWorldShift + reentryGuardTravel]`, not only at one point.
   - `reentryGuardTravel` should represent the minimum non-controllable/runtime handoff distance after returning to `Run`, derived from existing timing constants where possible, not a per-level magic number.
   - Implemented: `TargetRemovalPostActionSafety` now uses `JumpPlanningConstants.FireWindowBoundaryMargin` as the shared re-entry guard travel.

2. Revalidate tail actions during in-progress head projection.
   - Do not skip retained validation for the next not-yet-fired action solely because the previous action is in progress.
   - Keep the in-progress head atomic.
   - Validate or rebuild the tail from the projected completion state, so newly visible post-target threats can cancel stale jump-on actions before fire.
   - Implemented: `PlanBuilder` now treats only the in-progress head as atomic; the next action can still be retained, but only through the existing retained validation path.

3. Add compact temporary diagnostics for one verification pass.
   - Log candidate kind, target, `completionWorldShift`, screen/vision bounds, and the first post-target damaging obstacle considered by post-action safety.
   - Remove diagnostics after confirming the fix.

## Validation Plan

- Manual run: `01_New_York/Morning/test_super_jump_on_from_roof`.
- Expected: last pattern no longer chooses an unsafe `SuperJumpOnFromRoof` that lands into `smallNotAliveRoad`.
- Regression: previous roof jump-on/from-roof patterns still pass; safe `SuperJumpOnFromRoof` remains available when post-run re-entry is clear.

## Implementation Notes

- Changed `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/TargetRemovalPostActionSafety.cs`.
- Changed `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`.
- No temporary diagnostics were added in the first implementation pass; the code path was clear enough from existing logs and source.
