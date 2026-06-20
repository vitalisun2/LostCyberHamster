# New York Extra SwitchLane Analysis - 2026-06-20

## Scope

Один регресс в первом уровне New York Morning.

- Level: `01_New_York/Morning/level_01`.
- Место: участок `small_jumps` после первого корректного `JumpOn`, где дальше на нижней линии идут две `smallAlive`.
- Expected: без лишнего lane ping-pong; по двум нижним собакам достаточно одного `JumpOn` и одного безопасного `JumpOver`, если геометрия не дает два `JumpOn`.
- Actual: бот строит серии `SwitchLane`, затем проходит `smallAlive` через `JumpOver`, хотя `JumpOn`-ветки существуют.

## Sources

- Log: `LostCyberHamster/EditorLogs/diagnostic_log.txt`, run `2026-06-20 21:13-21:14`.
- Code: `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningBranchMetricsComparer.cs`.
- Code: `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningBranchMetrics.cs`.
- Code: `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`.
- Code: `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`.
- Temporary diagnostics during investigation: `Bot BRANCH_DIAG`, `Bot MERGE_DIAG`.

## Hypotheses

### H1: `JumpOn` для нижних собак не генерируется

Подтвердит:
- В candidate logs нет веток с `JumpOn(... desc=Jump on smallAlive)` на проблемном участке.

Опровергнет:
- В candidate logs есть такие ветки, но selected branch другая.

Status: rejected.

Факт: `21:14:06.279` есть candidate `index=22` с `JumpOn(idx=5,id=-78530,... desc=Jump on smallAlive)`, `major=1`.

### H2: Плохие первые действия приходят только из `committedPrefix`

Подтвердит:
- В `MERGE_DIAG` bad sequence находится в `committed=...`, а `tail=...` нормальный.

Опровергнет:
- В `MERGE_DIAG` плохая цепочка уже находится в fresh `tail=...`.

Status: rejected as primary cause, confirmed as secondary amplifier.

Факт: `21:14:06.311` fresh `tail` уже содержит `SwitchLane -> SwitchLane -> SwitchLane -> JumpOver...`. `committedPrefix` потом сохраняет первые действия, но не создает root cause.

### H3: Anti-ping-pong guard для `SwitchLane entry` сломан

Подтвердит:
- Плохая ветка проходит как `SwitchLane entry -> SwitchLane` в одном graph node, где `previousAction.IsOppositeLaneEntry` должен был отрезать продолжение.

Опровергнет:
- Плохая цепочка не является прямым `entry -> switch` внутри одного graph; она идет через обычные `SwitchLane` к разным obstacle index.

Status: rejected as primary cause.

Факт: `PlanningGraphBuilder.IsRedundantSwitchLaneContinuation` режет только direct `entry -> switch` или switch к той же/ранней ситуации. В selected branch `21:14:06.265` switch targets идут `idx=3 -> idx=4 -> idx=6`, то есть guard формально не срабатывает.

### H4: Ветка с меньшей route energy побеждает ветку с `JumpOn`

Подтвердит:
- Selected branch имеет меньше `route`, но `major=0`.
- Candidate с `JumpOn` имеет `major=1`, но больший `route`.
- В коде comparer сравнивает `RouteEnergyCost` раньше `MajorObjectiveCount`.

Опровергнет:
- Selected branch выигрывает по `major`, life, coin или action count, а не по route energy.

Status: confirmed root cause.

## Facts

- На момент диагностики `PlanningBranchMetricsComparer.Compare` сравнивал так: `Life -> RouteEnergyCost -> MajorObjectiveCount -> ObjectiveEnergyCost -> Coin -> ActionCount`.
- На момент диагностики `PlanningBranchMetrics` относил energy у `JumpOn` в `ObjectiveEnergyCost`, а обычные `JumpOver` - в `RouteEnergyCost`.
- `21:14:06.265`: selected branch:
  - metrics: `life=0, route=10, major=0, objective=0, coin=0, actions=6`;
  - chain: `SwitchLane -> SwitchLane -> SwitchLane -> JumpOver(smallAlive) -> SwitchLane -> SwitchLane`.
