# Async Replan Implementation Plan

Дата: 2026-06-23

## Цель

Перевести пересборку bot plan с синхронного выполнения внутри `RuntimeBotController.TickBot()` на асинхронное выполнение, чтобы убрать main-thread freeze от `max_search_depth = 6`, не меняя семантику выбора действий.

Критерии готовности:
- replan build не блокирует основной game tick;
- новый план применяется только на main thread;
- stale async results не применяются поверх более свежего runtime-состояния;
- committed-prefix и in-progress head-action сохраняют текущий контракт;
- dead-end report сохраняет текущий контракт подтверждения через потерю жизни;
- 30 build perf на `01_New_York/Morning/level_01` показывает снижение main-thread replan time;
- `tools/invoke_run_all_test_levels.ps1` проходит без `FAIL`, semantic mismatches и life-loss damage markers.

## Текущий Execution Path

Main-thread tick:
- `RuntimeBotController.TickBot()` строит live snapshot, двигает executor, обновляет in-progress head и вызывает rebuild при `_isReplanRequested`: `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs:373`.
- `RebuildPlanFromCurrentSnapshot()` consume-ит причины, синхронно строит plan, обновляет pending dead-end report, сравнивает с текущим планом и вызывает `_executor.SetPlan(plan)`: `RuntimeBotController.cs:431`.
- `BuildPlanForCurrentExecutionState()` выбирает live-root или committed-prefix + tail-root: `RuntimeBotController.cs:463`.
- `BuildTailRootState()` проектирует уже committed действия; для текущей in-progress головы использует `_executor.IsActionInProgress` и `_inProgressHeadFireTime`: `RuntimeBotController.cs:616`.
- `ProjectInProgressCommittedAction()` использует `TryGetRemainingPostFireWorldShift()` и `_transitionSimulator.ProjectInProgress(...)`: `RuntimeBotController.cs:640`.
- `SimulatePendingCommittedAction()` строит projected snapshot и симулирует pending committed action: `RuntimeBotController.cs:660`.
- Replan request lifecycle: `RequestReplan()` накапливает причины и ставит `_isReplanRequested`: `RuntimeBotController.cs:751`; `PromoteDeferredReplanReasons()` переносит deferred причины в pending: `RuntimeBotController.cs:774`; `ConsumeReplanReasons()` очищает pending request: `RuntimeBotController.cs:787`; `ClearAllReplanRequests()` сбрасывает pending/deferred: `RuntimeBotController.cs:806`.

Источники replan:
- initial `LevelStart` / `BotEnabled`;
- `ActionCompleted` deferred после execution tick;
- `ActionCancelled` immediate;
- `SpawnPattern` из `OnPatternSpawned()`: `RuntimeBotController.cs:1099`.

## Main Thread Boundaries

Обязательно остаётся на main thread:
- `SnapshotBuilder.Build()` из-за `Camera.main`, `Transform`, `BoxCollider2D.bounds`, `ObstacleSpawner.Instance`, `Time.time`: `SnapshotBuilder.cs:28`, `SnapshotBuilder.cs:92`, `SnapshotBuilder.cs:133`.
- `PlanExecutor.Tick()` / `_executor.SetPlan()` / live action handlers: они используют live hamster, obstacles, colliders и input side effects.
- `Debug.Log`, `DebugManager.DiagLog`, test result reporting и dead-end confirmation.
- применение async result к `_executor` и `_pendingDeadEndReport`.

Worker-safe input:
- `WorldSnapshot`, `HamsterSnapshot`, `ObstacleSnapshot`, `BotPlan`, `PlannedAction` являются immutable object graph для planning-слоя.
- Для async request нужно копировать `CurrentPlan.Actions` в массив, чтобы request не зависел от последующей замены `CurrentPlan`.

## Worker Blockers And Fixes

