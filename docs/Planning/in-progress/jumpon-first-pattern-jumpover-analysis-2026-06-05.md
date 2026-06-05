# JumpOn first pattern chooses JumpOver analysis

## Scope

- Уровень: `test_jump_on`.
- Регресс: первый паттерн `smallAlive`.
- Ожидание: бот выполняет `JumpOn` по target.
- Факт по визуальному прогону: бот выполняет `JumpOver`.

## Источники данных

- Свежий BOT/ECO/STAB лог последнего ручного прогона.
- Role-based planning path: `DecisionPointDetectorNew`, `ObstacleChainBuilderNew`, `ActionGeneratorNew`, `PlanningGraphBuilderNew`, `PlanEvaluator`.
- Стратегии: `JumpOverStrategyNew`, `JumpOnStrategyNew`, `SwitchLaneStrategyNew`.
- Policy/константы: `JumpOverPolicy`, `JumpOnPolicy`, `JumpOnObjectiveRules`.
- Тестовый уровень и паттерны `test_jump_on`.

## Гипотезы

1. `JumpOn` не создается для первого `smallAlive`.
   - Подтверждение: в BOT-логе нет кандидата/ветки `JumpOn` для первого obstacle или стратегия отсекает его проверкой.
   - Опровержение: `JumpOn` есть в ветках-кандидатах.
2. `JumpOn` создается, но проигрывает `JumpOver` в evaluator.
   - Подтверждение: обе ветки есть, выбран `JumpOver`; порядок `PlanEvaluator` объясняет выбор.
   - Опровержение: `JumpOn` отсутствует или невалиден.
3. `JumpOn` создается, но не помечается как target-objective.
   - Подтверждение: `JumpOn` action имеет `fulfillsJumpOnObjective=false`, поэтому evaluator сравнивает его с `JumpOver` как обычный безопасный action.
   - Опровержение: `JumpOn` имеет objective priority и всё равно проигрывает.

## Факты по коду

- `RuntimeBotController` использует role-based path и активные `StrategiesNew`.
- `ActionGeneratorNew` отдает один `DecisionPointNew` всем стратегиям без локального ранжирования.
- `smallAlive` классифицируется как `BlockingThreat + Target` через `ObstacleRoleClassifierNew`.
- `JumpOnStrategyNew` ищет первый element с ролью `Target`, затем вызывает `JumpOnSpecificationNew`.
- `JumpOnSpecificationNew` дополнительно проверяет: hamster в `Run`, не на крыше, не shifting, хватает energy, target на текущей линии, target можно атаковать ground jump-on.
- `PlanEvaluator` сначала сравнивает `JumpOnObjectiveCount`, затем energy/taps/timing. Если `JumpOn` создан с `fulfillsJumpOnObjective=true`, он должен выигрывать у обычного `JumpOver`.
- `SwitchLaneExecutor.IsCompleted` завершает action, когда `hamster.IsShifting.Value == false` и line уже равна target line.
- `TapMechanics.OnTap` сразу переключает animator bool и публичную `IsOnBottomLine`, но `IsShifting` берется из `Animator.IsInTransition(0)`.
- В кадр tap animator transition может еще не стартовать, поэтому публичный `IsShifting` может остаться `false`, хотя shift-анимация фактически начнется следующим animator/update циклом.
- `Hamster.Update` синхронизирует `IsShifting` через `TapMechanics.OnUpdate`; бот планирует в `GameManager.LateUpdate`.

## Факты по логам

- Автопрогон `test_jump_on` со speed `1` завершился `WIN`.
- В первом root-plan `JumpOn` по `smallAlive` создается: `objective=True`, energy `100`.
- Evaluator выбирает ветку `SwitchLane for target hunt -> JumpOn smallAlive`, то есть гипотеза "JumpOn проиграл JumpOver в evaluator" на root-узле не подтверждается.
- После `SwitchLane for target hunt` executor пишет `COMPLETE lane=bottom`.
- Сразу после этого текущий `DecisionPointNew` содержит тот же `smallAlive` на bottom с ролями `BlockingThreat|Target`, но `JumpOnStrategyNew` пишет `SKIP reason=spec_failed`, а `actions=none`.
- Значит текущий узкий участок расследования: почему `JumpOnSpecificationNew` возвращает `false` после успешного switch на target lane.
- Дополнительная диагностика показала конкретный предикат `spec_failed`: `hamsterBottom=True obstacleBottom=True isOnRoof=False isShifting=True canJumpOn=True`.
- То есть `JumpOn` отсекается не из-за lane, target type, energy или evaluator, а из-за `hamster.IsShifting == true`.

## Статус гипотез

- Гипотеза 1 частично подтверждена: `JumpOn` создается на root-симуляции, но после фактического `SwitchLane` не создается.
- Гипотеза 2 опровергнута для root-узла: когда `JumpOn` есть как objective branch, evaluator выбирает его.
- Гипотеза 3 опровергнута для root-узла: `JumpOn` помечен `objective=True`.

## Корень проблемы

`SwitchLane` action завершается слишком рано.

После tap публичная line уже меняется на target lane, но animator transition может еще не начаться. В этот короткий момент `SwitchLaneExecutor.IsCompleted` видит `IsShifting == false` и target lane, поэтому снимает head-action. Следующий replanning идет уже без in-progress projection/retained head, но live snapshot после обновления mechanics получает `IsShifting == true`. Из-за этого все road strategies, включая `JumpOn`, отсекаются по specification. Когда shift заканчивается, target уже слишком близко или planning успевает выбрать avoid-ветку.

Это объясняет, почему root evaluator выбирает `SwitchLane -> JumpOn`, но фактическое выполнение первого pattern не доходит до `JumpOn`.

## Решение

Исправить источник runtime-факта `Hamster.IsShifting`, а не добавлять задержку в bot executor.

`TapMechanics` уже пишет `Hamster.IsShifting` через `ShiftTransformAnimatorController.IsShifting()`. Значит корректный владелец fix — `ShiftTransformAnimatorController`: после `ToggleLane()` он должен считать hamster shifting, если animator bool уже указывает на новую линию, но текущий animator state ещё не соответствует этой линии. Это закрывает кадр до старта `Animator.IsInTransition` и оставляет `SwitchLaneExecutor` на простом контракте: ждать `Hamster.IsShifting == false` и целевую линию.

## Проверка

- Выполнен автопрогон: `tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_jump_on' -TimeoutSeconds 150 -PollMilliseconds 250`.
- Скрипт использовал speed `1`.
- Результат: `WIN`.
