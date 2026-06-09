# Rolling replanning после завершения action

Дата: 2026-06-09

## Цель

Перевести runtime bot controller с per-frame перестройки плана на rolling full replanning:

1. Если текущего плана нет, построить лучшую ветку глубиной до `PlanningGraphBuilder.MaxSearchDepth`.
2. Исполнять только head-action текущего плана.
3. Пока action ждёт trigger или уже выполняется, новый план не строить.
4. После `Completed` или `Cancelled` переснять live snapshot и построить новый план с нуля от фактического текущего состояния.
5. Не достраивать старый хвост и не валидировать retained-prefix в основном runtime loop.

## Проверенные факты по текущему коду

- `RuntimeBotController.OnLateUpdate()` вызывается из `GameManager.LateUpdate()` только при `GameState.PLAYING`; obstacle movement выполняется в update-listeners, поэтому bot tick идёт после движения мира за кадр.
- `RuntimeBotController.TickBot()` сейчас каждый tick:
  - строит `LastSnapshot`;
  - вызывает `_executor.Tick(_hamster)`;
  - переснимает snapshot при execution change;
  - безусловно вызывает `TrySetNewPlan()`.
- `PlanExecutor.Tick()` сейчас возвращает `bool`, который смешивает разные события: `Fired`, `Completed`, `Cancelled`.
- `PlanBuilder.Build(worldSnapshot, committedPlan, retainInProgressHead)` умеет сохранять committed-prefix и вызывает retained validation только через `ProjectCommittedPrefix`.
- Если передать в `PlanBuilder.Build()` пустой `BotPlan.Empty()`, committed-prefix не проецируется, retained validators и `ActionInProgressProjector` не участвуют.
- `PlanningGraphBuilder.MaxSearchDepth = 6`; глубина является максимумом, а не гарантией ровно шести action.
- Расход энергии уже учитывается внутри planning:
  - `PlannedAction.EnergyCost`;
  - strategy policy costs: обычные jump-варианты `10`, super-варианты `20`, switch/passive `0`;
  - specifications отбрасывают action при недостатке энергии;
  - `PlanningStateTransition` вычитает `action.EnergyCost`;
  - `PlanEvaluator` предпочитает меньший total energy cost.
- Runtime energy restore (`EnergyMechanics`) восстанавливает `1` energy в секунду до `100`, но planning сейчас это не прогнозирует.

## Non-goals первой итерации

- Не учитывать collectibles и случайные rewards в ветке.
- Не добавлять отдельный energy forecasting service: текущего `EnergyCost` + conservative subtraction достаточно для первого шага.
- Не удалять физически все retained validator classes. Основной runtime path перестанет их использовать; массовую зачистку лучше делать отдельной cleanup-итерацией после ручной проверки поведения.
- Не переписывать стратегии и не менять scoring, кроме scheduling rebuild.
- Не запускать Unity automation без отдельной команды пользователя.

## Проектное решение

Использовать event-based replanning на уровне `RuntimeBotController`.

Новый flow:

```text
TickBot
  snapshot = Build(live hamster)
  executionResult = executor.Tick(hamster)
  if executionResult != None:
      snapshot = Build(live hamster)
  if no current plan OR executionResult is Completed/Cancelled:
      build plan from scratch using BotPlan.Empty()
      replace executor plan even if the new plan is empty
```

Ключевое правило: `Fired` не вызывает replanning. Action уже отправлен в runtime, и controller ждёт `Completed`.

## План правок

### 1. Сделать результат executor tick явным

Файл: `LostCyberHamster/Assets/Scripts/Bot/Execution/PlanExecutor.cs`

- Добавить маленький enum `PlanExecutionTickResult` рядом с `PlanExecutor`.
- Изменить `Tick(Hamster hamster)`:
  - `None` при отсутствии плана, ожидании trigger или незавершённом in-progress action;
  - `Fired` после успешного `TryFire`;
  - `Cancelled` после `TryFire == Cancelled` и `AdvanceHead()`;
  - `Completed` после `handler.IsCompleted(...)` и `AdvanceHead()`.
- Не менять семантику `AdvanceHead()` и `SetPlan()` в этой итерации.

### 2. Перестроить scheduling в RuntimeBotController

Файл: `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`

- В `TickBot()` заменить bool-flow на `PlanExecutionTickResult`.
- Добавить явный predicate `ShouldRebuildPlan(...)`.
- Заменить per-frame `TrySetNewPlan()` на rebuild-only вызов:
  - initial build при пустом плане;
  - rebuild после `Completed`;
  - rebuild после `Cancelled`.
- При rebuild передавать в `PlanBuilder.Build()` пустой committed plan:
  - `BotPlan.Empty()`;
  - `retainInProgressHead: false`.
- Если новый план пустой, всё равно заменить текущий хвост на пустой план, чтобы старый tail не исполнялся после full rebuild.
- Логировать activation только для непустого плана.

### 3. Оставить PlanBuilder без поведенческого рефакторинга

Файл: `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`

- В первой итерации не менять `PlanBuilder`, потому что build-from-scratch уже доступен через пустой committed plan.
- Retained-prefix path остаётся compile-valid legacy/cleanup surface, но основной runtime loop его больше не вызывает.

### 4. Проверить затронутые файлы

- XML-summary и комментарии логических единиц в изменённых `.cs`.
- Поиск call sites `PlanExecutor.Tick(...)`.
- Проверка, что новый enum не требует правки `.csproj`, если размещён в существующем `.cs` файле.

## Валидация

Обязательная локальная проверка без Unity automation:

- `rg` по `PlanExecutor.Tick` и `PlanExecutionTickResult`.
- `git diff --check`.
- Targeted review изменённых файлов.

Unity compile/recompile и ручной gameplay-прогон по правилам проекта выполняются только по отдельной команде пользователя. После `.cs` изменений рекомендуется ручная проверка в Unity:

1. Старт уровня с bot enabled.
2. Убедиться, что план строится на старте.
3. Убедиться, что во время ожидания trigger нет per-frame план-активаций.
4. Убедиться, что после завершения action появляется новый план.
5. Отдельно проверить cancel path на позднем/недоступном action.

## Риски и ограничения

- Если full rebuild возвращает пустой план, старый хвост будет очищен. Это соответствует целевой модели, но может сделать проблему планировщика заметнее: bot будет ждать следующего tick с новым live snapshot вместо исполнения устаревшего tail.
- Future obstacles за пределами `SnapshotBuilder.VisionRightEdgeX` по-прежнему неизвестны planner-у до spawn/visibility. Это не меняется текущей задачей.
- Moving animated obstacles (`ObstacleMoveMechanics`) остаются вне текущей модели, как согласовано.
- Energy restore пока учитывается только через live snapshot после завершения action, но не прогнозируется внутри ветки. Это консервативно и не блокирует rolling replanning.

## Статус

- [x] Кодовый путь изучен.
- [x] План реализации подготовлен.
- [x] Реализация `PlanExecutionTickResult`.
- [x] Реализация rolling rebuild scheduling.
- [x] Локальная текстовая валидация.
- [x] Self-review и Learning Review.
