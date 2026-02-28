# Техническое задание: HamsterBot — автоматическое управление хомяком

**Версия:** 2.0  
**Дата:** 2026-02-28  
**Статус:** Реализация

---

## 0. Как пользоваться ботом — краткая инструкция

### Быстрый старт

1. **Запустите игру** (Play Mode в Unity или билд)
2. **Нажмите F1** — бот включится, в левом верхнем углу появится метка `BOT: PLAY`
3. Хомяк начнёт играть сам — убирайте руки с клавиатуры
4. **Нажмите F1 ещё раз** — бот выключится, управление вернётся вам

### Переключение режимов

Нажимайте **F2** для переключения между режимами (бот должен быть включён):

| Режим | Метка на экране | Что делает |
|---|---|---|
| **Play** | `BOT: PLAY` (зелёный) | Бот играет за вас — избегает препятствий, собирает бонусы, использует ульту |
| **Test** | `BOT: TEST` (оранжевый) | Бот целенаправленно тестирует все механики игры и записывает результаты |
| **Analytics** | `BOT: ANALYTICS` (синий) | Бот играет нормально, но ведёт подробный лог для анализа баланса |

### Где смотреть логи

После завершения сессии (конец уровня или выключение бота) файлы появятся:
- **В Unity Editor:** `EditorLogs/bot_sessions/`
- **В билде:** `Application.persistentDataPath/bot_sessions/`

Каждая сессия создаёт:
- `.log` файл — текстовый лог, читаемый человеком
- `.json` файл — структурированный отчёт для автоматического анализа

### Настройки (для продвинутых)

В Inspector на компоненте `HamsterBot` можно настроить:
- **Lookahead Seconds** (1.5) — как далеко бот «видит» вперёд (в секундах)
- **Reaction Delay** (0.1) — пауза между решениями (выше = более «человечный» бот)
- **Aggression Level** (0.7) — 0 = осторожный, 1 = агрессивный (больше напрыгиваний)
- **Planner Depth** (2) — на сколько шагов вперёд бот просчитывает стратегию

Можно создавать пресеты стратегий через `Create → Bot → Strategy Config` и переключать их.

### Советы

- В режиме **Test** бот намеренно врезается в препятствия — это нормально, он проверяет механики
- Если бот «застрял» — выключите (F1) и включите заново
- Для полного тестирования механик прогоните бота на нескольких уровнях — не все ситуации встречаются на одном уровне
- JSON-отчёт можно открывать в любом текстовом редакторе или загрузить в таблицу для графиков

---

## 1. Цель и назначение

### 1.1 Основная задача
Разработать бота, который в рантайме автоматически управляет хомяком, играя за игрока. Бот должен:
- Грамотно избегать препятствий
- Собирать бонусы и коллектиблы
- Напрыгивать на препятствия для получения бонусов и зарядки ульты
- Использовать суперудар в оптимальный момент

### 1.2 Дополнительная задача — тестирование механик
Бот должен иметь режим автотестирования, в котором он целенаправленно проверяет все игровые механики и взаимодействия, фиксируя результаты и ошибки в лог-файл.

### 1.3 Аналитика геймплея
Бот должен вести детализированный лог игровой сессии, позволяющий:
- Анализировать узкие места уровней
- Оценивать балансировку экономики
- Выявлять проблемные паттерны препятствий
- Сравнивать разные стратегии прохождения

---

## 2. Архитектура

### 2.1 Обзор компонентов

```
┌──────────────────────────┐
│     HamsterBotUI         │  Визуальный индикатор + переключение режимов
│  (MonoBehaviour, UI)     │
└──────────┬───────────────┘
           │
┌──────────▼───────────────┐
│     HamsterBot           │  MonoBehaviour-оркестратор
│  (включение/выключение,  │  Управляет режимами, вызывает AtomicEvent'ы
│   настройки, таймер)     │
└──────────┬───────────────┘
           │ Двухуровневая система принятия решений:
           │
     ┌─────▼──────┐   Срочная угроза < 0.3 сек?
     │ BotBrain   │ ← ДА → быстрое реактивное решение (Priority 1-2)
     │ (reactive) │   НЕТ → передаём в BotPlanner
     └─────┬──────┘
           │
     ┌─────▼──────────┐
     │ BotPlanner     │ ← Forward simulation: строит дерево решений
     │ (proactive)    │   на 2-3 шага вперёд, оценивает каждую ветку
     └─────┬──────────┘
           │
     ┌─────▼──────────────┐
     │ IStateEvaluator    │ ← Strategy pattern: Balanced / Aggressive / Defensive
     │ (scoring)          │   Оценивает итоговое состояние каждой ветки
     └────────────────────┘
           │
┌──────────▼───────────────┐
│   BotThreatScanner       │  Чистый C# — сканирование и классификация
│  (зона видимости,        │  препятствий перед хомяком
│   ThreatInfo)            │
└──────────┬───────────────┘
           │
┌──────────▼───────────────┐
│     BotLogger            │  Чистый C# — система логирования бота
│  (сессия, события,       │  Пишет в отдельный файл + summary
│   ошибки, аналитика)     │
└──────────┬───────────────┘
           │
┌──────────▼───────────────┐
│   BotMechanicsValidator  │  Чистый C# — режим тестирования механик
│  (сценарии, чеклист,     │  Прогоняет сценарии из hamster_collision_test_scenarios
│   asserts)               │
└──────────────────────────┘
```

### 2.2 Принцип интеграции — «Ещё один игрок»

