# Roof Jump On Road Targets

## Цель
Добавить семейство стратегий, которое планирует напрыгивание с крыши на дорожные `smallAlive` и `bigAlive` при сходе с крыши на дорогу. Planner должен использовать уже существующие runtime outcome `JumpOnObstacleFromRoof` / `SuperJumpOnObstacleFromRoof`, подтверждать target через roof-jump resolver, симулировать удаление target obstacle и возвращение хомяка в `Run`.

## Шаги реализации

### 1. Добавить action kinds для roof-to-road jump-on
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/PlanState/BotActionKind.cs`  
**Что меняется:** enum `BotActionKind`.  
**Суть изменения:** добавить `JumpFromRoofOnObstacle` и `SuperJumpFromRoofOnObstacle`. Отдельные kinds нужны, потому что runtime outcome уничтожает `smallAlive` / `bigAlive` при сходе с крыши, а существующий `JumpFromRoof` означает чистый сход на дорогу без удаления target.

### 2. Добавить classifier predicate для roof jump-on target
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Planning/ObstacleClassifier.cs`  
**Что меняется:** новый метод `CanJumpOnFromRoofObstacle(ObstacleTypeEnum obstacleType)`.  
**Суть изменения:** метод возвращает `true` для `smallAlive` и `bigAlive`. Это отделяет target-семантику roof jump-on от общего `DamagesOnGroundContact` и не меняет существующие правила `JumpFromRoof`, `JumpOver` и `JumpOn`.

### 3. Создать policy contract семьи
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnObstacle/IJumpFromRoofOnObstaclePolicy.cs`  
**Что меняется:** новый interface `IJumpFromRoofOnObstaclePolicy` и travel struct `JumpFromRoofOnObstacleTravel`.  
**Суть изменения:** contract хранит `ActionKind`, `EnergyCost`, `DescriptionPrefix`, `LogTag`, `ExpectedJumpOnState`, `TryGetTravel(out JumpFromRoofOnObstacleTravel)` и `Resolve(...)`. Travel разделяет `RunFromRoofTravel`, `RoofJumpTravel`, `ResolveTravel`, `ActionTravel` и `ResolveFireShiftOffset`, потому что resolver использует `transform_jump_from_roof` / `transform_super_jump_from_roof`, а полное jump-on действие завершается по отдельным clips `transform_jump_on_from_roof` / `transform_super_jump_on_obstacle_from_roof`.

### 4. Описать модель roof jump-on окна
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleWindowModel.cs`  
**Что меняется:** новый readonly struct `JumpFromRoofOnObstacleWindowModel`.  
**Суть изменения:** хранить `TargetObstacle`, `TargetObstacleIndex`, `TargetObstacleChainIndex`, `LastRoof`, `FirstFireShift`, `LastFireShift` и `SelectedFireShift`. Модель делает retained validation и strategy facade такими же явными, как у `JumpOnWindowModel` и roof-to-roof finder.

### 5. Проверить применимость roof jump-on
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleSpecification.cs`  
**Что меняется:** новый class `JumpFromRoofOnObstacleSpecification`.  
**Суть изменения:** strategy применима только в `RoofRun`, при `IsOnRoof`, известном `RoofSupportInstanceId`, отсутствии shift-а и достаточной энергии. В текущей decision chain ищется первый same-line target из `CanJumpOnFromRoofObstacle`; `RoofRunProjection.TryFindLastPassiveRoof(...)` должен найти последнюю крышу, а gap до target должен быть меньше `RunFromRoofTravel`, иначе обычный автоматический сход не опасен и road-стратегии смогут обработать target позже.

### 6. Рассчитать fire-window для центра хомяка внутри target
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleWindowCalculator.cs`  
**Что меняется:** новый static class `JumpFromRoofOnObstacleWindowCalculator`.  
**Суть изменения:** окно считается по тому же runtime-условию, что в `RoofJumpOutcomeResolver`: в resolver-точке центр хомяка должен быть внутри X-интервала target, правая граница расширяется на `hamster.Width * 0.2f`, а `bigAlive` дополнительно расширяет target-интервал на `30%` ширины obstacle влево и вправо. Окно пересекается с лимитом конца passive roof-run и pre-target clearance, затем сужается через `JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin()`.

