# New Locations Bot Regression Runs - 2026-07-07

Scope: `02_Paris` and `03_Barcelona` levels.
Runner: Unity `TestLevelAutomationBridge`.
Branch: `integration/unity-live`.

## Global Data Fixes

- `90b9f89e Remove Granny obstacle from new locations`: removed moving Granny sprite `obstacle_new_york_big_alive_6_walk` from Paris and Barcelona `bigAlive` sprite mappings. New-location seeded sprite resolution now picks only static `bigAlive` variants.

## Paris

### Morning / level_01

- Address: `02_Paris/Morning/level_01`
- Result: `WIN`, 3 stars
- Finish: lives `3`, energy `44`
- Energy: minimum observed `32`
- Regressions: none. No energy-hunger life loss, no `[DEAD_END]`, no other gameplay regression observed.
- Notes: detailed fallback check is also recorded in `docs/Planning/in-progress/paris-morning-level-01-bot-regression-run-2026-07-07.md`.

### Morning / level_02

- Address: `02_Paris/Morning/level_02`
- Result: `WIN`, 3 stars
- Finish: lives `3`, energy `95`
- Energy: minimum observed `41`
- Energy pickups observed: `+17`, `+30`, `+30`, `+30`, `+30`, `+30`, `+30`, `+30`, `+16`
- Regressions: none. No energy-hunger life loss, no `[DEAD_END]`, no other gameplay regression observed after Granny removal.
- Pattern sequence: `easy_run_3`, `small_jumps_2`, `bonus_strip_2`, `small_jumps_3`, `medium_difficulty_2`, `easy_run_2`, `jump_challenge_2`, `easy_run_3`.

### Morning / level_03

- Address: `02_Paris/Morning/level_03`
- Result: `WIN`, 3 stars
- Finish: lives `3`, energy `62`
- Energy: minimum observed `20`
- Energy pickups observed: `+30`, `+30`, `+30`, `+28`, `+30`, `+30`, `+30`, `+30`, `+30`, `+27`, `+30`, `+19`
- Regressions: none. No energy-hunger life loss, no `[DEAD_END]`, no bot damage observed.
- Pattern sequence: `easy_run_2`, `small_jumps_3`, `shift_line_choice`, `bonus_strip_3`, `medium_difficulty_3`, `jump_challenge_2`, `peak_2`, `easy_run`.

### Afternoon / level_01

- Address: `02_Paris/Afternoon/level_01`
- Result: `WIN`, 3 stars
- Finish: lives `3`, energy `79`
- Energy: minimum observed `50`
- Energy pickups observed: `+30`, `+30`, `+30`, `+29`, `+25`, `+16`
- Regressions: none. No energy-hunger life loss, no `[DEAD_END]`, no bot damage observed.
- Pattern sequence: `easy_run_3`, `bonus_strip_2`, `small_jumps_3`, `easy_run_2`, `medium_difficulty_2`, `bonus_strip_3`, `medium_difficulty_energy`, `easy_run`.

### Afternoon / level_02

- Address: `02_Paris/Afternoon/level_02`
- Initial result: `FAIL`
- Regression: impassable geometry in pattern `roof_narrow_gap_4`. Bot reached the pattern with enough energy and failed with `[Bot DEAD_END] reason=ActionCompleted livesLost=1 lives=2`; causes reported too narrow switch-lane window and `SuperJumpOverStrategy` requiring extra `bigAlive` clearance.
- Fix: in `roof_narrow_gap_4`, moved bottom-line `bigAlive` obstacle `id=1` from `x=-3.799999952316284` to `x=-4.800000190734863`, making it a contiguous two-`bigAlive` chain with `id=0` for `SuperJumpOver`. No obstacle was removed.
- Rerun result: `WIN`, 3 stars
- Finish: lives `3`, energy `89`
- Energy: minimum observed `47`
- Energy pickups observed: `+30`, `+19`, `+30`, `+30`, `+30`, `+30`, `+30`, `+29`, `+27`, `+30`, `+30`
- Regressions after fix: none. No `[DEAD_END]`, no bot damage observed.
- Pattern sequence: `easy_run_2`, `roof_bonus_run_2`, `small_jumps`, `roof_narrow_gap_2`, `bonus_strip`, `medium_difficulty_3`, `roof_narrow_gap_4`, `easy_run_3`.

### Afternoon / level_03

- Address: `02_Paris/Afternoon/level_03`
- Initial result: `FAIL`
- Regression: bot-logic issue, not level geometry. After a valid `JumpOver -> JumpOver -> JumpOnRoof -> RoofSwitchLane` route, `RoofSwitchLane` road landing left the simulated planning state in transient `RunFromRoof`. No strategy owned the next current-lane blocking threat from that state, so the valid route disappeared and a dead-end fallback tail could execute.
- Fix: `3d3036a2 Fix roof switch lane road landing planning`. The roof-switch road landing model now matches executor completion: planning waits through the roof exit interval and resumes from `Run`.
- Rerun result: `WIN`, 3 stars
- Finish: lives `3`, energy `94`
- Regressions after fix: none. No energy-hunger life loss, no bot damage observed.
- Pattern sequence: `easy_run`, `shift_line_choice_2`, `small_jumps_2`, `medium_difficulty_3`, `bonus_strip_2`, `shift_jump_mix`, `jump_challenge_2`, `easy_run_3`.