Бот **не модифицирует** существующие механики. Он использует те же `AtomicEvent`'ы, что и `KeyboardMechanics`:

| Действие бота | AtomicEvent на Hamster |
|---|---|
| Сменить линию | `TapRequest.Invoke()` |
| Обычный прыжок | `JumpRequest.Invoke()` |
| Суперпрыжок | `SuperJumpRequest.Invoke()` |
| Прыжок с крыши | `RoofJumpRequest.Invoke()` |
| Суперпрыжок с крыши | `SuperRoofJumpRequest.Invoke()` |
| Активировать ульту | `UltaEvent.Invoke()` |

Это даёт нулевые изменения в существующую кодовую базу и гарантирует, что бот тестирует **реальные** механики, а не обходные пути.

### 2.3 Структура файлов

```
Assets/Scripts/Bot/
├── BotAction.cs                 — enum действий бота
├── BotDecision.cs               — структура решения (действие + причина)
├── BotBrain.cs                  — реактивная логика (непосредственные угрозы)
├── BotThreatScanner.cs          — сканирование и классификация зоны впереди
├── BotStrategyConfig.cs         — ScriptableObject с настройками стратегии
├── HamsterBot.cs                — MonoBehaviour-оркестратор
├── HamsterBotUI.cs              — UI индикатор и переключение режимов
├── Planning/
│   ├── BotPlanner.cs            — forward simulation, дерево решений на 2-3 шага
│   ├── SimWorldState.cs         — лёгкая копия мира для симуляции
│   ├── IBotCommand.cs           — интерфейс команды (Command pattern)
│   ├── BotCommands.cs           — реализации команд (Jump, SwitchLane, etc.)
│   └── IStateEvaluator.cs       — интерфейс + реализации оценки состояния (Strategy pattern)
├── Logging/
│   ├── BotLogger.cs             — система логирования
│   ├── BotSessionReport.cs      — итоговый отчёт по сессии
│   └── BotLogEntry.cs           — модель записи лога
└── Testing/
    ├── BotMechanicsValidator.cs — режим тестирования механик
    └── BotTestScenario.cs       — модель тестового сценария
```

---

## 3. Режимы работы бота

### 3.1 Режим «Автоигра» (Play Mode)

**Цель:** Пройти уровень с максимальной эффективностью.

**Поведение:**
- Бот принимает решения каждый кадр (с cooldown `reactionDelay`)
- Использует приоритетную систему для выбора действий
- Собирает статистику и пишет лог

### 3.2 Режим «Тестирование механик» (Test Mode)

**Цель:** Целенаправленно проверить все игровые механики из [hamster_collision_test_scenarios.md](../../docs/hamster_collision_test_scenarios.md).

**Поведение:**
- Бот **намеренно** создаёт ситуации из чеклиста (напрыгивание, перепрыгивание, столкновение, урон)
- После каждого действия сверяет `HamsterState` с ожидаемым
- Результат (pass/fail) записывается в лог
- По завершении — summary с количеством пройденных/проваленных тестов

**Список проверяемых механик:**

| Группа | Количество сценариев | Источник |
|---|---|---|
| Jump Mechanics | 9 | `hamster_collision_test_scenarios.md` §1 |
| Roof Jump Mechanics | 7 | `hamster_collision_test_scenarios.md` §2 |
| Roof Run Mechanics | 2 | `hamster_collision_test_scenarios.md` §3 |
| Super Jump Mechanics | 12 | `hamster_collision_test_scenarios.md` §4 |
| Super Roof Jump Mechanics | 7 | `hamster_collision_test_scenarios.md` §5 |
| Сбор коллектиблов | 5 | collectableEnergetic, collectablePizza, collectableCrystal, collectableLife, collectableCoin |
| Энергетика | 4 | расход 10/20, восстановление 1/сек, бонус +20 |
| Ульта | 3 | зарядка +10%, активация при 100%, использование |
| Жизни | 3 | потеря при урон, восстановление бонусом, смерть при 0 |
| Бонусная механика | 4 | 30% шанс бонуса, распределение 85/5/10 |
| **Итого** | **~56** | |

### 3.3 Режим «Аналитика» (Analytics Mode)

**Цель:** Собрать максимум данных о прохождении для анализа баланса.

**Поведение:**
- Бот играет нормально, но с расширенным логированием
- Фиксирует каждое решение и его контекст
- По завершении генерирует структурированный отчёт

---

## 4. Детальная спецификация компонентов

### 4.1 BotAction — перечень действий

```csharp
public enum BotAction
{
    None,              // ничего не делать
    SwitchLane,        // тап — сменить линию (верх ↔ низ)
    Jump,              // обычный прыжок
    SuperJump,         // суперпрыжок (двойной, из воздуха)
    RoofJump,          // прыжок с крыши bigNotAlive
    SuperRoofJump,     // суперпрыжок с крыши
    UseUlta            // активировать ульта-способность
}
```

### 4.2 BotDecision — структура решения

```csharp
public struct BotDecision
{
    public BotAction Action;
    public string Reason;           // читаемое объяснение для лога
    public Obstacle TargetObstacle; // целевое препятствие (если есть)
    public float Confidence;        // 0..1 — уверенность в решении
}
```

### 4.3 BotThreatScanner — сканер обстановки

#### Входные данные
- `ObstacleSpawner.Instance.SpawnedObstacles` — список активных препятствий
- Позиция хомяка, его состояние, линия (верх/низ)
- `Consts.GameSpeedBase` (3.8 ед/сек) — для расчёта TimeToReach

