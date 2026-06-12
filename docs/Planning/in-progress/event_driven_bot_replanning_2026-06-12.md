# Event-driven bot replanning — 2026-06-12

## Статус

- Тип: план реализации и tracking выполнения.
- Ветка: `integration/unity-live`.
- Цель: заменить rolling replanning по таймеру на event-driven replanning для детерминированного runner-мира.
- Реализация выполнена в рамках этого документа.

## Целевая идея

Бот не должен пересобирать planning tree каждые `0.5s`. В детерминированном мире препятствия сдвигаются предсказуемо, поэтому replanning нужен только когда меняется входная картина или lifecycle исполнения:

1. `LevelStart` — первичный plan после старта gameplay.
2. `BotEnabled` — bot включили после выключения или при старте enabled-состояния.
3. `SpawnPattern` — `ObstacleSpawner` добавил новый pattern в `SpawnedObstacles`.
4. `ActionCompleted` / `ActionCancelled` — executor завершил или отменил текущую head-action.

Committed prefix удерживает ближайшие два действия: текущую head-action и следующий action для immediate handoff. Хвост после них свободно пересчитывается. Коммитить более длинный prefix и вводить soft-commit/revalidation сейчас не нужно.

## Исходные факты по коду перед реализацией

- `RuntimeBotController` тикает через `IGameLateUpdateListener.OnLateUpdate()` и вызывается из `GameManager.LateUpdate()`.
- `ObstacleSpawner` тикает через `IGameUpdateListener.OnUpdate()` и вызывается из `GameManager.Update()` до `LateUpdate()`. Поэтому spawn-event, выставленный в `Update`, может быть обработан ботом в том же кадре в `LateUpdate`.
- `RuntimeBotController.TickBot()` сейчас каждый tick:
  - строит `LastSnapshot`;
  - вызывает `_executor.Tick(_hamster)`;
  - при любом execution-result переснимает snapshot;
  - вызывает `ShouldRebuildPlan()`;
  - `ShouldRebuildPlan()` сейчас завязан только на `_nextRollingReplanTime`.
- В `RuntimeBotController` есть `_rollingReplanInterval = 0.5f` и `_nextRollingReplanTime`; это основная точка удаления timer-based replanning.
- `BuildPlanForCurrentExecutionState()` уже умеет:
  - строить plan с нуля, если текущего плана нет;
  - сохранять текущую head-action, если она уже in-progress;
  - сохранять следующий pending action, если trigger ещё reachable;
  - строить хвост от projected state после committed prefix.
- `PlanExecutor.Tick()` исполняет только head-action, но после completion уже делает immediate handoff: `AdvanceHead()` и сразу `TryFireCurrentHead()`.
- Текущий `PlanExecutionTickResult` — enum `None/Fired/Completed/Cancelled`. Он не может выразить факт "предыдущая action completed, следующая action fired в тот же tick". Для event-driven replanning это важно, потому что completion должен запросить replan хвоста даже если новая head-action уже fired.
- `SnapshotBuilder.CollectObstacles()` сейчас фильтрует obstacles по `screenLeftEdgeX` и `visionRightEdgeX`. Это не равно "все spawned obstacles".
- `ObstacleSpawner.SpawnPattern()` добавляет obstacles в `_spawnedObstacles`; `UnspawnObstacle()` удаляет их из списка при out-of-bounds или jump-on unspawn.
- Реальная скорость движения static obstacle: `ScrollLeftMechanics.Update()` использует `Consts.RoadScrollSpeed * 3.8f`; сейчас `RoadScrollSpeed = 1f`, `GameSpeedBase = 3.8f`.
- `WorldSnapshot.VisionRightEdgeX` был artificial horizon для старой модели видимости. В реализации поле удалено, retention не должен опираться на него.

## Non-goals

