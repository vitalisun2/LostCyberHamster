# Техническое задание: реализация бота по целевой архитектуре

**Версия:** 1.0  
**Дата:** 2026-03-13  
**Основа:** [bot concept brainstrom](bot%20concept%20brainstrom) v4.0, [bot_architecture.md](bot_architecture.md) v3.0  
**Статус текущего кода:** 6 файлов, ~950 строк (greedy single-step planner)  
**Целевой результат:** полноценный rule-based chain planner с перебором вариантов

---

## 1. Обзор текущего состояния

### 1.1. Что сейчас есть

| Файл | Строк | Назначение |
|---|---|---|
| `HamsterBot.cs` | ~520 | Singleton-оркестратор: dirty flag, Update loop, ExecuteAction, timing (ShouldDelay*), auto-restart |
| `BotChainPlanner.cs` | ~430 | Монолитный алгоритм: ScanObstacles + Classify + BuildChain (4 if/else ветки) |
| `BotAction.cs` | ~20 | Enum: None, SwitchLane, Jump, SuperJump, RoofJump, SuperRoofJump, UseUlta |
| `ObstacleInfo.cs` | ~60 | Readonly struct + ObjectCategory enum |
| `ChainStep.cs` | ~30 | Readonly struct: Action, TargetIndex, ExecuteAtDistance, EnergyCost, Reason |
| `HamsterBotUI.cs` | ~70 | OnGUI оверлей "BOT ON/OFF (F1)" |

### 1.2. Принципиальные отличия от целевой архитектуры

| Аспект | Текущий код | Целевая архитектура |
|---|---|---|
| **Планирование** | Greedy: одно действие за раз | Chain planner: 2–5 шагов с перебором вариантов |
| **Кандидаты** | 1 цепочка (первый подходящий вариант) | Несколько кандидатных цепочек с ветвлениями |
| **Projected State** | Отсутствует | Состояние проецируется после каждого шага |
| **Структура кода** | Монолит (BotChainPlanner) | Pipeline из 7 независимых компонентов |
| **Хранение плана** | `List<ChainStep>`, полная перестройка каждый раз | `CurrentPlan` с FIFO-очередью, keep tail + extend |
| **Валидация плана** | Отсутствует | PlanValidator: проверка хвоста перед пересчётом |
| **Scoring** | If/else приоритеты | ChainScorer: формализованная оценка (безопасность → стоимость → выгода) |
| **Триггеры пересчёта** | Dirty flag (10 кадров, изменение count, energy, state) | Event-driven: завершение шага + изменение состава объектов |
| **Автоматические переходы** | Частично (HasRoofObstacle) | Полная проекция RunFromRoof, автопереход крыш, отскок |
| **Snapshot** | Прямой доступ к Unity-объектам из BotChainPlanner | Единственная точка контакта — SnapshotBuilder |

---

## 2. План реализации — этапы

### Принципы декомпозиции

- Каждый этап — самодостаточный, компилируемый, тестируемый
- Бот продолжает работать после каждого этапа (не ломается)
- Более ранние этапы создают фундамент для поздних
- Этапы идут в порядке зависимости: сначала данные, потом алгоритмы

---

## Этап 1. Сущности данных

**Цель:** создать все типы данных, описанные в архитектуре, которых сейчас нет.

### 1.1. `BotSceneSnapshot` (новый файл)

**Путь:** `Assets/Scripts/Bot/Data/BotSceneSnapshot.cs`

```
public class BotSceneSnapshot
{
    public bool HamsterOnBottom;       // линия хомяка
    public bool HamsterOnRoof;         // пространство: дорога или крыша
    public float HamsterRightX;        // правый край хомяка (для расстояний)
    public int Energy;
    public int Lives;
    public int UltaCharge;
    public int Coins;
    public List<ObstacleInfo> VisibleObjects;  // ещё не классифицированные
}
```

**Что делать:**
- Создать файл
- `VisibleObjects` содержит объекты с `Category = Neutral` по умолчанию (классификация — отдельный этап)

### 1.2. `ProjectedState` (новый файл)

