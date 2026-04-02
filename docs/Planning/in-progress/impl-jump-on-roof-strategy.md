# Задача: impl-jump-on-roof-strategy

## Описание

Реализовать стратегию JumpOnRoof: бот запрыгивает на крышу bigNotAlive/mediumNotAlive, когда SwitchLane невозможен (другая линия заблокирована). Создать тестовый уровень с паттернами для этой ситуации.

## Предпосылки

- StrategyTable уже содержит ячейку `(false, bigNotAlive) → [Jump, SuperJump, SwitchLane]`
- Skeleton `JumpOnRoofStrategy` уже существует (возвращает "not implemented")
- Runtime: при Jump на bigNotAlive механика автоматически переводит хомяка в JumpOnRoof → RoofRun
- Runtime: при окончании крыши хомяк автоматически переходит RunFromRoof → Run (HamsterOnRoof сбрасывается)

## Шаги реализации

### 1. Planning layer

**JumpOnRoofStrategy.cs** — реализовать `TryBuildStep` и `Project`:
- Валидация: `problem.Kind == ThreatCollision`, энергии >= JumpEnergyCost
- Тайминги: аналогично JumpOverStrategy (fireDistance = 1.5, completionWorldShift = fireWorldShift + JumpLandingOffset)
- ApplyEffects:
  - `HamsterOnRoof = true` (ключевое отличие от JumpOver)
  - `Energy -= JumpEnergyCost`
  - Препятствие НЕ удаляется (хомяк на нём стоит)
- Проверка безопасности: нет ли smallNotAliveRoadAndRoof на крыше в точке приземления
- Проекция: аналогична, возвращает snapshot с HamsterOnRoof=true

**ActionGenerator.cs** — обновить таблицу:
```
(false, bigNotAlive) → [JumpOnRoof, SwitchLane]      // заменить Jump/SuperJump на JumpOnRoof
(false, mediumNotAlive) → [JumpOnRoof, SwitchLane]    // аналогично
```

### 2. Execution layer

**Handlers** — добавить маппинг в StepExecutor:
- `BotAction.JumpOnRoof → JumpHandler` (тот же handler — runtime сам определит JumpOnRoof по типу препятствия)

### 3. Тестовый уровень

**Паттерны** (добавить в PatternsCollection.json):

Паттерн 1: `test_jump_on_roof_01_forced_bottom`
- bigNotAlive на bottom (y=-2.8), top линия заблокирована bigNotAlive (y=-1.8)
- Хомяк вынужден запрыгнуть на крышу, другая линия недоступна
- 3 повтора с разным расстоянием

Паттерн 2: `test_jump_on_roof_02_forced_top`
- bigNotAlive на top (y=-1.8), bottom линия заблокирована
- Аналогичная ситуация с верхней линии

Паттерн 3: `test_jump_on_roof_03_mixed`
- Чередование: bottom → top → bottom
- Проверка переключения контекста

**Тестовый уровень** (создать JSON):
- `01_New_York/Morning/test_jump_on_roof/test_jump_on_roof.json`
- Последовательность: relief_energy → паттерн1 → relief_energy → паттерн2 → relief_energy → паттерн3

### 4. Регистрация тестового уровня

- Добавить в `invoke_run_all_test_levels.ps1` (массив $TestLevels)
- Добавить в `workflow.md` (таблица тестовых уровней)

## Что НЕ меняется

- Roof-стратегии (RoofJumpOver, RoofSwitchLane и др.) — пока skeleton
- ProblemResolver — работает как есть (находит bigNotAlive как Threat)
- Runtime механики — не трогаем

## Ожидаемое поведение бота

1. ProblemResolver находит bigNotAlive как Threat на той же линии
2. ActionGenerator смотрит таблицу → [JumpOnRoof, SwitchLane]
3. SwitchLane отклоняется (другая линия заблокирована, нет безопасного окна)
4. JumpOnRoof строит step → бот прыгает → приземляется на крышу
5. Runtime ведёт хомяка по крыше (RoofRun)
6. Крыша заканчивается → RunFromRoof → Run (автоматически)
7. Планер видит HamsterOnRoof=false → продолжает как обычно

## Критерии успеха

- Компиляция без ошибок
- Существующие 3 тестовых уровня: WIN
- Новый тестовый уровень test_jump_on_roof: WIN
- В логах видно, что бот выбирает JumpOnRoof (а не SwitchLane)