### 7. Подтвердить окно через roof-jump resolver
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleFireWindowFinder.cs`  
**Что меняется:** новый class `JumpFromRoofOnObstacleFireWindowFinder`.  
**Суть изменения:** finder строит `JumpObstacleData` через `JumpObstacleProjection`, переводит planning fire shift в resolver shift через travel offset, вызывает policy resolver и принимает candidate только если `State == ExpectedJumpOnState` и `TargetIndex == TargetObstacleIndex`. Это защищает от неверного порядка obstacles и от случаев, где resolver вернул `JumpFromRoofDamage`, `RoofJump` или обычный `JumpFromRoof`.

### 8. Обобщить post-action safety после удаления target
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/TargetRemovalPostActionSafety.cs`  
**Что меняется:** новый shared helper.  
**Суть изменения:** перенести смысл `JumpOnPostActionSafety.IsSafeAfterCompletion(...)` в общий helper для действий, которые уничтожают target и возвращают хомяка в `Run`. Этот helper нужен и ground `JumpOn`, и новому roof-to-road jump-on; знание о safety после удаления target должно быть в одном месте.

### 9. Перевести ordinary ground JumpOn на общий safety helper
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOn/JumpOnStrategy.cs`  
**Что меняется:** вызов post-action safety перед созданием action.  
**Суть изменения:** заменить обращение к `JumpOnPostActionSafety` на `TargetRemovalPostActionSafety`, не меняя поведение `JumpOn`. Это подтверждает, что новый helper эквивалентен существующей проверке.

### 10. Перевести retained ground JumpOn на общий safety helper
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnRetainedActionValidator.cs`  
**Что меняется:** финальная проверка безопасности retained action.  
**Суть изменения:** заменить вызов `JumpOnPostActionSafety.IsSafeAfterCompletion(...)` на общий helper. Retained validation должна остаться такой же, но больше не будет привязана к ground-only имени helper-а.

### 11. Удалить ground-only safety wrapper
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOn/JumpOnPostActionSafety.cs`  
**Что меняется:** удалить файл после переноса call sites.  
**Суть изменения:** удаление безопасно после шагов 9-10, потому что весь прежний контракт переезжает в `TargetRemovalPostActionSafety`, а иных ссылок на старый helper быть не должно.

### 12. Симулировать успешный roof jump-on
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleSimulator.cs`  
**Что меняется:** новый class `JumpFromRoofOnObstacleSimulator`.  
**Суть изменения:** после успешного action planning-state возвращает хомяка в `Run`, списывает энергию через `PlanningStateTransition.ApplyRunAfterOver(...)`, продвигает мир на `completionWorldShift` и пропускает уничтоженный target через `AdvanceAfterTargetRemoval(...)`. `ProjectInProgress(...)` использует `InProgressProjectionHelper.Project(..., skipTargetObstacleAfterCompletion: true)`.

### 13. Добавить retained validator
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleRetainedActionValidator.cs`  
**Что меняется:** новый class `JumpFromRoofOnObstacleRetainedActionValidator`.  
**Суть изменения:** validator повторяет specification-gate, переходит к тому же target inside-chain, восстанавливает remaining fire shift по trigger/target instance id, проверяет попадание в актуальное окно, заново подтверждает resolver outcome и прогоняет `TargetRemovalPostActionSafety`. Это не дает сохраненному action пережить смену target или сдвиг окна.

### 14. Добавить ordinary policy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoofOnObstacle/JumpFromRoofOnObstaclePolicy.cs`  
**Что меняется:** новый class `JumpFromRoofOnObstaclePolicy`.  
**Суть изменения:** policy задает `BotActionKind.JumpFromRoofOnObstacle`, стоимость `10`, expected state `HamsterStateEnum.JumpOnObstacleFromRoof`, resolver `RoofJumpOutcomeResolver.ResolveRoofJump(...)`, `ResolveTravel` по `transform_jump_from_roof` и `ActionTravel` по `transform_jump_on_from_roof`. Medium fallback clips использовать по тому же паттерну, что `JumpFromRoofPolicy`.

