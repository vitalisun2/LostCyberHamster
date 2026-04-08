# Покрытие бота: стратегии и взаимодействия

Единый документ: что бот умеет, что нет, и что предстоит.

## Активные стратегии

Включены **2 стратегии**: `SwitchLaneStrategy`, `JumpOverStrategy`.
Остальные 14 стратегий реализованы как .cs файлы, но закомментированы в `ActionGenerator` для поэтапной отладки.

## Pipeline

```
BotOrchestrator (event-driven)
  -> SnapshotBuilder
  -> BranchSelector
    -> ProblemResolver + ObjectClassifier
    -> ActionGenerator (StrategyTable)
    -> BranchGenerator (до 5 шагов)
    -> BranchEvaluator
  -> StepExecutor
```

Переоценка: `OnNewObjectAppeared` и `PlanExhausted`. `StepCompleted` продвигает committed plan.

## Матрица покрытия

### Условные обозначения

| Символ | Значение |
|---|---|
| done | Включено и работает: стратегия + handler |
| todo | Не включено (реализация может существовать, но отключена или отсутствует) |
| n/a | Действие неприменимо к этой ситуации |

### Дорога (HamsterOnRoof = false)

| Объект | SwitchLane | Jump (JumpOver) | JumpOnRoof | SuperJump (SuperJumpOver) | SuperJumpOnRoof |
|---|---|---|---|---|---|
| smallNotAliveRoad | done | done | n/a | n/a | n/a |
| smallNotAliveRoadAndRoof | done | done | n/a | n/a | n/a |
| bigNotAlive | done | n/a | todo | n/a | todo |
| mediumNotAlive | done | n/a | todo | n/a | todo |
| bigAlive | done | n/a | n/a | todo | n/a |
| smallAlive (Target) | todo | todo | n/a | todo | n/a |

### Крыша (HamsterOnRoof = true)

| Объект | RoofSwitchLane | RoofJumpOver | RoofJumpToRoof | RoofJumpOnTarget | RoofJumpDown | RoofSuperJump* |
|---|---|---|---|---|---|---|
| smallNotAliveRoadAndRoof | todo | todo | n/a | n/a | n/a | todo |
| bigNotAlive | n/a | n/a | todo | n/a | n/a | todo |
| mediumNotAlive | n/a | n/a | todo | n/a | n/a | todo |
| smallAlive (Target) | n/a | n/a | n/a | todo | n/a | todo |
| bigAlive (Target) | n/a | n/a | n/a | todo | n/a | todo |
| RunFromRoof зона | todo | n/a | n/a | n/a | n/a | n/a |

### Коллектиблы

| Ситуация | Действие | Статус |
|---|---|---|
| Collectible на той же дорожке | Автосбор (механика игры) | Работает без участия бота |
| Collectible на другой дорожке | SwitchLane для сбора | todo |
| Приоритизация Collectible vs Threat | -- | todo |

## StepExecutor: зарегистрированные handlers

| Action | Handler | Активно используется |
|---|---|---|
| SwitchLane | SwitchLaneHandler | да |
| JumpOver | JumpHandler | да |
| JumpOnRoof | JumpHandler | нет (стратегия отключена) |
| SuperJump | SuperJumpHandler | нет (стратегия отключена) |
| RoofJumpOver | JumpHandler | нет (стратегия отключена) |
| RoofSwitchLane | SwitchLaneHandler | нет (стратегия отключена) |

## Тестовые уровни

| Уровень | Адрес | Что тестирует |
|---|---|---|
| test_switch_lane | `01_New_York/Morning/test_switch_lane` | SwitchLane от различных obstacle |
| test_jump_over | `01_New_York/Morning/test_jump_over` | JumpOver через small препятствия |

## Ограничения

- Включены только 2 из 16 стратегий: `SwitchLaneStrategy`, `JumpOverStrategy`. Остальные закомментированы в `ActionGenerator`.
- Для bigAlive, bigNotAlive, mediumNotAlive на дороге единственный включённый ответ — SwitchLane.
- `ProblemResolver` ищет только ближайшую same-lane угрозу — collectibles и targets не являются planner-задачами.
- Roof-стратегии отключены.
- RunFromRoof safety: бот не проверяет зону спуска при планировании.

## Roadmap

| ID | Описание | Статус |
|---|---|---|
| T-1 | Включить SuperJump для `bigAlive` | todo |
| T-2 | Включить JumpOnRoof для `bigNotAlive`/`mediumNotAlive` | todo |
| T-3 | Включить SuperJumpOnRoof для `bigNotAlive`/`mediumNotAlive` | todo |
| T-4 | Roof coverage: стратегии на крыше | todo |
| T-5 | RunFromRoof safety planning | todo |
| T-6 | Target planning для `smallAlive` | todo |
| T-7 | Target planning для `bigAlive` (с крыши) | todo |
| T-8 | Сбор Collectible с другой дорожке | todo |
| T-9 | Приоритизация Collectible vs Threat | todo |
