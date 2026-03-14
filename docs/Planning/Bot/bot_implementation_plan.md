# Bot Implementation Plan

Поэтапная реализация бота с нуля.

**Основа:**

* [bot_architecture.md](bot_architecture.md) — архитектура (компоненты, pipeline, сущности)
* [bot universal algorithm brainstrom.md](bot%20universal%20algorithm%20brainstrom.md) — decision loop
* [bot concept brainstrom](bot%20concept%20brainstrom) — доменные детали

**Подход:** код бота пишется поэтапно. Каждый этап — рабочий бот с ограниченным
функционалом. Следующий этап добавляет возможности поверх предыдущего.

**Папка нового бота:** `Assets/Scripts/BotV2/`

Старый код (`Assets/Scripts/Bot/`) остаётся как справочник. Оттуда можно копировать
проверенные фрагменты (collision utils, timing, snapshot builder logic).

---

## Этапы

### Общее правило для этапов 1–4

* Все тестовые ситуации требуют только один шаг.
* Построение цепочек не используется.
* Бот работает по pipeline одного шага:
  `Trigger → Snapshot → Classify → ActionGenerator → ActionSelector → StepExecutor`

### Этап 1. Один объект, один простой тип угрозы

* Обрабатывается только `Threat`.
* На экране один объект.
* Один простой тип угрозы (`smallNotAliveRoad`).
* Бот строит один шаг.
* Проверяется корректность выбора безопасного действия.

### Этап 2. Один объект, все типы угроз

* Все типы `Threat` (`smallNotAliveRoad`, `smallNotAliveRoadAndRoof`, `bigNotAlive`, `mediumNotAlive`, `bigAlive`).
* На экране один объект.
* Если безопасных действий несколько — выбор по энергоэффективности.

### Этап 3. Один объект, все категории

* `Threat`, `Target`, `Collectible`.
* На экране один объект.
* Выбор по приоритетам: безопасность → профит → энергия.

### Этап 4. Несколько объектов, один шаг

* На экране несколько объектов, но ситуация решается одним решением.
* Бот определяет ближайший релевантный объект.
* Строит один безопасный шаг с учётом остальных видимых объектов.

### Этап 5. Последовательность одиночных шагов через пересчёт

* После завершения шага — пересчёт.
* После изменения состава видимых объектов — пересчёт.
* Полноценное построение цепочек ещё не используется.

### Этап 6. Цепочки из двух шагов

* Построение безопасных цепочек длиной 2.
* Небезопасные цепочки отбрасываются.
* Выбор лучшей по приоритетам.

### Этап 7. Цепочки из трёх и более шагов

* Более длинные цепочки.
* Сравнение вариантов по приоритетам: безопасность → профит → энергия.

### Этап 8. Полный алгоритм

* Все категории объектов, пересчёт, цепочки, keep tail + extend.
* Бот стабильно проходит тестовые ситуации без потери жизни.

---

## Реализация этапа 1

### Цель

Минимальный рабочий бот: видит один `smallNotAliveRoad`, выбирает безопасное действие,
исполняет его в правильный момент. 10/10 прогонов без потери жизни.

### Шаг 1.1. Структура папки и базовые сущности