**Путь:** `Assets/Scripts/Bot/Data/ProjectedState.cs`

```
public class ProjectedState
{
    public bool OnBottom;              // линия
    public bool OnRoof;                // пространство
    public float ApproxX;             // примерная X-позиция хомяка
    public int Energy;
    public int UltaCharge;
    public List<ObstacleInfo> RemainingObjects;  // ещё не обработанные объекты
}
```

**Что делать:**
- Создать файл
- Добавить метод `public static ProjectedState FromSnapshot(BotSceneSnapshot snapshot)` — начальное состояние
- Добавить метод `public ProjectedState Clone()` — для ветвления генератора

### 1.3. `ChainCandidate` (новый файл)

**Путь:** `Assets/Scripts/Bot/Data/ChainCandidate.cs`

```
public class ChainCandidate
{
    public List<ChainStep> Steps;
    public ProjectedState FinalState;
    public int TotalEnergyCost;
    public bool AllStepsSafe;          // все шаги безопасны
    public int TargetsDestroyed;       // количество уничтоженных Target
    public int CollectiblesGathered;   // количество собранных бонусов
    public float Score;                // итоговая оценка от ChainScorer
}
```

### 1.4. `CurrentPlan` (новый файл)

**Путь:** `Assets/Scripts/Bot/Data/CurrentPlan.cs`

```
public class CurrentPlan
{
    public List<ChainStep> Steps;      // FIFO-очередь
    public string Strategy;            // причина выбора (для логирования)
    
    public ChainStep? Head => Steps.Count > 0 ? Steps[0] : null;
    public bool IsEmpty => Steps.Count == 0;
    
    public void RemoveCompletedFromHead();
    public List<ChainStep> GetTail();  // шаги после Head (для keep tail)
}
```

### 1.5. Модификация `ChainStep` (существующий файл)

**Путь:** `Assets/Scripts/Bot/ChainStep.cs`

**Изменения:**
- Добавить поле `ChainStepStatus Status` (ready / in_progress / completed)
- Добавить `ObstacleInfo? TargetObstacle` (прямая ссылка вместо индекса)
- Сохранить `TargetObstacleIndex` для обратной совместимости на переходном этапе
- Сделать struct → class (нужна мутабельность для Status)

```
public enum ChainStepStatus { Ready, InProgress, Completed }

public class ChainStep
{
    public BotAction Action;
    public int TargetObstacleIndex;
    public ObstacleInfo? TargetObstacle;
    public float ExecuteAtDistance;
    public int EnergyCost;
    public string Reason;
    public ChainStepStatus Status;
}
```

### 1.6. Модификация `ObstacleInfo` (существующий файл)

**Путь:** `Assets/Scripts/Bot/ObstacleInfo.cs`

**Изменения:**
- Добавить `int StableId` — устойчивый идентификатор объекта (GetInstanceID от Obstacle), нужен PlanValidator для проверки, что объект всё ещё на экране
- Разделить Category на два момента: присваивается в ObjectClassifier, не в SnapshotBuilder

**Результат этапа:** все целевые структуры данных существуют и компилируются. Бот всё ещё работает по старой логике.

---

## Этап 2. SnapshotBuilder — изоляция Unity-зависимостей

**Цель:** вынести всё обращение к живым Unity-объектам в один компонент.

**Путь:** `Assets/Scripts/Bot/Pipeline/SnapshotBuilder.cs`

### Что делать:

1. Создать класс `SnapshotBuilder` с единственным публичным методом:
   ```
   public BotSceneSnapshot Build(Hamster hamster, float scanRange)
   ```

2. Перенести логику из `BotChainPlanner.ScanObstacles()`:
   - Чтение состояния хомяка (линия, пространство, энергия, жизни, ульта)
   - Чтение монет (`ResourceManager.GetCurrentBalance(ResourceType.Coins)`)
   - Сканирование `ObstacleSpawner.Instance.SpawnedObstacles`
   - Построение `ObstacleInfo[]` **без категорий** (Category = Neutral по умолчанию)
   - Сортировка по LeftX

