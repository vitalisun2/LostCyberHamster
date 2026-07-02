# New York Evening regression fix TODO - 2026-07-02

## Scope

Track the remaining New York Evening content regressions found during the 2026-07-02 regression cycle.

Authoritative analysis docs:
- `docs/Planning/in-progress/ny-evening-regressions-2026-07-02.md`
- `docs/Planning/in-progress/ny-evening-level-01-jump-challenge-analysis-2026-07-02.md`
- `docs/Planning/in-progress/ny-evening-level-02-roof-narrow-gap-3-analysis-2026-07-02.md`
- `docs/Planning/in-progress/ny-evening-level-04-roof-narrow-gap-4-analysis-2026-07-02.md`
- `docs/Planning/in-progress/ny-evening-level-05-shift-force-switch-analysis-2026-07-02.md`
- `docs/Planning/in-progress/ny-evening-level-05-peak-3-after-energy-tune-analysis-2026-07-02.md`

## Fixed

- [x] `level_05 / shift_force_switch`: proven energy starvation. Added `collectableEnergetic` at top lane `x=60.20`; targeted rerun confirmed the original insufficient-energy fail is removed.

## Remaining TODO

- [ ] `level_01 / jump_challenge`: tune pattern entry geometry. Current layout forces `PassiveRoofExit` plus `SwitchLane` into top-lane `bigAlive` with no valid switch or super-jump-over window.
- [ ] `level_02 / roof_narrow_gap_3`: tune final top roof exit spacing. Current layout exits too close to top-lane `smallAlive`, leaving no post-exit action window.
- [ ] `level_04 / roof_narrow_gap_4`: tune final top roof exit spacing. Current layout exits directly into top-lane `bigAlive`, and no switch/jump alternative has a valid window.
- [ ] `level_05 / peak_3`: tune pattern entry lane composition or spacing. Current layout starts with mutually incompatible top and bottom threats; escaping top lands into bottom `smallAlive`.

## Validation Plan

- After each content tune, run the targeted Evening level through `tools/invoke_open_unity_test_level.ps1`.
- After all remaining tunes, rerun `01_New_York/Evening/level_01..level_05`.
- Recheck `01_New_York/Night/level_05` after blockers before `shift_force_switch` are removed, because `shift_force_switch` is shared with Night content.

## Current Classification

The remaining Evening failures are proven level design / passability blockers, not bot logic regressions. The only energy-starvation regression in this batch was `shift_force_switch`, and that content issue is already tuned.
