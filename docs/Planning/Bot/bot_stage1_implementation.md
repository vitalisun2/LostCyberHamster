# Реализация этапа 1

Часть [bot_implementation_plan.md](bot_implementation_plan.md).

---

## Цель

Минимальный рабочий бот: видит один `smallNotAliveRoad`, выбирает безопасное действие,
исполняет его в правильный момент. 10/10 прогонов без потери жизни.

---

## Шаг 1.1. Структура папки и базовые сущности

Папка: `Assets/Scripts/BotV2/`

```
BotV2/
  Data/
    BotSceneSnapshot.cs
    ObstacleInfo.cs       (struct, без ObstacleRef — пока не нужен)
    ChainStep.cs
    BotAction.cs          (enum)
    ObjectCategory.cs     (enum: Threat, Target, Collectible, Neutral)
  Pipeline/
    SnapshotBuilder.cs
    ObjectClassifier.cs
    ActionGenerator.cs
    ActionSelector.cs
    StepExecutor.cs
  BotOrchestrator.cs      (MonoBehaviour, точка входа)
  BotLogger.cs            (логирование)
```

**BotSceneSnapshot:**

```
bool HamsterOnBottom
bool HamsterOnRoof
float HamsterRightX
int Energy
int Lives
float SnapshotTime
List<ObstacleInfo> VisibleObjects
```

**ObstacleInfo:**

```
ObstacleTypeEnum Type
bool IsTopLane
float LeftX, RightX, CenterX
float DistanceToHamster
ObjectCategory Category
int StableId
```

**ChainStep:**

```
BotAction Action
ObstacleInfo TargetObstacle
float ExecuteAtDistance
int EnergyCost
string Reason
ChainStepStatus Status
```

**BotAction (этап 1):**

```
None
SwitchLane
Jump
```

---

## Шаг 1.2. SnapshotBuilder

Единственный компонент с доступом к Unity-объектам.
Читает `Hamster` и `ObstacleSpawner`, строит `BotSceneSnapshot`.

Взята логика из `Assets/Scripts/Bot/Pipeline/SnapshotBuilder.cs`,
упрощена для этапа 1 (нет `ObstacleRef`, нет roof-проверки в ObstacleInfo).

---

## Шаг 1.3. ObjectClassifier

Для этапа 1: `smallNotAliveRoad` → `Threat`, всё остальное → `Neutral`.

---

## Шаг 1.4. ActionGenerator

Вход: `BotSceneSnapshot`.
Выход: список допустимых безопасных `ChainStep[]`.

Для этапа 1 — два варианта действий на ближайшую угрозу на линии хомяка:

* `SwitchLane` — если другая линия безопасна (нет Threat в зоне видимости)
* `Jump` — если тип перепрыгиваемый, есть ≥10 энергии, зона приземления (~3.8 юнитов) свободна

Тайминг огня:
* `SwitchLane`: `ExecuteAtDistance = 4.0`
* `Jump`: `ExecuteAtDistance = 1.5`

---

## Шаг 1.5. ActionSelector

Из безопасных действий выбрать лучшее по наименьшей стоимости энергии.
`SwitchLane` (0 энергии) предпочтительнее `Jump` (10 энергии).

---

## Шаг 1.6. StepExecutor

Ждёт правильной дистанции до объекта, отправляет игровую команду.

Получает живую дистанцию до объекта через `ObstacleSpawner.Instance` по `StableId`
(= `Obstacle.GetInstanceID()`).

Состояния шага:
* `Ready` → проверяем дистанцию → огонь → `InProgress`
* `InProgress` → ждём завершения действия → `Completed`

Признаки завершения:
* `SwitchLane`: `!IsShifting` спустя 0.1 с после огня
* `Jump`: `HamsterState == Run`

---

## Шаг 1.7. BotOrchestrator

MonoBehaviour. В `Update()`:

1. Проверить `HamsterState == Run` (контролируемое состояние)
2. Если есть активный шаг — пытаться исполнить (`StepExecutor.TryExecute()`)
3. Если шаг завершён или шага нет — запустить pipeline:
   `Snapshot → Classify → Generate → Select → SetStep`

Горячая клавиша `F1`: вкл/выкл бота.