3. Каждый `ObstacleInfo` получает `StableId = obs.GetInstanceID()`

4. SnapshotBuilder — **единственный** компонент, обращающийся к `ObstacleSpawner`, `Hamster`, `ResourceManager` напрямую. Все остальные компоненты pipeline работают только со snapshot.

**Результат этапа:** `ScanObstacles` удалён из `BotChainPlanner`, заменён вызовом `SnapshotBuilder.Build()` в `HamsterBot`.

---

## Этап 3. ObjectClassifier — выделение классификации

**Цель:** вынести логику классификации из монолита в отдельный компонент.

**Путь:** `Assets/Scripts/Bot/Pipeline/ObjectClassifier.cs`

### Что делать:

1. Создать класс с методом:
   ```
   public void Classify(BotSceneSnapshot snapshot)
   ```
   Метод проставляет Category каждому объекту в `snapshot.VisibleObjects`.

2. Перенести логику `BotChainPlanner.Classify()` — добросовестная копия текущей логики.

3. **Расширение:** добавить контекстно-зависимую классификацию (уже частично есть):
   - `bigAlive` с крыши → `Target` (атака с крыши), с дороги → `Threat`
   - `smallAlive` → `Target` (можно напрыгнуть с дороги и с крыши)
   - `bigNotAlive`, `mediumNotAlive` → `Threat` (но одновременно — потенциальная крыша)
   - Collectible приоритеты: жизнь > энергия > кристалл > монета (закодировать в поле `Priority`)

4. Добавить в `ObstacleInfo` поле `int CollectiblePriority` для порядка сбора бонусов.

**Результат этапа:** классификация изолирована. `BotChainPlanner.Classify()` удалён.

---

## Этап 4. ProjectedState — механизм проекции состояния

**Цель:** реализовать механизм проекции состояния хомяка после каждого шага, включая автоматические переходы.

**Путь:** `Assets/Scripts/Bot/Pipeline/StateProjector.cs`

### Что делать:

1. Создать класс `StateProjector` с методом:
   ```
   public ProjectedState Project(ProjectedState current, ChainStep step, ObstacleInfo target)
   ```

2. Для каждого типа действия реализовать проекцию:

   | Действие | Проекция |
   |---|---|
   | `SwitchLane` | `OnBottom = !OnBottom`, X += LaneSwitchTravel, Energy без изменений |
   | `Jump` | X += JumpLandingTravel, Energy -= 10, если Target → TargetsDestroyed++ |
   | `SuperJump` | X += SuperJumpLandingTravel, Energy -= 20 |
   | `RoofJump` | рассчитать: прыжок на соседнюю крышу ИЛИ прыжок вниз, Energy -= 10 |
   | `SuperRoofJump` | X += SuperJumpLandingTravel, Energy -= 20 |
   | `UseUlta` | UltaCharge = 0, удалить уничтоженные Threats из RemainingObjects |

3. **Критически важно** — проекция автоматических переходов:

   **a) RunFromRoof (спуск с крыши):**
   - После запрыгивания на крышу (bigNotAlive/mediumNotAlive) — проверить, где крыша заканчивается
   - Если projected-X > правый край крыши → хомяк автоматически спускается
   - Зона уязвимости при спуске: ~1.9 юнитов (рассчитать из длительности анимации `transform_run_from_roof`)
   - Если в зоне спуска стоит Threat → шаг небезопасен

   **b) Автопереход между смежными крышами:**
   - Если следующий bigNotAlive/mediumNotAlive стоит вплотную (gap < порог) → хомяк переходит автоматически
   - Бесплатно и безопасно

   **c) Отскок после JumpOn (SmallAlive):**
   - Расстояние отскока = `JumpOnBounceTravel` (3.5 юнитов)
   - Хомяк неуязвим во время отскока
   - Projected X = target.RightX + JumpOnBounceTravel
   - Проверить, что в зоне ПОСЛЕ отскока (приземление) безопасно