- `21:14:06.279`: candidate branch with `JumpOn`:
  - metrics: `life=0, route=20, major=1, objective=10, coin=0, actions=6`;
  - chain includes `JumpOn(idx=5,... desc=Jump on smallAlive)`.
- Because comparer checks `route` before `major`, selected `route=10, major=0` beats candidate `route=20, major=1`.
- `21:14:07.855`: same pattern repeats:
  - selected: `route=20, major=0`;
  - later candidates include `JumpOn` with `major=1`, but higher route.
- `21:14:06.311 MERGE_DIAG`: the bad branch is already in `tail`, so generation and final selection are the problem, not only committed-prefix retention.
- `ActionCount` is not the deciding factor in this proof: selected and key `JumpOn` candidate both have `actions=6`.

## Root Cause

Root cause: текущий порядок сравнения веток делает `RouteEnergyCost` более важным, чем получение major objective.

Из-за этого бот выбирает более дешевую по route energy ветку без цели (`major=0`) вместо ветки, где есть полезный `JumpOn` (`major=1`), если ради этого `JumpOn` нужно потратить хотя бы немного дополнительной route energy. Визуально это выглядит как лишний switchlane ping-pong и игнор собак, но первичная причина не в геометрии и не в отсутствии `JumpOn`-кандидатов. Кандидаты есть, они проигрывают в evaluator.

`committedPrefix` усиливает симптом: когда плохая ветка выбрана, первые один-два action удерживаются на следующих replans. Но `MERGE_DIAG` доказывает, что плохой tail уже выбран до merge.

## Proposed Solution

Смысловая правка: major objectives должны быть важнее энергии, но нужно отдельно штрафовать энергию, потраченную до первого major objective.

Это защищает от двух регрессов сразу:

- бот не игнорирует `JumpOn`/energy/crystal только ради экономии энергии;
- бот не тратит энергию заранее на обычный `JumpOver`, если тот лишь помогает добраться до будущего `JumpOn`, а рядом есть бесплатный обход через `SwitchLane`.

Финальный порядок:

`Life -> EnergyBeforeFirstMajor -> MajorObjectiveCount -> EnergyCost -> Coin -> ActionCount`

Что это даст:

- Если одна ветка тратит энергию до первой полезной цели, а другая может дойти до первой полезной цели бесплатно, бесплатная ветка выигрывает.
- Если энергия до первой полезной цели одинаковая, выигрывает ветка с большим числом `JumpOn`/energy/crystal.
- Старый кейс `smallNotAliveRoad` сохраняет смысл: обход через `SwitchLane` с меньшей `EnergyBeforeFirstMajor` выигрывает у прыжка, даже если прыжок позволяет увидеть больше дальних целей внутри action-depth horizon.
- Поскольку pruning и evaluator используют `PlanningBranchMetricsComparer`, изменение останется в одном месте и не создаст дублирующую логику.

## Implementation

- `PlanningBranchMetrics` хранит один `EnergyCost`.
- `PlanningBranchMetrics` дополнительно хранит `EnergyBeforeFirstMajor`.
- `PlanningBranchMetrics.Append` суммирует `PlannedAction.EnergyCost` без разделения на route/objective.
- `EnergyBeforeFirstMajor` увеличивается только пока в ветке еще не было major objective, и только если текущее действие само не является major objective.
- `PlanningBranchMetricsComparer` сравнивает `Life -> EnergyBeforeFirstMajor -> MajorObjectiveCount -> EnergyCost -> Coin -> ActionCount`.
- `PlanningBranch` больше не прокидывает `RouteEnergyCost` / `ObjectiveEnergyCost`.

## Verification

Выполнено для диагностики:

- `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore` - success, 43 existing warnings.
- Запущен `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 120 -TimeScale 1`.
- Уровень остановлен вручную до `[TEST RESULT]`, логи прочитаны.

Выполнено после правки:

- `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore` - success, 43 existing warnings.
- Запущен `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 120 -TimeScale 1`.
- Результат уровня: `[TEST RESULT] FAIL`; ранний участок стал работать лучше, оставшиеся регрессы вынесены на отдельный разбор.