Подписка на `DamageEvent` хомяка для логирования урона (см. шаг 1.8).

---

## Шаг 1.8. BotLogger

Логирование по стадиям pipeline. Формат: `[STAGE] detail`.

```
[SNAPSHOT] hamster=(lane=bottom state=Run energy=85) visible=3
[CLASSIFY] #0 smallNotAliveRoad dist=6.2 lane=bottom → Threat
[CLASSIFY] #1 decor dist=9.0 lane=top → Neutral
[GENERATE] obj=#0 action=SwitchLane cost=0 safe=true
[GENERATE] obj=#0 action=Jump cost=10 safe=true
[SELECT] chose SwitchLane (cost=0, reason="SwitchLane away from threat")
[EXECUTE] SwitchLane: liveDist=4.05 → FIRE
[RESULT] completed, hamster lane=top, lives=3, damage=false
```

При получении урона — расширенный блок:

```
[DAMAGE] ===
  killer: smallNotAliveRoad dist=0.1 lane=bottom
  hamster: state=Run lane=bottom energy=85 lives=2→1
  active step: SwitchLane status=Ready (NOT EXECUTED)
  last action: none
  diagnosis: step should have fired at dist=4.0 but dist=0.1
[DAMAGE] ===
```

Два уровня логов, переключаемых в Inspector:
* **Normal** — SELECT + EXECUTE + RESULT + DAMAGE
* **Verbose** — + SNAPSHOT + CLASSIFY + GENERATE (каждый вариант)

Логи пишутся через `DebugManager.DiagLog()` в файл `EditorLogs/diagnostic_log.txt`.

---

## Шаг 1.9. Тестовые паттерны

Файл: `Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`
(паттерны добавлены в конец существующего файла)

Маппинг ObstacleTypeEnum → type (int) в JSON:

| Enum | type |
|------|------|
| smallAlive | 0 |
| bigAlive | 1 |
| smallNotAliveRoad | 2 |
| smallNotAliveRoadAndRoof | 3 |
| bigNotAlive | 4 |
| collectableEnergetic | 5 |
| collectablePizza | 6 |
| collectableCrystal | 7 |
| collectableLife | 8 |
| collectableCoin | 9 |
| decor | 10 |
| mediumNotAlive | 11 |

Y-координаты линий:

| Линия | Y |
|-------|---|
| Нижняя (bottom) | -2.8 |
| Верхняя (top) | -1.8 |

**Паттерны:**

```
bot_s1_threat_bottom  "угроза на нижней линии"
  obstacles: [{type:2, x:15, y:-2.8}]

bot_s1_threat_top  "угроза на верхней линии"
  obstacles: [{type:2, x:15, y:-1.8}]
```

**Тестовый уровень:** `Assets/Content/locations/level_design_templates/levels/bot_test_level_stage1.json`

Два сценария в одном уровне:

`zigzag` — чередование угроз:
```
bot_s1_threat_bottom → bot_s1_threat_top → bot_s1_threat_bottom → bot_s1_threat_top
```

`sameline` — угрозы на одной линии:
```
bot_s1_threat_bottom → bot_s1_threat_bottom → bot_s1_threat_bottom
```

Покрываемые ситуации:

| Хомяк | Угроза | Ожидание |
|-------|--------|----------|
| bottom | bottom | SwitchLane на top |
| top | top | SwitchLane на bottom |
| top | bottom | ничего |
| bottom | top | ничего |

---

## Шаг 1.10. Запуск, отладка, 10/10

1. Запустить бот на тестовом уровне.
2. Читать логи из `EditorLogs/diagnostic_log.txt`.
3. При провале — анализировать DAMAGE блок, фиксить.
4. Критерий прохождения: 10 прогонов подряд, 0 потерянных жизней на каждом паттерне.

---

## Правила отладки

### Принцип изоляции

Одна итерация — одна причина. Не вносить несколько изменений одновременно.

### Три уровня анализа ошибки

| Уровень | Вопрос |
|---------|--------|
| **Решение** | Бот правильно выбрал действие? |
| **Планирование** | Цепочка корректна? (этап 6+) |
| **Исполнение** | Timing правильный? |

### Правило перехода

Не переходить к следующему этапу, пока текущий не пройден 10/10.
