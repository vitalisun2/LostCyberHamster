# New York bot regression cycle - 2026-07-02

## Scope

Дата запуска: 2026-07-02.

Триггер: за последние сутки были изменения в `LostCyberHamster/Assets/Scripts/Bot`:

- `ac8f173a` - `Add passive roof exit moving boundary`
- `c40f1ebf` - `Add road collectible routing to roof switch lane`

Команды и режимы:

- Test levels, первый batch:
  - `.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 180`
  - artifacts: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\all_test_levels_2026-07-02_060314`
- Test levels, оставшиеся 12 уровней после recovery bridge:
  - `tools/invoke_run_all_test_levels.ps1` через тот же bridge с явным списком оставшихся `test*.json`, `-TimeoutSeconds 240`, `-TimeScale 1`
  - artifacts: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\all_test_levels_2026-07-02_063505`
- Campaign New York Morning/Afternoon:
  - `tools/invoke_run_all_test_levels.ps1` через тот же bridge с явным списком `01_New_York/Morning/level_01..level_03` и `01_New_York/Afternoon/level_01..level_05`, `-TimeoutSeconds 480`, `-TimeScale 1`
  - artifacts: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_2026-07-02_071111`

Coverage:

- 17 unique test levels.
- 8 campaign levels: New York Morning `level_01..level_03`, Afternoon `level_01..level_05`.
- Campaign result: 8/8 `WIN`, all stars=3, damage markers=0.

## Summary

- Unique level cases: 25.
- Confirmed regressions: 1.
- Confirmed regression types:
  - `Bot architecture / decision logic`: 1.
- Follow-up fix status: `REG-2026-07-02-001` fixed and verified.
- Not confirmed candidates: 2.
- Temporary diagnostic code: none.

## REG-2026-07-02-001 - `test_collectables_05` stalls after road-landing `RoofSwitchLane`

- `type`: `Bot architecture / decision logic`
- `level`: `01_New_York/Morning/test_collectables`
- `pattern`: `test_collectables_05`
- `case`: `should ignore all bonuses for collecting life on roof if enough energy for jump on roof`
- `expected`: after scripted life loss, ignore road Coin/Crystal/Energy and collect Life on the roof without damage.
- `actual`: bot completes previous `RoofSwitchLane` to road in `RunFromRoof`, does not activate a new plan for `test_collectables_05`, then takes two hits from top-lane `bigNotAlive` and finishes with `lives=0`; bridge never receives `[TEST RESULT]`.
- `expected source`: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json:11301` and level reference `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_collectables/test_collectables.json:47`.
- `reproduction`: `.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 180`
- `artifacts`:
  - `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\all_test_levels_2026-07-02_060314\01_New_York_Morning_test_collectables_timeout.txt`
  - bridge response after recovery: `failed`, `Play mode ended before a [TEST RESULT] marker was detected.`

Root cause: previous pattern plans `PassiveCollect[Life:1] -> RoofSwitchLane[Coin:1]` and fires road-landing `RoofSwitchLane` at `06:16:49.752`. `RoofSwitchLaneExecutor.IsCompleted` completes road landing as soon as target lane is reached and the hamster is no longer `RoofRun`; it does not wait for ordinary `Run`, so completion is logged at `06:16:50.217 state=RunFromRoof` (`RoofSwitchLaneExecutor.cs:98`, `PlanningStateTransition.cs:252`). `RuntimeBotController` then requests `ActionCompleted` replan through the deferred async path (`RuntimeBotController.cs:1100`, `RuntimeBotController.cs:1164`), but ground/roof-entry actions are not valid from `RunFromRoof`: `JumpOnRoofSpecification` requires `HamsterStateEnum.Run` (`JumpOnRoofSpecification.cs:24`), and `PlanningStrategyApplicability.CanPlanGroundRun` also requires `Run` (`PlanningStrategyApplicability.cs:64`). The log has no `[Bot PLAN]` after `06:16:49.393`; `test_collectables_05` spawns at `06:16:50.010`, and the next facts are damage at `06:16:53.989` and `06:16:55.008` on top-lane `bigNotAlive#-964192`, then `[TEST FINISH] state=FINISHED lives=0 energy=96`. This excludes bridge-only failure: the timeout is a consequence of the gameplay failure and missing `[TEST RESULT]`.

Why expected is reachable and alternatives are excluded: the pattern provides enough energy (`energy=92` immediately after spawn, then regenerates to 96 before death), the expected Life is above a top-lane roof obstacle, and the same level already proves Life collectable routing works in `test_collectables_04` (`SwitchLane -> JumpOnRoof -> PassiveCollect[Life:1]`). There is no campaign-wide breakage: all 8 New York campaign levels pass with 3 stars and damage markers 0. The first code-location divergence is the action lifecycle contract for road-landing `RoofSwitchLane`: runtime reports completion while the hamster is still in `RunFromRoof`, but the planner cannot legally schedule the required next action from that state.

Architectural recommendation: make road-landing `RoofSwitchLane` and tail planning share one explicit lifecycle contract. Either complete road landing only after the runtime reaches ordinary `Run`, or teach the planning layer a first-class post-road-landing state that can safely reserve/schedule the next ground or roof-entry action during `RunFromRoof`; do not patch this as a test-specific override or threshold change.