#### Зона сканирования
```
Хомяк (X = -3.78)          Граница сканирования
  │◄─────────────────────────────►│
  LeftX                    LeftX + ScanDistance

  ScanDistance = GameSpeedBase * LookaheadSeconds
  При LookaheadSeconds = 1.5 → ScanDistance ≈ 5.7 ед
  При LookaheadSeconds = 2.0 → ScanDistance ≈ 7.6 ед
```

#### Выходная модель — ThreatInfo

```csharp
public struct ThreatInfo
{
    public Obstacle Obstacle;
    public ObstacleTypeEnum Type;
    public float DistanceX;          // расстояние от правого края хомяка до левого края препятствия
    public float TimeToReach;        // секунд до столкновения (DistanceX / GameSpeedBase)
    public bool IsOnCurrentLane;     // на текущей линии хомяка
    public bool IsOnOtherLane;       // на противоположной линии
    public bool IsCollectable;       // можно подобрать (coin, crystal, energetic, pizza, life)
    public bool IsSmallAlive;        // можно напрыгнуть для бонуса
    public bool IsRoofable;          // можно забежать на крышу (bigNotAlive, mediumNotAlive)
    public bool IsDangerous;         // может нанести урон при столкновении
    public bool IsOnRoof;            // стоит на крыше другого препятствия
}
```

#### Классификация по типам

| ObstacleTypeEnum | IsCollectable | IsSmallAlive | IsRoofable | IsDangerous |
|---|---|---|---|---|
| `smallAlive` | false | true | false | true* |
| `bigAlive` | false | false | false | true |
| `smallNotAliveRoad` | false | false | false | true |
| `smallNotAliveRoadAndRoof` | false | false | false | true |
| `bigNotAlive` | false | false | true | false** |
| `mediumNotAlive` | false | false | true | false** |
| `collectableEnergetic` | true | false | false | false |
| `collectablePizza` | true | false | false | false |
| `collectableCrystal` | true | false | false | false |
| `collectableLife` | true | false | false | false |
| `collectableCoin` | true | false | false | false |

\* `smallAlive` — опасен при столкновении, но безопасен при напрыгивании  
\** `bigNotAlive`/`mediumNotAlive` — безопасны (хомяк запрыгивает на крышу), но опасны если на крыше `smallNotAliveRoadAndRoof`

### 4.4 BotBrain — логика принятия решений

#### Основной метод

```csharp
public BotDecision Evaluate(BotWorldState state)
```

#### Входные данные — BotWorldState

```csharp
public struct BotWorldState
{
    // Хомяк
    public HamsterStateEnum HamsterState;
    public bool IsOnBottomLine;
    public int Energy;
    public int Lives;
    public int UltaCharge;
    public bool IsDamaged;
    public bool IsShifting;
    public Vector3 HamsterPosition;
    public float HamsterLeftX;
    public float HamsterRightX;
    
    // Обстановка
    public List<ThreatInfo> CurrentLaneThreats;   // на текущей линии
    public List<ThreatInfo> OtherLaneThreats;     // на другой линии
    public List<ThreatInfo> Collectables;          // коллектиблы (обе линии)
}
```

#### Приоритетная система решений

Бот оценивает ситуацию по приоритетам (от высшего к низшему). Первое правило, давшее действие ≠ `None`, побеждает.

```
PRIORITY 1: Защита от смерти
│
├─ Состояние Dead? → None (игра окончена)
├─ Состояние IsDamaged? → None (ждём окончания анимации)
├─ Состояние IsShifting? → None (ждём завершения смены полосы)
│
PRIORITY 2: Реакция на непосредственную угрозу (TimeToReach < ReactionWindowSec)
│
├─ Опасное препятствие на текущей линии в зоне реакции?
│   ├─ Тип smallAlive + Energy >= 10 → Jump (напрыгнуть = безопасно + бонус + ульта)
│   ├─ Тип smallNotAlive/smallNotAliveRoadAndRoof + Energy >= 10 → Jump (перепрыгнуть)
│   ├─ Тип bigNotAlive/mediumNotAlive + Energy >= 10 → Jump (залезть на крышу)
│   ├─ Тип bigAlive + другая линия безопасна → SwitchLane
│   ├─ Тип bigAlive + другая линия опасна + Energy >= 10 → Jump (попытка перепрыгнуть)
│   └─ Обе линии опасны + Energy >= 10 → Jump (прыгаем, шанс выше, чем столкновение)
│
PRIORITY 3: Действия на крыше (если HamsterState == RoofRun)
│
├─ Крыша заканчивается, впереди пусто → None (спрыгнет автоматически через RoofRunMechanics)
├─ Впереди smallNotAliveRoadAndRoof на крыше + Energy >= 10 → RoofJump
├─ Впереди obstacle внизу, можно напрыгнуть с крыши → RoofJump
├─ Впереди bigNotAlive (перебежать на следующую) → None (автоматически)
│
PRIORITY 4: Использование ульты
│
├─ UltaCharge == 100?
│   ├─ Впереди кластер из 2+ опасных препятствий на текущей линии → UseUlta
│   ├─ Lives <= 1 и впереди опасность → UseUlta (спасаем жизнь)
│   └─ Иначе → подождать лучший момент
│
PRIORITY 5: Сбор бонусов — напрыгивание
│
├─ smallAlive на текущей линии в зоне прыжка + Energy >= 10 → Jump
│   (даже если не в непосредственной зоне реакции — ради бонусов/ульты)
│
PRIORITY 6: Сбор коллектиблов
│
├─ Ценный коллектибл (life, crystal, energetic) на другой линии?
│   ├─ Другая линия безопасна → SwitchLane
│   └─ Другая линия опасна → None (не рискуем)
├─ Монета на другой линии + другая линия безопасна → SwitchLane (низкий приоритет)
│
PRIORITY 7: Энергосбережение
│
├─ Energy < 30 → предпочитать SwitchLane вместо Jump
├─ Energy < 10 → только SwitchLane (прыгать нечем)
│
PRIORITY 8: None (всё спокойно)
```

