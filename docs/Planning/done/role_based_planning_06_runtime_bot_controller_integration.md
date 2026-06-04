# Module plan: RuntimeBotController integration

## Назначение

Подключить role-based planning path в существующей runtime-точке сборки зависимостей. `RuntimeBotController` остаётся единственным controller/lifecycle entrypoint. `RuntimeCompositionNew` и `RuntimeBotControllerNew` не создавать.

## Факты по текущему коду

- `RuntimeBotController.Awake` создаёт список `IPlanningStrategy`, затем на нём строит `PlanExecutor`, `PlanBuilder`, `ActionGenerator`, `TransitionSimulator`, `RetainedActionRevalidator`, `ActionInProgressProjector`.
- `CreateStrategies` сейчас регистрирует все стратегии.
- `PlanExecutor` работает с action execution handlers по `BotActionKind`, а не с `DecisionPoint`.
- `TransitionSimulator` и `ActionInProgressProjector` строят maps по `BotActionKind`.
- New-path strategies используют контракт `IPlanningStrategyNew`, поэтому классы с constructor/API от strategy contract получают отдельные `*New` версии.

## Целевая форма

Composition остаётся прямо в `RuntimeBotController`:

- `CreateRoleBasedStrategiesForMigration()` возвращает только role-based `SwitchLane` на первом этапе.
- `PlanExecutorNew` принимает `IPlanningStrategyNew` и публикует тот же runtime execution surface.
- `PlanBuilderNew` работает с `ActionGeneratorNew`, `PlanningGraphBuilderNew`, `TransitionSimulatorNew`, `RetainedActionRevalidatorNew`, `ActionInProgressProjectorNew`.
- `ActionInProgressProjectorNew` принимает `IPlanningStrategyNew`.
- `TransitionSimulatorNew` уже принимает `IPlanningStrategyNew`.
- Старый `CreateStrategies` остаётся до полного cleanup.

Не создавать `Assets/Scripts/Bot/RuntimeNew`, `RuntimeCompositionNew` или `RuntimeBotControllerNew`: это был бы новый runtime lifecycle/composition слой без собственной ответственности.

## Переключение пути

На время миграции нужен явный и локальный выбор active path:

- local constant в `RuntimeBotController`;
- без UI и без debug toggles, пока пользователь не попросит;
- нельзя смешивать old strategies и role-based strategies в одном action generator.

## Что не делать

- Не удалять старый path до миграции всех strategies.
- Не создавать `RuntimeCompositionNew`.
- Не создавать `RuntimeBotControllerNew`.
- Не добавлять runtime setting/UI ради временного переключателя.
- Не коммитить debug logs/toggles.

## Риски

- SwitchLane-only path ожидаемо неполный для уровней, где нужен jump. Это этап миграции, а не regression old path.
- В active path не должно быть двух simulator/executor registrations для одного `BotActionKind`.

## Валидация будущей реализации

- Active role-based path регистрирует только `SwitchLane` strategy.
- Старый path компилируется и остаётся доступен до cleanup.
- Runtime ручная проверка выполняется пользователем на `integration/unity-live`.