1. `BotAnimationTravelProvider` лениво ищет `TransformAnimatorController` через Unity API и вычисляет данные clips: `BotAnimationTravelProvider.cs:18`, `BotAnimationTravelProvider.cs:39`, `BotAnimationTravelProvider.cs:64`.
   - Решение: добавить main-thread prewarm всех известных bot animation clip names до первого async request.
   - Добавить lock вокруг cache dictionary.
   - В worker на cache miss не вызывать Unity API; возвращать `false` и логировать/диагностировать на main thread при необходимости.

2. `JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin()` читает `Time.timeScale`: `JumpPlanningConstants.cs:18`.
   - Сейчас результат не зависит от `timeScale`, метод всегда возвращает `FireWindowBoundaryMargin`.
   - Решение: убрать runtime `Time.timeScale` dependency из overload без параметров и возвращать constant напрямую.

3. Planning verbose logs:
   - `ActionGenerator` пишет verbose diagnostics: `ActionGenerator.cs:317`, `ActionGenerator.cs:336`.
   - `PlanningState` пишет verbose roof support skip: `PlanningState.cs:150`.
   - `PassiveCollectStrategy` и `JumpOnRoofFireWindowFinder` пишут verbose diagnostics: `PassiveCollectStrategy.cs:62`, `JumpOnRoofFireWindowFinder.cs:466`.
   - `DebugManager.DiagLogVerbose()` по умолчанию disabled, но сам `DebugManager` пишет в file и не является частью planning contract.
   - Решение: не включать verbose diagnostics в worker. Если в будущем нужен worker diagnostics, собирать strings в result и писать их на main thread.

4. Shared mutable strategy instances:
   - `RuntimeBotController.Awake()` создаёт один `IReadOnlyList<IPlanningStrategy>` и использует его для executor, transition simulator и plan builder.
   - Решение: вынести создание strategies в factory method и создать отдельный dependency graph для async builder. Main executor остаётся со своими strategies/executors; worker получает свои strategies/simulators/planner.

## Async Architecture

Добавить в `RuntimeBotController`:

Состояние:
- `_asyncPlanBuilder`: отдельный worker planner wrapper.
- `_runningReplanTask`: текущий `Task<AsyncPlanBuildResult>`.
- `_runningReplanRequestId`: id запроса, который сейчас строится.
- `_nextReplanRequestId`: монотонный id.
- `_hasAsyncResultToApply` не нужен, если completion проверяется polling-ом в tick.
- `_asyncReplanErrorLogged` или main-thread error handling, чтобы не молчать при worker exception.

Request model:
- `RequestId`.
- `WorldSnapshot Snapshot`.
- `BotPlan CurrentPlanSnapshot` или `PlannedAction[] CurrentActions`.
- `bool IsActionInProgress`.
- `PlannedAction InProgressHeadAction`.
- `float InProgressHeadFireTime`.
- `BotReplanReason ReplanReasons`.

Worker result:
- `RequestId`.
- `BotReplanReason ReplanReasons`.
- `PlanBuildResult BuildResult`.
- optional `Exception Error`.

Main-thread flow в `TickBot()` после `RequestReplanForExecutionResult(executionResult)`:
1. `TryApplyCompletedAsyncReplan()`.
2. Если нет running task и `_isReplanRequested`, вызвать `StartAsyncReplanFromCurrentSnapshot()`.

`StartAsyncReplanFromCurrentSnapshot()`:
1. `ConsumeReplanReasons()`.
2. Скопировать current immutable runtime context в request.
3. Запустить `Task.Run(() => _asyncPlanBuilder.Build(request))`.
4. Не трогать `_executor` в worker.

`TryApplyCompletedAsyncReplan()`:
1. Если task не завершён - return.
2. Забрать result/exception.
3. Если после dispatch появились новые `_pendingReplanReasons` или `_deferredReplanReasons`, result stale: не применять, оставить pending request для нового dispatch.
4. Если result request id не равен `_runningReplanRequestId`, discard.
5. Если worker exception: main-thread `Debug.LogError`, `RequestReplan(reasons)`, return.
6. Применить текущий старый contract:
   - если `buildResult.HasDeadEnd` => `RememberPendingDeadEndReport(...)`, иначе `ClearPendingDeadEndReport()`;
   - если `plan.IsEquivalentTo(CurrentPlan)` => не вызывать `SetPlan`;
   - иначе `_executor.SetPlan(plan)` и `LogPlanActivation(plan, replanReasons)`.