4. Реализовать метод `IsSafeAfterProjection(ProjectedState state)`:
   - Проверяет, нет ли Threat на пути хомяка в projected-состоянии
   - Учитывает иммунные зоны (отскок, RunFromRoof)

### Константы для проекции (из runtime-анализа):

| Параметр | Значение | Источник |
|---|---|---|
| GameSpeedBase | 3.8 m/s | Consts.cs |
| JumpLandingTravel | 3.8 юнитов | ~1с прыжка * 3.8 m/s |
| SuperJumpLandingTravel | 4.6 юнитов | ~1.2с * 3.8 m/s |
| JumpOnBounceTravel | 3.5 юнитов | (1.817 - 0.85) * 3.8 |
| LaneSwitchTravel | 1.14 юнитов | 0.3с * 3.8 m/s |
| RunFromRoofVulnerability | ~1.9 юнитов | ~0.5с * 3.8 m/s (уточнить по анимации) |

**Результат этапа:** есть механизм проекции, который позволяет строить цепочки шаг за шагом с пониманием последствий.

---

## Этап 5. ChainGenerator — генерация кандидатных цепочек

**Цель:** полностью переписать алгоритм построения цепочки по архитектуре: итеративная генерация с ветвлениями и отсечением.

**Путь:** `Assets/Scripts/Bot/Pipeline/ChainGenerator.cs`

### Что делать:

1. Создать класс с методом:
   ```
   public List<ChainCandidate> Generate(ObstacleInfo[] classified, ProjectedState initial, int maxDepth = 5)
   ```

2. Реализовать **рекурсивный/итеративный** алгоритм из архитектуры (раздел 8):

   ```
   function Generate(state, depth):
       if depth == 0 or нет объектов впереди:
           return [текущая цепочка]
       
       nextObject = ближайший Threat или Target на пути в state
       if nextObject == null:
           return [текущая цепочка]  // дорога чистая
       
       variants = GenerateVariants(state, nextObject)
       
       candidates = []
       for each variant in variants:
           newState = StateProjector.Project(state, variant.step, nextObject)
           if IsSafe(newState):
               subcandidates = Generate(newState, depth - 1)
               for each sub in subcandidates:
                   candidates.add(variant.step + sub)
       
       return candidates
   ```

3. **GenerateVariants** — для каждого объекта на пути генерировать все допустимые варианты:

   | Тип объекта | Варианты |
   |---|---|
   | `smallAlive` (Target) | SwitchLane, Jump (JumpOver), Jump (JumpOn), SuperJump |
   | `smallAlive` (Threat, если путь к JumpOn неясен) | SwitchLane, Jump (JumpOver), SuperJump |
   | `bigAlive` (Threat с дороги) | SwitchLane, SuperJump |
   | `bigAlive` (Target с крыши) | Jump/RoofJump (атака с крыши), SwitchLane |
   | `bigNotAlive` / `mediumNotAlive` | SwitchLane, Jump (на крышу, если с дороги) |
   | `smallNotAliveRoad` | SwitchLane, Jump, SuperJump |
   | `smallNotAliveRoadAndRoof` | SwitchLane, Jump, SuperJump |

   Каждый вариант фильтруется:
   - Хватает ли энергии?
   - Допустимо ли действие из текущего пространства? (SuperJump с крыши = SuperRoofJump)
   - Другая линия безопасна (для SwitchLane)?

4. **Отсечение (pruning):**
   - Если шаг ведёт к потере жизни → отбросить всю ветку
   - Если зона приземления небезопасна → отбросить
   - Если автоматический переход (RunFromRoof) в опасную зону → отбросить
   - Ограничение глубины: maxDepth = 5 (обычно 2–4 шага)
   - Ограничение количества кандидатов: maxCandidates = 50 (предотвращение комбинаторного взрыва)

5. **Collectible-шаги:**
   - Если на пути нет Threat/Target, но есть Collectible на другой линии → генерировать SwitchLane
   - Приоритеты: жизнь(4) > энергия(3) > кристалл(2) > монета(1)
   - Только если текущая линия безопасна