- Не менять scoring, `PlanEvaluator`, веса energy/tap/progression.
- Не менять `PlanningGraphBuilder.MaxSearchDepth = 6`.
- Не добавлять committed prefix длиннее двух actions.
- Не добавлять soft-commit/revalidation для tail-actions.
- Не переводить planner на будущие unspawned level-data; horizon остаётся равен всем active spawned obstacles.
- Не решать отдельно animated/moving obstacle cases; они будут отдельным этапом.
- Не переписывать стратегические классы action families.

## Архитектурное решение

### Replan request вместо таймера

В `RuntimeBotController` заменить timer-check на явный request-флаг:

```csharp
private bool _isReplanRequested;
private BotReplanReason _pendingReplanReasons;
```

`RequestReplan(reason)` только помечает необходимость пересборки. Сам rebuild должен оставаться в `TickBot()` после execution tick и после актуального snapshot, чтобы planning не запускался из чужого lifecycle callback.

Рекомендуемый enum:

```csharp
[Flags]
private enum BotReplanReason
{
    None = 0,
    LevelStart = 1,
    BotEnabled = 2,
    SpawnPattern = 4,
    ActionCompleted = 8,
    ActionCancelled = 16
}
```

### Execution result с несколькими фактами

Заменить `PlanExecutionTickResult` enum на flags enum:

```csharp
[Flags]
public enum PlanExecutionTickResult
{
    None = 0,
    Fired = 1,
    Completed = 2,
    Cancelled = 4
}
```

Тогда `PlanExecutor.Tick()` после completion может вернуть `Completed | Fired`, если следующий head-action стартовал в тот же tick. Это сохраняет immediate handoff и при этом даёт controller-у основание пересобрать хвост после новой in-progress head.

### Spawn event

В `ObstacleSpawner` добавить code-level event:

```csharp
public event Action<int, string> PatternSpawned;
```

Событие вызывать в конце `SpawnPattern(int patternIndex)` после того, как все obstacles:

- получили runtime position;
- перенесены в spawned container;
- прошли `InitializeMechanics()`;
- добавлены в `_spawnedObstacles`.

Если pattern не содержит obstacles и `SpawnPattern()` делает early return, replan не нужен.

`RuntimeBotController` должен подписываться на текущий `ObstacleSpawner.Instance` после resolve scene dependencies и отписываться при destroy или смене spawner instance.

### Snapshot всех spawned obstacles

`SnapshotBuilder` должен строить snapshot по всему `ObstacleSpawner.SpawnedObstacles`, без фильтра по `screenLeftEdgeX`/`visionRightEdgeX`.

Screen bounds всё ещё нужны:

- `ScreenLeftEdgeX`;
- `ScreenRightEdgeX`;
- возможно retention pending head.

Artificial `VisionRightEdgeX` больше не нужен как horizon. Поле должно быть удалено из `WorldSnapshot`, а все call sites должны перейти на `ScreenRightEdgeX` или на явную работу со всеми `SpawnedObstacles`.

### Committed prefix: head + next action

Оставить текущую идею `BuildPlanForCurrentExecutionState()`:

- если action in-progress: project current head;
- если следующий pending action trigger reachable: simulate pending action;
- rebuild tail от состояния после committed prefix;
- иначе rebuild full plan from live snapshot.

Не сохранять третий и последующие actions как committed. Tail всегда является новым best branch от состояния после committed prefix.

## План реализации

### Шаг 1. Подготовить execution result для event-driven flow

Файлы:

- `LostCyberHamster/Assets/Scripts/Bot/Execution/PlanExecutor.cs`
- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

Действия:

1. Сделать `PlanExecutionTickResult` flags enum.
2. Обновить `PlanExecutor.Tick()`:
   - no plan -> `None`;
   - first fire -> `Fired`;
   - completed head without immediate next fire -> `Completed`;
   - completed head and immediate next fire -> `Completed | Fired`;
   - cancelled head -> `Cancelled`.
3. Обновить проверки в `RuntimeBotController`:
   - `HasFlag(Fired)` или bitwise helper для fire-point update;
   - `HasFlag(Completed)` / `HasFlag(Cancelled)` для cleanup и replan request;
   - `result != None` для повторного snapshot.

