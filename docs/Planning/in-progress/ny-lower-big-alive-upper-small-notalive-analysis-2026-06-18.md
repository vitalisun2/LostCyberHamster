# NY level 01 dead-end life-loss analysis — 2026-06-18

## Scope

Один regression/investigation cycle: первый уровень New York (`01_New_York/Morning/level_01`), сбор всех потерь жизни после отключения pause-on-dead-end и разбор первого confirmed dead-end.

Expected:
- при потере жизни validation run не останавливается сразу;
- каждая потеря жизни логируется;
- confirmed dead-end логирует причины dead-end;
- loss без pending dead-end логируется отдельно для будущего расследования;
- первый confirmed dead-end проверяется на уровень: это действительно непроходимая геометрия или runtime dead-end после более раннего плохого выбора.

Actual:
- level run завершился `lives=0`;
- зафиксированы 3 потери жизни: 1 confirmed dead-end и 2 unclassified life losses;
- первый confirmed dead-end произошёл на нижнем `bigAlive`, но asset geometry показывает верхний jumpable route.

Где воспроизводится:
- `01_New_York/Morning/level_01`, automation run 2026-06-18 13:49-13:50 MSK.

## Sources

- User report, 2026-06-18: ручной прогон первого уровня New York.
- Runtime run, 2026-06-18 13:49-13:50 MSK: `01_New_York/Morning/level_01`.
- Raw diagnostic log copy: `Temp/ny_level_01_diagnostic_2026-06-18_main.txt`.
- Pre-unblock diagnostic log copy: `Temp/ny_level_01_diagnostic_2026-06-18_pre_unblock.txt`.
- Level sequence: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/level_01/level_01.json`.
- Pattern geometry: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Type mapping: `LostCyberHamster/Assets/Scripts/Common/Models/ObstacleTypeEnum.cs`.
- Jumpability facts: `LostCyberHamster/Assets/Scripts/Bot/Planning/ObstacleClassifier.cs`.
- `docs/rules/AGENTS.md`
- `docs/rules/agent_efficiency_playbook.md`
- `docs/rules/architecting_and_coding_principles.md`
- `docs/rules/code_conventions.md`
- `docs/rules/temporary_current_rules.md`
- `docs/rules/iteration_cycle.md`
- `docs/rules/agent_tools.md`
- `docs/Planning/in-progress/bot_collectible_objectives_plan_2026-06-16.md`

## Hypotheses

### H1 — planner не построил безопасную opposite-lane ветку

Подтвердит:
- в `BOT`-логе перед потерей жизни есть decision point для нижнего `BigAlive`, но нет кандидата `SwitchLane -> JumpOver`/safe branch на верхнюю линию;
- код `ActionGenerator`/`DecisionPointDetector` отбрасывает верхний `SmallNotAlive` или opposite chain.

Опровергнет:
- лог показывает, что безопасная opposite-lane ветка была построена и оценена как candidate.

Статус: not proven. Current logs only print selected plans, not all branch candidates, so the presence/absence of an opposite-lane candidate is not proven by this run.

### H2 — безопасная ветка построена, но проиграла в evaluator/dominance

Подтвердит:
- в `BOT`-логе есть candidate через верхнюю линию, но выбран unsafe/менее безопасный branch;
- код `PlanEvaluator`/`PlanningBranchMetrics` допускает ранжирование unsafe branch выше safe branch или схлопывает safe branch.

Опровергнет:
- candidate отсутствует или выбранный branch считался safe по всем проверкам до runtime.

Статус: not proven. No candidate-ranking log was collected in this diagnostic pass. Per user instruction, `PlanEvaluator` semantics were not changed.

### H3 — выбранный план был безопасным по planner-модели, но runtime execution не успел/не выполнил lane switch/jump

Подтвердит:
- `BOT`-лог показывает выбранный safe plan, но `EXEC`/runtime события не fire/completed вовремя;
- есть разрыв между planned fire window и runtime состоянием (`IsShifting`, jump window, retained handoff).

Опровергнет:
- plan изначально не содержит требуемых действий или execution корректно выполнил выбранный unsafe action.

Статус: partially supported. Logs prove the selected plan executed `SwitchLane` down before `smallNotAliveRoadAndRoof` and then hit bottom `bigAlive`. They do not yet prove whether the bad action was generated, ranked, or retained incorrectly.

### H4 — collision/snapshot классифицировал `SmallNotAlive` или `BigAlive` неверно

Подтвердит:
- snapshot/chain показывает неправильные role/type/bounds/lane для одного из obstacles;
- level asset содержит другой obstacle или lane, чем визуальный отчёт.

Опровергнет:
- level data и snapshot согласуются с визуальным отчётом: нижний `BigAlive`, верхний `SmallNotAlive`.

Статус: disproven for the first confirmed dead-end. Runtime log, level asset, enum mapping, and classifier agree on bottom `bigAlive` and top `smallNotAliveRoadAndRoof`.

## Facts

### Diagnostic change for investigation only

- `RuntimeBotController.OnLivesLost` теперь не останавливает игру на confirmed dead-end и не пишет промежуточный `[TEST RESULT] FAIL`.
- Confirmed dead-end пишется в `BOT` и `STAB`.
- Life loss без pending dead-end пишется как `[Bot LIFE_LOSS] confirmed=false reason=no-pending-dead-end`.
- `RuntimeBotEventTracker.OnGameFinished` пишет финальный `[TEST RESULT] FAIL reason=lives-zero`, чтобы automation bridge завершал прогон при смерти на уровне.
- Planner/evaluator semantics не менялись.

### Run summary

- Уровень: `01_New_York/Morning/level_01`.
- Финал: `[TEST RESULT] FAIL reason=lives-zero`.
- Потери жизни: 3.
- Confirmed dead-end: 1.
- Life loss без dead-end diagnosis: 2.

### Life-loss events

1. `13:50:23.050` — damage на нижней линии:
   - obstacle: `bigAlive#-17638`, `x=[-2.96,-1.96]`, lane=`bottom`;
   - hamster: `x=[-4.60,-2.96]`, lane=`bottom`, state=`Run`;
   - immediately followed by `[Bot DEAD_END] confirmed=true`.

