# Bot runtime performance optimization backlog

Дата: 2026-05-30

Контекст: при прогоне тестовых уровней в Unity Editor Play Mode были видны периодические статтеры. Диагностика показала, что основной пик приходится на bot replanning: в тяжёлые секунды `PlanBuilder.Build()` занимает десятки миллисекунд, а `GC Allocated In Frame` часто превышает 1 MB/frame.

Уже сделано в текущей итерации:

- Закеширован `TransformAnimatorController` и travel distance по animation clip name для planning policies.
- В `SnapshotBuilder` закешированы `Camera.main`, collider хомяка и collider obstacle по instance id.

## 1. Ограничить частоту полного replanning

Сейчас `RuntimeBotController.Update()` каждый кадр строит snapshot и вызывает полный `PlanBuilder.Build(...)`. Это главный источник периодических CPU/GC пиков.

Предложение:

- Оставить лёгкую валидацию текущего head action каждый кадр.
- Полный replanning запускать только когда текущий план пуст, стал невалиден, завершилось действие, появился новый required decision point, либо по throttle 5-10 Hz.
- При action in progress не раскрывать новое дерево без явного invalidation-сигнала.

Ожидаемый эффект: убрать серии из десятков тяжёлых кадров подряд, сохранив реактивность бота.

## 2. Снизить allocations внутри planner

Горячие места:

- `PlanningGraphBuilder.BuildBranches(...)` создаёт `List<PlanningBranch>`, `Dictionary<PlanningStateKey, PlanningBranchMetrics>`, `PlanningGraphNode`.
- `PlanningSnapshotProjector.Project(...)` создаёт новый `List<ObstacleSnapshot>` и новый `WorldSnapshot` на каждом projected state.
- `PlanningBranch.FromLeaf(...)` создаёт и разворачивает список actions.
- `PlanningBranchMetrics.Append(...)` создаёт новый metrics object на каждое ребро графа.

Предложение:

- Заменить projected snapshot copy на lightweight projected view с `ProjectionWorldShift`.
- Переиспользовать временные коллекции через pool или per-planner scratch buffers.
- Рассмотреть `struct`/value-модели для маленьких immutable metrics, если это не ухудшит читаемость.
- Не создавать `BotPlan.Empty()` в часто вызываемых свойствах без необходимости.

Ожидаемый эффект: уменьшить `GC Allocated In Frame` в planner spikes.

## 3. Ограничить branching/depth для optional objectives

Текущий `PlanningGraphBuilder` раскрывает дерево до `MaxSearchDepth = 6`. На roof/from-roof цепочках и optional jump-on objectives это даёт резкий рост вариантов.

Предложение:

- Для committed prefix сначала revalidate текущую цепочку, а не пересобирать все альтернативы.
- Optional objectives анализировать с отдельным budget: например не раскрывать optional ветку, если required threat уже имеет валидное дешёвое решение.
- Ввести лимиты на число candidates/branches на один frame и fallback на лучший найденный план.

Ожидаемый эффект: снизить worst-case `planBuildMs`.

## 4. Убрать LINQ allocations из `ObstacleSpawner`

В runtime path есть LINQ:

- `IsPreviousPatternFullyOnScreen()` использует `Where(...).ToList()`, `Any()`, `Max()`.
- `SpawnPattern()` использует `Where(...).ToList()`, `Any()`, `Min()`.

Предложение:

- Заменить на обычные `for` loops без промежуточных списков.
- При загрузке уровня сгруппировать instantiated obstacles по `PatternIndex`, чтобы `SpawnPattern()` не фильтровал весь список.

Это не главный источник найденного статтера, но это дешёвая оптимизация runtime allocations.

## 5. Отделить долгую perf-диагностику от synchronous file logging

Текущие диагностические spike-строки пишутся через `DebugManager.DiagStability(...)`, который синхронно вызывает `File.AppendAllText(...)`.

Предложение:

- Оставить `ProfilerRecorder`/summary за feature flag.
- Для долгих perf-прогонов писать агрегаты раз в секунду или буферизовать строки и flush делать вне hot path.
- Детальные spike-строки включать только для короткой диагностики.

Ожидаемый эффект: диагностика не будет усиливать измеряемый статтер.
