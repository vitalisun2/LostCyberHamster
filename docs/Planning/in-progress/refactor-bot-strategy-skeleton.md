# Задача: refactor-bot-strategy-skeleton

## Описание

Рефакторинг скелета системы стратегий бота для явного представления полной матрицы ситуаций. **Логику новых ситуаций НЕ добавляем** — только переструктурируем существующую так, чтобы следующие задачи могли легко заполнять пустые ячейки.

## Проблема

- `BotAction` enum слишком грубый: один `Jump` скрывает разные семантики (перепрыгнуть, запрыгнуть на крышу, напрыгнуть на врага)
- Стратегии проверяют типы препятствий через if'ы внутри `TryBuildStep` — неясно, что вообще покрыто
- Нет явной таблицы "какие стратегии применимы к какому типу препятствия"

## Решение

1. **Расширить `BotAction` enum** до семантических действий, отражающих реальные механики:
   - `SwitchLane` (сейчас есть)
   - `JumpOver` (перепрыгнуть малое препятствие)
   - `JumpOnRoof` (запрыгнуть на крышу)
   - `JumpOnTarget` (напрыгнуть на alive врага)
   - `SuperJumpOver` (перелететь большого врага)
   - `SuperJumpOnRoof` (суперпрыжком на крышу)
   - `RoofJumpOver` (перепрыгнуть с крыши)
   - `RoofJumpDown` (спрыгнуть с крыши)
   - `RoofSwitchLane` (сместиться с крыши на другую линию/дорогу)

2. **Создать явную таблицу в `ActionGenerator`:**
   ```
   StrategyTable: (onRoof: bool, obstacleType) → IActionStrategy[]
   ```
   Таблица явно показывает, какие стратегии применимы в какой ситуации.

3. **Удалить type-checks из стратегий:**
   - `JumpStrategy.IsSmallObstacle()` → больше не нужна (диспатч уже в таблице)
   - Каждая стратегия содержит только business-логику: энергия, зона приземления, тайминги

4. **Переименовать стратегии для ясности:**
   - `JumpStrategy` → может быть `JumpOverStrategy` или остаться (зависит от рефакторинга)
   - Все новые стратегии должны быть однозначны по названию

## Файлы к изменению

### Core изменения:
- `Assets/Scripts/Bot/Models/BotAction.cs` — расширить enum
- `Assets/Scripts/Bot/Planning/ActionGenerator.cs` — добавить StrategyTable, переделать Generate() логику
- `Assets/Scripts/Bot/Planning/Strategies/JumpStrategy.cs` — удалить `IsSmallObstacle()`, упростить `TryBuildStep()`
- `Assets/Scripts/Bot/Planning/Strategies/SuperJumpStrategy.cs` — удалить type-check, упростить
- `Assets/Scripts/Bot/Planning/Strategies/SwitchLaneStrategy.cs` — проверить, что тип не проверяется (уже норм)

### Новые стратегии (skeleton, пока не реализованы):
- `Assets/Scripts/Bot/Planning/Strategies/JumpOnRoofStrategy.cs` (новая)
- `Assets/Scripts/Bot/Planning/Strategies/JumpOnTargetStrategy.cs` (новая)
- `Assets/Scripts/Bot/Planning/Strategies/SuperJumpOnRoofStrategy.cs` (новая)
- `Assets/Scripts/Bot/Planning/Strategies/RoofJumpOverStrategy.cs` (новая)
- `Assets/Scripts/Bot/Planning/Strategies/RoofJumpDownStrategy.cs` (новая)
- `Assets/Scripts/Bot/Planning/Strategies/RoofSwitchLaneStrategy.cs` (новая)

Все новые стратегии пока возвращают `Rejected("not implemented")` для всех кейсов — это skeleton.

## Что НЕ меняется

- Pipeline (BranchSelector → BranchGenerator → BranchEvaluator) — работает как есть
- Execution layer (StepExecutor, handlers) — не трогаем (пока)
- Тесты и их поведение — должны пройти как раньше
- Текущая логика `JumpStrategy`, `SuperJumpStrategy`, `SwitchLaneStrategy` — остаётся, но переструктурируется

## Тестирование

Все тестовые уровни должны пройти без регрессии:
- `test_threat_small_notalive_road_switchlane`
- `test_threat_small_notalive_road_jump`
- `test_threat_bigalive`

Логи должны показывать те же решения бота, что раньше.

## Метрики успеха

1. ✅ Компиляция без ошибок
2. ✅ Все тестовые уровни пройдены
3. ✅ Логи свидетельствуют об отсутствии регрессии
4. ✅ `ActionGenerator.StrategyTable` явно показывает полную матрицу
5. ✅ Стратегии не содержат if'ов на тип препятствия
6. ✅ Все 9 стратегий (текущие 3 + 6 новых skeleton'ов) зарегистрированы и доступны

## Статус

- [x] Создан worktree (`refactor-strats`)
- [x] BotAction enum расширен (21 действие вместо 4)
- [x] StrategyTable добавлена в ActionGenerator (явная матрица `(onRoof, obstacleType) → BotAction[]`)
- [x] Текущие 3 стратегии упрощены (удалены type-checks, остаётся бизнес-логика)
- [x] 14 новых skeleton-стратегий созданы (для крыши и целевых действий)
- [x] Commit & push выполнены (2 коммита: refactor + fix)
- [ ] Code review ожидается
- [ ] Merge в main

## Реализованные изменения

### Enum BotAction (21 значение)
- Ground: `SwitchLane`, `Jump`, `SuperJump` + skeleton `JumpOnRoof`, `JumpOnTarget`, `SuperJumpOnRoof`, `SuperJumpOnTarget`
- Roof: skeleton `RoofJumpOver`, `RoofJumpDown`, `RoofJumpToRoof`, `RoofJumpOnTarget`, `RoofSuperJumpOver`, `RoofSuperJumpDown`, `RoofSuperJumpToRoof`, `RoofSuperJumpOnTarget`, `RoofSwitchLane`

### ActionGenerator.StrategyTable
Явная матрица маппирует:
- Ground: smallNotAliveRoad/RoadAndRoof → [Jump, SwitchLane]
- Ground: bigAlive → [SuperJump, SwitchLane]  
- Ground: bigNotAlive/mediumNotAlive → [Jump, SuperJump, SwitchLane]
- Ground: smallAlive → [Jump, SuperJump]
- Roof: все типы → соответствующие RoofXxx стратегии

### Стратегии
- Упрощены: JumpOverStrategy (был JumpStrategy), SuperJumpOverStrategy (был SuperJumpStrategy), SwitchLaneStrategy
- Skeleton: 14 новых стратегий (все возвращают "not implemented")

## Совместимость
- Execution layer (StepExecutor) не изменен — по-прежнему ожидает `BotAction.Jump` и `BotAction.SuperJump`
- Skeleton-стратегии генерируют "не implemented" → не будут выбраны планером
- Регрессия исключена: существующая логика работает идентично старому коду
