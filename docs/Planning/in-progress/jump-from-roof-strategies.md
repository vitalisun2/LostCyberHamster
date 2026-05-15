# Jump From Roof Strategies

## Цель

Добавить `JumpFromRoofStrategy` и `SuperJumpFromRoofStrategy` для опасного схода с крыши: hamster в `RoofRun`, passive roof continuation заканчивается, а первый non-roof obstacle после последней passive roof находится ближе, чем `RunFromRoof` успевает безопасно завершиться.

## Общий подход

Стратегии должны повторять текущий канон jump-семейств:

- concrete strategy - тонкий facade: собирает policy, shared specification, fire-window finder, simulator, retained validator и executor;
- policy хранит отличия обычной и super-версии: `ActionKind`, energy cost, description, expected runtime state, runtime travel по клипам и resolver call;
- shared-код хранит общую механику семьи: applicability, chain/window calculation, shifted obstacle projection, resolver validation, simulator и retained validation;
- executor хранит только runtime input: ordinary вызывает один request, super выполняет двухфазный input через половину `DoubleJumpDetector.DoubleJumpThreshold`;
- финальная валидность candidate action проверяется runtime-equivalent resolver'ом.

Для `JumpFromRoof` success outcome - только чистый `JumpFromRoof` / `SuperJumpFromRoof`. `JumpOnObstacleFromRoof` / `SuperJumpOnObstacleFromRoof` здесь не считаются успехом: напрыгивание с крыши будет отдельной стратегией.

## Краткая структура

- **Блок 1. Вход в planning и decision point**
  Определяем, где заканчивается пассивный бег по крышам и какой obstacle после него должен стать причиной действия.
  - ✅ **1.1.** Добавляем отдельные action kinds для ordinary/super roof-to-road прыжков.
  - ✅ **1.2.** Находим последнюю passive roof, которую runtime пройдет без input.
  - ✅ **1.3.** Строим decision point уже после этой roof, а не на промежуточных passive roof.

- **Блок 2. Shared-механика JumpFromRoof**
  Создаем общий слой стратегии, который одинаково используется ordinary и super-версией.
  - ✅ **2.1.** Policy contract задает отличия конкретной версии.
  - ✅ **2.2.** Runtime travel описывает danger distance и action distance.
  - ✅ **2.3.** Specification проверяет, что автоматический сход с крыши опасен.
  - ✅ **2.4.** Chain/window logic выбирает момент прыжка по obstacle chain.
  - ✅ **2.5.** Chain model хранит выбранный obstacle range и fire-window.
  - ✅ **2.6.** Resolver validation подтверждает candidate через runtime-equivalent механику.
  - ✅ **2.7.** Simulator переводит planning-state в `Run`.
  - ✅ **2.8.** Retained validator пересчитывает актуальность уже выбранного action.

- **Блок 3. Ordinary JumpFromRoof**
  Подключаем обычную версию стратегии поверх shared-механики.
  - ✅ **3.1.** Policy берет ordinary runtime travel и ordinary resolver.
  - ✅ **3.2.** Strategy facade строит `PlannedAction` без дублирования shared-кода.
  - ✅ **3.3.** Executor отправляет один `RoofJumpRequest` и ждет завершения в `Run`.

- **Блок 4. SuperJumpFromRoof**
  Подключаем super-версию той же стратегии с другим travel, resolver и input flow.
  - ✅ **4.1.** Policy использует super roof jump travel и `SuperRoofJumpOutcomeResolver`.
  - ✅ **4.2.** Strategy facade остается тем же тонким слоем поверх shared-механики.
  - ✅ **4.3.** Executor делает двухфазный input: roof jump, затем super upgrade.

- **Блок 5. Интеграция и проверка**
  Включаем готовые ordinary и super strategies в общий bot pipeline.
  - ✅ **5.1.** Регистрируем ordinary `JumpFromRoofStrategy` в `RuntimeBotController`.
  - ✅ **5.2.** Обновляем project files при необходимости.
  - ✅ **5.3.** Регистрируем `SuperJumpFromRoofStrategy` в `RuntimeBotController`.

## Детальная структура

### Блок 1. Вход в planning и decision point

Этот блок определяет, где заканчивается пассивный бег по крышам и какой obstacle после этого становится реальной причиной для roof-to-road стратегии.

- **1.1. Добавить action kinds**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/PlanState/BotActionKind.cs`
  - **Что меняется:** enum `BotActionKind`.
  - **Суть изменения:** добавить `JumpFromRoof` и `SuperJumpFromRoof`. Эти action kinds нужны отдельно от `RoofJumpOver` / `SuperRoofJumpOver`, потому что итог planning-состояния после них - `Run`, а не продолжение `RoofRun`.

- **1.2. Найти последнюю passive roof**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Planning/RoofRunProjection.cs`
  - **Что меняется:** новый метод `TryFindLastPassiveRoof(...)`.
  - **Суть изменения:** метод возвращает последнюю roof, которую runtime пройдет без input из текущего `RoofRun`, и ее index в projected snapshot. Он не строит список крыш и не меняет `ObstacleChain`; он только дает точку, после которой надо искать обычный decision point.