2. `13:50:25.685` — damage на нижней линии:
   - obstacle: `smallNotAliveRoad#-17686`, `x=[-2.99,-1.59]`, lane=`bottom`;
   - `[Bot LIFE_LOSS] confirmed=false reason=no-pending-dead-end`.

3. `13:50:28.202` — damage на нижней линии:
   - obstacle: `smallAlive#-17730`, `x=[-3.00,-1.48]`, lane=`bottom`;
   - `[Bot LIFE_LOSS] confirmed=false reason=no-pending-dead-end`;
   - после этого `TEST FINISH state=FINISHED lives=0`.

## Dead-End Reports

### Dead-end #1 — confirmed, but not proven as intrinsic level dead-end

Log:
- `13:50:18.381` plan after switching to top lane: `JumpOver -> JumpOver -> SwitchLane`.
- `13:50:19.487` bot fires `JumpOver` over `smallNotAliveRoad`.
- `13:50:21.266` bot fires `JumpOver` over `smallNotAliveRoad`.
- `13:50:22.286` bot fires `SwitchLane` to bottom before `smallNotAliveRoadAndRoof`.
- `13:50:22.751` switch completes, lane=`bottom`.
- `13:50:23.050` damage from bottom `bigAlive`.
- `13:50:23.054` confirmed dead-end:
  - `reason=ActionCompleted`;
  - `depth=0`;
  - `nextObstacleIndex=0`;
  - `projection=0.00`.

Logged strategy reasons:
- `SwitchLaneStrategy`, current lane: no safe switch window, safe interval too narrow.
- `SuperJumpOverStrategy`: no safe jump-over window; `bigAlive` needs extra clearance that is absent in this section.
- `SwitchLaneStrategy`, opposite lane: no safe switch window, safe interval too narrow.

Static pattern geometry from `small_jumps` around the failing area:

| id | type | semantic type | x | lane |
|---:|---:|---|---:|---|
| 4 | 1 | `bigAlive` | 18.60 | bottom |
| 17 | 3 | `smallNotAliveRoadAndRoof` | 19.40 | top |
| 5 | 1 | `bigAlive` | 19.80 | bottom |

Static rules:
- `ObstacleTypeEnum`: type `1` is `bigAlive`, type `3` is `smallNotAliveRoadAndRoof`.
- `ObstacleClassifier.CanJumpOverOnGround` returns true for `smallNotAliveRoadAndRoof`.
- `ObstacleClassifier.CanJumpOverOnGround` does not return true for `bigAlive`; `bigAlive` requires super-jump handling and extra clearance.

Conclusion:
- В момент confirmed diagnosis состояние уже действительно безвыходное: бот находится на нижней линии, `bigAlive` уже почти в overlap с хомяком, а доступные стратегии больше не имеют безопасного окна.
- Это не доказывает, что участок уровня сам по себе непроходим. Напротив, геометрия показывает альтернативу на верхней линии: `smallNotAliveRoadAndRoof` на `x=19.40` перепрыгивается `JumpOver`, тогда как нижняя линия занята `bigAlive` на `x=18.60/19.80`.
- Реальная причина этого confirmed dead-end для дальнейшего расследования: бот ранее выбрал `SwitchLane` вниз перед `smallNotAliveRoadAndRoof`, хотя верхняя линия оставалась jump-over маршрутом. Нужно отдельно разбирать, почему `SwitchLane` был сгенерирован/выбран как допустимый action в этой ветке.

