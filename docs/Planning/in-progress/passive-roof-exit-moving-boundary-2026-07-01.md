# Passive roof exit как MovingBoundary

## Цель

Сделать естественный сход с крыши полноценной planning situation, чтобы граф мог строить ветку `PassiveRoofExit -> ground actions` без runtime-only ожидания и без смешивания ответственности в `ActionGenerator`.

## Архитектурная форма

- `DecisionPoint` получает `DecisionPointKind`.
- Chain-based точки остаются `DecisionPointKind.ObstacleChain`.
- Новый тип ситуации: `DecisionPointKind.MovingBoundary`.
- Первый boundary kind: `MovingBoundaryKind.PassiveRoofExit`.

## Ответственности

- `MovingBoundaryDecisionPointDetector`: только обнаруживает ситуацию движения `RoofRun -> PassiveRoofExit`; не считает timing/safety/action параметры.
- `ActionGenerator`: вызывает detector и передает найденный `DecisionPoint` в обычный strategy pipeline.
- `PassiveRoofExitStrategy`: принимает route-chain context как раньше и новый moving-boundary context.
- `PassiveRoofExitPlanner`: считает last roof, run-from-roof travel window, safety и completion shift.
- `PassiveRoofExitSimulator` и `PassiveRoofExitExecutor`: остаются владельцами simulation/runtime lifecycle.

## Ограничения реализации

- Не создавать synthetic obstacle chain.
- Не строить `PlannedAction` напрямую в `ActionGenerator`.
- Не менять `RoofSwitchLane`.
- Не переносить `PassiveCollect` или `PassiveAdvance` в moving boundary в этой задаче.
- Не переименовывать `PassiveRoofExitPlanner`: в проекте `Planner` уже используется для passive/no-input actions, а `WindowFinder` — для active fire-window actions.

## Интеграция

1. Расширить `DecisionPoint` enum-ами и factory для moving boundary.
2. Добавить `MovingBoundaryDecisionPointDetector.TryDetectPassiveRoofExit`.
3. Добавить hook в `ActionGenerator` после route/collectible collectors, когда нет current-lane decision и не создано actions.
4. Расширить `PassiveRoofExitStrategy.CanConsider`.
5. Добавить boundary path в `PassiveRoofExitPlanner`.
6. Для boundary action использовать `targetObstacleIndex = -1`, `targetObstacleInstanceId = null`, `triggerObstacleInstanceId = lastRoof.InstanceId`.
7. Добавить `PassiveRoofExit` в route setup/bridge metrics, чтобы no-input boundary не выигрывал равные ветки только за счет нулевой энергии.

## Проверка

- Compile / Unity recompile.
- Все test levels.
- Campaign New York Morning.
- Campaign New York Afternoon.
- Перед коммитом: локальное code review по ответственности, null-contracts, метрикам, diagnostics, regressions.