- **1.3. Начинать roof-run detection после последней passive roof**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointDetector.cs`
  - **Что меняется:** `TryDetect`.
  - **Суть изменения:** для `RoofRun` detector должен сначала получить `lastRoofIndex`, затем искать первый damaging same-lane obstacle после него. Passive roof остаются вне `ObstacleChain`; chain остается обычной цепочкой blocking obstacles.

### Блок 2. Shared-механика JumpFromRoof

Этот блок создает общий слой семьи стратегий: проверку применимости, расчет окна, validation через runtime resolver, simulation и retained validation. Обычная и super-стратегии должны переиспользовать этот слой.

- **2.1. Создать policy contract семьи**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/IJumpFromRoofPolicy.cs`
  - **Что меняется:** новый interface `IJumpFromRoofPolicy`.
  - **Суть изменения:** contract задает `ActionKind`, `EnergyCost`, `DescriptionPrefix`, `ExpectedSuccessState`, `TryGetTravel(out JumpFromRoofTravel travel)` и `Resolve(...)`. Это тот же паттерн, что `IJumpOverPolicy`, `IJumpOnRoofPolicy`, `IRoofJumpOverPolicy`.

- **2.2. Описать runtime travel**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/JumpFromRoofTravel.cs`
  - **Что меняется:** новый readonly struct `JumpFromRoofTravel`.
  - **Суть изменения:** хранить три дистанции: `RunFromRoofTravel` для определения опасного автоматического схода, `RoofJumpTravel` и `JumpFromRoofTravel` для `RoofJumpResolveContext`. `RunFromRoofTravel` всегда берется из runtime run-from-roof clip; ordinary/super policy отличаются action clips и upgrade-delay.

- **2.3. Проверить применимость roof-to-road действия**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/JumpFromRoofSpecification.cs`
  - **Что меняется:** новый class `JumpFromRoofSpecification`.
  - **Суть изменения:** strategy применима только когда hamster в `RoofRun`, стоит на roof support, не shifting, энергии хватает, первый obstacle после `lastRoof` не является roof, dangerous для ground contact, и `gap = firstObstacle.LeftX - lastRoof.RightX` меньше `RunFromRoofTravel`. Если gap безопасный, действие не планируется: runtime сойдет на дорогу, дальше работают ground strategies.

- **2.4. Рассчитать obstacle chain для прыжка с крыши**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/JumpFromRoofChainCalculator.cs`
  - **Что меняется:** новый static class `JumpFromRoofChainCalculator`.
  - **Суть изменения:** считать общее fire-window для одного или нескольких non-roof obstacles из `DecisionPoint.Chain`, по аналогии с `JumpOverChainCalculator`. Расчет использует существующий `JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin()` и не добавляет новые margins.

- **2.5. Хранить результат chain calculation**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/JumpFromRoofChainModel.cs`
  - **Что меняется:** новый readonly struct `JumpFromRoofChainModel`.
  - **Суть изменения:** хранить первый/последний obstacle chain, их indices, obstacle count и fire-window (`FirstFireShift`, `LastFireShift`, `SelectedFireShift`). Model нужна для build action и retained validation.

- **2.6. Подтвердить fire-window через resolver**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/JumpFromRoofFireWindowFinder.cs`
  - **Что меняется:** новый class `JumpFromRoofFireWindowFinder`.
  - **Суть изменения:** finder сдвигает obstacles через `JumpObstacleProjection`, строит `RoofJumpResolveContext` и вызывает policy resolver. Ordinary policy использует `RoofJumpOutcomeResolver.ResolveRoofJump`; super policy использует уже существующий `SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump`. Candidate проходит только при `ExpectedSuccessState`.

- **2.7. Симулировать завершение в Run**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/JumpFromRoofSimulator.cs`
  - **Что меняется:** новый class `JumpFromRoofSimulator`.
  - **Суть изменения:** после успешного action planning-state переходит в `Run`, сбрасывает roof support и продвигает world shift. Можно переиспользовать `PlanningStateTransition.ApplyRunAfterOver` и `PlanningStateTransition.Advance`, потому что итоговое состояние совпадает с успешным ground over-action.

