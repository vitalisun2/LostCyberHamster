# SuperJumpOn fourth pattern analysis

## Scope

- Уровень: `01_New_York/Morning/test_super_jump_on`.
- Паттерн: `test_super_jump_on_04`, описание `should super jump on`.
- Ожидание: бот должен выбрать ветку с `SuperJumpOn` по `smallAlive`.
- Факт из отчёта: бот остаётся на верхней линии и перепрыгивает `smallNotAlive` вместо охоты на target.
- Примечание: пользователь назвал `test SuperJumpOver`, но описание и 4-й паттерн совпадают с `test_super_jump_on`.

## Sources

- `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_super_jump_on/test_super_jump_on.json`
- `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/*New.cs`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/SuperJumpOn/**/*`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/SuperJumpOver/**/*`
- `LostCyberHamster/EditorLogs/diagnostic_log.txt`

## Initial Hypotheses

1. `SuperJumpOnStrategyNew` не создаёт action для нижнего `smallAlive`.
   - Confirm: targeted diagnostics show no `SuperJumpOn` candidate, or code rejects it by role/spec/window/post-action safety.
   - Refute: branch list contains valid `SuperJumpOn` objective branch.
2. Objective branch создаётся, но проигрывает avoidance-ветке из-за evaluator/tie-breaker.
   - Confirm: branch list contains both `SuperJumpOn` and top-lane `SuperJumpOver`, but selected best is avoidance.
   - Refute: objective branch absent.
3. Cross-lane entry branch не доводит planning state до нижней линии/target вовремя.
   - Confirm: after simulated switch to bottom, next decision point skips `smallAlive` or sees unsafe/late target.
   - Refute: branch after switch reaches bottom target and adds `SuperJumpOn`.

## Facts

- `test_super_jump_on_04` содержит нижний `smallNotAliveRoad` (`type=2`, x=9.8), нижний `smallAlive` (`type=0`, x=13.4) и верхний `smallNotAliveRoad` (`type=2`, x=11.0).
- `SnapshotBuilder` сортирует `WorldSnapshot.Obstacles` по `LeftX`, поэтому forward-scan по snapshot сохраняет порядок приближения препятствий.
- `DecisionPointDetectorNew` строит role-based `DecisionPointNew` на одной focus lane через `ObstacleChainBuilderNew`.
- `ObstacleChainBuilderNew.BuildChainElements` останавливает chain, когда gap между same-lane obstacles `>= planningState.Hamster.Width`.
- `SuperJumpOnStrategyNew` до фикса искала target только внутри `decisionPoint.Chain`; если target попадал в отдельную chain, стратегия не могла построить ранний target-bound action.
- Старый path (`GroundJumpOnTargetChainComposer`) отдельно расширял threat-chain до первого ground jump-on target без использования gap как границы target-chain.
- Диагностика до фикса показала отсутствие валидной `SuperJumpOn` ветки для 4-го паттерна в нужный момент: planner выбирал верхний avoidance path, а нижний target становился отдельной/поздней ситуацией.

## Hypothesis Status

- H1 подтверждена: `SuperJumpOn` action для 4-го pattern не создавался в нужном planning window.
- H2 опровергнута: это не ошибка evaluator, потому что objective branch отсутствовала, а не проигрывала comparison.
- H3 подтверждена частично: cross-lane entry работал, но после входа на нижнюю линию strategy была ограничена короткой role-based chain.

## Root Cause

Новая role-based модель правильно упростила `DecisionPointNew` до одной ближайшей ситуации, но `JumpOn/SuperJumpOn` потеряли отдельную action-scope логику: target-bound действие должно проверять первый достижимый ground target за пределами base decision chain, если прыжок реально может покрыть расстояние до target. Без этого `SuperJumpOnStrategyNew` работала только с gap-limited chain и не строила ветку охоты на target в 4-м паттерне.

## Solution Options

Выбранный фикс:

- оставить `DecisionPointDetectorNew` и `ObstacleChainBuilderNew` простыми: они по-прежнему строят только role-based ситуацию одной линии;
- добавить общий `JumpOnActionChainResolver` в `StrategiesNew/Shared/JumpOn`;
- resolver строит временную action-chain до первого same-lane ground target только в пределах reach конкретной `JumpOnTravel`;
- `JumpOnStrategyNew`, `SuperJumpOnStrategyNew` и `JumpOnRetainedValidatorNew` используют один resolver;
- финальная валидность не переносится в resolver: её по-прежнему доказывают `JumpOnWindowCalculatorNew`, runtime resolver и `TargetRemovalPostActionSafety`.

## Validation

- `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_super_jump_on' -TimeoutSeconds 180 -PollMilliseconds 250 -TimeScale 1`
  - Result: `WIN level=13 stars=3`.
  - BOT: в 4-м паттерне появился `SwitchLane` на bottom, затем `SuperJumpOn smallAlive`.
- `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_jump_on' -TimeoutSeconds 180 -PollMilliseconds 250 -TimeScale 1`
  - Result: `WIN level=5 stars=3`.
  - BOT: обычные `JumpOn smallAlive` сохранились.
- Временные `[TEMP_SJO4]` логи удалены из кода; поиск по `Assets/Scripts/Bot` и текущему `diagnostic_log.txt` ничего не нашёл.