6. **Ульта как вариант:**
   - Если UltaCharge >= 100 и впереди кластер из 2+ Threats → добавить UseUlta как вариант
   - После UseUlta: удалить уничтоженные объекты из RemainingObjects → продолжить генерацию

### Результат этапа:
- `BotChainPlanner.BuildChain()` полностью заменён
- Генератор возвращает `List<ChainCandidate>` — множество вариантов
- Каждый вариант — безопасная цепочка из 1–5 шагов с projected outcome

---

## Этап 6. ChainScorer — оценка кандидатов

**Цель:** формализовать оценку и ранжирование кандидатных цепочек.

**Путь:** `Assets/Scripts/Bot/Pipeline/ChainScorer.cs`

### Что делать:

1. Создать класс с методом:
   ```
   public void Score(List<ChainCandidate> candidates)
   ```

2. Формула оценки (из архитектуры, раздел 9):

   **Три критерия в порядке приоритета:**

   a) **Безопасность** (бинарный):
   - `AllStepsSafe == true` → проходит
   - `AllStepsSafe == false` → отбрасывается (страховка, обычно уже отсечены в генераторе)

   b) **Стоимость** (меньше = лучше):
   - `TotalEnergyCost` — суммарная трата энергии
   - Нормализация: `costScore = 1.0 - (TotalEnergyCost / maxPossibleCost)`

   c) **Выгода** (больше = лучше):
   - `TargetsDestroyed * targetWeight + CollectiblesGathered * collectibleWeight`
   - targetWeight = 10 (уничтожение цели + монеты + заряд ульты)
   - collectibleWeight = зависит от типа (жизнь=8, энергия=5, кристалл=3, монета=1)

3. Итоговый Score:
   ```
   Score = safetyBonus + (1 - normalizedCost) * costWeight + benefitScore * benefitWeight
   ```
   Где safetyBonus > costWeight + benefitWeight (безопасность всегда главнее)

4. Сортировать кандидатов по убыванию Score.

**Результат этапа:** все кандидаты получают формализованную оценку и отсортированы.

---

## Этап 7. PlanValidator — валидация текущего плана

**Цель:** реализовать механизм keep tail + extend.

**Путь:** `Assets/Scripts/Bot/Pipeline/PlanValidator.cs`

### Что делать:

1. Создать класс с методом:
   ```
   public PlanDecision Validate(BotSceneSnapshot snapshot, CurrentPlan currentPlan)
   ```
   
   ```
   public enum PlanDecision { KeepTail, FullRebuild }
   ```

2. Правила валидации хвоста:

   a) **Объекты на месте?**
   - Для каждого шага хвоста: проверить, что `StableId` цели всё ещё есть в `snapshot.VisibleObjects`
   - Если хотя бы один объект исчез → `FullRebuild`

   b) **Путь до ближайшего шага безопасен?**
   - Проверить, что между текущей позицией хомяка и первым шагом хвоста нет новых Threats
   - Если новая Threat на пути → `FullRebuild`

   c) **Нет более приоритетной цели?**
   - Если появилась новая Target, ближе и выгоднее текущего плана → `FullRebuild`
   - Сравнивать: новая Target.Distance < ближайший шаг плана.Distance И новая Target достижима

   d) **Хвост всё ещё безопасен?**
   - Перепроецировать хвост с текущего состояния через StateProjector
   - Если какой-то шаг больше не безопасен (новый объект на пути) → `FullRebuild`

3. Если все проверки пройдены → `KeepTail`.

**Результат этапа:** бот не перестраивает план без причины, что обеспечивает стабильное и последовательное поведение.

---

## Этап 8. PlanSelector — выбор плана и keep tail + extend

**Цель:** объединить результаты ChainGenerator с текущим планом.

**Путь:** `Assets/Scripts/Bot/Pipeline/PlanSelector.cs`

### Что делать:

1. Создать класс с методом:
   ```
   public CurrentPlan Select(
       PlanDecision decision, 
       CurrentPlan currentPlan, 
       List<ChainCandidate> candidates)
   ```

