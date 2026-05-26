# Jump On Strategies

## Цель
Добавить семейство стратегий `JumpOnStrategy` / `SuperJumpOnStrategy` для ситуации, когда хомяк бежит по дороге и ближайший same-line `smallAlive` можно безопасно уничтожить прыжком сверху. Стратегии должны строить отдельные `JumpOn` / `SuperJumpOn` actions, подтверждать окно запуска через runtime-equivalent resolver и симулировать удаление target obstacle из planning-состояния.

## Шаги реализации

### 1. Добавить action kinds для jump-on семьи
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/PlanState/BotActionKind.cs`  
**Что меняется:** enum `BotActionKind`.  
**Суть изменения:** добавить `JumpOn` и `SuperJumpOn`. Отдельные kinds нужны, потому что runtime outcome `JumpOnObstacle` / `SuperJumpOnObstacle` уничтожает `smallAlive` и возвращает хомяка в `Run`; это не `JumpOver`, где obstacle остается в мире и результатом считается перелет.

### 2. Добавить classifier predicate для ground jump-on target
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Planning/ObstacleClassifier.cs`  
**Что меняется:** новый метод `CanJumpOnGroundObstacle(ObstacleTypeEnum obstacleType)`.  
**Суть изменения:** метод возвращает `true` только для `ObstacleTypeEnum.smallAlive`. Это дает единый источник доменного правила для specification и policies, не смешивая jump-on с уже существующим `CanJumpOverOnGround`, где `smallAlive` остается допустимым для safe-over outcome.

### 3. Создать policy contract семьи
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/IJumpOnPolicy.cs`  
**Что меняется:** новый interface `IJumpOnPolicy`.  
**Суть изменения:** contract задает `ActionKind`, `EnergyCost`, `DescriptionPrefix`, `LogTag`, `ExpectedJumpOnState`, `TryGetTravel(out float travel)`, `GetResolveInput(...)` и `Resolve(...)`. Shared-код будет одинаково рассчитывать окно, target и simulation, а обычная и super-версии будут отличаться только runtime policy.

### 4. Описать модель jump-on окна
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnWindowModel.cs`  
**Что меняется:** новый readonly struct `JumpOnWindowModel`.  
**Суть изменения:** хранить `TargetObstacle`, `TargetObstacleIndex`, `FirstFireShift`, `LastFireShift` и `SelectedFireShift`. Модель нужна retained validator'у и strategy facade, чтобы не пересчитывать смысл окна из разрозненных float-значений.

### 5. Проверить применимость jump-on action
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnSpecification.cs`  
**Что меняется:** новый class `JumpOnSpecification`.  
**Суть изменения:** strategy применима только если хомяк не на крыше, не меняет линию, имеет достаточно энергии, decision chain существует, а первый obstacle chain является same-line `smallAlive`. Target должен быть первым obstacle, потому что `JumpOutcomeResolver` и `SuperJumpOutcomeResolver` обходят obstacles слева направо и возвращают jump-on outcome по первому подходящему `smallAlive`.

### 6. Рассчитать fire window для центра хомяка внутри smallAlive
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnWindowCalculator.cs`  
**Что меняется:** новый static class `JumpOnWindowCalculator`.  
**Суть изменения:** вычислять окно запуска по runtime-условию `smallAlive`: после полного action travel центр хомяка должен оказаться внутри X-интервала target, с тем же правым допуском `hamster.Width * 0.2f`, который используют `JumpOutcomeResolver` и `SuperJumpOutcomeResolver`. Правая граница дополнительно ограничивается pre-fire контактом с target (`target.LeftX - hamster.HamsterRightX`) и общим `JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin()`.

