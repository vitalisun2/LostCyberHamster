# BotV2.3 Planner Refactor Plan

Дата: 2026-03-18
Статус: обновлённый рабочий план после анализа runtime-поведения
Основа: переход от частично двухшагового planner-а к bounded tree planner-у
Вне scope текущего документа: preview/render как отдельная задача после стабилизации planner-а

## 1. Цель рефакторинга

Перейти от текущей модели:

1. сгенерировать safe first steps;
2. локально достроить второй шаг для части first step;
3. выбрать лучший кандидат по смешанной логике генерации и сравнения;
4. исполнить только голову, а не полноценную ветвь как runtime truth;

к новой модели:

1. взять текущий live snapshot;
2. перечислить все допустимые активные шаги из этого состояния;
3. построить все safe ветви до заданной глубины;
4. для каждого шага использовать единый state projection;
5. оценить ветви целиком по безопасности, target-ам, бонусам и энергии;
6. выбрать лучшую ветвь;
7. сохранить её как `CurrentPlan`;
8. передать в executor только голову ветви;
9. на следующем триггере пересчитать всё заново от live snapshot.

## 2. Архитектурные решения, зафиксированные после анализа

### 2.1. `first relevant distance` не является частью целевой архитектуры

Это вспомогательная эвристика текущего `ActionGenerator`, а не правильная модель планирования.

Целевое правило:

1. если объект уже виден на экране;
2. если он ещё не пройден хомяком;
3. если он справа от хомяка;

то он считается актуальным для дерева и может участвовать в прогнозе.

Следствие:

1. planner не должен ждать специальную дистанцию до препятствия, чтобы начать его учитывать;
2. пересчёт должен опираться на триггеры изменения live scene, а не на distance-window вокруг первого объекта.

### 2.2. Триггеры пересчёта остаются event-driven

Подтверждённые триггеры:

1. изменение состава видимых объектов;
2. завершение шага;
3. отмена шага;
4. изменение управляемого состояния.

Отдельный пересчёт каждые 10 кадров не нужен, пока event-driven триггеров хватает.

### 2.3. Шаг в дереве = только активная команда

Шагами дерева считаются только реальные input-действия:

1. `SwitchLane`;
2. `Jump`;
3. `SuperJump`;
4. в будущем `Ulta`.

`Collectible` не должен становиться отдельным `CollectBonus`-шагом, потому что его подбор не требует отдельной команды.

### 2.4. Бонусы и target-ы должны учитываться как outcome ветви

Хотя бонус не является отдельным шагом, он обязан влиять на выбор ветви.

Целевое правило:

1. шаг меняет состояние;
2. projection определяет, какие объекты были собраны или пройдены по пути;
3. evaluator учитывает это в итоговой ценности ветви.

Следствие:

1. бот может осознанно выбрать `SwitchLane`, если эта ветвь безопасно приводит к бонусу;
2. внешне это будет выглядеть как целевое движение за бонусом;
3. при этом модель останется чистой: шаги — это input, а не world events.

### 2.5. Нужен единый universal projector

Не должно быть отдельных архитектурных сущностей вида `ProjectAfterFirstStep()` или специальных “post-switch validators” как основного решения.

Целевой контракт:

1. вход: произвольный `PlannerState` и `ChainStep`;
2. выход: `StepProjectionResult`;
3. внутри результата: новое состояние, собранные outcomes, consumed/passed objects, валидность шага.

Один и тот же метод должен работать для первого, второго и последующих шагов. Разница только во входном состоянии.

### 2.6. Архитектуру надо делать не жёстко на 2 шага, а с параметром глубины

Практический rollout остаётся поэтапным:

1. сначала стабилизируем depth=2;
2. но новые структуры данных и builder надо проектировать так, чтобы глубина была параметром;
3. реальная целевая верхняя граница для экрана — примерно до 5 шагов.

Это значит:

1. не завязывать новые структуры на `FirstStep` / `SecondStep`;
2. сразу строить на `List<ChainStep>` и агрегированных outcome-данных;
3. depth=2 оставить как стартовую конфигурацию и regression milestone.

## 3. Что уже сделано в коде

Ниже перечислено не как “всё готово”, а как уже существующий фундамент, на который можно опираться.

