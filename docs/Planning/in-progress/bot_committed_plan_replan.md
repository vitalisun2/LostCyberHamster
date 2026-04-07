# Рефакторинг: committed plan и event-driven replanning

## Контекст

Этот документ фиксирует архитектурную доработку bot planner/runtime без привязки к одному тестовому уровню и без временных костылей под `test_roof_jump_over`.

Цель работы:

- убрать full replan по завершению каждого шага;
- сделать `committed plan` первым классом runtime-модели;
- триггерить пересборку плана только по действительно согласованным runtime-событиям;
- заменить текущий план только если новый branch стал строго лучше;
- увеличить глубину lookahead с `3` до `5`.

## Что зафиксировано по коду

### Текущее состояние runtime

- Точка входа бота: `BotOrchestrator`.
- Каждый кадр runtime делает:
  - построение live snapshot через `SnapshotBuilder`;
  - обновление baseline видимых объектов;
  - `StepExecutor.PollCompletion()`;
  - full replan или preview-only replan;
  - `StepExecutor.TryFire()`.
- Текущие runtime-триггеры replanning:
  - `VisibleObjectBaselineTracker.OnBaselineChanged`;
  - `StepExecutor.OnStepCompleted`.

### Текущее состояние плана

- Единственный runtime storage плана: `CurrentPlan`.
- `BotPlanRuntime.ApplyPlan(...)` всегда делает `ReplaceFrom(...)`.
- Preview и committed plan сейчас не разделены:
  - `ReplanPreviewOnly(...)` тоже вызывает `ApplyPlan(...)`;
  - значит preview-пересчёт перетирает исполняемый хвост.
- После completion шага orchestrator не двигает хвост существующего плана, а заново строит лучший branch с нуля.

### Текущее состояние planner

- Planner pipeline:
  - `ProblemResolver`
  - `ActionGenerator`
  - `BranchGenerator`
  - `BranchEvaluator`
- `ProblemResolver` сейчас решает только одну ближайшую `same-lane` проблему типа `ThreatCollision`.
- `ObjectClassifier` уже умеет размечать `Target` и `Collectible`, но `ProblemResolver` их пока не поднимает в problem model.
- `BranchGenerator` строит ветви глубиной только `3`.
- `BranchEvaluator` сравнивает ветви в порядке:
  - safety
  - energy
  - depth
  - first fire timing

### Что важно по runtime-механике игры

- Препятствия в мире не “возникают из ниоткуда” внутри planner:
  - паттерны заранее готовы;
  - новый паттерн просто выпускается справа в сцену;
  - obstacle дальше едут влево общим scroll.
- Реально стабильные post-step состояния для бота сейчас только два:
  - `Run`
  - `RoofRun`
- Для текущей фазы рефакторинга сознательно не учитываем:
  - walking-объекты;
  - `Target` как самостоятельную planner-задачу;
  - collectible-логики и их бонусную экономику;
  - расширение problem model за пределы текущего `ThreatCollision`.

### Что важно по экономике

- `Jump` тратит `10` энергии.
- `JumpOnRoof` тратит `10` энергии.
- Дополнительный `SuperJump` тратит ещё `10`, суммарно `20`.
- Энергия регенерируется runtime-механикой по `+1` в секунду до cap `100`.
- Для planner это значит:
  - стоимость шага является реальным runtime-фактом;
  - текущий `BranchOutcome.TotalEnergyCost` остаётся валидной метрикой при сравнении веток.

## Симптом текущей архитектуры

Основная проблема не в конкретном roof-сценарии, а в том, что bot не умеет удерживать уже выбранное намерение.

Сейчас происходит следующее:

1. planner выбирает подходящий branch;
2. head шага отдаётся executor’у;
3. шаг завершается;
4. orchestrator поднимает full replan;
5. planner снова выбирает branch “с нуля” из нового live snapshot;
6. хвост уже выбранного плана теряется.

Это делает поведение неустойчивым даже в детерминированных паттернах.

## Архитектурное решение

### 1. Вводим committed plan как runtime-интенцию

`CurrentPlan` становится очередью уже принятого плана.

Новые обязанности:

- хранить текущий head и хвост;
- уметь сдвигать голову после completion;
- уметь отдавать retainable ready-tail для сравнения с новым планом.

### 2. Completion больше не триггерит full replan

После завершения шага runtime делает только `AdvancePlan()`:

- completed head удаляется;
- следующий ready-step становится новым head;
- executor продолжает работать с этим head.

Full replan по факту completion больше не выполняется.

### 3. Full replan делаем только в двух случаях

#### Новый видимый объект

Runtime-событие:

- появился новый `StableId` среди видимых объектов.

Важно:

- runtime не знает категорию объекта;
- runtime не решает, threat это, roof или future target;
- runtime только фиксирует факт появления нового объекта в видимой области.

#### План исчерпан

После `AdvancePlan()` хвост может стать пустым.

Если ready-step больше нет, planner обязан построить новый plan с нуля.

### 4. Replacement policy вместо постоянной замены плана

При событии “появился новый видимый объект” runtime не должен безусловно перезаписывать committed plan.