2. **Режим KeepTail:**
   - Взять хвост текущего плана (все шаги кроме completed)
   - `ChainGenerator` вызывается с `ProjectedState` из **конца хвоста** (не из snapshot)
   - Достроенные шаги добавляются в хвост
   - Результат: объединённый план = хвост + новые шаги

3. **Режим FullRebuild:**
   - Выбрать лучшего кандидата (первый по Score после сортировки)
   - Создать новый `CurrentPlan` из выбранного кандидата
   - Записать strategy = `"rebuild: {причина}"`

4. **Если кандидатов нет:**
   - План пустой
   - Логировать через QA-логирование (аналог текущего `LogNoSafePath`)

**Результат этапа:** план обновляется корректно, с сохранением валидного хвоста.

---

## Этап 9. BotTimingPolicy — точное исполнение шагов

**Цель:** выделить логику тайминга и исполнения из HamsterBot в отдельный компонент.

**Путь:** `Assets/Scripts/Bot/Pipeline/BotTimingPolicy.cs`

### Что делать:

1. Создать класс с методом:
   ```
   public bool TryExecuteHead(CurrentPlan plan, Hamster hamster)
   ```
   Возвращает `true`, если шаг выполнен.

2. Перенести из `HamsterBot`:
   - Проверку `step.ExecuteAtDistance` — объект достаточно близко?
   - `ShouldDelayJumpOver()` — CollisionUtils.IsOverlapAtShift
   - `ShouldDelayJumpOn()` — CollisionUtils.IsHamsterCenterInsideObstacleAtShift
   - `EnsureWorldShiftsCached()` — кэширование worldShift из TransformAnimatorController
   - `ExecuteAction()` — отправка реальных игровых событий (JumpRequest, TapRequest, etc.)

3. **Жизненный цикл шага:**
   - `Ready` → проверяем условия → `InProgress` (действие отправлено)
   - `InProgress` → ждём завершения (хомяк вернулся в Run/RoofRun) → `Completed`
   - `Completed` → удаляется из головы плана в начале следующего пересчёта

4. Перевод шага из Ready → InProgress → Completed внутри BotTimingPolicy.

**Результат этапа:** вся логика тайминга изолирована. HamsterBot только вызывает `BotTimingPolicy.TryExecuteHead()`.

---

## Этап 10. HamsterBot — рефакторинг оркестратора

**Цель:** привести HamsterBot к целевой роли чистого оркестратора pipeline.

### Что делать:

1. **Удалить** из HamsterBot:
   - Всю логику ShouldDelay*, EnsureWorldShiftsCached → перенесена в BotTimingPolicy
   - Прямые вызовы _planner.ScanObstacles / _planner.BuildChain

2. **Заменить** Update-логику на целевой pipeline:

   ```
   void Update()
   {
       // 1. Проверки: игра запущена? хомяк в управляемом состоянии?
       // 2. Удалить completed-шаги из головы CurrentPlan
       // 3. Проверить триггеры пересчёта
       if (!NeedsReplan()) return;
       
       // 4. Pipeline:
       var snapshot = _snapshotBuilder.Build(_hamster, _scanRange);
       _objectClassifier.Classify(snapshot);
       var decision = _planValidator.Validate(snapshot, _currentPlan);
       
       List<ChainCandidate> candidates;
       if (decision == PlanDecision.KeepTail)
       {
           var tailState = GetTailProjectedState();
           candidates = _chainGenerator.Generate(snapshot.VisibleObjects, tailState);
       }
       else
       {
           var initialState = ProjectedState.FromSnapshot(snapshot);
           candidates = _chainGenerator.Generate(snapshot.VisibleObjects, initialState);
       }
       
       _chainScorer.Score(candidates);
       _currentPlan = _planSelector.Select(decision, _currentPlan, candidates);
       
       // 5. Исполнение
       _timingPolicy.TryExecuteHead(_currentPlan, _hamster);
   }
   ```