#### Ключевые параметры (настраиваемые)

| Параметр | Значение по умолчанию | Описание |
|---|---|---|
| `LookaheadSeconds` | 1.5 сек | Дальность сканирования |
| `ReactionWindowSec` | 0.6 сек | Зона непосредственной реакции |
| `ReactionDelay` | 0.1 сек | Cooldown между решениями (имитация человека) |
| `AggressionLevel` | 0.7 (0..1) | 0 = пассивный (уклонение), 1 = агрессивный (напрыгивание) |
| `UltaClusterThreshold` | 2 | Минимум опасных препятствий для использования ульты |
| `EnergyConserveThreshold` | 30 | Ниже этого — экономим энергию |

### 4.5 HamsterBot — оркестратор (MonoBehaviour)

#### Ответственности
1. Хранить ссылки на `Hamster`, `BotBrain`, `BotLogger`
2. В `Update()` — запросить решение у `BotBrain` и выполнить
3. Управлять режимами (Play / Test / Analytics)
4. Включаться/выключаться через UI или горячую клавишу

#### Жизненный цикл

```
[Awake]
  ├─ Найти Hamster в сцене
  ├─ Создать BotBrain, BotThreatScanner, BotLogger
  └─ Подписаться на GameManager события (Start, Pause, Resume, Finish)

[Game Start]
  └─ BotLogger.StartSession(levelName, mode)

[Update] (каждый кадр, когда бот активен)
  ├─ Проверить cooldown (reactionDelay)
  ├─ BotThreatScanner.Scan() → List<ThreatInfo>
  ├─ Собрать BotWorldState
  ├─ BotBrain.Evaluate(state) → BotDecision
  ├─ ExecuteAction(decision)
  └─ BotLogger.LogDecision(decision, state)

[Game Finish]
  └─ BotLogger.EndSession() → сгенерировать отчёт
```

#### Связь с Hamster через AtomicEvent

```csharp
private void ExecuteAction(BotDecision decision)
{
    if (decision.Action == BotAction.None) return;
    
    switch (decision.Action)
    {
        case BotAction.SwitchLane:
            _hamster.TapRequest.Invoke();
            break;
        case BotAction.Jump:
            HandleJump();
            break;
        case BotAction.SuperJump:
            _hamster.SuperJumpRequest.Invoke();
            break;
        case BotAction.RoofJump:
            _hamster.RoofJumpRequest.Invoke();
            break;
        case BotAction.SuperRoofJump:
            _hamster.SuperRoofJumpRequest.Invoke();
            break;
        case BotAction.UseUlta:
            _hamster.UltaEvent.Invoke();
            break;
    }
    
    _lastActionTime = Time.time;
}

/// <summary>
/// Определяет тип прыжка в зависимости от текущего состояния.
/// Если хомяк на крыше — RoofJump, иначе — обычный Jump.
/// </summary>
private void HandleJump()
{
    if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
        _hamster.RoofJumpRequest.Invoke();
    else
        _hamster.JumpRequest.Invoke();
}
```

#### Настраиваемые параметры (Inspector, Odin)

```csharp
[Header("Bot Settings")]
[SerializeField] private bool _botEnabled = false;
[SerializeField] private BotMode _mode = BotMode.Play;
[SerializeField] private BotStrategyConfig _strategyConfig;

[Header("Timing")]
[SerializeField] [Range(0.5f, 3f)] private float _lookaheadSeconds = 1.5f;
[SerializeField] [Range(0f, 0.5f)] private float _reactionDelay = 0.1f;

[Header("Strategy")]
[SerializeField] [Range(0f, 1f)] private float _aggressionLevel = 0.7f;

public enum BotMode
{
    Play,       // обычная автоигра
    Test,       // тестирование механик
    Analytics   // расширенная аналитика
}
```

### 4.6 HamsterBotUI — визуальный интерфейс

#### Элементы
- **Индикатор "BOT"** — текстовая метка в верхнем левом углу экрана
  - Зелёный: Play Mode
  - Оранжевый: Test Mode
  - Синий: Analytics Mode
- **Горячая клавиша F1** — toggle вкл/выкл
- **Горячая клавиша F2** — переключение режимов (Play → Test → Analytics → Play)

#### Интеграция с KeyboardMechanics
В `KeyboardMechanics.OnUpdate()` добавить:
```csharp
if (_keyboard.f1Key.wasPressedThisFrame)
    HamsterBot.Instance.ToggleEnabled();

if (_keyboard.f2Key.wasPressedThisFrame)
    HamsterBot.Instance.CycleMode();
```

---

## 5. Система логирования

### 5.1 Архитектура логирования

Бот пишет в **отдельный файл**, не смешивая с `DebugManager.DiagLog()`.

```
EditorLogs/
├── diagnostic_log.txt          — существующий DiagLog (Editor-only)
└── bot_sessions/
    ├── bot_2026-02-28_14-30-25_play.log       — лог сессии Play
    ├── bot_2026-02-28_14-35-10_test.log       — лог сессии Test
    ├── bot_2026-02-28_14-40-00_analytics.log  — лог сессии Analytics
    └── bot_2026-02-28_14-40-00_analytics.json — структурированный отчёт
```

