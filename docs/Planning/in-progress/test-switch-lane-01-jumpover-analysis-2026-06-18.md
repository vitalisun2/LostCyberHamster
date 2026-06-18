# test_switch_lane_01 JumpOver analysis - 2026-06-18

## Scope

Regression: `01_New_York/Morning/test_switch_lane`, first checked pattern `test_switch_lane_01`.

Expected: pattern description says `should switch lane`, so the bot must fire `SwitchLane` for this pattern.

Actual: the bot fires `JumpOver`:

- Source log: `Temp/all_test_levels_progress_fix_2026-06-18/01_New_York_Morning_test_switch_lane.txt`
- Lines 3-5: `SPAWN pattern=test_switch_lane_01`, then `Bot PLAN JumpOver`, then `FIRE kind=JumpOver`.

## Sources

- Test run log: `Temp/all_test_levels_progress_fix_2026-06-18/01_New_York_Morning_test_switch_lane.txt`
- Targeted candidate log: `Temp/test_switch_lane_01_candidates_2026-06-18.txt`
- Pattern catalog: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`
- Level file: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_switch_lane/test_switch_lane.json`
- Ranking code: `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanEvaluator.cs`
- Action generation: `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
- Strategies: `SwitchLaneStrategy`, `JumpOverStrategy`

## Hypotheses

1. `SwitchLaneStrategy` does not generate a candidate for this geometry.
   - Confirms: candidate diagnostics show no `SwitchLane` successful branch for the first planning build.
   - Refutes: candidate diagnostics include at least one successful branch that starts with `SwitchLane`.
   - Status: refuted.

2. `JumpOver` wins because the new progress-first branch ranking prefers a branch with larger `FinalNextObstacleIndex`.
   - Confirms: candidate diagnostics show selected `JumpOver` branch has higher `finalNext` than `SwitchLane` branch and wins despite expected pattern action.
   - Refutes: selected branch does not win on `finalNext`.
   - Status: confirmed.

3. The test expectation is impossible or stale.
   - Confirms: asset geometry makes `SwitchLane` unsafe and no valid switch candidate exists.
   - Refutes: a valid `SwitchLane` candidate exists and the level can still finish.
   - Status: refuted.

## Facts

- `test_switch_lane_01` description is `should switch lane`.
- Pattern geometry contains bottom `smallNotAliveRoadAndRoof` at `x=3.6` and top `smallAlive` at `x=8.4`.
- Targeted candidate log lines 5-8:
  - line 5: `SPAWN pattern=test_switch_lane_01`
  - line 6: selected branch is `JumpOver`, `cost=10`, `taps=0`, `finalNext=2`.
  - line 7: valid `SwitchLane` candidate exists, `cost=0`, `taps=1`, `finalNext=1`.
  - line 8: `JumpOver` is selected.
- Current `PlanEvaluator.CompareBranches` compared `FinalNextObstacleIndex` before energy and taps for all successful branches, not only max-depth horizon branches.

## Root Cause

The previous progress-ranking fix was applied too broadly. It correctly fixed max-depth horizon branches, but it also changed normal short one-action choices. In `test_switch_lane_01`, the valid `SwitchLane` candidate lost to `JumpOver` only because `JumpOver` advances `finalNext` from 1 to 2. This violates the test pattern expectation and is not caused by missing switch generation or impossible geometry.

## Fix

Remove global `FinalNextObstacleIndex` ranking from `PlanEvaluator`. Branch selection returns to the simple order: JumpOn objective priority, then energy, then tap count.

The switch-lane oscillation that originally motivated progress ranking is handled at graph-generation level instead: `PlanningGraphBuilder` prunes redundant consecutive `SwitchLane` branches that bounce between lanes without advancing to a later obstacle.

## Verification

- `01_New_York/Morning/test_switch_lane`: WIN, `test_switch_lane_01` selects `SwitchLane`; candidate log shows `SwitchLane` wins over `JumpOver`.
- User manually ran all test levels: OK.
- User manually ran New York level 1: OK.
