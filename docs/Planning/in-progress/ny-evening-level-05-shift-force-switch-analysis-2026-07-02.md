# New York Evening level_05 shift_force_switch analysis - 2026-07-02

## Scope

- Regression: `EV-REG-2026-07-02-004`.
- Level: `01_New_York/Evening/level_05`.
- Pattern: `shift_force_switch`.
- Observed result: `FAIL`.
- Request: prove root cause with Bug Regression Workflow; user allowed adding an energetic for energy-starvation regressions.

## Sources

- Workflow: `.github/prompts/manual-stages/bug-regression-workflow.prompt.md`.
- Discovery report: `docs/Planning/in-progress/ny-evening-regressions-2026-07-02.md`.
- Saved log: `Temp/campaign_ny_evening_2026-07-02_112134/01_New_York_Evening_level_05.txt`.
- Level JSON: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Evening/level_05/level_05.json`.
- Pattern catalog: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Code paths:
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/Contracts/IPlanningStrategy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

## Commands

- Used existing Evening campaign run artifacts.
- Mapped `shift_force_switch` spawn `obstacleIds` to template slots.
- Read final execution window around log lines 320-377.
- Checked `LevelResolver`: level `overrides` only change sprite names by obstacle id, they cannot add a collectable.

## Facts

- `level_05` sequence has `shift_force_switch` at pattern index 5.
- `shift_force_switch` is referenced by `01_New_York/Evening/level_05` and `01_New_York/Night/level_05`.
- Relevant mapped slots:
  - slot 35: `runtimeId=-1144496`, `smallAlive`, top lane, local `x=36.00`.
  - slot 36: `runtimeId=-1144520`, `bigAlive`, top lane, local `x=42.40`.
  - slot 37: `runtimeId=-1144544`, `bigAlive`, top lane, local `x=49.40`.
  - slot 38: `runtimeId=-1144568`, `smallAlive`, top lane, local `x=55.20`.
  - slot 39: `runtimeId=-1144592`, `smallAlive`, top lane, local `x=65.40`.
- Final execution:
  - line 332: `JumpOn` over `smallAlive#-1144496`, energy becomes `51`.
  - lines 339-343: `SuperJumpOver#-1144520`, energy becomes `33` after two spends.
  - lines 348-352: `SuperJumpOver#-1144544`, energy becomes `15` after two spends.
  - line 357: `JumpOn#-1144568`, energy becomes `7`.
  - line 363: energy regenerates to `9`.
  - line 364: `PassiveAdvance` completes.
  - line 367: damage by `smallAlive#-1144592`.
- Dead-end causes at lines 370-375:
  - `JumpOverStrategy`: insufficient energy, required `10`, available `9`.
  - `SuperJumpOverStrategy`: insufficient energy, required `20`, available `9`.
  - `JumpOnStrategy`: insufficient energy, required `10`, available `9`.
  - `SuperJumpOnStrategy`: insufficient energy, required `20`, available `9`.
  - lane-switch strategies also have no safe window, so jump energy is the only remaining viable class of action.

## Code Path

- `PlanningStrategyResult.InsufficientEnergy` emits the exact `Недостаточно энергии` messages with required and available energy.
- `ActionGenerator` collects those applicable strategy dead-end reasons.
- `PlanBuilder` returns a dead-end fallback when no successful branch is available.
- `RuntimeBotController` logs confirmed `[Bot DEAD_END]` after the collision, using the pending planning report.

## Proven Root Cause

`shift_force_switch` consumes the available energy through a forced top-lane chain (`JumpOn`, two `SuperJumpOver`, `JumpOn`) and reaches the final `smallAlive#-1144592` with only `9` energy. The remaining valid avoidance classes are jump actions requiring at least `10` or `20` energy, while lane switching has no safe window. Therefore the level design starves the bot by exactly one energy before the final required action.

## Excluded Alternatives

- Geometry-only no-window: excluded as sole root cause because four jump strategies explicitly fail on energy, not geometry.
- Executor lifecycle bug: excluded because all preceding actions complete normally in `Run`.
- Missing collectible pickup logic: no uncollected energy collectable exists between the last spend and `smallAlive#-1144592` in the original `shift_force_switch` segment.

## Recommendation

Add an energy pickup before the final `smallAlive#-1144592`, ideally in a level-specific variant so shared Night content is not unintentionally retuned. Longer-term, add a content validation rule that simulates mandatory action energy budget across a pattern and flags any path that reaches a required jump action below its energy cost.

## Energy Tune Verification

- User allowed adding an energetic for confirmed energy-starvation regressions.
- Applied content tune in `PatternsCollection.json`:
  - `shift_force_switch.nextObstacleId`: `40` -> `41`.
  - Added slot `id=40`, `type=5` (`collectableEnergetic`), top lane, local `x=60.20`, `y=-1.80`.
- Targeted run: `01_New_York/Evening/level_05`, `TimeoutSeconds=600`, `TimeScale=1`.
- New artifact: `Temp/evening_level05_energy_tune_2026-07-02_1234/01_New_York_Evening_level_05_after_energy_tune.txt`.
- Verification facts:
  - line 362: new energetic fired as `PassiveCollect`.
  - line 364: `collectableEnergetic#-1151392` collected on top lane.
  - line 366: energy added `+30`, value `38`.
  - lines 372-377: the formerly failing final `smallAlive` from `shift_force_switch` was handled by `JumpOn` and completed.
- Result: original `shift_force_switch` energy starvation is removed.
- New uncovered result: same run later fails at `peak_3` entry with geometry/no-safe-window causes. Tracked separately in `docs/Planning/in-progress/ny-evening-level-05-peak-3-after-energy-tune-analysis-2026-07-02.md`.