Для Build-версии файлы сохраняются в `Application.persistentDataPath/bot_sessions/`.

### 5.2 BotLogger — API

```csharp
public class BotLogger
{
    // Жизненный цикл сессии
    public void StartSession(string levelName, BotMode mode);
    public void EndSession();
    
    // Решения бота
    public void LogDecision(BotDecision decision, BotWorldState state);
    
    // Игровые события
    public void LogEvent(BotLogEventType type, string details);
    
    // Ошибки и аномалии
    public void LogError(string context, string expected, string actual);
    public void LogAnomaly(string description);
    
    // Генерация отчётов
    public BotSessionReport GenerateReport();
    public void SaveReportAsJson(string path);
}
```

### 5.3 Типы логируемых событий

```csharp
public enum BotLogEventType
{
    // Решения
    Decision_SwitchLane,
    Decision_Jump,
    Decision_SuperJump,
    Decision_RoofJump,
    Decision_SuperRoofJump,
    Decision_UseUlta,
    Decision_None,
    
    // Результаты
    Result_JumpOnObstacle,
    Result_JumpOver,
    Result_JumpOnRoof,
    Result_RoofRun,
    Result_Damage,
    Result_Death,
    Result_CollectedBonus,
    Result_CollectedCoin,
    Result_CollectedCrystal,
    Result_UltaUsed,
    
    // Ошибки (тестовый режим)
    Test_Pass,
    Test_Fail,
    Test_UnexpectedState,
    
    // Аномалии
    Anomaly_StuckInState,
    Anomaly_UnreachableObstacle,
    Anomaly_EnergyInconsistency,
    Anomaly_CollisionMissed
}
```

### 5.4 Формат записи лога

#### Текстовый формат (человекочитаемый)
```
[14:30:25.123] SESSION START | Level: level_01 | Mode: Play
[14:30:25.456] SCAN | Lane: Bottom | Threats: 3 (smallAlive@2.1, bigNotAlive@4.5, collectableCoin@3.0)
[14:30:25.456] DECISION | Jump | Target: smallAlive@2.1 | Reason: "Напрыгнуть для бонуса" | Energy: 80 | Confidence: 0.95
[14:30:25.800] RESULT | JumpOnObstacle | State: JumpOnObstacle | Expected: JumpOnObstacle | PASS
[14:30:26.100] EVENT | BonusReceived | Type: Energy | Amount: +20 | Energy: 90
[14:30:26.500] DECISION | SwitchLane | Reason: "Коллектибл на другой линии" | Energy: 90
...
[14:32:15.000] SESSION END | Duration: 109.5s | Result: Win
```

#### JSON-формат (для автоматического анализа) — генерируется в конце сессии
```json
{
  "sessionId": "bot_2026-02-28_14-30-25",
  "level": "level_01",
  "mode": "Play",
  "duration": 109.5,
  "result": "Win",
  "stats": {
    "totalDecisions": 245,
    "jumps": 38,
    "superJumps": 5,
    "laneSwitches": 42,
    "roofJumps": 8,
    "ultaUsed": 2,
    "obstaclesJumpedOn": 15,
    "obstaclesJumpedOver": 12,
    "damagesTaken": 2,
    "coinsCollected": 28,
    "crystalsCollected": 1,
    "energyBonuses": 3,
    "livesLost": 2,
    "livesRecovered": 1,
    "finalLives": 2,
    "finalEnergy": 65,
    "ultaChargeEvents": 15
  },
  "patterns": {
    "mostDangerousPattern": "pattern_12",
    "patternDamageMap": { "pattern_3": 0, "pattern_5": 1, "pattern_12": 1 },
    "averageDecisionsPerPattern": 8.2
  },
  "economy": {
    "coinsFromJumpOn": 45,
    "coinsFromCollectibles": 28,
    "totalCoins": 73,
    "energySpent": 380,
    "energyRecovered": 60,
    "netEnergyBurn": 320
  },
  "errors": [],
  "anomalies": []
}
```

### 5.5 Данные для аналитики геймплея

Логирование специально фиксирует данные, полезные для баланса:

| Метрика | Зачем |
|---|---|
| Монеты за уровень (по источникам) | Проверка экономики: max ~176 по GameEconomy.md |
| Расход/восстановление энергии | Баланс энергетики: не слишком легко / не слишком сложно |
| Урон по паттернам | Какие паттерны слишком сложные? |
| % решений SwitchLane vs Jump | Разнообразие геймплея |
| Время реакции (решение → результат) | Достаточно ли времени у реального игрока? |
| «Тупиковые» ситуации | Обе линии заблокированы, прыжка не хватает — значит уровень нечестный |
| Использование ульты (момент + контекст) | Оптимальные моменты для ульты |
| Бонусы от напрыгивания (тип + частота) | Проверка дроп-рейтов (85/5/10) |

---

## 6. Режим тестирования механик

### 6.1 Принцип работы

`BotMechanicsValidator` подписывается на `GameEventsManager` и `Hamster.HamsterState` для отслеживания результатов действий. Когда бот выполняет действие, валидатор:

1. Фиксирует **текущий контекст** (состояние хомяка, тип препятствия, позиции)
2. Фиксирует **ожидаемый результат** (какой `HamsterStateEnum` должен получиться)
3. Ждёт **фактического результата** (смены состояния)
4. Сравнивает и пишет **PASS** или **FAIL** в лог

### 6.2 Автоматическое сопоставление с тестовыми сценариями

Матрица из `hamster_collision_test_scenarios.md` кодируется в маппинг:

```csharp
// Пример: Jump Mechanics, сценарий #1
new BotTestScenario
{
    Group = "Jump Mechanics",
    ScenarioId = 1,
    Description = "Напрыгнули на smallAlive",
    RequiredAction = BotAction.Jump,
    TargetObstacleType = ObstacleTypeEnum.smallAlive,
    ExpectedState = HamsterStateEnum.JumpOnObstacle,
    InteractionType = InteractionType.JumpOn
}
```

### 6.3 Как бот создаёт нужные ситуации

В тестовом режиме бот **не избегает** опасностей, а наоборот **ищет** нужные ситуации для текущего непроверенного сценария:

| Сценарий | Требуемое действие бота |
|---|---|
| «Напрыгнуть на smallAlive» | Дождаться smallAlive на линии, прыгнуть с правильным таймингом |
| «Столкнуться с bigAlive» | Дождаться bigAlive, прыгнуть так, чтобы задеть по XY |
| «Perепрыгнуть smallNotAlive» | Прыгнуть раньше, чтобы перелететь |
| «Запрыгнуть на крышу bigNotAlive» | Прыгнуть на bigNotAlive |
| «Прыжок с крыши без урона» | Сначала забраться на крышу, потом прыгнуть когда впереди чисто |

Для сценариев, требующих урона, бот **временно игнорирует** опасность и намеренно идёт на столкновение. Валидатор отслеживает, что урон действительно нанесён.

### 6.4 Формат отчёта тестирования

```
=== MECHANICS TEST REPORT ===
Level: level_01 | Duration: 245s | Date: 2026-02-28

--- Jump Mechanics ---
  [PASS] #1 Напрыгнули на smallAlive → JumpOnObstacle
  [PASS] #2 Перепрыгнули smallAlive → JumpOver
  [FAIL] #3 Столкнулись с smallNotAliveRoad → Expected: JumpDamageForSmallNotAlive, Got: Jump
  [SKIP] #5 Столкнулись с smallNotAliveRoadAndRoof на крыше — ситуация не встретилась
  ...

--- Summary ---
  Total scenarios:  56
  Passed:           41
  Failed:           3
  Skipped:          12 (ситуация не возникла на уровне)
  
--- Failed details ---
  Jump #3: smallNotAliveRoad collision not detected
    Context: hamsterX=-3.78, obstacleX=-2.10, energy=80, state=Run
    Expected: JumpDamageForSmallNotAlive
    Actual: Jump (obstacle was not in overlap range)
    Possible bug: collision tolerance too tight?
```

---

## 7. Включение/выключение бота

### 7.1 Способы управления

| Способ | Что делает | Когда использовать |
|---|---|---|
| **F1** | Toggle вкл/выкл бота | Быстрое переключение в рантайме |
| **F2** | Переключение режима (Play → Test → Analytics) | Смена режима на лету |
| **Inspector** | Checkbox + все настройки | Тонкая настройка, дебаг |
| **Программный API** | `HamsterBot.Instance.SetEnabled(true/false)` | Из других систем |

### 7.2 Визуальная индикация

Когда бот включён, в верхнем левом углу экрана отображается:
```
┌─────────────────┐
│ BOT: PLAY ▶     │  (зелёный — автоигра)
│ BOT: TEST ✓     │  (оранжевый — тестирование)
│ BOT: ANALYTICS  │  (синий — аналитика)
└─────────────────┘
```

### 7.3 Взаимодействие с ручным управлением

Когда бот включён:
- `KeyboardMechanics` и тач-ввод **блокируются** (кроме F1/F2/Esc)
- Все действия хомяка управляются **только** ботом
- При выключении — ввод возвращается игроку

---

## 8. Подписки на GameEventsManager

Бот подписывается на существующие события для отслеживания результатов:

```csharp
// Подписки BotLogger / BotMechanicsValidator
GameEventsManager.OnObstacleJumpedOn    += OnJumpedOn;
GameEventsManager.OnObstacleJumpedOver  += OnJumpedOver;
GameEventsManager.OnObstacleCollision   += OnCollision;
GameEventsManager.OnCoinCollected       += OnCoinCollected;
GameEventsManager.OnCrystalsCollected   += OnCrystalCollected;
GameEventsManager.OnEnergyAdded         += OnEnergyAdded;
GameEventsManager.OnEnergySpent         += OnEnergySpent;
GameEventsManager.OnLivesAdded          += OnLivesAdded;
GameEventsManager.OnLivesLost           += OnLivesLost;
GameEventsManager.OnUltaUsed            += OnUltaUsed;
GameEventsManager.OnUltaActivated       += OnUltaActivated;
GameEventsManager.OnLevelCompleted      += OnLevelCompleted;
```

Также бот наблюдает за `Hamster.HamsterState` (подписка на `AtomicVariable.OnChanged`) для отслеживания переходов состояний.

---

## 9. Настройки стратегии (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "BotStrategy", menuName = "Bot/Strategy Config")]
public class BotStrategyConfig : ScriptableObject
{
    [Header("Сканирование")]
    [Range(0.5f, 3f)] public float LookaheadSeconds = 1.5f;
    [Range(0.2f, 1f)] public float ReactionWindowSec = 0.6f;
    
    [Header("Таймер")]
    [Range(0f, 0.5f)] public float ReactionDelay = 0.1f;
    
    [Header("Стратегия")]
    [Range(0f, 1f)] public float AggressionLevel = 0.7f;
    public int UltaClusterThreshold = 2;
    public int EnergyConserveThreshold = 30;
    