### 15. Добавить ordinary strategy facade
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleStrategy.cs`  
**Что меняется:** новый class `JumpFromRoofOnObstacleStrategy`.  
**Суть изменения:** facade собирает policy, specification, fire-window finder, simulator, retained validator и executor. `PlannedAction` trigger'ится по первому obstacle chain, target хранит actual `smallAlive` / `bigAlive`, `completionWorldShift = fireShift + travel.ActionTravel`, `postFireWorldShift = travel.ActionTravel`, описание формируется как `"Jump from roof on <target>"`.

### 16. Добавить ordinary executor
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoofOnObstacle/JumpFromRoofOnObstacleExecutor.cs`  
**Что меняется:** новый class `JumpFromRoofOnObstacleExecutor`.  
**Суть изменения:** executor принимает только `BotActionKind.JumpFromRoofOnObstacle`, требует `RoofRun`, достаточную energy и live trigger через `ActionTriggerGate`, затем вызывает `hamster.RoofJumpRequest.Invoke()`. Завершение action подтверждается возвратом runtime state в `Run`, потому что `transform_jump_from_roof_end` переводит `JumpOnObstacleFromRoof` в `Run`.

### 17. Добавить timing super roof jump-on
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoofOnObstacle/SuperJumpFromRoofOnObstacleTiming.cs`  
**Что меняется:** новый static class с `UpgradeDelaySeconds` и `UpgradeDelayTravel`.  
**Суть изменения:** policy и executor должны использовать один и тот же delay второго input. Для roof-to-road варианта взять pattern `SuperJumpFromRoofExecutor`: второй input через половину `DoubleJumpDetector.DoubleJumpThreshold`.

### 18. Добавить super policy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoofOnObstacle/SuperJumpFromRoofOnObstaclePolicy.cs`  
**Что меняется:** новый class `SuperJumpFromRoofOnObstaclePolicy`.  
**Суть изменения:** policy задает `BotActionKind.SuperJumpFromRoofOnObstacle`, стоимость `20`, expected state `HamsterStateEnum.SuperJumpOnObstacleFromRoof`, resolver `SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump(...)`, `ResolveTravel` по `transform_super_jump_from_roof`, `ActionTravel` по `transform_super_jump_on_obstacle_from_roof` и `ResolveFireShiftOffset` из `SuperJumpFromRoofOnObstacleTiming.UpgradeDelayTravel`.

### 19. Добавить super strategy facade
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoofOnObstacle/SuperJumpFromRoofOnObstacleStrategy.cs`  
**Что меняется:** новый class `SuperJumpFromRoofOnObstacleStrategy`.  
**Суть изменения:** facade использует тот же shared stack `JumpFromRoofOnObstacleSpecification` / `FireWindowFinder` / `Simulator`, но с super policy и super executor. Геометрия target window не дублируется между ordinary и super вариантами.

### 20. Добавить super executor
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoofOnObstacle/SuperJumpFromRoofOnObstacleExecutor.cs`  
**Что меняется:** новый class `SuperJumpFromRoofOnObstacleExecutor`.  
**Суть изменения:** executor повторяет двухфазный input `SuperJumpFromRoofExecutor`: сначала `RoofJumpRequest`, затем через `SuperJumpFromRoofOnObstacleTiming.UpgradeDelaySeconds` вызывает `SuperRoofJumpRequest`, если runtime state допускает upgrade (`RoofJump`, `JumpFromRoof`, damage-состояния и `JumpOnObstacleFromRoof`). Завершение - возврат в `Run`.