### 7. Подтвердить окно через runtime resolver
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnFireWindowFinder.cs`  
**Что меняется:** новый class `JumpOnFireWindowFinder`.  
**Суть изменения:** finder строит `JumpObstacleData` через `JumpObstacleProjection`, переводит planning fire shift в runtime resolver input через `policy.GetResolveInput(...)`, вызывает policy resolver и принимает candidate только если `State == ExpectedJumpOnState` и `TargetIndex == TargetObstacleIndex`. Это защищает план от формулы, которая не учла порядок obstacles или фактические resolver-guards.

### 8. Добавить planning-переход после удаления target
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/Simulation/PlanningStateTransition.cs`  
**Что меняется:** новый метод `AdvanceAfterTargetRemoval(...)`.  
**Суть изменения:** метод должен работать как `Advance(...)`, но начинать поиск следующего obstacle минимум с `action.TargetObstacleIndex + 1`. Это отражает runtime: `transform_jumped_on` вызывает `DestroyObstacleEvent`, и `smallAlive`, на которого напрыгнули, больше не должен блокировать следующий planning step.

### 9. Симулировать успешный jump-on
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnSimulator.cs`  
**Что меняется:** новый class `JumpOnSimulator`.  
**Суть изменения:** после успешного action planning-state возвращает хомяка в `Run`, списывает energy через существующую модель `ApplyRunAfterOver(...)`, продвигает мир на `completionWorldShift` и пропускает уничтоженный target через `AdvanceAfterTargetRemoval(...)`. `ProjectInProgress(...)` должен использовать `InProgressProjectionHelper.Project(..., skipTargetObstacleAfterCompletion: true)`.

### 10. Добавить retained validator
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnRetainedActionValidator.cs`  
**Что меняется:** новый class `JumpOnRetainedActionValidator`.  
**Суть изменения:** validator повторно проверяет, что retained action все еще направлен в первый `smallAlive` текущей decision chain, пересчитывает актуальное fire window, восстанавливает оставшийся fire shift по trigger/target instance id и заново подтверждает outcome через `JumpOnFireWindowFinder`. Это не дает сохраненному action пережить сдвиг окна или смену target.

### 11. Добавить ordinary policy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOn/JumpOnPolicy.cs`  
**Что меняется:** новый class `JumpOnPolicy`.  
**Суть изменения:** policy задает `BotActionKind.JumpOn`, стоимость `10`, expected state `HamsterStateEnum.JumpOnObstacle`, travel по клипу `transform_jump`, resolver `JumpOutcomeResolver.ResolveJump` и `GetResolveInput(...)` без дополнительного смещения.

### 12. Добавить ordinary strategy facade
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOn/JumpOnStrategy.cs`  
**Что меняется:** новый class `JumpOnStrategy`.  
**Суть изменения:** facade собирает `JumpOnPolicy`, `JumpOnSpecification`, `JumpOnFireWindowFinder`, `JumpOnSimulator`, retained validator и executor. `PlannedAction` должен trigger'иться по target `smallAlive`, хранить target instance id, `postFireWorldShift = travel`, `completionWorldShift = fireShift + travel` и описание `"Jump on smallAlive"`.

### 13. Добавить ordinary executor
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOn/JumpOnExecutor.cs`  
**Что меняется:** новый class `JumpOnExecutor`.  
**Суть изменения:** executor принимает только `BotActionKind.JumpOn`, требует `Run`, достаточную energy и live trigger через `ActionTriggerGate`, затем вызывает `hamster.JumpRequest.Invoke()`. Завершение action подтверждается возвратом runtime state в `Run`, как у `JumpOver`, но с отдельным kind и логированием `JumpOn`.

### 14. Добавить super policy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOn/SuperJumpOnPolicy.cs`  
**Что меняется:** новый class `SuperJumpOnPolicy`.  
**Суть изменения:** policy задает `BotActionKind.SuperJumpOn`, стоимость `20`, expected state `HamsterStateEnum.SuperJumpOnObstacle`, action travel как `transform_super_jump + DoubleJumpDetector.DoubleJumpThreshold / 2 * Consts.GameSpeedBase`, resolver `SuperJumpOutcomeResolver.ResolveSuperJump` и `GetResolveInput(...)`, который сдвигает resolver fire point на задержку второго input.

