# Bot Test Level Regressions Analysis - 2026-06-22

## Scope

Analyze regressions reported on New York Morning bot test levels after the branch evaluator priority change.

Fix scope:
- Life-loss / damage regressions on test levels.

Analysis-only scope:
- Description / expectation mismatches where the bot survives but chooses a different action than expected.

Out of scope for this pass:
- Non-test New York level `01_New_York/Morning/level_01`, already verified as passable after the evaluator change.
- Manual tuning of level geometry unless logs prove the level is geometrically impossible.

## Reported Regressions

Potential analysis-only mismatch:
- `test_jump_on`: in one pattern the bot jumps over upper-lane `smallNotAlive` instead of jumping on a lower-lane dog. Need prove whether selected full branch is actually better.

Life-loss regressions to investigate and fix:
- `test_super_jump_on_roof`: bot crashes instead of switching to lower lane and using super jump onto roof.
- `test_roof_jump_over`: second pattern, bot does not switch to upper line and loses a life.
- `test_roof_super_jump_over`: second pattern, bot does not switch to lower line for roof entry and crashes.
- `test_jump_from_roof_to_roof`: second pattern, bot does not switch to upper line for roof entry and crashes into lower `smallNotAlive`.
- `test_super_jump_from_roof_to_roof`: third pattern, bot runs upper line and crashes into multiple `smallNotAlive`; expected lower-line roof entry and roof-to-roof continuation.
- `test_jump_from_roof`: pattern with three upper `bigAlive`, bot should use lower roof path and jump from roof over `smallNotAlive`; instead loses life.
- `test_super_jump_from_roof`: life loss, details to prove from logs.
- `test_jump_on_from_roof`: first patterns ignore roof entry for from-roof jump-on target; later loses life.
- `test_super_jump_on_from_roof`: often ignores from-roof jump-on targets; later crashes into `bigNotAlive`.

## Data Sources

- Runtime logs copied per test-level run under `LostCyberHamster/EditorLogs/regression_runs/2026-06-22-bot-test-levels/`.
- Current runtime diagnostic stream: `LostCyberHamster/EditorLogs/diagnostic_log.txt`.
- Bot planning code:
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningBranchMetricsComparer.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningBranchMetrics.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
- Strategy code for roof/from-roof cases under `LostCyberHamster/Assets/Scripts/Bot/Strategies/`.

## Hypotheses

### H1 - Evaluator under-prioritizes required roof entry because roof-entry actions are not major objectives

What would confirm:
- Logs show safe roof-entry branches are generated but lose to branches with lower `EnergyBeforeFirstMajor`, fewer actions, coins, or later major objectives.
- Strategy actions for roof entry have no major-objective marker and therefore are treated as route cost.

What would refute:
- Roof-entry branches are not generated at all, or generated branch is unsafe/null before evaluator.

Status: pending.

### H2 - Decision point / role detection does not expose opposite-lane roof paths as actionable branches

What would confirm:
- At the failure point, `ActionGenerator` has no candidate switching into the lane needed for roof entry, despite visible geometry.
- Decision point logs/code show the needed roof obstacle chain is optional/ignored/not detected on opposite lane.

What would refute:
- Candidate branch with the correct lane switch and roof action exists before evaluation.

Status: pending.

### H3 - Simulator rejects roof/from-roof branches because post-action safety or completion projection fails

What would confirm:
- Candidate action is generated but `_transitionSimulator.Simulate(...)` returns null or a dead-end reason for the roof/from-roof action.
- Window finder accepts runtime window but post-action safety rejects the state.

What would refute:
- Branch reaches candidate list and loses only at comparer/evaluator.

Status: pending.

### H4 - Current same-state pruning drops roof branches as "not better" before their later safety benefit appears

What would confirm:
- With the same `PlanningStateKey`, a non-roof prefix prunes a roof prefix because the roof prefix has higher current energy/action cost but later avoids damage.
- Branches disappear before final evaluator even though they would become safe later.

What would refute:
- Same-state pruning does not touch the failing branch, or final candidates already include it.

Status: pending.

### H5 - Some reported action mismatches are acceptable full-branch decisions, not safety regressions