Follow-up fix: `RoofSwitchLaneExecutor.IsCompleted` now treats `targetLanding=road` as complete only after the runtime reaches ordinary `Run`. It waits through `RoofRun` and `RunFromRoof`, and exits the wait on damage/death or unexpected runtime state with a cancel log. Roof-to-roof `RoofSwitchLane` completion remains unchanged.

Post-fix verification:

- Targeted `01_New_York/Morning/test_collectables`: `WIN`, stars=3; log preserved at `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\roof_switch_lane_fix_2026-07-02_082315\test_collectables_after_fix.txt`.
- Targeted `01_New_York/Morning/test_roof_switch_lane`: `WIN`, stars=3; log preserved at `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\roof_switch_lane_fix_2026-07-02_082315\test_roof_switch_lane_after_fix.txt`.
- Full test-level sweep: `.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 240 -TimeScale 1`; result `16 WIN + 1 SEMF`, where the only SEMF is the known `test_roof_switch_lane_04` parser/source mismatch from `NC-2026-07-02-001`. `test_collectables` is `WIN`, damage markers=0. Artifacts: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\all_test_levels_2026-07-02_082908`.
- Follow-up semantic source/tool fix: `test_roof_switch_lane_04` and `test_roof_switch_lane_05` descriptions now say `roof switch lane from roof to road`, and `tools/invoke_run_all_test_levels.ps1` maps `roof switch lane` directly to `RoofSwitchLane`. Targeted semantic re-check on the latest `test_roof_switch_lane` `WIN` log returned `Result=OK`, `FailedPatterns=[]`, damage markers=0.
- Campaign New York Morning/Afternoon follow-up: explicit `launch_test_level` batch for Morning `level_01..level_03` and Afternoon `level_01..level_05`, `TimeoutSeconds=480`, `TimeScale=1`; result `8/8 WIN`, all stars=3, damage markers=0. Artifacts: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\campaign_ny_after_fix_2026-07-02_092211`.

Regression status: fixed. The confirming log shows the formerly early `RoofSwitchLane` completion now happens at `state=Run`, followed by a new plan `JumpOnRoof -> PassiveCollect[Life:1] -> PassiveRoofExit` for `test_collectables_05`.

## Not Confirmed

### NC-2026-07-02-001 - `test_roof_switch_lane_04` semantic mismatch is a parser/source issue

- `level`: `01_New_York/Morning/test_roof_switch_lane`
- `pattern`: `test_roof_switch_lane_04`
- `case`: `should switch lane from roof to road - avoiding danger`
- `expected reported by runner`: `SwitchLane`
- `actual`: level `WIN`, damage markers=0; pattern segment fires `RoofSwitchLane` for the roof-to-road case.
- `artifact`: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\all_test_levels_2026-07-02_063505\01_New_York_Morning_test_roof_switch_lane.txt`

Reason excluded: the authoritative pattern description says `from roof to road` (`PatternsCollection.json:11527`), and runtime executes the matching domain action: `06:56:13.551 [Bot EXEC] FIRE kind=RoofSwitchLane ... targetLanding=road desc=Roof switch lane to road before smallNotAliveRoad`. The semantic runner maps only phrases starting with `switch lane from one roof` to `RoofSwitchLane`; `switch lane from roof to road` falls through to the generic `switch lane` mapping and becomes ordinary `SwitchLane` (`tools/invoke_run_all_test_levels.ps1:211`, `tools/invoke_run_all_test_levels.ps1:227`). This is not a bot gameplay regression; it is a test expectation/parser source issue.

Recommendation: update the test-level semantic parser/source contract so `switch lane from roof to road` maps to `RoofSwitchLane`, or make the expected action explicit in the pattern metadata. Do not change bot behavior for this candidate.

Follow-up: fixed by making the pattern descriptions explicit (`should roof switch lane from roof to road ...`) and adding a direct `roof switch lane` mapping to the semantic runner. The same cleanup was applied to `test_roof_switch_lane_05`, which had the same ambiguous roof-to-road wording. Re-check result on the latest `test_roof_switch_lane` WIN log: `OK`, no failed patterns.

### NC-2026-07-02-002 - busy errors after `test_collectables` are secondary bridge fallout

- `level`: remaining test levels in the first batch after `01_New_York/Morning/test_collectables`
- `actual`: first batch reported `busy: Unity Editor is already processing another test-level automation request`.
- `artifact`: `C:\Personal\crystal-wave\repos\LostCyberHamster_2025\Temp\all_test_levels_2026-07-02_060314`

Reason excluded: `test_collectables` reached game-over without `[TEST RESULT]`, leaving the bridge in play mode until it was stopped after preserving the diagnostic log. The remaining test levels were rerun in the second filtered batch and produced valid artifacts.

## Cleanup

- Code fix applied in `LostCyberHamster/Assets/Scripts/Bot/Strategies/RoofSwitchLane/RoofSwitchLaneExecutor.cs`.
- Level pattern text updated in `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json` for explicit `roof switch lane` wording.
- Semantic runner mapping updated in `tools/invoke_run_all_test_levels.ps1`.
- No config was modified.
- No temporary diagnostic code was added.
- Unity play mode was stopped once after preserving the `test_collectables` log to release the stuck bridge.
- Expected final working-tree delta: `RoofSwitchLaneExecutor.cs`, `PatternsCollection.json`, `tools/invoke_run_all_test_levels.ps1`, plus this report.