Status:
- Confirmed runtime dead-end after bad lane choice.
- Not confirmed as intrinsic level geometry dead-end.

## Unclassified Life Loss

### U1 — `smallNotAliveRoad#-17686`

Status: needs later investigation.

Facts:
- Occurs after dead-end #1.
- No `Bot PLAN` / `Bot EXEC` appears between dead-end #1 and this hit.
- Logged as `confirmed=false reason=no-pending-dead-end`.

Likely diagnostic root cause:
- После confirmed dead-end игра больше не паузится, но `RuntimeBotController` не получает нового replan trigger на `OnLivesLost`. Pending diagnosis очищается, нового плана нет, бот продолжает бежать по нижней линии без классифицированного dead-end.

### U2 — `smallAlive#-17730`

Status: needs later investigation.

Facts:
- Occurs after U1, still no new `Bot PLAN` / `Bot EXEC`.
- Logged as `confirmed=false reason=no-pending-dead-end`.
- Ends run with `lives=0`.

Likely diagnostic root cause:
- Same as U1: after no-pause dead-end continuation, there is no life-loss replan/diagnosis path. This is a logging/diagnostic workflow gap, not evidence of a separate level geometry dead-end.

## Root Cause

Для исходного визуального регресса root cause пока не доказан до уровня planner/gameplay fix.

Доказано:
- life loss #1 совпадает с confirmed dead-end diagnosis;
- этот diagnosis описывает уже созданное безвыходное состояние на нижней линии;
- состояние не является доказанным intrinsic level dead-end, потому что статическая геометрия `small_jumps` показывает jumpable top-lane alternative.

Текущий рабочий root-cause candidate для следующего цикла:
- planner/executor допустил или выбрал `SwitchLane` вниз перед `smallNotAliveRoadAndRoof`, хотя на нижней линии в том же участке находится `bigAlive`; нужно разбирать generation/safety/branch selection для этого `SwitchLane`.

Отдельный diagnostic root cause:
- после отключения pause-on-dead-end последующие life losses не получают dead-end diagnosis, потому что `OnLivesLost` не инициирует replan и pending diagnosis очищен.

## Proposed Fix

Не выбран: пользователь явно попросил пока не делать gameplay/planner fix.

## Verification

- `01_New_York/Morning/level_01` прогнан через automation после diagnostic changes.
- Automation result: `FAIL reason=lives-zero`.
- Логи сохранены в `Temp/ny_level_01_diagnostic_2026-06-18_main.txt`.

Validation of requested logging:
- confirmed dead-end life loss has a matching `[Bot DEAD_END] confirmed=true`;
- two later life losses have explicit `[Bot LIFE_LOSS] confirmed=false reason=no-pending-dead-end`;
- the level no longer pauses at the first confirmed dead-end.

## Status

Investigation paused after data collection and first dead-end analysis. No planner fix applied.

## Follow-up Observation: Successful Run Regressions

Source:
- `Temp/ny_level_01_after_depth_fix_no_lives_replan_2026-06-18_1425.txt`

Scope:
- Successful `01_New_York/Morning/level_01` run after removing `LivesLost` replan.
- No life loss or dead-end in this run.
- Two observed quality regressions: a `SuperJumpOn` where a normal jump looked sufficient, and a later visible lane-switch oscillation.

Facts: `SuperJumpOn smallAlive#-32690`
- `patternIndex=6 pattern=medium_difficulty` spawned at `14:24:13.238`.
- The relevant plan around `14:24:16-14:24:20` contains:
  - `JumpOver smallNotAliveRoadAndRoof#-32644`
  - `SuperJumpOn smallAlive#-32690`
  - `SwitchLane before bigAlive#-32738`
  - `JumpOver smallNotAliveRoadAndRoof#-32482`
  - `JumpOver smallAlive#-32506`
  - `SwitchLane before bigAlive#-32530`
  - `SwitchLane before smallNotAliveRoad#-32802`
- `SuperJumpOn` fires at `14:24:17.997`: `triggerX=-1.35`, `obstacleLeftX=-1.35`, `window=[-0.57,-2.14]`.
- Immediately before fire, snapshot at `14:24:17.548` shows `lane=top`, not bottom.
- The action is retained as part of `RuntimeBotController` committed-prefix replanning: after an `ActionCompleted` replan, the first two actions of the current plan are simulated and kept before rebuilding the tail.

Interpretation:
- Superseded: committed-prefix retention explains why an already planned `SuperJumpOn` was not reconsidered later, but it does not explain why `SuperJumpOn` was planned initially.
- Additional diagnostics below cover the initial planning decision.