What would confirm:
- On `test_jump_on`, selected branch is safe and wins by documented priority over the expected local `JumpOn` branch.
- No life loss occurs and selected branch has better `EnergyBeforeFirstMajor`, `MajorObjectiveCount`, `EnergyCost`, or coin value under current rules.

What would refute:
- The branch skips an equally cheap major objective, or later causes damage.

Status: pending.

## Facts

- Full test-level run started with `tools/invoke_run_all_test_levels.ps1 -TimeoutSeconds 180 -TimeScale 1`.
- Runner discovered 16 test levels.
- Per-level logs directory: `Temp/all_test_levels_2026-06-22_184422`.
- Proven spawn-spacing fix was committed separately: `3dda7ec Fix bot lookahead spacing for relief patterns`.
- After that fix, the first roof-entry spacing regressions passed without life loss:
  - `test_super_jump_on_roof`: `WIN lives=3`
  - `test_roof_jump_over`: `WIN lives=3`
  - `test_roof_super_jump_over`: `WIN lives=3`
- Current clean repro after the spacing fix: `01_New_York/Morning/test_jump_from_roof_to_roof`.
- Clean log copy: `Temp/jump_from_roof_to_roof_after_spawn_fix_clean_2026-06-22.txt`.
- In the failing run, the bot had a safe future roof plan at one point:
  - `PassiveRoofExit -> SwitchLane -> PassiveCollect[Energy:30] -> PassiveCollect[Energy:2] -> SwitchLane -> SwitchLane -> JumpOnRoof`
- After `PassiveRoofExit`, the selected plan became:
  - `SwitchLane -> PassiveCollect[Energy:30] -> SwitchLane -> PassiveAdvance`
- The first `PassiveCollect[Energy:30]` was cancelled with `target-not-found` because the collectible was picked up during/around the preceding `SwitchLane`.
- After that cancel, the selected plan became:
  - `SwitchLane -> PassiveAdvance`
- Runtime then executed two `PassiveAdvance` actions for the same semantic subject:
  - `Passive advance past collectableEnergetic`
  - `Passive advance past collectableEnergetic`
- The bot then lost a life on top lane against `smallNotAliveRoadAndRoof`.
- `PassiveAdvancePlanner` explicitly builds `PassiveAdvance` only for opposite-lane chains.
- `ObstacleRoleClassifier` gives collectibles an active planning role.
- `ObstacleChainElement.HasAnyActivePlanningRole` treats a collectible-only chain as active because its role count is non-zero.
- `DecisionPointDetector` therefore can return an optional-only collectible as the nearest decision point.
- `ActionGenerator` creates `PassiveAdvance` for any opposite-lane decision point, including collectible-only decision points.
- `PassiveAdvancePlanner` does not require `chain.HasAnyRequiredPlanningRole()`, so it permits `PassiveAdvance past collectable...`.
- `PassiveAdvanceSimulator.ProjectInProgress` uses `skipTargetObstacleAfterCompletion: false`; when the in-progress action is "pass past collectible", the tail projection can see the same collectible again and build another `PassiveAdvance` for it.
- Asset facts for `test_jump_from_roof_to_roof_03`:
  - bottom lane starts with `type 11` at `x=-6.4`, where `type 11` is `mediumNotAlive` roof support.
  - top lane has `type 3` at `x=-6.0`, `-4.4`, `-2.8`, `-1.2`, where `type 3` is `smallNotAliveRoadAndRoof`.
  - Therefore, after the relief energy collectible, the route must be built through/past the optional collectible toward the bottom roof entry; waiting/skipping around the collectible on top can lose the roof-entry window.

## Root Cause

Pending final confirmation, but the main proven mechanism is:

- Optional-only collectibles currently participate in decision point detection as active planning situations.
- Because of that, an opposite-lane collectible can become the nearest decision point and produce `PassiveAdvance past collectable...`.
- This violates the intended invariant: collectibles may add branch value, but they must not block or postpone route construction toward required threats/roof supports/targets behind them.
- In the current repro, this lets the planner spend decision depth and runtime time on skipping an energy collectible instead of preserving the path toward the next required roof-entry situation.
- The duplicate `PassiveAdvance past collectableEnergetic` indicates an additional projection bug for in-progress `PassiveAdvance`: the same passed collectible is not skipped/removed for tail planning.

