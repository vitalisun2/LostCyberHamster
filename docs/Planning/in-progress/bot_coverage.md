# Покрытие бота: стратегии и взаимодействия

Единый документ: что бот умеет, что нет, и что предстоит.

## Pipeline

```
BotOrchestrator (event-driven)
  -> SnapshotBuilder -> ObjectClassifier -> ProblemResolver
  -> ActionGenerator (StrategyTable: 17 стратегий)
  -> BranchGenerator (до 5 шагов) -> BranchEvaluator -> StepExecutor
```

Пересчёт: `VisibleObjectsChanged`, `StepCompleted`, `StepCancelled`, `ManagedStateChanged`.

## Матрица покрытия

### Условные обозначения

| Символ | Значение |
|---|---|
| done | Реализовано: стратегия + handler + тест |
| todo | Не реализовано (skeleton или отсутствует) |
| n/a | Действие неприменимо к этой ситуации |

### Дорога (HamsterOnRoof = false)

| Объект | SwitchLane | Jump (JumpOver) | JumpOnRoof | SuperJump (SuperJumpOver) | SuperJumpOnRoof |
|---|---|---|---|---|---|
| smallNotAliveRoad | done | done | n/a | n/a | n/a |
| smallNotAliveRoadAndRoof | done | done | n/a | n/a | n/a |
| bigNotAlive | done | n/a | done | n/a | todo |
| mediumNotAlive | done | n/a | done | n/a | todo |
| bigAlive | done | n/a | n/a | done | n/a |
| smallAlive (Target) | todo | todo | n/a | todo | n/a |

### Крыша (HamsterOnRoof = true)

| Объект | RoofSwitchLane | RoofJumpOver | RoofJumpToRoof | RoofJumpOnTarget | RoofJumpDown | RoofSuperJump* |
|---|---|---|---|---|---|---|
| smallNotAliveRoadAndRoof | todo | todo | n/a | n/a | n/a | todo |
| bigNotAlive | n/a | n/a | todo | n/a | n/a | todo |
| mediumNotAlive | n/a | n/a | todo | n/a | n/a | todo |
| smallAlive (Target) | n/a | n/a | n/a | todo | n/a | todo |
| bigAlive (Target) | n/a | n/a | n/a | todo | n/a | todo |
| RunFromRoof зона | todo (нет проверки безопасности спуска) | n/a | n/a | n/a | n/a | n/a |

### Коллектиблы

| Ситуация | Действие | Статус |
|---|---|---|
| Collectible на той же дорожке | Автосбор (механика игры) | Работает без участия бота |
| Collectible на другой дорожке | SwitchLane для сбора | todo |
| Приоритизация Collectible vs Threat | -- | todo |

## StepExecutor: зарегистрированные handlers

`SwitchLane` -> SwitchLaneHandler, `Jump` -> JumpHandler, `JumpOnRoof` -> JumpHandler, `SuperJump` -> SuperJumpHandler.

Все остальные BotAction (14 skeleton) не имеют маппинга в StepExecutor -- стратегии возвращают "not implemented".

## Тестовые уровни

| Уровень | Адрес | Что тестирует |
|---|---|---|
| test_threat_small_notalive_road_switchlane | `01_New_York/Morning/test_threat_small_notalive_road_switchlane` | SwitchLane от smallNotAliveRoad |
| test_threat_small_notalive_road_jump | `01_New_York/Morning/test_threat_small_notalive_road_jump` | Jump через smallNotAliveRoad |
| test_threat_bigalive | `01_New_York/Morning/test_threat_bigalive` | bigAlive: SwitchLane + SuperJump |
| test_jump_on_roof | `01_New_York/Morning/test_jump_on_roof` | bigNotAlive: JumpOnRoof (forced) |

## Ограничения

- `ProblemResolver` обрабатывает только `ThreatCollision` -- collectibles и targets не являются проблемами для планировщика.
- Roof-стратегии -- skeleton, не реализованы.
- RunFromRoof safety: бот не проверяет зону спуска при планировании JumpOnRoof.

## Roadmap

| ID | Описание | Статус |
|---|---|---|
| T-1 | SuperJump для `bigAlive` (forced) | done |
| T-2 | JumpOnRoof для `bigNotAlive`/`mediumNotAlive` | done |
| T-3 | Roof coverage: `smallNotAliveRoadAndRoof` на крыше | todo |
| T-4 | RunFromRoof safety planning | todo |
| T-5 | Target planning для `smallAlive` | todo |
| T-6 | Target planning для `bigAlive` (с крыши) | todo |
| T-7 | Сбор Collectible с другой дорожки | todo |
| T-8 | Приоритизация Collectible vs Threat | todo |