### Follow-up: Root Cause for Lower-Line `SuperJumpOn smallAlive`

Sources:
- `Temp/ny_level_01_jump_on_diag_2026-06-18_1437.txt`
- `Temp/ny_level_01_post_action_diag_2026-06-18_1443.txt`
- `Temp/ny_level_01_jump_on_points_2026-06-18_1500.txt`
- Code: `JumpOnStrategy`, `SuperJumpOnStrategy`, `JumpOnWindowCalculator`, `JumpOnFireWindowFinder`, `TargetRemovalPostActionSafety`, `CollisionController`

Scope:
- Investigate why lower-line `smallAlive` was planned as `SuperJumpOn` instead of normal `JumpOn`.
- This is separate from the later lane-switch oscillation.

Facts:
- Superseded earlier interpretation: normal `JumpOn` is not intrinsically impossible in this local geometry; only the single selected midpoint was proven unsafe.
- `JumpOnWindowCalculator.TryCalculate` chooses exactly one `SelectedFireShift` as midpoint: `(firstFireShift + lastFireShift) * 0.5f`.
- Ground `JumpOnFireWindowFinder.TryFindFireShift` checked only `window.SelectedFireShift` through runtime resolver. `JumpOnStrategy`/`SuperJumpOnStrategy` then checked post-action only for that returned single point.
- `JumpOnFromRoofFireWindowFinder` already has a stronger model: it collects multiple timing candidates and selects the first one that also passes post-action safety.
- Diagnostic run `Temp/ny_level_01_jump_on_points_2026-06-18_1500.txt` proves the point-selection issue for the lower-line `smallAlive`:
  - `rootNext=11`, target `smallAlive#-58384@idx15`, `window=[3.24,4.89]`;
  - `middle fire=4.06 completion=10.97 runtimeOk=True postActionOk=False`;
  - `first fire=3.24 completion=10.14 runtimeOk=True postActionOk=False`;
  - `last fire=4.89 completion=11.79 runtimeOk=True postActionOk=True`.
- The same pattern repeats at `window=[2.58,4.22]`: `middle` and `first` fail post-action, `last` passes.
- The close overlap found by `POST_ACTION_DIAG` is still a real reason for rejecting that specific midpoint. It is not a reason to reject the whole fire-window.

Conclusion:
- Proven root cause for the lower-line `SuperJumpOn`: ground `JumpOn` planning evaluated only the midpoint of a valid fire-window. When the midpoint failed post-action safety, the normal `JumpOn` action was not created, even though the late edge of the same window passed runtime and post-action checks.
- `SuperJumpOn` was chosen because it remained available after normal `JumpOn` was incorrectly eliminated at candidate generation time.
- This is a planner candidate-selection bug, not a level-geometry issue and not an evaluator-ranking issue.

Fix:
- Move post-action validation into shared `JumpOnFireWindowFinder`.
- For ground `JumpOn`/`SuperJumpOn`, try fire-window points in order: `middle`, `first`, `last`.
- Select the first point that passes both runtime resolver and `TargetRemovalPostActionSafety`.
- Return dead-end only if none of the three points passes.
- Remove temporary `JUMPON_POINT_DIAG` logging after the fix.

Verification after fix:
- Source: `Temp/ny_level_01_after_trifire_jump_on_2026-06-18_1507.txt`.
- Unity compile succeeded before the run.
- Original lower-line `smallAlive` case now plans ordinary `JumpOn` instead of `SuperJumpOn`:
  - representative target `smallAlive#-63300`;
  - selected `JumpOn`, `trigger=-1.73`, `complete=11.79`, `postFire=6.90`;
  - execution fires and completes `JumpOn` successfully.
- Temporary `JUMPON_POINT_DIAG`/`POST_ACTION_DIAG` logs are absent from the run.
- Full-level run still ended with `FAIL reason=lives-zero` at a later, separate dead-end with low energy; not treated as this regression.

Facts: later lane-switch sequence
- The actually executed sequence after `smallAlive#-32506` is:
  - `14:24:24.777` switch to top before `bigAlive#-32530`
  - `14:24:26.635` switch to bottom before `smallNotAliveRoad#-32802`
  - `14:24:31.036` switch to top before `bigAlive#-32576`
- A planned `Switch lane entry before smallNotAliveRoad#-32802` appears in intermediate plans, but no matching `EXEC FIRE` appears in this late section; it was removed by a later replan before execution.

Interpretation:
- The visible `top -> bottom -> top` movement is tied to alternating blockers on the two lanes, not a confirmed duplicate execution around the same obstacle.
- Whether the sequence is strategically optimal is not proven from current logs; the current logs show executed actions and selected branches, but not all rejected successful candidates.
