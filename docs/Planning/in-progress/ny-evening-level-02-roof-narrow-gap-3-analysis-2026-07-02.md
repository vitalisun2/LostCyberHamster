# New York Evening level_02 roof_narrow_gap_3 analysis - 2026-07-02

## Scope

- Regression: `EV-REG-2026-07-02-002`.
- Level: `01_New_York/Evening/level_02`.
- Pattern: `roof_narrow_gap_3`.
- Observed result: `FAIL`.
- Request: prove root cause with Bug Regression Workflow.

## Sources

- Workflow: `.github/prompts/manual-stages/bug-regression-workflow.prompt.md`.
- Discovery report: `docs/Planning/in-progress/ny-evening-regressions-2026-07-02.md`.
- Saved log: `Temp/campaign_ny_evening_2026-07-02_112134/01_New_York_Evening_level_02.txt`.
- Level JSON: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Evening/level_02/level_02.json`.
- Pattern catalog: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Code paths:
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/PassiveRoofExit/PassiveRoofExitPlanner.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/RoofExitSafety.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverFireWindowFinder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOn/JumpOnWindowCalculator.cs`

## Commands

- Used existing Evening campaign run artifacts; no extra runtime diagnostics were added.
- Mapped `roof_narrow_gap_3` spawn `obstacleIds` to template obstacle slots.
- Read final execution window around log lines 95-132.

## Facts

- `level_02` sequence has `roof_narrow_gap_3` at pattern index 2.
- Spawn line 25 maps `roof_narrow_gap_3` runtime ids to template slots.
- Relevant mapped slots:
  - slot 28: `runtimeId=-1126400`, `bigNotAlive`, top lane, local `x=34.20`.
  - slot 29: `runtimeId=-1126420`, `bigNotAlive`, top lane, local `x=40.20`.
  - slot 30: `runtimeId=-1126440`, `bigNotAlive`, top lane, local `x=45.80`.
  - slot 31: `runtimeId=-1126464`, `smallAlive`, top lane, local `x=52.80`.
- Final execution:
  - line 103: `RoofSwitchLane` lands on top roof `bigNotAlive#-1126400`.
  - lines 106 and 113: two `JumpFromRoofOnRoof` actions continue through top roofs to `bigNotAlive#-1126440`.
  - line 118: `PassiveRoofExit` fires with target `smallAlive#-1126464`.
  - line 120: `PassiveRoofExit` completes in `Run`.
  - line 122: damage by `smallAlive#-1126464`, lane `top`, hamster `x=[-4.60,-2.96]`, obstacle `x=[-2.99,-1.47]`.
- Dead-end causes at lines 125-130:
  - `SwitchLaneStrategy`: current lane safe interval too narrow.
  - `JumpOverStrategy` and `SuperJumpOverStrategy`: runtime model does not confirm safe traversal.
  - `JumpOnStrategy`: safety margin leaves no safe jump-on window.
  - `SuperJumpOnStrategy`: target does not intersect allowed trajectory.
  - `SwitchLaneStrategy`: opposite lane has no positive launch interval.
- Energy is not limiting: last energy before damage is `36`; no cause says `Недостаточно энергии`.

## Code Path

- `PassiveRoofExitPlanner` builds a model only if `RoofExitSafety.IsSafeDuringRunFromRoof` accepts the roof-exit transition.
- `RoofExitSafety` checks direct RunFromRoof overlap and immediate Run re-entry guard, not the existence of a later jump/switch window.
- `PlanBuilder.Build` selects a successful branch first. If no successful branch exists, it selects a `PlanningDeadEndBranch` and may return the safe-prefix actions together with the dead-end report.
- `ActionGenerator` accumulates dead-end reasons from every applicable strategy.
- `RuntimeBotController` stores the pending dead-end report when the plan is built, and logs confirmed `[Bot DEAD_END]` only after the life loss.

## Proven Root Cause

`roof_narrow_gap_3` has a top-lane roof chain ending at `bigNotAlive#-1126440`, followed too closely by top-lane `smallAlive#-1126464`. The passive roof exit itself is valid, but after returning to `Run` there is no valid fire-window for the next required avoidance action. Every applicable strategy reports a geometry/window rejection, and the collision confirms the pending dead-end.

## Excluded Alternatives

- Energy starvation: excluded by energy `36` and no insufficient-energy causes.
- Executor lifecycle bug: excluded because `PassiveRoofExit` completed in `Run` before damage.
- Missed single-strategy action: excluded because current-lane switch, opposite-lane switch, jump-over, super jump-over, jump-on, and super jump-on all returned no-window reasons.

## Recommendation

Tune `roof_narrow_gap_3` geometry: increase the distance between the final top roof exit and `smallAlive#-1126464`, or move the threat so at least one post-exit strategy has a valid fire-window. Architecturally, add a content validation rule for roof-chain exits: passive roof exit must be followed by a reachable action window, not merely by a locally safe Run re-entry.