Сбросы:
- `Disable()` должен отменять/инвалидировать async result и очищать request state: `RuntimeBotController.cs:356`.
- `ResetRuntimeStateForNewGameManager()` должен отменять/инвалидировать async result: `RuntimeBotController.cs:1039`.
- `OnDestroy()` должен отменять/инвалидировать async result.

Stale result contract:
- Если во время worker build пришёл `SpawnPattern`, `ActionCompleted` или `ActionCancelled`, старый result не применяется.
- Если executor сам продвинул head, это создаёт deferred/immediate replan через текущий execution path, значит старый result будет discarded.
- Если ничего нового не произошло, result можно применить к текущему plan с обычной `IsEquivalentTo(CurrentPlan)` проверкой.

## Worker Plan Build Algorithm

Выделить метод, эквивалентный текущему `BuildPlanForCurrentExecutionState()`, но работающий только по request:
- если нет actions или есть `ActionCancelled`, строить `_planBuilder.Build(snapshot)`;
- иначе построить committed-prefix из request plan actions;
- root = `PlanningState.FromSnapshot(snapshot)`;
- tailRoot = `BuildTailRootState(request, root, committedPrefix)`;
- если tailRoot null => `_planBuilder.Build(snapshot)`;
- иначе `_planBuilder.Build(snapshot, tailRoot)`, затем склеить committed prefix + tail actions в `BotPlan`.

Отличие от текущего runtime method:
- `BuildTailRootState` не читает `_executor` и `LastSnapshot`, а использует поля request.
- `ProjectInProgressCommittedAction` использует captured `IsActionInProgress`, `InProgressHeadAction`, `InProgressHeadFireTime`, `Snapshot.SnapshotTime`.

## Implementation Steps

1. Подготовить worker-safe runtime constants:
   - изменить `JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin()` на constant-return без `Time.timeScale`.
   - добавить в `BotAnimationTravelProvider` prewarm API и lock-protected cache.
   - в `RuntimeBotController.Awake()` после `BotAnimationTravelProvider.Reset()` вызвать prewarm.

2. Вынести factory strategies:
   - добавить private static `CreatePlanningStrategies()` в `RuntimeBotController`;
   - main dependencies используют один набор;
   - async builder получает отдельный набор.

3. Добавить async planner wrapper:
   - желательно отдельный файл под `Assets/Scripts/Bot/Planning/AsyncPlanRebuild...` либо private sealed helper рядом, если scope небольшой.
   - helper не должен ссылаться на UnityEngine.

4. Изменить `RuntimeBotController`:
   - добавить поля async task/request id;
   - заменить `RebuildPlanFromCurrentSnapshot()` на dispatch/apply pipeline;
   - сохранить sync logic как helper для worker-equivalent implementation или удалить после переноса;
   - добавить invalidation в `Disable`, `ResetRuntimeStateForNewGameManager`, `OnDestroy`.

5. Компиляция:
   - после новых `.cs` файлов запускать `tools/invoke_open_unity_test_level.ps1` или bridge recompile, чтобы Unity обновил `.csproj`.
   - затем `dotnet build ... --no-restore`.

6. Perf measurement:
   - временно добавить profiler/log `[Bot PERF]` только на main-thread dispatch/apply path и worker elapsed:
     - dispatch count;
     - request id;
     - mainThreadScheduleMs;
     - workerElapsedMs;
     - mainThreadApplyMs;
     - stale/discard count.
   - прогнать `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 160`.
   - собрать первые 30 replan samples и сравнить с baseline:
     - sync optimized baseline: total `493.486 ms`, avg `16.450 ms`, max `64.383 ms`, `>16=14`, `>33=3`;
     - expected async main-thread schedule/apply должен быть около sub-ms/несколько ms; worker elapsed может остаться ~64 ms, но не должен фризить кадр.
   - удалить временный profiler.