    [Header("Тестовый режим")]
    public bool StopOnFirstFailure = false;
    public int MaxTestDurationSeconds = 300;
    
    [Header("Логирование")]
    public bool LogEveryDecision = false;    // true = каждое решение в лог (много данных)
    public bool LogOnlyActions = true;       // только решения != None
    public bool GenerateJsonReport = true;
}
```

Позволяет создавать пресеты: «Осторожный бот», «Агрессивный бот», «Тестовый бот» — и переключать через Inspector.

---

## 10. Зависимости и изменения в существующем коде

### 10.1 Минимальные изменения

| Файл | Изменение | Причина |
|---|---|---|
| `KeyboardMechanics.cs` | +2 строки: обработка F1/F2 | Горячие клавиши бота |
| `GameEventsManager.cs` | Без изменений | Все нужные события уже есть |
| `Hamster.cs` | Без изменений | AtomicEvent'ы уже public |
| `ObstacleSpawner.cs` | Без изменений | `SpawnedObstacles` уже public |
| `CollisionUtils.cs` | Без изменений | Бот использует ту же логику для оценки |

### 10.2 Новые файлы

Все файлы из раздела 2.3 — около 10 новых C#-файлов в `Assets/Scripts/Bot/`.

---

## 11. Ограничения и известные сложности

### 11.1 SuperJump — двойной прыжок
`DoubleJumpDetector` требует два нажатия пробела с коротким интервалом. Бот должен вызывать `SuperJumpRequest` напрямую — это работает, так как `SuperJumpMechanics` слушает отдельный `AtomicEvent`, минуя `DoubleJumpDetector`.

### 11.2 Тайминг прыжков
Бот знает `worldShift` от анимации прыжка (из `HelpMethods.GetWorldShiftForClip`), поэтому может точно рассчитать, долетит ли хомяк до препятствия. Однако для первой версии достаточно эвристики на основе `TimeToReach`.

### 11.3 Тестовый режим — не все сценарии возникнут
На одном уровне могут не встретиться все комбинации препятствий. Поэтому:
- Валидатор отмечает непроверенные как **SKIP**
- Для полного покрытия нужно прогонять бота на нескольких уровнях
- Или создать специальный test-уровень с полным набором ситуаций

### 11.4 Рандомизация бонусов
Дроп бонусов от напрыгивания рандомизирован (30% шанс, внутри 85/5/10). Тестовый режим может зафиксировать распределение, но для статистической достоверности нужно много прогонов.

---

## 12. План реализации

### Фаза 1: Каркас (core)
1. `BotAction.cs`, `BotDecision.cs` — модели данных
2. `BotThreatScanner.cs` — сканирование обстановки
3. `BotBrain.cs` — реактивная логика (Priorities 1-2: выживание)
4. `HamsterBot.cs` — оркестратор с toggle вкл/выкл
5. `BotLogger.cs` — базовое файловое логирование
6. Интеграция F1/F2 в `KeyboardMechanics`

### Фаза 2: Полная реактивная стратегия
7. `BotBrain.cs` — приоритеты 3-8 (ульта, бонусы, энергия, крыша)
8. `BotStrategyConfig.cs` — ScriptableObject с настройками
9. `HamsterBotUI.cs` — визуальный индикатор

### Фаза 3: Forward Simulation (BotPlanner)
10. `SimWorldState.cs` — лёгкая модель мира для симуляции
11. `IBotCommand.cs`, `BotCommands.cs` — Command pattern
12. `IStateEvaluator.cs` — Strategy pattern (Balanced/Aggressive/Defensive)
13. `BotPlanner.cs` — дерево решений с lookahead
14. Интеграция двухуровневой системы (BotBrain → BotPlanner)

### Фаза 4: Аналитика
15. `BotSessionReport.cs` — генерация JSON-отчётов
16. `BotLogEntry.cs` — модель записи

### Фаза 5: Тестирование механик
17. `BotTestScenario.cs` — модель сценария
18. `BotMechanicsValidator.cs` — валидатор механик
19. Кодирование всех 56 сценариев
20. Тестовый прогон и фиксинг

---

## 14. Forward Simulation — стратегическое планирование на 2-3 шага

### 14.1 Зачем нужно

Реактивная система (BotBrain, приоритеты) хорошо справляется с непосредственными угрозами, но не может обработать ситуации, где нужно просчитать последствия:

- Напрыгнуть на `smallAlive` → отскок → врезаться в `bigAlive` сразу за ним?
- Сменить линию сейчас → добраться до бонуса → успеть вернуться до следующего препятствия?
- Использовать ульту сейчас или подождать более плотный кластер?

### 14.2 Двухуровневая система принятия решений

```
HamsterBot.Update():
│
├─ BotThreatScanner.Scan() → threats
│
├─ Ближайшая угроза < 0.3 сек?
│   ├─ ДА → BotBrain.EvaluateUrgent()  ← мгновенная реакция, без планирования
│   └─ НЕТ → BotPlanner.PlanBestAction() ← forward simulation на 2-3 шага
│
└─ ExecuteAction(decision)
```

**Почему двухуровневая:** Человек тоже так работает — в панике реагирует рефлексом (BotBrain), а при запасе времени строит план (BotPlanner). Это и реалистично, и эффективно (планирование дороже по CPU, но вызывается только когда есть время).

### 14.3 BotPlanner — алгоритм

```
PlanBestAction(currentState):
  simState = SimWorldState.FromReal(currentState)
  bestSequence = Search(simState, depth=0)
  return bestSequence.FirstAction