### 3.1. Runtime `CurrentPlan` уже введён

Статус: выполнено как базовый фундамент.

Что уже есть:

1. `CurrentPlan` хранит `List<ChainStep>`;
2. есть `Head`;
3. есть `Clear()`;
4. оркестратор уже работает через `_currentPlan`, а не через отдельную `_activeStep` модель как основной runtime truth.

### 3.2. Оркестратор уже работает по event-driven trigger-ам

Статус: выполнено.

Что уже есть:

1. `VisibleObjectsChanged`;
2. `StepCompleted`;
3. `StepCancelled`;
4. `ManagedStateChanged`.

Это совпадает с целевым направлением и не требует отката.

### 3.3. Есть начальная depth=2 chain-модель

Статус: частично выполнено.

Что уже есть:

1. `ChainGenerator` уже ушёл от чисто двухшаговой схемы и умеет строить depth-parameterized ветви;
2. добавлены `PlannerState`, `StepProjectionResult` и `StateProjector` как новый projection foundation;
3. `CurrentPlan` и `ChainCandidate` уже опираются на `List<ChainStep>` как на основной carrier.

Что пока не готово:

1. вокруг новых `List<ChainStep>` всё ещё живут compatibility accessors `FirstStep` / `SecondStep`;
2. projection foundation уже введён, но его branch/outcome contract ещё не доведён до финального evaluator-а;
3. collectibles всё ещё partially зашиты в уровень генерации шагов, а не branch outcomes;
4. branch scoring и dead-end rejection ещё не доведены до финального чистого pipeline.

### 3.4. Preview пока не является источником истины

Статус: зафиксировано как верное решение.

Preview должен следовать runtime plan memory после стабилизации planner-а, а не определять архитектуру planner-а.

## 4. Целевой runtime flow

```
Trigger
  -> SnapshotBuilder
  -> ObjectClassifier
  -> SafeStepEnumerator
  -> PlanTreeBuilder(maxDepth)
  -> BranchEvaluator
  -> BranchSelector
  -> CurrentPlan
  -> StepExecutor(head only)
```

После завершения или отмены головы:

```
Trigger
  -> rebuild from current live snapshot
```

На этом этапе `keep-tail` не нужен. Сначала нужен корректный full rebuild.

## 5. Целевые структуры данных

### 5.1. `PlanBranch`

Новая основная структура выбранной ветви.

Поля первой рабочей версии:

1. `List<ChainStep> Steps`
2. `PlannerState FinalState`
3. `BranchOutcome Outcome`
4. `int TotalProfit`
5. `int TotalEnergyCost`
6. `bool AllStepsSafe`
7. `DecisionRank BestRank`
8. `string SelectionReason`

### 5.2. `PlannerState`

Универсальное состояние planner-а, достаточное для повторного применения `ProjectStep(...)`.

Минимум:

1. текущая линия;
2. roof/non-roof состояние;
3. `HamsterRightX` и ширина;
4. текущая энергия;
5. lives;
6. remaining objects;
7. набор уже consumed/passed object ids.

### 5.3. `StepProjectionResult`

Новый результат единичного прогноза шага.

Минимум:

1. `bool IsSafe`
2. `PlannerState NextState`
3. `List<ObstacleInfo> CollectedObjects`
4. `List<int> ConsumedObjectIds`
5. `int EnergyDelta`
6. `string DebugReason`

### 5.4. `BranchOutcome`

Отдельная агрегированная структура для результатов ветви.

Минимум:

1. собранные collectibles;
2. достигнутые target-ы;
3. суммарный energy gain/loss;
4. суммарный profit;
5. флаг damage / death / unsafe.

### 5.5. `CurrentPlan`

Текущая runtime-структура выбранной ветви.

Поля:

1. `List<ChainStep> Steps`
2. `string OriginReason`
3. `int HeadStableId`
4. `bool IsEmpty`

Методы:

1. `Head`
2. `RemoveCompletedFromHead()`
3. `Clear()`

## 6. План исполнения по этапам

Статусы:

1. `✅ выполнено` — уже есть в коде и соответствует текущему направлению;
2. `◐ частично` — фундамент есть, но контракт не доведён;
3. `⬜ todo` — ещё не реализовано как нужно.

