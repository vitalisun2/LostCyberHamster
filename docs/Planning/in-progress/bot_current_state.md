# Текущее состояние бота

## Что реализовано (этапы 1–12)

Все базовые этапы пройдены. Бот работает на event-driven pipeline с многошаговым планировщиком.

### Архитектура pipeline

```
BotOrchestrator (event-driven)
  → SnapshotBuilder → ObjectClassifier → ProblemResolver
  → ActionGenerator (SwitchLaneStrategy + JumpStrategy)
  → BranchGenerator (до 5 шагов глубиной)
  → BranchEvaluator → StepExecutor
```

### Что бот умеет

**Обработка угроз — ProblemResolver находит ближайший same-lane Threat:**

| Тип угрозы | SwitchLane | Jump |
|---|---|---|
| `smallNotAliveRoad` | ✅ | ✅ |
| `smallNotAliveRoadAndRoof` | ✅ | ✅ |
| `bigNotAlive` | ✅ | ❌ |
| `mediumNotAlive` | ✅ | ❌ |
| `bigAlive` (на земле) | ✅ | ❌ |

**Цепочки действий:**
- Многошаговое планирование (до 5 шагов): `SwitchLane + Jump`, `Jump + SwitchLane`, `SwitchLane + SwitchLane back`
- Отбрасывает цепочки, где любой шаг ведёт к урону
- ProjectedWorld корректно проецирует состояние после каждого шага

**Оценка цепочек (ранжирование):**
- Безопасность → ранг (жизнь > target > collectible > threat-safety) → профит → энергия

**Event-driven пересчёт:**
- `VisibleObjectsChanged`, `StepCompleted`, `StepCancelled`, `ManagedStateChanged`

## Тестовые уровни (автоматизированные)

Запуск: `.\invoke_run_all_test_levels.ps1` или `.\invoke_open_unity_test_level.ps1 -LevelAddress '<адрес>'`

| Имя уровня | Адрес | Что тестирует |
|---|---|---|
| test_threat_small_notalive_road_switchlane | `01_New_York/Morning/test_threat_small_notalive_road_switchlane` | SwitchLane от smallNotAliveRoad |
| test_threat_small_notalive_road_jump | `01_New_York/Morning/test_threat_small_notalive_road_jump` | Jump через smallNotAliveRoad |

## Что НЕ реализовано (этапы 13–15)

| Этап | Описание |
|---|---|
| 13 | Крышные переходы в контексте цепочки — прыжок на `bigNotAlive`/`mediumNotAlive`, RunFromRoof safety в многошаговом контексте |
| 14 | Ульта — активация как шаг цепочки при скоплении неизбежных угроз |
| 15 | Полная регрессия — прогон всех накопленных тестовых паттернов (этапы 1–14) |

## Ограничения текущей реализации

- `ProblemResolver` обрабатывает только `ThreatCollision` — collectibles и targets пока не являются самостоятельными "проблемами" для планировщика.
- `JumpStrategy` ограничена малыми препятствиями: только `smallNotAliveRoad` и `smallNotAliveRoadAndRoof`. Для `bigNotAlive` и `mediumNotAlive` — только `SwitchLane`.
- Тестовые уровни пока покрывают только `smallNotAliveRoad + SwitchLane/Jump`. Уровни для остальных threat-типов не созданы.