Search(state, depth):
  if depth >= MaxDepth OR state.IsGameOver:
    return Evaluate(state)  ← Strategy pattern

  best = Worst
  for each command in AvailableCommands:
    if !command.CanExecute(state): continue       ← pruning
    nextState = command.Simulate(state)
    if nextState.LivesLost > 0: continue          ← pruning: не идём на верную смерть
    result = Search(nextState, depth + 1)
    if result.Score > best.Score: best = result

  return best
```

### 14.4 Производительность

Мир **полностью детерминирован**: препятствия движутся с `GameSpeedBase = 3.8` ед/сек, позиции заранее известны. Симуляция — чистая арифметика, без физики и рендеринга.

| Параметр | Значение |
|---|---|
| Глубина дерева | 2-3 шага |
| Доступных действий на шаг | ~4-5 (из 7 минус невалидные) |
| Максимум листьев | 5³ = 125 |
| После pruning | ~20-40 реально |
| Стоимость одной симуляции | Арифметика: сдвинуть X, проверить overlap |
| Итого за кадр | < 0.1 мс |

### 14.5 SimWorldState — лёгкая модель мира

Копия реального состояния без MonoBehaviour, Transform и коллайдеров:

```csharp
public struct SimWorldState
{
    // Хомяк
    public float Time;
    public bool IsOnBottomLine;
    public HamsterStateEnum HamsterState;
    public int Energy;
    public int Lives;
    public int UltaCharge;
    public bool IsDamaged;

    // Накопленная статистика (для скоринга)
    public int CoinsCollected;
    public int BonusesCollected;
    public int ObstaclesJumpedOn;
    public int DamagesTaken;
    public int LivesLost;
    public float EnergySpent;

    // Препятствия — массив структур (без аллокаций)
    public SimObstacle[] Obstacles;
    public int ObstacleCount;
}

public struct SimObstacle
{
    public float X;
    public float Width;
    public float Height;
    public ObstacleTypeEnum Type;
    public bool IsOnBottomLine;
    public bool IsCollected;
}
```

### 14.6 IBotCommand — Command Pattern

Каждое действие бота — объект, умеющий применить себя к симулированному состоянию:

```csharp
public interface IBotCommand
{
    BotAction ActionType { get; }
    bool CanExecute(SimWorldState state);
    SimWorldState Simulate(SimWorldState state);
}
```

Реализации: `JumpCommand`, `SwitchLaneCommand`, `SuperJumpCommand`, `RoofJumpCommand`, `UseUltaCommand`, `NoneCommand`.

Каждая команда:
1. Проверяет предусловия (`CanExecute`) — энергия, состояние хомяка
2. Клонирует состояние
3. Применяет эффект (расход энергии, смещение позиции)
4. Продвигает препятствия на `stepDuration` секунд вперёд
5. Проверяет столкновения/сбор бонусов
6. Возвращает новое состояние

### 14.7 IStateEvaluator — Strategy Pattern

Разные стратегии по-разному оценивают итоговое состояние ветки:

```csharp
public interface IStateEvaluator
{
    float Evaluate(SimWorldState state);
}
```

| Стратегия | Приоритеты | Когда использовать |
|---|---|---|
| **BalancedEvaluator** | Бонусы +2, напрыгивание +3, ульта +1, урон -15, энергия -0.1 | Стандартная игра |
| **AggressiveEvaluator** | Напрыгивание +8, ульта +5, урон -10 | Максимум очков/бонусов |
| **DefensiveEvaluator** | Урон -30, смерть -100, полное здоровье +10 | Когда мало жизней |

`CompositeEvaluator` смешивает стратегии с весами (например, 70% Balanced + 30% Defensive).

### 14.8 Пример: сложная ситуация с отскоком

**Ситуация:** Верхняя линия. На нижней — `smallAlive` (можно напрыгнуть), сразу за ним `bigAlive`.

```
Вариант A: SwitchLane → Jump на smallAlive → отскок → bigAlive
  Шаг 1: SwitchLane → переход на нижнюю
  Шаг 2: Jump → напрыгиваем, отскок
  Шаг 3: приземление → overlap с bigAlive → УРОН
  Score: +3 (бонус) -15 (урон) = -12

Вариант B: SwitchLane → SuperJump → перелетаем оба
  Шаг 1: SwitchLane → переход на нижнюю
  Шаг 2: SuperJump → перелетаем и smallAlive, и bigAlive
  Шаг 3: приземление → безопасно
  Score: +3 (бонус) -0.2 (энергия 20) = +2.8

Вариант C: None → None → None (ждём)
  Score: 0

Бот выбирает Вариант B (Score: +2.8)
```

---

## 15. Критерии приёмки

- [ ] Бот включается по F1 и показывает индикатор
- [ ] Бот переключает режимы по F2
- [ ] В режиме Play бот проходит level_01 без смерти (при 3+ жизнях)
- [ ] Бот собирает коллектиблы и напрыгивает на smallAlive
- [ ] Бот использует ульту при скоплении препятствий
- [ ] BotPlanner просчитывает 2-3 шага и избегает ситуации «напрыгнул + отскок в bigAlive»
- [ ] Лог-файл создаётся в `EditorLogs/bot_sessions/`
- [ ] JSON-отчёт генерируется в конце сессии
- [ ] В тестовом режиме проверяются все доступные сценарии
- [ ] Тестовый отчёт показывает PASS/FAIL/SKIP для каждого сценария
- [ ] При выключении бота управление возвращается игроку
- [ ] Существующие механики не модифицированы (бот работает through AtomicEvent)