7. Regression validation:
   - запуск `.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 120`.
   - скрипт сам проверяет:
     - test result `WIN/FAIL`;
     - damage markers/life loss через `DamageMarkers`;
     - semantic mismatch по description через `Get-LevelSemanticSummary()`: `tools/invoke_run_all_test_levels.ps1:287`.
   - В текущем runner `test_collectables` может давать `UNKNOWN:*` для `should collect...`, потому что mapping описывает movement actions. Это не life-loss регресс, но требует отдельной ручной интерпретации или расширения runner-а, если после async появится semantic failure только по collectables.
   - Если появились life-loss regressions, разрешено чинить сразу.
   - После фиксов повторить impacted level, затем all test levels.

## Risk Matrix

- Старый план применяется после spawn/action-completion: решается discard при pending/deferred reasons.
- First plan приходит на кадр позже: смягчается spawn lookahead; если тесты покажут miss, можно оставить only initial replan sync как fallback, но это нежелательно и будет отдельным решением по фактам.
- Worker вызывает Unity API через clip cache: решается prewarm + cache-miss guard.
- Worker exception silently kills planning: result должен переносить error на main thread и re-request.
- Слишком много discarded tasks при частых spawns: допускается один running task, pending coalescing; новый request стартует после завершения старого.
- Невозможность отменить уже running CPU task: stale discard дешевле и проще; cancellation можно добавить позже, если worker queue станет проблемой.

## Open Checks Before Coding

- Дождаться/учесть отчеты subagents по RuntimeBotController, thread-safety и validation.
- Проверить, что новые helper classes попали в `Assembly-CSharp.csproj` после Unity regenerate.
- Проверить, что `DebugManager.DiagLogVerbose` остается disabled в test automation.

## Implementation Result

Статус на 2026-06-23:
- async replan реализован внутри `RuntimeBotController` через worker `Task<AsyncPlanBuildResult>`;
- main thread только захватывает `WorldSnapshot`, запускает worker и применяет готовый результат;
- worker получает отдельный набор planning strategies через `CreatePlanningStrategies()`;
- stale result отбрасывается по request id, runtime generation и queued replan reasons;
- reset/disable/destroy инвалидируют pending async result;
- `BotAnimationTravelProvider` prewarm/cache защищен для worker-доступа;
- зависимость `JumpPlanningConstants` от `Time.timeScale` удалена;
- временные perf/branch diagnostic logs удалены.

Perf на первых 30 replan samples `01_New_York/Morning/level_01`:
- sync optimized baseline: total `493.486 ms`, avg `16.450 ms`, max `64.383 ms`, `>16ms=14`, `>33ms=3`;
- async main-thread schedule/apply: total `28.624 ms`, avg `0.954 ms`, max `2.373 ms`, `>16ms=0`, `>33ms=0`;
- async worker build остался CPU-heavy вне main thread: total `640.158 ms`, avg `21.339 ms`, max `99.730 ms`;
- main-thread replan work снизился примерно на `94.2%` по total/avg и на `96.3%` по max.

Regression validation:
- `dotnet build .\LostCyberHamster\LostCyberHamster.sln --no-restore`: 0 errors, existing warnings only;
- `tools/invoke_run_all_test_levels.ps1`: `All 16 levels passed`, damage markers 0, semantic mismatches 0.

Additional collectables fix found during validation:
- root cause: `ActionGenerator` создавал optional collectable action только для текущей линии; optional-only collectables на противоположной линии пропускались `TryDetectRoute`, поэтому ветки для energy/третьей coin в `test_collectables` не доходили до evaluator;
- fix: `ActionGenerator` добавляет safe `SwitchLane` к positive optional collectable на противоположной линии, `SwitchLaneStrategy` разрешает такой chain, а проверка positive value централизована в `CollectibleValuePolicy.HasPositiveCollectible`;
- fixture `test_collectables` разделена штатными `relief` spacer-ами, чтобы независимые expected-сценарии не конкурировали в одном planning horizon.
