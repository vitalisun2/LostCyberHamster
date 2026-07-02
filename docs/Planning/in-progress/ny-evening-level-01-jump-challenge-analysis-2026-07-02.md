# New York Evening level_01 jump_challenge analysis - 2026-07-02

## Scope

- Regression: `EV-REG-2026-07-02-001`.
- Level: `01_New_York/Evening/level_01`.
- Pattern: `jump_challenge`.
- Current observed result: `FAIL`.
- User request: analyze one regression with Bug Regression Workflow, prove root cause, do not fix.

## Sources

- Workflow: `.github/prompts/manual-stages/bug-regression-workflow.prompt.md`.
- Discovery report: `docs/Planning/in-progress/ny-evening-regressions-2026-07-02.md`.
- Run artifact: `Temp/campaign_ny_evening_2026-07-02_112134/01_New_York_Evening_level_01.txt`.
- Level JSON: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Evening/level_01/level_01.json`.
- Pattern catalog: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.

## Commands

- Evening discovery batch was already run through `launch_test_level` with `TimeoutSeconds=600`, `TimeScale=1`.
- Analysis uses saved log and source data; no new gameplay run was needed.
- Read `level_01.json` pattern sequence.
- Read `jump_challenge` and `medium_difficulty_3` from `PatternsCollection.json`.
- Mapped `jump_challenge` runtime `obstacleIds` from the saved log back to pattern obstacles.
- Read planning/dead-end code paths:
  - `PlanBuilder.cs`
  - `PlanningGraphBuilder.cs`
  - `ActionGenerator.cs`
  - `IPlanningStrategy.cs`
  - `RuntimeBotController.cs`
  - `SwitchLaneStrategy.cs`
  - `SwitchLaneFireWindowCalculator.cs`
  - `SwitchLaneTiming.cs`
  - `JumpOverChainCalculator.cs`
  - `SuperJumpOverStrategy.cs`

## Known Facts

- `level_01` failed.
- Damage line in saved log: line 411.
- Dead-end line in saved log: line 412.
- Damage obstacle maps by spawn obstacle id to pattern `jump_challenge`, pattern index 6.
- Dead-end causes mention no safe lane-switch window and no safe super jump-over window for `bigAlive`.
- `level_01` pattern sequence is:
  - `easy_run`
  - `small_jumps_2`
  - `roof_bonus_run_2`
  - `shift_line_choice`
  - `bonus_strip_2`
  - `medium_difficulty_3`
  - `jump_challenge`
  - `easy_run_3`
- `jump_challenge` spawn log line 285 lists runtime ids for pattern index 6.
- Runtime-id mapping for the first `jump_challenge` obstacles:
  - pattern obstacle 0: `runtimeId=-1122214`, `smallNotAliveRoadAndRoof`, local `x=-4.60`, bottom lane.
  - pattern obstacle 1: `runtimeId=-1122236`, `smallNotAliveRoadAndRoof`, local `x=0.00`, bottom lane.
  - pattern obstacle 15: `runtimeId=-1122562`, `bigAlive`, local `x=-5.20`, top lane.
  - pattern obstacle 16: `runtimeId=-1122586`, `bigAlive`, local `x=-4.00`, top lane.
- Final plan/execution before damage:
  - line 391: `JumpOnRoof -> PassiveCollect[Energy:30] -> PassiveRoofExit -> SwitchLane`.
  - line 401: `PassiveRoofExit` fired before `smallNotAliveRoadAndRoof#-1122214`.
  - line 403: current plan became `PassiveRoofExit -> SwitchLane`.
  - line 405: `PassiveRoofExit` completed in `Run`.
  - line 408: `SwitchLane` fired before `smallNotAliveRoadAndRoof#-1122214`, target lane `top`.
  - line 409: `SwitchLane` completed on `top`.
  - line 411: damage by `bigAlive#-1122562` on `top`; hamster `x=[-4.60,-2.96]`, obstacle `x=[-2.96,-1.96]`.
- Energy facts:
  - line 397: energy pickup to `37`.
  - lines 404, 406, 407, 410: energy regenerated to `38`, `39`, `40`, `41`.
  - no dead-end cause says `Недостаточно энергии`.
- Planning/dead-end code facts:
  - `PlanBuilder.Build` selects a successful branch first; if none exists, it selects `PlanningDeadEndBranch` and can return its safe-prefix actions with the dead-end report.
  - `PlanningGraphBuilder` creates `PlanningDeadEndReport` when generation has no actions for an unresolved situation and has dead-end reasons.
  - `ActionGenerator` applies all applicable strategy results and records `StrategyDeadEndReason`.
  - `RuntimeBotController` remembers a pending dead-end report from plan build and confirms it only after actual life loss.
  - `SwitchLaneStrategy` returns the observed `безопасный интервал слишком узкий` reason when safe intervals exist but none can produce a usable sample after margins.
  - `SwitchLaneFireWindowCalculator` builds unsafe target-lane intervals from damaging obstacles and subtracts them from the possible launch window.
  - `SuperJumpOverStrategy` checks energy before window search; observed message came after energy check, from `JumpOverChainCalculator` rejecting the `bigAlive` padded window.

## Hypotheses

- H1: level geometry/design creates no valid safe window around `jump_challenge`.
- H2: bot logic misses a valid action window even though level geometry is passable.
- H3: failure is caused by energy starvation.
- H4: failure is secondary to previous-pattern action lifecycle/timing rather than `jump_challenge` geometry.

## Hypothesis Status

- H1 is supported by runtime id mapping, final plan trace, dead-end causes, and strategy code paths.
- H2 is not supported by the available evidence: the planner selected only safe-prefix dead-end fallback after no successful branch was available, and the three recorded causes are window/geometry causes from applicable strategies.
- H3 is excluded by energy value `41` before damage and lack of `Недостаточно энергии` causes.
- H4 is not supported: `PassiveRoofExit` completed in `Run`, `SwitchLane` completed on the requested lane, then the confirmed dead-end was against the `jump_challenge` top-lane `bigAlive`.