3. **Триггеры пересчёта** (два, как в архитектуре):
   
   a) **Завершился шаг:** Head.Status == Completed И хомяк в Run/RoofRun
   
   b) **Изменился состав объектов:** сравнение текущего набора StableId с предыдущим
   
   Заменить текущий dirty flag на event-driven подход:
   ```
   private HashSet<int> _prevObjectIds = new();
   
   bool ObjectSetChanged(BotSceneSnapshot snapshot)
   {
       var currentIds = snapshot.VisibleObjects.Select(o => o.StableId).ToHashSet();
       if (_prevObjectIds.SetEquals(currentIds)) return false;
       _prevObjectIds = currentIds;
       return true;
   }
   ```

4. **Сохранить:**
   - Singleton, DontDestroyOnLoad, OnSceneLoaded
   - Auto-restart on death
   - F1 toggle
   - Логирование через DebugManager.DiagLog

**Результат этапа:** HamsterBot — чистый оркестратор. Вся логика распределена по pipeline-компонентам.

---

## Этап 11. Тестирование и отладка

**Цель:** убедиться, что бот работает корректно с новой архитектурой.

### 11.1. Unit тесты для StateProjector

Проверить проекции каждого типа действия:
- SwitchLane: линия меняется, энергия не тратится
- Jump: X сдвигается на JumpLandingTravel, 10 энергии
- JumpOn SmallAlive: X = target.RightX + JumpOnBounceTravel, 10 энергии
- SuperJump на BigAlive: X += SuperJumpLandingTravel, 20 энергии
- Jump на BigNotAlive (на крышу): OnRoof = true, 10 энергии
- RunFromRoof: проверка зоны уязвимости
- Автопереход между крышами: OnRoof остаётся true

### 11.2. Unit тесты для ChainGenerator

- Пустая сцена → пустая цепочка
- Один SmallAlive впереди → минимум 2 варианта (SwitchLane, JumpOn)
- BigAlive на обеих линиях → SuperJump или Ulta
- SmallAlive за которым стоит BigAlive → JumpOn отбрасывается (отскок в BigAlive)
- BigNotAlive → вариант с крышей, проверка RunFromRoof

### 11.3. Unit тесты для PlanValidator

- Объект исчез → FullRebuild
- Новая Threat перед первым шагом → FullRebuild
- Ничего не изменилось → KeepTail

### 11.4. Интеграционный тест

- Запуск бота на реальном уровне
- Проверка из DiagLog: план строится, шаги выполняются, keep tail работает
- Сравнение: было N смертей со старым ботом → стало M со новым

### 11.5. QA-логирование

Расширить `LogNoSafePath`:
- Записывать все отброшенные кандидаты с причинами
- Записывать PlanDecision (KeepTail / FullRebuild) и причину
- Записывать score лучших 3 кандидатов

---

## Этап 12. Полировка и оптимизация

### 12.1. Ограничение генерации

- maxDepth = 5
- maxCandidates = 50 (обрезать наименее перспективные ветки)
- maxBranchingPerObject = 4 (не больше 4 вариантов для одного объекта)
- Профилирование: pipeline должен укладываться в < 1мс

### 12.2. Cleanup старого кода

- Удалить `BotChainPlanner.cs` (вся логика перенесена в pipeline)
- Удалить неиспользуемые методы из HamsterBot
- Привести все pipeline-компоненты к единому стилю

### 12.3. Расширенное QA-логирование

Добавить форматированный вывод в DiagLog:
```
[Bot#42] REPLAN: FullRebuild (new threat appeared)
  Candidates: 5 generated, 3 safe, best score=0.87
  Selected: [SwitchLane→Jump→Run] cost=10 benefit=1target
  Plan: [1]SwitchLane@d=1.5 [2]Jump(JumpOn SmallAlive)@d=3.2
```

---

## 3. Файловая структура после реализации