Критерий готовности:

- Immediate handoff сохраняется.
- Controller видит completion даже при `Completed | Fired`.

### Шаг 2. Заменить rolling timer на request-based replanning

Файл:

- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

Действия:

1. Удалить `_rollingReplanInterval` и `_nextRollingReplanTime`.
2. Добавить replan request state: `_isReplanRequested`, `_pendingReplanReasons`.
3. Добавить `RequestReplan(BotReplanReason reason)`.
4. `ShouldRebuildPlan()` заменить на проверку `_isReplanRequested`.
5. `RebuildPlanFromCurrentSnapshot()` должен consume request:
   - сохранить reasons в локальную переменную;
   - сбросить `_isReplanRequested`;
   - построить plan;
   - если plan equivalent, не менять executor;
   - если plan не equivalent, `SetPlan(plan)` и логировать activation вместе с reason.
6. В `TickBot()` вызывать `RequestReplan(ActionCompleted)` / `RequestReplan(ActionCancelled)` после executor tick и перед `ShouldRebuildPlan()`.
7. Если `CurrentPlan` пуст и это первый playable tick после resolve dependencies, должен быть request `LevelStart` или `BotEnabled`, а не implicit rebuild каждый кадр.

Критерий готовности:

- В обычных кадрах без request план не пересобирается.
- После completion/cancel пересборка происходит в том же `LateUpdate`.

### Шаг 3. Добавить LevelStart и BotEnabled triggers

Файл:

- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

Действия:

1. Добавить `Listeners.IGameStartListener` к `RuntimeBotController`.
2. Реализовать `OnStart()` как request-only callback: `RequestReplan(LevelStart)`.
3. В `Enable()`:
   - включить controller;
   - запросить `BotEnabled`;
   - если dependencies ещё не найдены, request должен сохраниться до первого ready tick.
4. В `Disable()`:
   - сбросить `LastSnapshot`;
   - очистить executor;
   - очистить in-progress fire point;
   - очистить pending replan request, чтобы выключенный бот не тащил старое событие.
5. Защититься от случая, когда controller зарегистрировался после `StartGame()`:
   - при первом ready tick, если plan пуст и ещё не было initial request, запросить `LevelStart`.
   - не превращать это в rebuild every frame после пустого результата; нужен одноразовый флаг, например `_initialReplanRequestedForCurrentGame`.

Критерий готовности:

- При старте уровня первый plan строится без timer.
- При F1 OFF/ON plan строится заново без timer.

### Шаг 4. Добавить SpawnPattern trigger

Файлы:

- `LostCyberHamster/Assets/Scripts/System/ObstacleSpawner.cs`
- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

Действия:

1. В `ObstacleSpawner` добавить event `PatternSpawned`.
2. В конце успешного `SpawnPattern()` вызвать event с `patternIndex` и pattern name.
3. В `RuntimeBotController` добавить поле текущей подписки на spawner, например `_subscribedObstacleSpawner`.
4. При resolve dependencies подписываться на `ObstacleSpawner.Instance`, если он есть.
5. При смене spawner instance или `OnDestroy()` отписываться.
6. Handler `OnPatternSpawned(int patternIndex, string patternName)` должен только вызвать `RequestReplan(SpawnPattern)` и, опционально, verbose-log с pattern info.

Критерий готовности:

- Новый pattern, появившийся в `GameManager.Update()`, вызывает replan в ближайшем `RuntimeBotController.OnLateUpdate()`.
- Нет прямого вызова planner из `ObstacleSpawner`.

### Шаг 5. Сделать snapshot по всем spawned obstacles

Файлы:

- `LostCyberHamster/Assets/Scripts/Bot/Perception/SnapshotBuilder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Perception/WorldSnapshot.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningSnapshotProjector.cs`
- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

Действия:

1. В `SnapshotBuilder.Build()` оставить расчёт `screenLeftEdgeX` и `screenRightEdgeX`.
2. Удалить `_extraVisionScreenFraction` и `visionRightEdgeX`.
3. Из `CollectObstacles()` убрать параметры границ и фильтр:
   - не отбрасывать `bounds.max.x < screenLeftEdgeX`;
   - не отбрасывать `bounds.min.x > visionRightEdgeX`.
4. `CollectObstacles()` должен по-прежнему:
   - читать только `ObstacleSpawner.SpawnedObstacles`;
   - пропускать null obstacle/collider;
   - сортировать snapshot по `LeftX`.
5. Удалить `VisionRightEdgeX` из `WorldSnapshot` и `PlanningSnapshotProjector`.
6. В `RuntimeBotController.IsActionInExecutionRegion()` использовать `ScreenRightEdgeX` для всех action kinds.

Критерий готовности:

- Все active spawned obstacles попадают в `WorldSnapshot.Obstacles`.
- Старый artificial vision horizon не участвует в planning или head retention.

### Шаг 6. Сохранить bounded committed prefix

Файл:

- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

Действия:

1. Не добавлять committed prefix длиннее двух actions.
2. Сохранить `BuildTailRootState()` как единственную точку решения:
   - in-progress head -> `ProjectInProgress`;
   - pending committed action with reachable trigger -> `Simulate`;
   - иначе full rebuild.
3. Проверить, что `SetPlan(plan)` сохраняет `_isActionInProgress`, если новая head equivalent старой.
4. Не добавлять revalidation layer для tail.

Критерий готовности:

- На spawn-event пересчитывается хвост после текущего committed prefix.
- Если pending committed action уже не reachable, новый plan строится от live snapshot.

### Шаг 7. Диагностика

Файл:

- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

Действия:

1. Расширить plan activation log:
   - `reason=LevelStart|SpawnPattern|ActionCompleted...`;
   - `actions=...`;
   - `score=...`;
   - `chain=...`.
2. Не добавлять шумный log на каждый frame.
3. Для equivalent plan можно оставить без log или писать только verbose.

Критерий готовности:

- По BOT/diagnostic log видно, почему случился rebuild.
- В отсутствие событий нет повторяющегося plan-log каждые `0.5s`.

### Шаг 8. Документация после реализации

Файлы:

- `docs/architecture_knowledge_base.md`
- этот plan-файл

Действия:

1. Обновить `Bot Domain Knowledge`:
   - planner event-driven;
   - timer removed;
   - spawned obstacles horizon.
2. Обновить `Bot Architecture Pipeline` triggers.
3. В этом plan-файле отмечать завершённые шаги или добавить секцию `Implementation notes`.

## Проверка реализации

### Локальные проверки кода

- После изменения `.cs` проверить `Assembly-CSharp.csproj`, если создавались/удалялись `.cs` файлы. План не требует новых `.cs` файлов.
- Проверить XML summary и комментарии только в затронутых `.cs`.
- Recompile/Unity validation запускать по явному запросу пользователя согласно project rules.

### Ручная runtime-проверка

Проверять через Unity/manual run:

1. Start level:
   - первый plan появляется после `LevelStart`/first ready tick;
   - нет ожидания `0.5s` timer.
2. Spawn pattern:
   - при появлении нового pattern в `_spawnedObstacles` в log появляется rebuild с reason `SpawnPattern`;
   - snapshot содержит obstacles нового pattern, даже если они правее старого vision horizon.
3. Stable no-event frames:
   - если нет spawn и action lifecycle event, plan activation log не повторяется каждые `0.5s`.
4. Action completion:
   - после completion/cancel появляется rebuild с reason `ActionCompleted`/`ActionCancelled`.
   - если completion сразу fire-ит следующий head в том же tick, rebuild строит хвост после новой in-progress head и следующего retained action, если он есть.
5. Bot toggle:
   - F1 OFF очищает plan;
   - F1 ON запрашивает новый plan без timer.

### Representative уровни

Минимальный набор ручных прогонов:

- простой jump-over test level;
- jump-on chain test level;
- switch-lane test level;
- `01_New_York/Morning/level_01` как production representative.

Для анализа логов использовать `tools/read_log_channel.ps1` по правилам `iteration_cycle.md`, если пользователь запускает прогон и возвращает feedback.

## Риски и как их закрывать

### Риск: пропущенный spawn-event оставит bot без актуального хвоста

Причина: после удаления timer больше нет периодической пересборки, которая раньше могла скрыть пропущенный event.

Митигация:

- гарантировать подписку controller-а на `ObstacleSpawner.Instance`;
- иметь one-shot initial replan при first ready tick для случая поздней регистрации controller-а;
- логировать `SpawnPattern` reason.

### Риск: completion + immediate fire потеряет completion-event

Причина: текущий enum не выражает два факта в одном tick.

Митигация:

- flags `PlanExecutionTickResult`;
- replan request по `Completed`, fire-point update по `Fired`.

### Риск: удаление vision horizon меняет pending-head retention

Причина: `VisionRightEdgeX` раньше давал target-bound jump-on более широкий execution region.

Митигация:

- не использовать artificial vision horizon для retention;
- третья и последующие actions не committed, а пересчитываются при spawn/action event.

### Риск: all spawned snapshot включает obstacles слева от экрана

Причина: фильтр по `screenLeftEdgeX` будет удалён.

Митигация:

- `ObstacleChainBuilder.TryCreateActiveElement()` уже отбрасывает obstacles, у которых `RightX <= hamster.HamsterLeftX`;
- `ObstacleSpawner.UnspawnObstacle()` всё равно удаляет объекты из `_spawnedObstacles`;
- после реализации проверить, что roof-support/start-index logic не ломается на offscreen-left obstacles.

### Риск: event-driven replan проявит скрытые расхождения planner/runtime

Причина: timer мог случайно маскировать stale tail.

Митигация:

- сначала менять orchestration, не scoring/strategies;
- проверять логи выбранной ветки и execution result;
- не добавлять soft-commit до стабилизации bounded prefix варианта.

## Критерии готовности

- В коде нет `_rollingReplanInterval` и `_nextRollingReplanTime`.
- Rebuild вызывается только через queued reasons: `LevelStart`, `BotEnabled`, `SpawnPattern`, `ActionCompleted`, `ActionCancelled`.
- `SnapshotBuilder` строит `WorldSnapshot.Obstacles` из всех `ObstacleSpawner.SpawnedObstacles`.
- `RuntimeBotController` не строит plan напрямую из spawn callback; callback только requests replan.
- Committed prefix не длиннее двух actions.
- Immediate handoff в `PlanExecutor` сохранён.
- Completion не теряется при `Completed | Fired`.
- В diagnostic log plan activation содержит reason.
- Документация `architecture_knowledge_base.md` обновлена после реализации.

## Implementation notes

- `PlanExecutionTickResult` переведён на flags enum, чтобы `Completed | Fired` сохранял immediate handoff и не терял completion-trigger для replan.
- `RuntimeBotController` переведён с rolling timer на queued `BotReplanReason`.
- `ObstacleSpawner` публикует `PatternSpawned` после успешного добавления obstacles pattern-а в `_spawnedObstacles`.
- `SnapshotBuilder` собирает все active spawned obstacles без `screenLeftEdgeX`/right-horizon фильтра.
- `WorldSnapshot.VisionRightEdgeX` удалён полностью.
- `docs/architecture_knowledge_base.md` обновлён под event-driven replanning.

## Замечания по Clean Code / KISS

- Не вводить отдельный `BotReplanningService` на первом шаге: orchestration уже живёт в `RuntimeBotController`, а изменение касается его lifecycle.
- Event callback должен быть command (`RequestReplan`), не query с побочным эффектом.
- Не добавлять soft-commit и revalidation layer без доказанной необходимости.
- Держать изменение по слоям: execution result -> replan trigger -> spawn event -> snapshot horizon -> docs.