### 21. Уточнить фильтр ordinary/super jump-on candidates
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`  
**Что меняется:** helper, который сейчас удаляет `SuperJumpOn`, если для того же target есть ordinary `JumpOn`.  
**Суть изменения:** расширить этот же локальный фильтр на пару `JumpFromRoofOnObstacle` / `SuperJumpFromRoofOnObstacle`. Это повторяет текущий подход: ordinary action для того же target предпочтительнее super, потому что дешевле и не требует второго input.

### 22. Зарегистрировать стратегии в bot pipeline
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`  
**Что меняется:** using-директивы и `CreateStrategies()`.  
**Суть изменения:** добавить `new JumpFromRoofOnObstacleStrategy()` и `new SuperJumpFromRoofOnObstacleStrategy()` рядом с roof-to-road jump family, перед generic `JumpFromRoofStrategy()` / `SuperJumpFromRoofStrategy()`. Так specific target-destroying actions участвуют в generation, simulation, retained validation, in-progress projection и execution.

### 23. Обновить project files после новых scripts
**Файл:** `LostCyberHamster/Assembly-CSharp.csproj`  
**Что меняется:** `<Compile Include="...">` entries для новых `.cs`, удаление entry для `JumpOnPostActionSafety.cs`.  
**Суть изменения:** после создания scripts проверить, что solution видит новые файлы. `.meta` для новых scripts не писать руками; их должен создать Unity import.

### 24. Добавить целевые test patterns
**Файл:** `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`  
**Что меняется:** новые patterns `test_jump_from_roof_on_obstacle_*` и `test_super_jump_from_roof_on_obstacle_*`.  
**Суть изменения:** добавить минимальные roof-to-road сценарии для `smallAlive` и `bigAlive`, включая negative variants, где resolver должен вернуть damage или generic roof jump, а новая strategy не должна планировать action.

### 25. Добавить целевые test levels
**Файл:** `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_jump_from_roof_on_obstacle/test_jump_from_roof_on_obstacle.json`  
**Что меняется:** новый reference-based test level для ordinary roof jump-on.  
**Суть изменения:** уровень должен ссылаться на ordinary patterns и проверяться пользователем вручную через стандартный bot test flow.

### 26. Добавить целевой super test level
**Файл:** `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_super_jump_from_roof_on_obstacle/test_super_jump_from_roof_on_obstacle.json`  
**Что меняется:** новый reference-based test level для super roof jump-on.  
**Суть изменения:** уровень должен ссылаться на super patterns и отделять проверку super timing от ordinary timing.

### 27. Уточнить jump constraints
**Файл:** `docs/Planning/level_design_jump_constraints.md`  
**Что меняется:** добавить строки `Jump From Roof On Obstacle` и `Super Jump From Roof On Obstacle`.  
**Суть изменения:** зафиксировать, что действие уничтожает один target `smallAlive` / `bigAlive` на дороге, а остальные препятствия вокруг него только ограничивают окно и безопасность. Количество покрываемых препятствий должно подтверждаться runtime-прогоном, не выводиться только из длины клипа.

## Риски и неопределённости
- Нужен runtime-прогон, чтобы подтвердить фактические `ActionTravel` для `transform_jump_on_from_roof` и `transform_super_jump_on_obstacle_from_roof`; resolver-точка уже доказуемо использует `transform_jump_from_roof` / `transform_super_jump_from_roof`.
- `DecisionPointDetector` менять не планируется: для roof-состояния он уже пропускает passive roof chain и ищет same-line `DamagesOnGroundContact`, куда входят `smallAlive` и `bigAlive`.
- Если manual run покажет, что generic `JumpFromRoof` стабильно выигрывает у target-destroying action в нужном сценарии, это нужно решать отдельной локальной приоритизацией, а не глобальным изменением evaluator-а заранее.