Нужна политика замены:

1. берём retainable-tail текущего committed plan;
2. строим новый лучший branch для текущего snapshot;
3. если среди новых branches есть retained-вариант, сравниваем его с новым best;
4. заменяем committed plan только если новый branch стал строго лучше retained-варианта.

Если retained-варианта среди новых branches уже нет, считаем текущий хвост невалидным относительно новой scene-конфигурации и заменяем его новым best.

### 5. Строгое сравнение для replacement не должно дёргаться на fire timing

Для initial branch selection fire timing остаётся частью обычного `BranchEvaluator`.

Но для replacement policy fire timing не должен быть причиной churn.

Поэтому “strictly better for replacement” сравнивает только:

- safety
- total energy
- branch depth

Если по этим метрикам новый branch не лучше, retained branch сохраняется.

Это не костыльный stickiness, а нормальная политика замены committed intent.

### 6. Replan во время `InProgress` шага не коммитим

Текущий шаг уже запущен и не должен пересобираться посередине анимации.

Поэтому:

- событие появления нового объекта только помечает, что нужен `EvaluateReplacement`;
- само сравнение и возможная замена committed tail происходят в ближайший момент, когда executor не находится в `InProgress`.

В этой фазе рефакторинга preview как отдельная сущность не нужен:

- renderer показывает committed plan;
- mid-step preview-перепланирование убирается.

## Границы работы

### Входит в рефакторинг

- `BotOrchestrator`
- `CurrentPlan`
- `BotPlanRuntime`
- tracker появления новых видимых объектов
- `BranchSelector`
- `BranchEvaluator`
- `BranchGenerator` depth `5`
- edit-mode тесты на committed plan / replacement policy / depth `5`

### Не входит в этот рефакторинг

- roof-specific семантика (`Roof` category, отдельные `ProblemKind`);
- переделка JSON уровней и паттернов;
- moving `bigAlive`;
- `Target`/collectible planning;
- автопрогоны уровней.

## Целевая модель runtime

### Tick flow

1. Построить live snapshot.
2. Обновить tracker новых visible objects.
3. Дать executor’у завершить текущий шаг.
4. Если шаг завершился:
   - `AdvancePlan()`
   - если план пуст, запросить full replan
5. Если executor не в `InProgress` и есть pending replanning:
   - классифицировать snapshot
   - найти лучший branch с учётом retainable-tail
   - при необходимости заменить committed plan
   - передать актуальный head в executor
6. `TryFire()`

### Invariants

- Исполняется только head committed plan.
- Completion не пересобирает дерево сам по себе.
- Новый visible object не ломает план автоматически.
- Замена плана происходит только после сравнения retained-tail и нового best branch.

## План изменений по коду

### 1. `CurrentPlan`

Добавить:

- `AdvanceCompletedHead()`
- `SnapshotRetainableSteps()`
- `HasSamePrefix(...)` или эквивалент для сравнения retained tail

Оставить:

- `ReplaceFrom(...)`
- `Clear()`

### 2. `BotPlanRuntime`

Переделать из “always apply new branch” в runtime-координатор committed plan:

- `CommitPlan(...)`
- `AdvancePlan(...)`
- `GetRetainableSteps()`
- `SetPreviewFromCommittedPlan(...)`
- очистка/логирование без preview-only replanning

### 3. Tracker видимых объектов

Заменить baseline-трекер изменения множества на tracker появления новых ids.

Новый смысл:

- событие только когда появился новый `StableId`, которого не было в baseline;
- исчезновение объекта само по себе event не генерирует.

### 4. `BotOrchestrator`

Изменить event-модель:

- `OnStepCompleted` больше не вызывает `RequestReplan()`;
- completion вызывает только `AdvancePlan`;
- отдельный флаг pending-evaluation для новых объектов;
- отдельный флаг plan-exhausted replan.

Убрать:

- `ReplanPreviewOnly(...)`
- логику preview-replace во время `InProgress`

Оставить:

- один вход в planner через классифицированный snapshot;
- один committed plan storage.

### 5. `BranchSelector`

Расширить API:

- уметь принимать retainable ready-tail текущего плана;
- находить matching retained candidate среди новых branches;
- возвращать retained candidate, если новый best не стал строго лучше.

### 6. `BranchEvaluator`

Сохранить:

- обычный comparator для выбора лучшего branch.

Добавить:

- `IsStrictlyBetterForReplacement(...)`

### 7. `BranchGenerator`

- увеличить глубину planning до `5`;
- вынести число в `BotConsts`.

## Проверки после рефакторинга

Без автопрогонов уровней в этой задаче.

Нужно сделать:

- компиляционная проверка кода;
- edit-mode тесты на planner/runtime-логику, если они не требуют запуска уровней;
- остановка на code review.

## Ожидаемый эффект

- Бот перестаёт терять хвост плана после каждого completion.
- Runtime становится event-driven, а не “перепридумывающим всё каждый шаг”.
- Planner получает устойчивую replacement policy без глобального переворота tie-breaker.
- Архитектура остаётся расширяемой под будущие `Target`, `Roof` и другие категории объектов.
