# New York Evening regression discovery - 2026-07-02

## Scope

Дата запуска: 2026-07-02.

Цель: прогнать New York Evening campaign levels и зафиксировать observed regressions без root-cause анализа и без исправлений.

Условия:

- Ветка/workspace: `integration/unity-live`, основной workspace.
- Проверялась текущая рабочая версия бота, включая уже внесенный `RoofSwitchLane` fix.
- Запуск: explicit `launch_test_level` batch через Unity automation bridge, `TimeoutSeconds=600`, `TimeScale=1`.
- Artifacts: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_evening_2026-07-02_112134`.
- Summary JSON: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_evening_2026-07-02_112134\summary.json`.

Diagnostic markers used:

- `[TEST RESULT]`, `[TEST FINISH]`
- `[CollisionController] damage`
- `[Bot DEAD_END]`
- `[Energy] change/spent/added`
- `[Bot PATTERN] SPAWN`

## Summary

- Levels run: 5.
- Clean: 1 (`01_New_York/Evening/level_03`).
- Observed regressions: 4.
- Regression candidates by observed type:
  - `Level design / geometry / no safe window`: 3.
  - `Level design / energy starvation`: 1.
- No bot logic root-cause analysis was performed in this pass.
- No code, level, config, or diagnostic changes were made for this Evening discovery pass.

## Observed Regressions

### EV-REG-2026-07-02-001 - `level_01`, `jump_challenge`

- `type`: `Level design / geometry / no safe window candidate`
- `level`: `01_New_York/Evening/level_01`
- `pattern`: `jump_challenge`
- `patternIndex`: 6
- `result`: `FAIL`
- `log`: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_evening_2026-07-02_112134\01_New_York_Evening_level_01.txt`
- `damage`: line 411, `bigAlive#-1122562`, lane `top`, `livesBefore=3`
- `dead-end`: line 412, `reason=ActionCompleted`, `livesLost=1`, `lives=2`
- `energy`: min logged value `2`, last logged value before fail `41`; dead-end causes did not report insufficient energy.

Observed dead-end causes:

- `SwitchLaneStrategy`: current lane, no safe lane-switch window; safe interval too narrow.
- `SuperJumpOverStrategy`: no safe jump-over window; `bigAlive` requires extra gap.
- `SwitchLaneStrategy`: opposite lane, no safe lane-switch window; safe interval too narrow.

Notes: classify as geometry/no-safe-window candidate for follow-up triage.

### EV-REG-2026-07-02-002 - `level_02`, `roof_narrow_gap_3`

- `type`: `Level design / geometry / no safe window candidate`
- `level`: `01_New_York/Evening/level_02`
- `pattern`: `roof_narrow_gap_3`
- `patternIndex`: 2
- `result`: `FAIL`
- `log`: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_evening_2026-07-02_112134\01_New_York_Evening_level_02.txt`
- `damage`: line 122, `smallAlive#-1126464`, lane `top`, `livesBefore=3`
- `dead-end`: line 123, `reason=ActionCompleted`, `livesLost=1`, `lives=2`
- `energy`: min logged value `33`, last logged value before fail `36`; dead-end causes did not report insufficient energy.

Observed dead-end causes:

- `SwitchLaneStrategy`: current lane, no safe lane-switch window; safe interval too narrow.
- `JumpOverStrategy`: no safe jump-over window; runtime model does not confirm safe traversal.
- `SuperJumpOverStrategy`: no safe jump-over window; runtime model does not confirm safe traversal.
- `JumpOnStrategy`: safety margin left no safe jump-on window.
- `SuperJumpOnStrategy`: no safe jump-on window; target does not intersect allowed jump trajectory.
- `SwitchLaneStrategy`: opposite lane, no safe lane-switch window; no positive launch interval remains before obstacle.

Notes: classify as geometry/no-safe-window candidate for follow-up triage.

### EV-REG-2026-07-02-003 - `level_04`, `roof_narrow_gap_4`