```
Assets/Scripts/Bot/
├── HamsterBot.cs              (рефакторинг — чистый оркестратор)
├── HamsterBotUI.cs            (без изменений)
├── BotAction.cs               (без изменений)
├── ObstacleInfo.cs            (модификация: +StableId)
├── ChainStep.cs               (модификация: struct→class, +Status, +TargetObstacle)
├── Data/
│   ├── BotSceneSnapshot.cs    (новый)
│   ├── ProjectedState.cs      (новый)
│   ├── ChainCandidate.cs      (новый)
│   └── CurrentPlan.cs         (новый)
├── Pipeline/
│   ├── SnapshotBuilder.cs     (новый)
│   ├── ObjectClassifier.cs    (новый)
│   ├── StateProjector.cs      (новый)
│   ├── ChainGenerator.cs      (новый)
│   ├── ChainScorer.cs         (новый)
│   ├── PlanValidator.cs       (новый)
│   ├── PlanSelector.cs        (новый)
│   └── BotTimingPolicy.cs     (новый)
└── [удалён] BotChainPlanner.cs
```

**Итого:**
- Новых файлов: 12
- Модифицированных файлов: 3 (HamsterBot, ObstacleInfo, ChainStep)
- Удалённых файлов: 1 (BotChainPlanner)
- Без изменений: 2 (HamsterBotUI, BotAction)

---

## 4. Зависимости между этапами

```
Этап 1 (сущности данных)
  ↓
Этап 2 (SnapshotBuilder) ─────────────────────────┐
  ↓                                                │
Этап 3 (ObjectClassifier) ←───────────────────────┤
  ↓                                                │
Этап 4 (StateProjector) ←─── нужен для 5,7,8 ────┤
  ↓                                                │
Этап 5 (ChainGenerator) ←───── зависит от 4 ──────┤
  ↓                                                │
Этап 6 (ChainScorer)                               │
  ↓                                                │
Этап 7 (PlanValidator) ←──── зависит от 4 ─────────┤
  ↓                                                │
Этап 8 (PlanSelector) ←──── зависит от 6,7 ────────┘
  ↓
Этап 9 (BotTimingPolicy) ←── перенос из HamsterBot
  ↓
Этап 10 (HamsterBot рефакторинг) ←── всё готово
  ↓
Этап 11 (тестирование)
  ↓
Этап 12 (полировка)
```

---

## 5. Критерии готовности

### Минимально жизнеспособный результат (после этапа 10):
- Бот строит цепочки из 2–5 шагов с перебором вариантов
- Projected State корректно проецирует последствия каждого шага
- PlanValidator обеспечивает keep tail + extend
- Два runtime-триггера вместо dirty flag
- Все pipeline-компоненты изолированы (SRP)
- Бот не деградирует относительно текущей версии

### Полный результат (после этапа 12):
- Unit тесты для StateProjector, ChainGenerator, PlanValidator
- QA-логирование с подробным выводом кандидатов и причин решений
- Pipeline укладывается в < 1мс
- Старый BotChainPlanner удалён
- Документация обновлена (architecture_knowledge_base.md)

---

## 6. Риски и митигация

| Риск | Вероятность | Митигация |
|---|---|---|
| Комбинаторный взрыв в ChainGenerator | Средняя | Жёсткие лимиты: maxDepth=5, maxCandidates=50, pruning |
| Regression: бот стал хуже после рефакторинга | Средняя | Инкрементальный подход: каждый этап сохраняет работоспособность |
| Неточность проекции RunFromRoof | Высокая | Взять точные значения длительности анимации из клипов; добавить safety margin |
| Тайминг: pipeline не успевает за кадр | Низкая | Профилирование, кэширование, ранний выход при достаточном количестве кандидатов |
| Сложность отладки ветвлений | Средняя | Подробное QA-логирование (этап 12.3), визуализация плана в HamsterBotUI |

---

## 7. Что НЕ входит в это ТЗ

- **Офлайн-солвер** (раздел 15 концепта) — отдельная задача на будущее
- **Визуализация плана** в Game View (рисование линий/маркеров) — можно добавить позже
- **Адаптация к конкретным скинам** ульты — бот знает только "ульта уничтожает угрозы"
- **Обучение / ML** — бот остаётся rule-based
- **Изменения в runtime-механиках** (JumpMechanics, CollisionUtils и т.д.) — только чтение