Создать `Assets/Scripts/BotV2/` со структурой:

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
```

**BotAction (этап 1):**

```
None
SwitchLane
Jump
```

### Шаг 1.2. SnapshotBuilder

Единственный компонент с доступом к Unity-объектам.
Читает `Hamster` и `ObstacleSpawner`, строит `BotSceneSnapshot`.

Можно взять логику из `Assets/Scripts/Bot/Pipeline/SnapshotBuilder.cs`
и упростить для этапа 1 (не нужны roof-проверки).

### Шаг 1.3. ObjectClassifier

Для этапа 1: `smallNotAliveRoad` → `Threat`, всё остальное → `Neutral`.

### Шаг 1.4. ActionGenerator

Вход: `ObstacleInfo[]`, состояние хомяка.
Выход: список допустимых безопасных `ChainStep[]`.

Для этапа 1 — два варианта:
* `SwitchLane` — если объект на текущей линии, другая линия безопасна
* `Jump` — если объект на текущей линии, тип перепрыгиваемый, есть 10 энергии

Прогнозирование безопасности (минимальное):
* `SwitchLane` безопасен, если на другой линии нет Threat в ближайшей зоне
* `Jump` безопасен, если зона приземления (~3.8 юнитов вперёд) свободна

### Шаг 1.5. ActionSelector

Из безопасных действий — выбрать лучшее.
Этап 1: профит не актуален, только безопасность → энергия.
`SwitchLane` (0 энергии) лучше `Jump` (10 энергии) при равной безопасности.

### Шаг 1.6. StepExecutor

Ждёт правильной дистанции до объекта, отправляет игровую команду.

Для `SwitchLane`: fire при dist ≈ 4.0 (заблаговременно).
Для `Jump`: fire при dist ≈ 1.5 (ближе к объекту).

Можно взять timing логику из `Assets/Scripts/Bot/Pipeline/BotTimingPolicy.cs`.

### Шаг 1.7. BotOrchestrator

MonoBehaviour. В `Update()`:

1. Проверить, что хомяк в управляемом состоянии (`Run`)
2. Если есть активный шаг — пытаться исполнить (StepExecutor)
3. Если шаг завершён или нет шага — запустить pipeline:
   `Snapshot → Classify → Generate → Select → передать в StepExecutor`

Горячая клавиша F1: вкл/выкл бота.

### Шаг 1.8. BotLogger

Логирование по стадиям pipeline. Формат: `[STAGE] detail`.

```
[SNAPSHOT] hamster=(lane=bottom state=Run energy=85) visible=3
[CLASSIFY] #0 smallNotAliveRoad dist=6.2 lane=bottom → Threat
[CLASSIFY] #1 decor dist=9.0 lane=top → Neutral
[GENERATE] obj=#0 action=SwitchLane cost=0 safe=true
[GENERATE] obj=#0 action=Jump cost=10 safe=true
[SELECT] chose SwitchLane (cost=0, reason="cheapest safe action")
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

### Шаг 1.9. Тестовые паттерны

Создать файл `Assets/Content/locations/level_design_templates/levels/bot_test_patterns.json`
с минимальными паттернами для этапа 1.

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

**Паттерны этапа 1:**

Два базовых паттерна (по одному `smallNotAliveRoad`):

```
bot_s1_threat_bottom  "угроза на нижней линии"
  obstacles: [{type:2, x:15, y:-2.8}]

bot_s1_threat_top  "угроза на верхней линии"
  obstacles: [{type:2, x:15, y:-1.8}]
```

**Тестовые уровни** (сборки из паттернов):

```
bot_test_stage1_zigzag
  Последовательность: threat_bottom → threat_top → threat_bottom → threat_top
  
  Что происходит:
  1. Хомяк bottom, threat bottom → SwitchLane на top
  2. Хомяк top, threat top → SwitchLane на bottom
  3. Хомяк bottom, threat bottom → SwitchLane на top
  4. Хомяк top, threat top → SwitchLane на bottom
  
  Проверяет:
  - SwitchLane работает в обе стороны
  - бот корректно определяет "свою линию" по текущей позиции хомяка
  - бот не хардкодит линию хомяка как bottom

bot_test_stage1_sameline
  Последовательность: threat_bottom → threat_bottom → threat_bottom
  
  Что происходит:
  1. Хомяк bottom, threat bottom → SwitchLane на top
  2. Хомяк top, threat bottom → не мешает, проезжаем
  3. Хомяк top, threat bottom → не мешает, проезжаем
  
  Проверяет:
  - бот НЕ реагирует на угрозу, которая не на его линии
  - нет ложных срабатываний после SwitchLane
```

Все 4 ситуации, покрываемые этими уровнями:

| Хомяк | Угроза | Ожидание | Покрывается |
|-------|--------|----------|-------------|
| bottom | bottom | SwitchLane | zigzag шаг 1,3 |
| bottom | top | ничего | — (хомяк стартует bottom, threat на top — первый паттерн zigzag если бы top шёл первым, но мы это покрываем через sameline шаг 2,3 зеркально) |
| top | top | SwitchLane | zigzag шаг 2,4 |
| top | bottom | ничего | sameline шаг 2,3 |

Собрать тестовый уровень из этих паттернов:
`Assets/Content/locations/level_design_templates/levels/bot_test_level_stage1.json`

### Шаг 1.10. Запуск, отладка, 10/10

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