### 15. Добавить super strategy facade
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOn/SuperJumpOnStrategy.cs`  
**Что меняется:** новый class `SuperJumpOnStrategy`.  
**Суть изменения:** facade использует тот же shared stack `JumpOnSpecification` / `JumpOnFireWindowFinder` / `JumpOnSimulator`, но с `SuperJumpOnPolicy` и super executor. Отличия super-версии не должны дублировать расчет target window.

### 16. Добавить super executor
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOn/SuperJumpOnExecutor.cs`  
**Что меняется:** новый class `SuperJumpOnExecutor`.  
**Суть изменения:** executor повторяет двухфазный input road super-jump стратегий: сначала `JumpRequest`, затем через половину `DoubleJumpDetector.DoubleJumpThreshold` вызывает `SuperJumpRequest`, если runtime state допускает upgrade (`Jump`, `JumpOver`, `JumpOnObstacle`, `JumpOnRoof`, jump-damage states). Завершение - возврат в `Run`.

### 17. Зарегистрировать стратегии в bot pipeline
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`  
**Что меняется:** using-директивы и `CreateStrategies()`.  
**Суть изменения:** добавить `new JumpOnStrategy()` и `new SuperJumpOnStrategy()` рядом с ground jump families, чтобы новые actions участвовали в generation, simulation, retained validation, in-progress projection и execution.

### 18. Обновить project files после новых scripts
**Файл:** `LostCyberHamster/Assembly-CSharp.csproj`  
**Что меняется:** `<Compile Include="...">` entries для новых `.cs`.  
**Суть изменения:** после создания scripts проверить, что Unity/solution видит новые файлы. Если Unity еще не импортировала assets, добавить compile entries в `.csproj`; `.meta` для новых scripts не писать руками, их должен создать Unity import.

### 19. Добавить целевые test patterns
**Файл:** `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`  
**Что меняется:** новые patterns `test_jump_on_*` и `test_super_jump_on_*`.  
**Суть изменения:** добавить минимальные road-сценарии с `ObstacleTypeEnum.smallAlive` на верхней и нижней линии, а также вариант, где `JumpOn` и `JumpOver` оба потенциально доступны, чтобы ручной прогон показал фактический выбор evaluator'а.

### 20. Добавить целевые test levels
**Файл:** `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_jump_on/test_jump_on.json`  
**Что меняется:** новый reference-based test level для ordinary jump-on.  
**Суть изменения:** уровень должен ссылаться на `test_jump_on_*` patterns и проверяться пользователем вручную через стандартный bot test flow.

### 21. Добавить целевой super test level
**Файл:** `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_super_jump_on/test_super_jump_on.json`  
**Что меняется:** новый reference-based test level для super jump-on.  
**Суть изменения:** уровень должен ссылаться на `test_super_jump_on_*` patterns и отделять проверку super timing от ordinary timing.

### 22. Уточнить jump constraints
**Файл:** `docs/Planning/level_design_jump_constraints.md`  
**Что меняется:** строки `Jump On -` и `Super Jump On -`.  
**Суть изменения:** зафиксировать, что обе стратегии целятся в один `smallAlive`: runtime уничтожает только target obstacle, а не всю chain. Если после реализации выяснится, что super-вариант безопасно покрывает дополнительный road obstacle без отдельной стратегии, это нужно доказать отдельным runtime-прогоном и не предполагать по длине клипа.

### 23. Проверить целевой сценарий
**Файл:** `docs/Planning/in-progress/jump-on-strategies-checklist.md`  
**Что меняется:** статусы checklist после реализации.  
**Суть изменения:** агент после code changes проверяет статический diff/project-file consistency; compile/recompile и запуск test levels выполняются только по явному запросу пользователя. Ручная runtime-проверка остается за пользователем на `test_jump_on` и `test_super_jump_on`.
