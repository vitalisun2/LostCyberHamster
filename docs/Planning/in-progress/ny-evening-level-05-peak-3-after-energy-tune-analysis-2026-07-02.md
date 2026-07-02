# New York Evening level_05 peak_3 after energy tune analysis - 2026-07-02

## Scope

- Regression: newly uncovered after fixing `EV-REG-2026-07-02-004` energy starvation.
- Level: `01_New_York/Evening/level_05`.
- Pattern: `peak_3`.
- Observed result after energy tune: `FAIL`.
- Expected result: after `shift_force_switch` is made passable, the bot must have a valid first action window at the next pattern boundary and must not lose a life on `peak_3` entry.
- Actual result: the bot completes a `SwitchLane` away from the top-lane entry threat, lands on bottom lane, and immediately loses a life on the bottom-lane entry threat.
- Request context: prove the root cause in a separate regression-analysis document.

## Sources

- Saved post-tune log: `Temp/evening_level05_energy_tune_2026-07-02_1234/01_New_York_Evening_level_05_after_energy_tune.txt`.
- Level JSON: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Evening/level_05/level_05.json`.
- Pattern catalog: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Code paths:
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverFireWindowFinder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOn/JumpOnWindowCalculator.cs`

## Commands

- Ran `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Evening/level_05' -TimeoutSeconds 600 -TimeScale 1`.
- Copied `LostCyberHamster/EditorLogs/diagnostic_log.txt` to the saved post-tune artifact path.
- Mapped `peak_3` spawn `obstacleIds` to template slots.

## Facts

- `level_05.json` has `shift_force_switch` at sequence index 5 and `peak_3` immediately after it at sequence index 6.
- The post-tune run reaches and resolves the formerly failing tail of `shift_force_switch`:
  - line 362: `PassiveCollect` fires for the new energy collectible.
  - line 364: `collectableEnergetic#-1151392` is collected on top lane.
  - lines 365-366: energy increases by `+30` to `38`.
  - lines 372-377: the formerly failing final `smallAlive#-1151370` is handled by `JumpOn` and completes in `Run`.
- Relevant `peak_3` slot mapping from post-tune log:
  - slot 0: `runtimeId=-1151414`, `smallNotAliveRoad`, bottom lane, local `x=-4.80`.
  - slot 1: `runtimeId=-1151438`, `smallAlive`, bottom lane, local `x=1.20`.
  - slot 21: `runtimeId=-1151906`, `smallAlive`, top lane, local `x=-0.20`.
- Final execution:
  - line 380: `SwitchLane` fires before top-lane `smallAlive#-1151906`, target lane `bottom`.
  - line 381: `SwitchLane` completes on bottom lane.
  - line 382: damage by bottom-lane `smallAlive#-1151438`, hamster `x=[-4.60,-2.96]`, obstacle `x=[-2.99,-1.47]`.
- Dead-end causes at lines 385-390:
  - current-lane `SwitchLaneStrategy`: safe interval too narrow.
  - `JumpOverStrategy` and `SuperJumpOverStrategy`: runtime model does not confirm safe traversal.
  - `JumpOnStrategy` and `SuperJumpOnStrategy`: target does not intersect allowed jump trajectory.
  - opposite-lane `SwitchLaneStrategy`: no positive launch interval remains.
- No insufficient-energy cause is present in this post-tune failure.
- Energy at the last visible tick before `SwitchLane` is `33`, so ordinary jump actions are not blocked by the `10` energy threshold and super jump actions are not blocked by the `20` threshold.

## Code Path

- `PlanningGraphBuilder.ExploreNode` collects candidate actions and stores a `PlanningDeadEndBranch` when generation reaches an unresolved situation with no successful actions (`PlanningGraphBuilder.cs:124-139`, `244-254`).
- `PlanBuilder.Build` returns a dead-end fallback plan when there is no successful branch but the best dead-end branch has a safe prefix (`PlanBuilder.cs:97-108`, `130-138`). That is why a valid first `SwitchLane` can still be followed by a pending dead-end diagnosis.
- `SwitchLaneStrategy.CollectActions` builds the escape action from the current-lane top threat and rejects other switch variants when the target/current lane fire window cannot be sampled (`SwitchLaneStrategy.cs:76-131`, `158-184`).
- `JumpOverFireWindowFinder` rejects jump-over alternatives when the runtime-equivalent resolver cannot confirm a safe over result (`JumpOverFireWindowFinder.cs:59-80`).
- `JumpOnWindowCalculator` rejects jump-on alternatives when the target does not intersect the allowed jump trajectory or the raw fire window collapses (`JumpOnWindowCalculator.cs:70-130`, `138-150`).
- `RuntimeBotController.ApplyPlanBuildResult` stores the dead-end report as pending (`RuntimeBotController.cs:662-680`, `712-721`), and `OnLivesLost` confirms it only after actual damage (`RuntimeBotController.cs:733-744`).
- The runtime log matches this path exactly: safe-prefix `SwitchLane` fires and completes, then collision with `smallAlive#-1151438` confirms the stored dead-end report.

## Proven Root Cause

`peak_3` begins with mutually incompatible first-action requirements on both lanes. The top lane has `smallAlive#-1151906` at local `x=-0.20`, so staying on top is immediately unsafe and the planner can only preserve a safe prefix by switching to bottom. The bottom lane has `smallAlive#-1151438` at local `x=1.20`, so the target lane is already inside the hamster collision interval right after the switch completes (`hamsterX=[-4.60,-2.96]`, obstacle `x=[-2.99,-1.47]`). The planner reports no successful branch: switch windows collapse, jump-over runtime safety is not confirmed, jump-on trajectories do not intersect, and the opposite-lane switch has no positive launch interval. Therefore the proven root cause is `peak_3` entry geometry: the pattern starts with overlapping cross-lane threats that leave no valid safe window after the required first lane switch.

## Excluded Alternatives

- Energy starvation: excluded by energy `33` before `SwitchLane` and by the absence of any `Недостаточно энергии` dead-end cause in lines 385-390.
- `shift_force_switch` original issue: excluded because the new energetic is collected, energy rises to `38`, and the former final `smallAlive` is handled successfully before `peak_3`.
- Executor completion bug: excluded because line 381 confirms `SwitchLane` completes on the requested bottom lane before the collision.
- Bot planner false negative: excluded by runtime collision coordinates after the completed switch; the target bottom lane is physically occupied by `smallAlive#-1151438`, matching the planner's no-safe-window report.
- Missing diagnostic coverage: excluded because existing `[Bot EXEC]`, `[CollisionController]`, `[Bot DEAD_END]`, and `[Energy]` lines provide the action, lane, obstacle ids, energy, damage coordinates, and strategy rejection reasons.

## Recommendation

Tune the `peak_3` entry in content: move either the top-lane `smallAlive` or the bottom-lane `smallAlive` so that the first forced lane switch has a safe post-completion window. Add a pattern-boundary validation rule that rejects pattern starts where both lanes require mutually incompatible first actions inside the same fire-window.

## Temporary Diagnostics

No temporary diagnostic code was added. The proof uses the saved post-tune diagnostic log and existing bot diagnostics.