- **2.8. Добавить retained validator**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoof/JumpFromRoofRetainedActionValidator.cs`
  - **Что меняется:** новый class `JumpFromRoofRetainedActionValidator`.
  - **Суть изменения:** validator пересчитывает актуальный chain window, восстанавливает remaining fire shift по `TriggerObstacleInstanceId`, проверяет границы окна и повторяет resolver validation. Это тот же retained-подход, что у `JumpOverRetainedActionValidator` и `RoofJumpOverRetainedActionValidator`.

### Блок 3. Ordinary JumpFromRoof

Этот блок подключает обычную версию стратегии к shared-механике: policy задает runtime numbers и expected outcome, strategy собирает candidate action, executor отправляет один roof-jump input.

- **3.1. Добавить ordinary policy**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoof/JumpFromRoofPolicy.cs`
  - **Что меняется:** новый class `JumpFromRoofPolicy`.
  - **Суть изменения:** policy задает `BotActionKind.JumpFromRoof`, energy cost `10`, expected state `HamsterStateEnum.JumpFromRoof`, travel по `transform_run_from_roof`, `transform_roof_jump`, `transform_jump_from_roof`, resolver `RoofJumpOutcomeResolver.ResolveRoofJump`.

- **3.2. Добавить ordinary strategy facade**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoof/JumpFromRoofStrategy.cs`
  - **Что меняется:** новый class `JumpFromRoofStrategy`.
  - **Суть изменения:** facade повторяет структуру `JumpOverStrategy` / `RoofJumpOverStrategy`: specification -> policy travel -> fire-window finder -> build `PlannedAction`. `triggerObstacleInstanceId` указывает на первый obstacle chain; покрытый диапазон хранится в chain model и используется при build action/retained validation.

- **3.3. Добавить ordinary executor**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoof/JumpFromRoofExecutor.cs`
  - **Что меняется:** новый class `JumpFromRoofExecutor`.
  - **Суть изменения:** executor требует `BotActionKind.JumpFromRoof`, `HamsterStateEnum.RoofRun`, энергию и прохождение `ActionTriggerGate`; затем вызывает `hamster.RoofJumpRequest.Invoke()` и считает action завершенным при возврате в `Run`.

### Блок 4. SuperJumpFromRoof

Этот блок добавляет super-версию без дублирования shared-логики: отличаются policy travel/resolver/expected outcome и executor с двухфазным input.

- **4.1. Добавить super policy**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoof/SuperJumpFromRoofPolicy.cs`
  - **Что меняется:** новый class `SuperJumpFromRoofPolicy`.
  - **Суть изменения:** policy задает `BotActionKind.SuperJumpFromRoof`, expected state `HamsterStateEnum.SuperJumpFromRoof`, danger travel по `transform_run_from_roof`, action travel по `transform_super_roof_jump` и `transform_super_jump_from_roof` с текущим upgrade-delay подходом, resolver `SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump`. Energy cost брать по текущему канону super strategies, согласованно с `SuperRoofJumpOverPolicy`.

- **4.2. Добавить super strategy facade**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoof/SuperJumpFromRoofStrategy.cs`
  - **Что меняется:** новый class `SuperJumpFromRoofStrategy`.
  - **Суть изменения:** facade использует тот же shared stack, что ordinary strategy, но с `SuperJumpFromRoofPolicy` и `SuperJumpFromRoofExecutor`. Отличия обычной и super-версии не должны уходить в duplicated chain/window code.

- **4.3. Добавить super executor**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoof/SuperJumpFromRoofExecutor.cs`
  - **Что меняется:** новый class `SuperJumpFromRoofExecutor`.
  - **Суть изменения:** executor повторяет подход `SuperRoofJumpOverExecutor`: fire через `RoofJumpRequest`, затем upgrade через `SuperRoofJumpRequest` после половины double-jump window, если state допускает super roof upgrade. Завершение - `Run`.

### Блок 5. Интеграция и проверка

Этот блок включает готовые ordinary и super strategies в bot runtime.

- **5.1. Зарегистрировать ordinary strategy**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
  - **Что меняется:** `CreateStrategies`.
  - **Суть изменения:** добавить `JumpFromRoofStrategy` в общий список strategies. Через этот список она автоматически получает участие в action generation, execution, simulation, retained validation и in-progress projection.

- **5.2. Обновить project files**
  - **Файл:** `LostCyberHamster/Assembly-CSharp.csproj`
  - **Что меняется:** `<Compile Include="...">` entries.
  - **Суть изменения:** добавить новые runtime scripts, если Unity еще не обновила project files. `.meta` для новых `Assets/` scripts не писать вручную; их должен сгенерировать Unity import.

- **5.3. Зарегистрировать super strategy**
  - **Файл:** `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
  - **Что меняется:** `CreateStrategies`.
  - **Суть изменения:** добавить `SuperJumpFromRoofStrategy` рядом с `JumpFromRoofStrategy`, чтобы super-вариант участвовал в action generation, execution, simulation, retained validation и in-progress projection через общий список strategies.