## Fix Plan

Pending final root-cause proof. The likely architectural direction is:

- Make optional-only collectibles pass-through for route-building decision point detection.
- Keep collectible value as an optional objective on reachable branches.
- Do not let `PassiveAdvance` be generated for collectible-only opposite-lane chains.
- Fix in-progress `PassiveAdvance` projection so a passed boundary obstacle cannot immediately produce the same `PassiveAdvance` again.

## Implementation Plan - Collectibles Pass Through Route Building

Design constraints:
- Keep route building and objective scoring separated.
- Do not tune evaluator priorities to hide route-generation bugs.
- Do not add per-level/special-case logic.
- Reuse the existing `ObstacleChain.HasAnyRequiredPlanningRole()` concept instead of introducing a second definition of "required".

Planned code changes:

1. `DecisionPointDetector`
   - Add route-detection API that skips optional-only chains.
   - Existing `TryDetect` remains the "any active planning role" API for cases that explicitly need optional collectables.
   - The new route API uses the same `ObstacleChainBuilder`; it only advances the detection start index past optional-only chains.

2. `ActionGenerator`
   - Use route-detection for current-lane and opposite-lane route actions.
   - Also collect current-lane optional collectable actions from the old "any" detector, but only when the nearest chain is optional-only.
   - Do not generate opposite-lane `SwitchLane` / `PassiveAdvance` only because an optional collectable exists there.

3. `SwitchLaneStrategy` and `PassiveAdvanceStrategy`
   - Guard opposite-lane entry/advance with `HasAnyRequiredPlanningRole()`.
   - This keeps the invariant local to the strategies too, so future callers cannot accidentally reintroduce "entry before collectable" route blockers.

4. `PassiveAdvanceSimulator`
   - In in-progress projection, skip the boundary obstacle after projected completion.
   - A completed "advance past X" must not let tail planning immediately build another "advance past X".

Expected behavior on the current repro:
- `collectableEnergetic` from `relief_energy` no longer becomes the route decision point.
- Planning can look through it and see `test_jump_from_roof_to_roof_03`.
- Opposite-lane bottom `mediumNotAlive` roof-support becomes the route entry target.
- Duplicate `PassiveAdvance past collectableEnergetic` disappears.

## Verification

Pre-commit targeted check:
- `tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_jump_from_roof_to_roof' -TimeoutSeconds 220 -TimeScale 1`
- Result: `WIN level=5 stars=3`
- Damage markers: none in the relevant log.
- Confirmed behavior change:
  - `PassiveAdvance past collectableEnergetic` disappeared from the failing relief-energy transition.
  - The plan now keeps route construction through energy pickup toward `SwitchLane -> JumpOnRoof -> JumpFromRoofOnRoof`.

Pre-commit full safety scan:
- `tools/invoke_run_all_test_levels.ps1 -TimeoutSeconds 220 -TimeScale 1`
- Result: all 16 levels finished with `Damage markers: 0`.
- The runner still reports semantic mismatches on some test descriptions; those are separate expectation/evaluator issues, not life-loss regressions from this fix.

Self-review notes:
- The fix does not tune evaluator metrics.
- The fix centralizes "skip optional-only collectables for route detection" in `DecisionPointDetector.TryDetectRoute`.
- `PlanningGraphBuilder.HasUnresolvedPlanningSituation` reuses the same route detector instead of duplicating optional-skip logic.
- `SwitchLaneStrategy` and `PassiveAdvanceStrategy` now reject optional-only opposite-lane chains as a local strategy contract.
- `PassiveAdvanceSimulator.ProjectInProgress` now skips the passed boundary obstacle to avoid duplicate `PassiveAdvance` for the same subject.
- Intentional scope limit: this patch does not implement a separate "switch lanes only to hunt opposite-lane collectible" branch. It only ensures collectables do not block route construction and remain collectable on reachable/current paths.