- `type`: `Level design / geometry / no safe window candidate`
- `level`: `01_New_York/Evening/level_04`
- `pattern`: `roof_narrow_gap_4`
- `patternIndex`: 2
- `result`: `FAIL`
- `log`: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_evening_2026-07-02_112134\01_New_York_Evening_level_04.txt`
- `damage`: line 134, `bigAlive#-1137018`, lane `top`, `livesBefore=3`
- `dead-end`: line 135, `reason=ActionCompleted`, `livesLost=1`, `lives=2`
- `energy`: min logged value `18`, last logged value before fail `21`; dead-end causes did not report insufficient energy.

Observed dead-end causes:

- `SwitchLaneStrategy`: current lane, no safe lane-switch window; safe interval too narrow.
- `SuperJumpOverStrategy`: no safe jump-over window; `bigAlive` requires extra gap.
- `SwitchLaneStrategy`: opposite lane, no safe lane-switch window; no positive launch interval remains before obstacle.

Notes: classify as geometry/no-safe-window candidate for follow-up triage.

### EV-REG-2026-07-02-004 - `level_05`, `shift_force_switch`

- `type`: `Level design / energy starvation candidate`
- `level`: `01_New_York/Evening/level_05`
- `pattern`: `shift_force_switch`
- `patternIndex`: 5
- `result`: `FAIL`
- `log`: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_evening_2026-07-02_112134\01_New_York_Evening_level_05.txt`
- `damage`: line 367, `smallAlive#-1144592`, lane `top`, `livesBefore=3`
- `dead-end`: line 368, `reason=SpawnPattern`, `livesLost=1`, `lives=2`
- `energy`: min logged value `7`, last logged value before fail `10`; dead-end causes reported available energy `9` for jump strategies.

Observed dead-end causes:

- `SwitchLaneStrategy`: current lane, no safe lane-switch window; safe interval too narrow.
- `JumpOverStrategy`: insufficient energy for jump-over; required `10`, available `9`.
- `SuperJumpOverStrategy`: insufficient energy for super jump-over; required `20`, available `9`.
- `JumpOnStrategy`: insufficient energy for jump-on; required `10`, available `9`.
- `SuperJumpOnStrategy`: insufficient energy for super jump-on; required `20`, available `9`.
- `SwitchLaneStrategy`: opposite lane, no safe lane-switch window; safe interval too narrow.

Notes: classify as energy starvation candidate. There are also lane-switch no-window causes in the same dead-end, but the jump strategies explicitly report insufficient energy.

Post-analysis update:

- Root cause confirmed as energy starvation in `shift_force_switch`; detailed analysis: `docs/Planning/in-progress/ny-evening-level-05-shift-force-switch-analysis-2026-07-02.md`.
- Added an energetic to `shift_force_switch` at top lane `x=60.20`; targeted rerun picked it up and removed the original insufficient-energy dead-end.
- The same rerun uncovered a later geometry/no-safe-window failure at `peak_3`; detailed analysis: `docs/Planning/in-progress/ny-evening-level-05-peak-3-after-energy-tune-analysis-2026-07-02.md`.

## Clean Level

### `01_New_York/Evening/level_03`

- `result`: `WIN`, stars=3.
- `damage`: 0.
- `dead-end`: 0.
- `finish`: `[TEST FINISH] state=FINISHED lives=3 energy=48`.
- `log`: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_evening_2026-07-02_112134\01_New_York_Evening_level_03.txt`

## Follow-up Queue

Suggested triage order:

1. `EV-REG-2026-07-02-004` - likely energy starvation in `level_05 / shift_force_switch`.
2. `EV-REG-2026-07-02-002` - no-window in `level_02 / roof_narrow_gap_3`.
3. `EV-REG-2026-07-02-003` - no-window in `level_04 / roof_narrow_gap_4`.
4. `EV-REG-2026-07-02-001` - no-window in `level_01 / jump_challenge`.

This ordering is only for investigation convenience; no root cause has been confirmed yet.
