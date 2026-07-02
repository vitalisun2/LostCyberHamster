# New York Evening level_04 roof_narrow_gap_4 analysis - 2026-07-02

## Scope

- Regression: `EV-REG-2026-07-02-003`.
- Level: `01_New_York/Evening/level_04`.
- Pattern: `roof_narrow_gap_4`.
- Observed result: `FAIL`.
- Request: prove root cause with Bug Regression Workflow.

## Sources

- Workflow: `.github/prompts/manual-stages/bug-regression-workflow.prompt.md`.
- Discovery report: `docs/Planning/in-progress/ny-evening-regressions-2026-07-02.md`.
- Saved log: `Temp/campaign_ny_evening_2026-07-02_112134/01_New_York_Evening_level_04.txt`.
- Level JSON: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Evening/level_04/level_04.json`.
- Pattern catalog: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Code paths:
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/PassiveRoofExit/PassiveRoofExitPlanner.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/RoofExitSafety.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverChainCalculator.cs`

## Commands

- Used existing Evening campaign run artifacts; no extra runtime diagnostics were added.
- Mapped `roof_narrow_gap_4` spawn `obstacleIds` to template obstacle slots.
- Read final execution window around log lines 100-141.

## Facts

- `level_04` sequence has `roof_narrow_gap_4` at pattern index 2.
- Relevant mapped slots:
  - slot 33: `bigNotAlive`, top lane, local `x=50.40`.
  - slot 34: `bigNotAlive`, top lane, local `x=55.20`.
  - slot 35: `bigNotAlive`, top lane, local `x=60.20`.
  - slot 36: `runtimeId=-1137018`, `bigAlive`, top lane, local `x=66.20`.
- Final execution:
  - lines 115-119: `JumpOnRoof` lands on top roof.
  - lines 122-128: `SuperRoofJumpOver` handles the roof hazard chain and remains in `RoofRun`.
  - line 129: `PassiveRoofExit` fires before `bigAlive#-1137018`.
  - line 133: `PassiveRoofExit` completes in `Run`.
  - line 134: damage by `bigAlive#-1137018`, lane `top`, hamster `x=[-4.60,-2.96]`, obstacle `x=[-2.96,-1.96]`.
- Dead-end causes at lines 137-139:
  - `SwitchLaneStrategy`: current lane safe interval too narrow.
  - `SuperJumpOverStrategy`: `bigAlive` requires extra gap.
  - `SwitchLaneStrategy`: opposite lane has no positive launch interval.
- Energy is not limiting: energy is `21` before damage; no cause says `Недостаточно энергии`.

## Code Path

- `PassiveRoofExitPlanner` allows the no-input roof exit because the RunFromRoof transition itself passes `RoofExitSafety`.
- `PlanBuilder` falls back to a safe-prefix dead-end branch when no successful branch exists.
- `ActionGenerator` records applicable strategy failures. The logged causes come from strategy window calculations, not from post-damage inference.
- `RuntimeBotController` confirms the already pending dead-end only after the collision.

## Proven Root Cause

`roof_narrow_gap_4` ends a top-lane roof sequence with a passive exit directly into top-lane `bigAlive#-1137018`. The exit transition can complete, but the following avoidance window is impossible: lane-switch windows collapse and the only jump-over option needs extra `bigAlive` clearance that the geometry does not provide. This is a level geometry/no-safe-window regression.

## Excluded Alternatives

- Energy starvation: excluded by energy `21` and no insufficient-energy causes.
- Road-landing action lifecycle bug: excluded because `PassiveRoofExit` completed in `Run`.
- Wrong target mapping: excluded by runtime-id mapping from pattern index 2 to slot 36.

## Recommendation

Tune `roof_narrow_gap_4` by moving `bigAlive` farther after the roof exit or reshaping the final top roof sequence so the bot gets a valid post-exit switch/jump window. Add the same content validation as for `roof_narrow_gap_3`: roof exits must be checked against the next actionable window, not only against immediate re-entry safety.
