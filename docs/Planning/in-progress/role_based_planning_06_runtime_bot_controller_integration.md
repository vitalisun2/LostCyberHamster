# Module plan: RuntimeBotController integration

## Назначение

Подключить role-based planning path в существующей точке сборки зависимостей. Отдельного класса `RuntimeCompositionNew` нет и создавать его не нужно.

## Факты по текущему коду

- `RuntimeBotController.Awake` создаёт список `IPlanningStrategy`, затем на нём строит `PlanExecutor`, `PlanBuilder`, `ActionGenerator`, `TransitionSimulator`, `RetainedActionRevalidator`, `ActionInProgressProjector`.
- `CreateStrategies` сейчас регистрирует все стратегии.
- `PlanExecutor` работает с action execution handlers по `BotActionKind`, а не с `DecisionPoint`.
- `TransitionSimulator` и `ActionInProgressProjector` строят maps по `BotActionKind`.

## Целевая форма

Composition остаётся в `RuntimeBotController` или маленькой private factory рядом с ним:

- `CreateRoleBasedStrategiesForMigration()` возвращает только role-based `SwitchLane` на первом этапе.
- `PlanExecutor` переиспользуется, если strategy отдаёт тот же executor contract.
- `TransitionSimulator`/`ActionInProgressProjector` переиспользуются или получают минимальный adapter к role-based strategy contract.
- Старый `CreateStrategies` остаётся до полного cleanup.

Не создавать `Assets/Scripts/Bot/RuntimeNew` без доказанной причины. Это был бы новый слой без собственной доменной ответственности.

## Переключение пути

На время миграции нужен явный и локальный выбор active path:

- private factory/constant в `RuntimeBotController`;
- без UI и без debug toggles, пока пользователь не попросит;
- нельзя смешивать old strategies и role-based strategies в одном action generator.

## Что не делать

- Не удалять старый path до миграции всех strategies.
- Не создавать `RuntimeCompositionNew`.
- Не добавлять runtime setting/UI ради временного переключателя.
- Не коммитить debug logs/toggles.

## Риски

- SwitchLane-only path ожидаемо неполный для уровней, где нужен jump. Это этап миграции, а не regression old path.
- В active path не должно быть двух simulator/executor registrations для одного `BotActionKind`.

## Валидация будущей реализации

- Active role-based path регистрирует только `SwitchLane` strategy.
- Старый path компилируется и остаётся доступен до cleanup.
- Runtime ручная проверка выполняется пользователем на `integration/unity-live`.