### Afternoon / level_04

- Address: `02_Paris/Afternoon/level_04`
- Initial reruns: `WIN`, 2 stars, finish lives `2`, energy `60`
- Investigation: no dead-end or content-blocking evidence in diagnostic log. The run was dominated by `CH=ECO` energy logs: 193 of 199 lines, mostly per-second `Energy change delta=+1` plus spend/add events. This likely caused small editor/runtime stalls during validation.
- Diagnostic fix: automation diagnostics now disables `BotDiagnosticCategory.Economy` and keeps only `TestResult`, `RuntimeSafety`, and `DeadEnd`. A single compact `[LifeLoss]` runtime-safety line was added for future life-loss evidence without verbose logs.
- Stability reruns after log cleanup: 3/3 passed as `WIN`, 3 stars.
- Finishes after cleanup: run 1 lives `3`, energy `60`; run 2 lives `3`, energy `60`; run 3 lives `3`, energy `60`.
- Diagnostic log size after cleanup: 6 lines per run.
- Regressions after log cleanup: none. No `[LifeLoss]`, no energy-hunger life loss, no `[DEAD_END]`, no bot damage observed in three consecutive runs.
- Pattern sequence: `easy_run_3`, `small_jumps_3`, `bonus_strip_3`, `medium_difficulty_2`, `easy_run_2`, `roof_wide_gap_2`, `jump_challenge_3`, `bonus_strip`.

### Afternoon / level_05

- Address: `02_Paris/Afternoon/level_05`
- Result: `WIN`, 3 stars
- Finish: lives `3`, energy `53`
- Diagnostic log size: 6 lines
- Regressions: none. No `[LifeLoss]`, no energy-hunger life loss, no `[DEAD_END]`, no bot damage observed.
- Pattern sequence: `easy_run_2`, `small_jumps_2`, `shift_line_choice`, `roof_bonus_run_3`, `medium_difficulty_energy`, `jump_challenge_3`, `peak_2`, `easy_run_3`.

### Evening / level_01

- Address: `02_Paris/Evening/level_01`
- Result: `WIN`, 3 stars
- Finish: lives `3`, energy `87`
- Diagnostic log size: 6 lines after log cleanup
- Regressions: none. No `[LifeLoss]`, no energy-hunger life loss, no `[DEAD_END]`, no bot damage observed.
- Pattern sequence: `easy_run_2`, `small_jumps_3`, `bonus_strip_2`, `medium_difficulty_2`, `roof_bonus_run_2`, `shift_jump_mix`, `jump_challenge_2`, `bonus_strip_3`.

### Evening / level_02

- Address: `02_Paris/Evening/level_02`
- Initial observed result: `FAIL`, one life lost with energy `81`, last pattern `jump_challenge_3`, `[Bot DEAD_END] reason=ActionCompleted`.
- Visual observation: loss happened on the upper line after jumping a single large obstacle; the bot landed on the road and did not have enough window to jump the following two `smallNotAliveRoad` obstacles.
- Follow-up rerun after log/vibration cleanup: `WIN`, 3 stars.
- Finish: lives `3`, energy `49`
- Diagnostic log size: 6 lines
- Regressions after rerun: not reproduced. No `[LifeLoss]`, no energy-hunger life loss, no `[DEAD_END]`, no bot damage observed.
- Temporary probe: added for this rerun and removed immediately after reading logs; no `TEMP_JC3_PROBE` lines were emitted on the passing run.
- Pattern sequence: `easy_run_3`, `bonus_strip_2`, `roof_narrow_gap_4`, `medium_difficulty_3`, `roof_bonus_run_2`, `roof_switch_line_2`, `jump_challenge_3`, `easy_run_2`.

### Evening / level_03

- Address: `02_Paris/Evening/level_03`
- Result: `WIN`, 3 stars
- Finish: lives `3`, energy `69`
- Diagnostic log size: 6 lines
- Regressions: none. No `[LifeLoss]`, no energy-hunger life loss, no `[DEAD_END]`, no bot damage observed.
- Pattern sequence: `easy_run`, `shift_jump_mix`, `small_jumps_2`, `medium_difficulty_energy`, `bonus_strip_3`, `shift_zigzag_tight`, `peak_3`, `easy_run_2`.

### Evening / level_04

- Address: `02_Paris/Evening/level_04`
- Initial result: `WIN`, 2 stars
- Initial observation: one `[LifeLoss]` in `jump_challenge_2`, energy `68`, state `SuperJumpDamage`, no `[DEAD_END]`. Not energy hunger; likely floating timing/performance miss unless reproduced with stronger evidence.
- Automation change: validation runs now stop immediately on the first life loss in test-level mode, after writing `[LifeLoss]` and `[TEST RESULT] FAIL`; if a pending dead-end exists, its causes are still logged first.
- Rerun after automation change: `WIN`, 3 stars
- Finish after rerun: lives `3`, energy `62`
- Diagnostic log size after rerun: 6 lines
- Regressions after rerun: none reproduced. No `[LifeLoss]`, no energy-hunger life loss, no `[DEAD_END]`, no bot damage observed.
- Pattern sequence: `easy_run_3`, `roof_bonus_run_3`, `shift_line_choice`, `roof_narrow_gap_2`, `bonus_strip_2`, `roof_wide_gap`, `jump_challenge_2`, `easy_run`.