## Этап 0. Подготовка контрактов

Статус этапа: ◐ частично

### 0.1. Зафиксировать терминологию в коде и документах — ◐

Нужно привести термины к одной схеме:

1. `safe step`;
2. `safe branch`;
3. `current plan`;
4. `head`;
5. `rebuild`;
6. `planner state`;
7. `branch outcome`.

### 0.2. Зафиксировать scope ближайшей реализации — ✅

Ближайший rollout:

1. сначала depth=2 как validate milestone;
2. full rebuild;
3. без keep-tail reuse;
4. без preview refactor.

При этом новые API проектируются уже с параметром глубины.

## Этап 1. Ввести `CurrentPlan` как реальный runtime state

Статус этапа: ✅ выполнено как базовый слой

### 1.1. Создать класс `CurrentPlan` — ✅

### 1.2. Подключить `CurrentPlan` в `BotOrchestrator` — ✅

### 1.3. Переключить `StepExecutor.SetStep()` на работу от головы `CurrentPlan` — ✅

### 1.4. После завершения шага очищать runtime plan и запускать rebuild — ✅

Примечание:

1. сейчас после завершения шага план полностью очищается, а не продолжает жить как keep-tail;
2. для full rebuild milestone это приемлемо и даже желательно.

## Этап 2. Выделить перечислитель safe steps

Статус этапа: ◐ частично

### 2.1. Сохранить `ActionGenerator`, но переосмыслить его как `SafeStepEnumerator` — ✅

Широкий rename сейчас не обязателен.

### 2.2. Зафиксировать контракт компонента — ◐

Целевой контракт:

1. вход — `PlannerState` или snapshot;
2. выход — все допустимые активные safe steps из данного состояния;
3. компонент не выбирает лучший шаг;
4. компонент не знает про ветвь целиком;
5. компонент не должен кодировать branch reward внутри самого action type.

### 2.3. Убрать `first relevant distance` и cluster-window из target architecture — ✅

Вместо этого:

1. учитывать все видимые непройденные объекты;
2. отбор делать через safety и branch scoring, а не через distance-гильотину.

Дополнительное зафиксированное правило после runtime-отладки:

1. `ThreatSafety`-шаг `SwitchLane` должен привязываться к ближайшей same-lane угрозе;
2. иначе executor ждёт `ExecuteAtDistance` по дальнему obstacle и опаздывает к ближнему убийце.

### 2.4. Вынести collectibles из “специальных шагов на бонус” в branch-aware model — ⬜

Разрешено временно сохранять `SwitchLane` к collectible как bootstrap-эвристику,
но целевой контракт должен быть таким:

1. шаг — только активная команда;
2. collectible — outcome ветви.

Критерий готовности этапа:

1. можно взять любой state и получить полный список safe actions по всем актуальным видимым объектам.

## Этап 3. Унифицировать projection layer

Статус этапа: ✅ выполнено

### 3.1. Заменить частную модель `ProjectNextState(...)` на universal `ProjectStep(...)` — ✅

`StateProjector.Project(PlannerState, ChainStep)` — единый метод для всех шагов.

### 3.2. Для каждого типа шага определить правила трансформации состояния

Для каждого шага зафиксировать:

1. итоговую линию;
2. итоговую энергию;
3. roof/non-roof состояние;
4. какие объекты считаются пройденными;
5. какие объекты считаются собранными;
6. какие остаются в remaining set.

### 3.3. Убрать ложную семантику из projection layer

Если projection говорит, что `smallAlive` фактически приводит к `JumpOver`, ветка не должна маркироваться как `JumpOnBounce` только по типу цели.

### 3.4. Проверить projection против игровых механик

Нужно отдельно проверить:

1. `SwitchLane`;
2. `JumpOver`;
3. `JumpOnBounce`;
4. `JumpOnRoof`;
5. `SuperJump`.

### 3.5. Добавить unit tests на projected state и step outcomes — ✅

Тесты в `BotV2StateProjectorTests.cs` и `PlannerContractTests.cs`.

Критерий готовности этапа:

1. любой шаг прогнозируется единым методом;
2. collectible и target outcome появляются из projection, а не из ad hoc логики выше.

