# Bot action safety checker plan

Цель: не создавать planning-ветки из unsafe actions. Runtime mechanics не трогаем.

## 1. Добавить safety checker

### 1.1. Создать `BotActionSafetyChecker`

- В `LostCyberHamster/Assets/Scripts/Bot/Planning/` создать `BotActionSafetyChecker.cs`.
- Добавить метод `bool IsSafe(PlanningState state, PlannedAction action, WorldSnapshot world)`.
- Для `BotActionKind.Jump` вызвать проверку обычного прыжка.
- Для `BotActionKind.SuperJump` вызвать проверку super jump.
- Для `BotActionKind.Tap` вернуть `true`, если `TargetBottomLine` есть и это другая линия.
- Для неизвестного action вернуть `false`.

## 2. Проверить прыжки по runtime-правилам

### 2.1. Добавить safety для Jump

- В `BotActionSafetyChecker` добавить `IsSafeJump`.
- Использовать правила из `JumpMechanics` и `CollisionUtils`.
- Проверять все same-lane obstacles в reach обычного jump, не только target obstacle.
- Вернуть `false`, если любой obstacle дал бы damage.
- Вернуть `true`, если jump безопасен.

### 2.2. Добавить safety для SuperJump

- В `BotActionSafetyChecker` добавить `IsSafeSuperJump`.
- Использовать правила из `SuperJumpMechanics` и `CollisionUtils`.
- Проверять все same-lane obstacles в reach super jump, не только target obstacle.
- Вернуть `false`, если любой obstacle дал бы damage.
- Вернуть `true`, если super jump безопасен.

## 3. Встроить checker в planning

### 3.1. Подключить checker в `TransitionSimulator`

- В `LostCyberHamster/Assets/Scripts/Bot/Planning/TransitionSimulator.cs` создать поле `BotActionSafetyChecker`.
- В `Simulate` перед strategy simulation вызвать `IsSafe`.
- Если `IsSafe == false`, вернуть `null`.
- Если `IsSafe == true`, оставить текущую simulation-логику.

### 3.2. Добавить диагностику unsafe reject

- В `TransitionSimulator` залогировать `REJECT_UNSAFE_ACTION`.
- В лог добавить action kind, target obstacle id и description.

## 4. Проверить `test_superjump_over`

### 4.1. Прогнать тестовый уровень

- Прогнать `01_New_York/Morning/test_superjump_over`.
- Проверить, что `SwitchLane -> Jump` в первой ситуации reject'ится.
- Проверить, что выбирается `SuperJump`.
- Проверить, что жизнь не теряется.
