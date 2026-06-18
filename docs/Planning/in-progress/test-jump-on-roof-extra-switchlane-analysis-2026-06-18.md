# test_jump_on_roof_01 extra SwitchLane analysis - 2026-06-18

## Scope

Regression: `01_New_York/Morning/test_jump_on_roof`, first checked pattern `test_jump_on_roof_01`.

Expected: pattern description says `should jump on roof`, so the meaningful action for the pattern is `JumpOnRoof`.

Actual: current run executes several `SwitchLane` actions before `JumpOnRoof`.

- Source log: `Temp/all_test_levels_2026-06-18_184905/01_New_York_Morning_test_jump_on_roof.txt`
- Lines 3-12: `SPAWN test_jump_on_roof_01`, plan starts as `SwitchLane x6`, then replans to `SwitchLane -> SwitchLane -> JumpOnRoof`, and only after that fires `JumpOnRoof`.

## Sources

- Test run log: `Temp/all_test_levels_2026-06-18_184905/01_New_York_Morning_test_jump_on_roof.txt`
- Targeted candidate log: `Temp/test_jump_on_roof_01_candidates_2026-06-18.txt`
- Level file: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_jump_on_roof/test_jump_on_roof.json`
- Pattern catalog: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`
- Planning code: `PlanBuilder`, `PlanningGraphBuilder`, `PlanEvaluator`, `PlanningBranchMetrics`
- Generation/simulation code: `ActionGenerator`, `SwitchLaneStrategy`, `JumpOnRoofStrategy`, `PlanningStateTransition`

## Hypotheses

1. `JumpOnRoofStrategy` does not generate a valid candidate at the first planning build.
   - Confirms: candidate diagnostics show no branch with `JumpOnRoof` from the first build.
   - Refutes: candidate diagnostics show a branch containing `JumpOnRoof`.
   - Status: refuted.

2. `PlanEvaluator` chooses the switch-only horizon branch because it has zero energy cost and no objective marker, while `JumpOnRoof` costs energy.
   - Confirms: candidate diagnostics show selected branch is switch-only, has lower energy, and competing `JumpOnRoof` branch exists.
   - Refutes: selected branch wins by another proven metric.
   - Status: confirmed.

3. The switch cascade is caused by action generation creating only opposite-lane entries and no current-lane roof action.
   - Confirms: candidate diagnostics/action generation show only `SwitchLane` candidates before the first `JumpOnRoof`.
   - Refutes: current-lane `JumpOnRoof` candidate exists in the same build.
   - Status: refuted.

## Facts

- In the current log, the first selected plan after `test_jump_on_roof_01` is `SwitchLane -> SwitchLane -> SwitchLane -> SwitchLane -> SwitchLane -> SwitchLane`.
- The same pattern later fires `JumpOnRoof`, so the level geometry is passable and the target action is eventually available.
- The current evaluator compares objective priority, then horizon progress for max-depth branches, then total energy, then tap count.
- Targeted candidate log lines 4-11 show the first planning build has 7 branches:
  - selected branch: `SwitchLane x6`, `cost=0`, `taps=6`, `count=6`, `jumpObj=0`, `finalNext=0`.
  - competing branch: `SwitchLane -> JumpOnRoof`, `cost=10`, `taps=1`, `count=2`, `jumpObj=0`, `finalNext=0`.
  - competing branches with `JumpOnRoof` after 3 or 5 switch actions also exist.
- The selected branch contains no meaningful obstacle-resolution action; it only alternates lane switches until the planning depth limit.
- `JumpOnRoof` candidates have `jumpObj=0`; the current objective-priority metric is only about jump-on collectible objectives, not about ordinary pattern target actions.
- After the first actual `SwitchLane`, the next build selects `JumpOnRoof` as best branch, proving the action itself is valid and safe.

## Root Cause

The planning graph allowed consecutive `SwitchLane` actions that bounced the branch between lanes without progressing to a later obstacle. Those ping-pong branches were then passed to `PlanEvaluator` as normal candidates. Because `SwitchLane` costs zero energy, a switch-only chain could beat the real solution branch `SwitchLane -> JumpOnRoof`.

In simple terms, this was not an evaluator problem first. The graph was generating a meaningless route, and the evaluator was asked to rank it as if it were a normal plan.

## Fix

- Mark `SwitchLane` actions that are only opposite-lane entries with `PlannedAction.IsOppositeLaneEntry`.
- In `PlanningGraphBuilder`, prune redundant `SwitchLane -> SwitchLane` continuations:
  - always prune after an opposite-lane entry;
  - prune when the next switch returns to the same or an earlier target obstacle index.
- Remove global `FinalNextObstacleIndex` ranking from `PlanEvaluator`; progress-first ranking was a symptom fix and broke short semantic choices such as `test_switch_lane_01`.

## Verification

- `01_New_York/Morning/test_jump_on_roof`: WIN, `test_jump_on_roof_01` now selects `SwitchLane -> JumpOnRoof` instead of `SwitchLane x6`.
- User manually ran all test levels: OK.
- User manually ran New York level 1: OK.