## Этап 4. Построить safe branches как список ветвей, а не как `first/second` модель

Статус этапа: ✅ выполнено

### 4.1. Обобщить `ChainGenerator` до `PlanTreeBuilder(maxDepth)` — ✅

### 4.2. Перейти с `FirstStep/SecondStep` на `List<ChainStep>` — ✅

Все compatibility accessors удалены. `ChainCandidate` = `List<ChainStep> Steps` + `BranchOutcome Outcome`.

### 4.3. На первом rollout собирать все ветви длины `1..2` — ✅

Builder собирает до `MaxBranchDepth=5` через рекурсивный `ExploreBranch`.

### 4.4. Сразу проектировать builder как depth-parameterized — ✅

`ChainGenerator` параметризован через `MaxBranchDepth`.

### 4.5. Builder только перечисляет safe branches и не выбирает победителя — ✅

`CompareCandidates` удалён из `ChainGenerator`. Сортировка и выбор перенесены в `BranchEvaluator`.

Критерий готовности этапа:

1. из одного snapshot можно получить полный список safe branches длины `1..maxDepth`;
2. внутри builder-а нет локального выбора “лучшего второго шага”.

## Этап 5. Ввести branch scoring как модель выбора

Статус этапа: ✅ выполнено

### 5.1. Создать явные `BranchEvaluator` и `BranchSelector` — ✅

### 5.2. Считать reward ветви по outcome-данным, а не только по rank шага — ✅

`BranchOutcome`: `CollectedObjects`, `TotalProfit`, `TotalEnergyCost`, `NetEnergyGain`, `BestRank`, `AllStepsSafe`.

### 5.3. Лексикографический выбор вместо непрозрачного float-score — ✅

Порядок сравнения первой версии:

1. `AllStepsSafe == true`;
2. выше `BestRank` / ключевой outcome priority;
3. выше `TotalProfit`;
4. выше `NetEnergyGain`;
5. ниже `TotalEnergyCost`.

### 5.4. Бонусы должны выигрывать ветку без превращения в отдельный action-type — ◐

Bootstrap-эвристика `SwitchLane` к collectible ещё есть в `ActionGenerator`.
`BranchOutcome` уже учитывает collectibles как outcome.

Критерий готовности этапа:

1. лучшая ветвь выбирается по итоговому outcome ветви;
2. бот может осознанно идти за бонусом, даже если бонус не является отдельным шагом.

## Этап 6. Подключить branch flow в `BotOrchestrator`

Статус этапа: ✅ выполнено

### 6.1. Заменить текущий `ChainGenerator.Generate()` flow на чистый branch pipeline — ✅

`RunPipeline` использует `BranchEvaluator.SelectBest(chains)`. Добавлены `LogBranchSelection` и `FormatBranch`.

### 6.2. Сохранять выбранную ветвь как `CurrentPlan` — ✅

### 6.3. Передавать в executor только голову плана — ✅

### 6.4. После триггера выполнять полный rebuild — ✅

Критерий готовности этапа:

1. оркестратор реально работает через выбранную ветвь из полного branch build-а.

## Этап 7. Упростить `StepExecutor`

Статус этапа: ✅ выполнено

### 7.1. Оставить в executor только timing и live revalidation

### 7.2. Не позволять executor менять смысл шага

Если planner выбрал шаг, executor не должен скрыто превращать его в другую ветку поведения. Он может:

1. подождать окно;
2. отменить шаг;
3. запросить rebuild.

### 7.3. Оставить late cancel как защитный механизм

Late cancel нужен, если live scene разрушила предпосылки planner-а.

Критерий готовности этапа:

1. executor больше не конкурирует с planner-ом за принятие решений.

## Этап 8. Добавить диагностику ветвей

Статус этапа: ✅ выполнено

### 8.1. Логировать все safe first steps — ✅

### 8.2. Логировать safe branches целиком — ✅

`LogBranchSelection` логирует top-5 ветвей целиком с outcome данными.

### 8.3. Логировать победившую ветвь и runner-up — ✅

`FormatBranch` выводит: шаги, safety, rank, profit, energy для победителя.

### 8.4. Логировать `CurrentPlan` после выбора — ✅

Критерий готовности этапа:

1. по логу можно восстановить весь выбор planner-а и понять, почему бонусная ветка победила или проиграла.

## Этап 9. Добавить тесты на planner contract

Статус этапа: ✅ выполнено

Файл: `Assets/Editor/Tests/EditMode/Bot/PlannerContractTests.cs`

### 9.1. Тест: один safe first, несколько safe second — ✅

### 9.2. Тест: два safe first, у каждого свои safe second — ✅

### 9.3. Тест: лучшая ветвь не совпадает с ветвью лучшего первого шага — ✅

### 9.4. Тест: unsafe second steps полностью отсекаются — ✅

### 9.5. Тест: ветвь длины 1 сохраняется, если second step отсутствует — ✅

### 9.6. Тест: bonus выигрывает ветку как outcome, а не как отдельный action type — ✅

### 9.7. Тест: после завершения головы план очищается и rebuild вызывается заново — ✅

### 9.8. Тест: BranchEvaluator корректно сортирует по safety → rank → profit — ✅

Дополнительно добавлен тест на depth > 2 ветви.

Критерий готовности этапа:

1. тесты покрывают core contract нового planner flow.

## Этап 10. После стабилизации depth=2 открыть путь к depth=3..5

Статус этапа: ✅ выполнено

### 10.1. Поднять `maxDepth` без изменения core data model — ✅

`MaxBranchDepth=5` уже установлен. Все структуры работают с произвольной глубиной.

### 10.2. Проверить, что branching factor ограничен и не взрывает runtime

Практические ограничения:

1. использовать только safe actions;
2. дискретизировать timing-варианты `SwitchLane` умеренно;
3. ограничить глубину примерно до 5 шагов.

### 10.3. Добавить regression patterns на 3+ шага

Критерий готовности этапа:

1. увеличение глубины требует только смены конфигурации и тестов, а не полного переписывания planner-а.

## Этап 11. Только после стабилизации вернуться к preview

Статус этапа: ✅ выполнено

### 11.1. Подключить preview к `CurrentPlan` — ✅

`UpdateTrajectoryPreview` принимает `ChainCandidate` и передаёт `PreviewSteps` в renderer.

### 11.2. Рендерить не "лучший кандидат текущего прохода", а текущий runtime plan — ✅

`BotTrajectoryRenderer` рендерит `List<ChainStep>` из плана.

### 11.3. Показывать последующие шаги как отражение `CurrentPlan.Steps` — ✅

Renderer поддерживает N шагов с alpha fade (1.0 → 0.75 → 0.50, min 0.2).

Критерий готовности этапа:

1. preview становится тупым отражением runtime plan memory.

## 7. Приоритет исполнения этапов

Рекомендуемый порядок реальной работы:

1. Этап 2
2. Этап 3
3. Этап 4
4. Этап 5
5. Этап 6
6. Этап 7
7. Этап 8
8. Этап 9
9. Этап 10
10. Этап 11

Этап 1 уже выполнен и служит фундаментом.

## 8. Минимальный MVP ближайшей рабочей версии

Если делать ближайшую рабочую версию без лишнего размаха, MVP состоит из:

1. `CurrentPlan` как runtime truth;
2. removal of `first relevant distance` из planner contract;
3. `PlannerState` + `StepProjectionResult`;
4. universal `ProjectStep(...)`;
5. `PlanTreeBuilder(maxDepth=2)` на `List<ChainStep>`;
6. `BranchEvaluator` / `BranchSelector`;
7. оркестратор, который выбирает лучшую ветвь и исполняет её голову.

Этого уже достаточно, чтобы проверить, что новая архитектура реально работает лучше текущей на проблемных сценариях.

## 9. Следующий практический шаг

Первым кодовым шагом следующей итерации должен быть не UI и не preview, а очистка planner contract.

Рекомендуемый первый набор правок:

1. убрать `first relevant distance` из `ActionGenerator` contract;
2. вынести projection в отдельный universal result model;
3. перестать описывать бонус как “специальный шаг”, а перевести его в outcome ветви;
4. после этого уже обобщать `ChainCandidate`/`ChainGenerator` до depth-parameterized ветвей.

Именно этот порядок минимизирует риск снова закодировать правильную идею в неправильную форму.