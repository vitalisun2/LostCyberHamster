# Jump From Roof On Roof Strategies

## Цель
Добавить семейство стратегий `JumpFromRoofOnRoof` / `SuperJumpFromRoofOnRoof` для ситуации, когда хомяк бежит по `bigNotAlive` или `mediumNotAlive`, впереди есть следующая крыша, но пассивный `RoofRun` не может перейти на нее из-за gap/препятствий между крышами. Стратегии должны планировать roof jump до автоматического схода и подтверждать успех через runtime-equivalent roof-jump resolver.

## Шаги реализации

### 1. Добавить action kinds для семьи
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/PlanState/BotActionKind.cs`
**Что меняется:** enum `BotActionKind`.
**Суть изменения:** добавить `JumpFromRoofOnRoof` и `SuperJumpFromRoofOnRoof`. Отдельные kinds нужны, потому что это не `JumpFromRoof` на дорогу и не `RoofJumpOver` над occupant'ом на текущей крыше: успешный outcome возвращает `RoofRun` на новой roof support.

### 2. Создать policy contract семьи
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/IJumpFromRoofOnRoofPolicy.cs`
**Что меняется:** новый interface `IJumpFromRoofOnRoofPolicy`.
**Суть изменения:** contract задает `ActionKind`, `EnergyCost`, `DescriptionPrefix`, `ExpectedSuccessState`, `TryGetTravel(out JumpFromRoofOnRoofTravel travel)` и `Resolve(...)`. Это сохраняет существующий паттерн jump-семейств: common-код не знает, обычный это roof jump или super roof jump.

### 3. Описать runtime travel
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/JumpFromRoofOnRoofTravel.cs`
**Что меняется:** новый readonly struct `JumpFromRoofOnRoofTravel`.
**Суть изменения:** хранить `RunFromRoofTravel`, `RoofJumpTravel` и `JumpFromRoofTravel`. `RunFromRoofTravel` нужен для отсечения безопасного автоматического схода, `RoofJumpTravel` - для посадки на следующую roof, `JumpFromRoofTravel` - для resolver-проверки road obstacles между крышами.

### 4. Проверить применимость roof-to-roof действия
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/JumpFromRoofOnRoofSpecification.cs`
**Что меняется:** новый class `JumpFromRoofOnRoofSpecification`.
**Суть изменения:** strategy применима только из `RoofRun`, при наличии roof support, без lane shift, с достаточной энергией и при наличии последней passive roof. Спецификация должна найти reachable target roof после `lastRoof`, убедиться, что это не passive continuation, и не планировать действие, если gap до первой blocking obstacle/target roof безопасно покрывается `RunFromRoof`.

### 5. Смоделировать gap между крышами
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/JumpFromRoofOnRoofGapModel.cs`
**Что меняется:** новый readonly struct `JumpFromRoofOnRoofGapModel`.
**Суть изменения:** хранить `lastRoof`, `targetRoof`, world index target roof, первый blocking obstacle после `lastRoof`, количество non-roof obstacles в gap и правую границу covered gap. Модель отделяет доменную ситуацию "прыжок на следующую крышу" от обычной `ObstacleChain`, где target roof может не быть первым obstacle.

### 6. Рассчитать fire window для roof-to-roof jump
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/JumpFromRoofOnRoofWindowCalculator.cs`
**Что меняется:** новый static class `JumpFromRoofOnRoofWindowCalculator`.
**Суть изменения:** вычислять окно запуска как пересечение трех ограничений: успеть до автоматического `RunFromRoof`, не получить pre-fire collision с первым non-roof obstacle в gap, и попасть на target roof по `RoofJumpTravel`. Если в gap есть road obstacles, левая граница окна также должна гарантировать, что `JumpFromRoofTravel` перелетает их правую границу.

### 7. Подтвердить окно через runtime resolver
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/JumpFromRoofOnRoofFireWindowFinder.cs`
**Что меняется:** новый class `JumpFromRoofOnRoofFireWindowFinder`.
**Суть изменения:** finder строит shifted obstacle snapshot через `JumpObstacleProjection`, формирует `RoofJumpResolveContext` и вызывает policy resolver. Candidate валиден только если resolver возвращает `ExpectedSuccessState` и target index указывает на найденную target roof.

### 8. Симулировать успешную посадку на новую roof
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/JumpFromRoofOnRoofSimulator.cs`
**Что меняется:** новый class `JumpFromRoofOnRoofSimulator`.
**Суть изменения:** после успешного action planning-state должен остаться в `RoofRun`, списать энергию и записать `ResultRoofSupportInstanceId` новой крыши. Для advance использовать тот же смысл, что `RoofJumpOverSimulator`: target roof уже покрыта действием, дальнейший план начинается после нее.

### 9. Добавить retained validator
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpFromRoofOnRoof/JumpFromRoofOnRoofRetainedActionValidator.cs`
**Что меняется:** новый class `JumpFromRoofOnRoofRetainedActionValidator`.
**Суть изменения:** validator повторно находит актуальный gap model, восстанавливает оставшийся fire shift по `TriggerObstacleInstanceId`, проверяет границы актуального окна и снова подтверждает outcome resolver-ом. Это не дает сохраненному action обойти semantic applicability после сдвига мира.

### 10. Добавить ordinary policy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoofOnRoof/JumpFromRoofOnRoofPolicy.cs`
**Что меняется:** новый class `JumpFromRoofOnRoofPolicy`.
**Суть изменения:** policy задает `BotActionKind.JumpFromRoofOnRoof`, стоимость обычного roof jump, expected state `HamsterStateEnum.RoofJump`, travel по `transform_run_from_roof`, `transform_roof_jump`, `transform_jump_from_roof` с medium fallback и resolver `RoofJumpOutcomeResolver.ResolveRoofJump`.

### 11. Добавить ordinary strategy facade
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoofOnRoof/JumpFromRoofOnRoofStrategy.cs`
**Что меняется:** новый class `JumpFromRoofOnRoofStrategy`.
**Суть изменения:** facade собирает policy, specification, fire-window finder, simulator, retained validator и executor. `PlannedAction` должен trigger'иться по первому blocking obstacle в gap, а если препятствий в gap нет - по target roof; target/result roof support указывают на target roof.

### 12. Добавить ordinary executor
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpFromRoofOnRoof/JumpFromRoofOnRoofExecutor.cs`
**Что меняется:** новый class `JumpFromRoofOnRoofExecutor`.
**Суть изменения:** executor принимает только `BotActionKind.JumpFromRoofOnRoof`, требует `RoofRun` и энергию, проходит `ActionTriggerGate`, вызывает `hamster.RoofJumpRequest.Invoke()` и завершает action при возврате в `RoofRun`.

### 13. Добавить super policy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoofOnRoof/SuperJumpFromRoofOnRoofPolicy.cs`
**Что меняется:** новый class `SuperJumpFromRoofOnRoofPolicy`.
**Суть изменения:** policy задает `BotActionKind.SuperJumpFromRoofOnRoof`, expected state `HamsterStateEnum.SuperRoofJump`, travel по `transform_run_from_roof`, `transform_super_roof_jump`, `transform_super_jump_from_roof` с medium fallback и upgrade-delay, resolver `SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump`.

### 14. Добавить super strategy facade
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoofOnRoof/SuperJumpFromRoofOnRoofStrategy.cs`
**Что меняется:** новый class `SuperJumpFromRoofOnRoofStrategy`.
**Суть изменения:** facade использует тот же shared stack, что ordinary strategy, но с super policy и super executor. Отличия super-версии не должны дублировать расчет gap/window.

### 15. Добавить super executor
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpFromRoofOnRoof/SuperJumpFromRoofOnRoofExecutor.cs`
**Что меняется:** новый class `SuperJumpFromRoofOnRoofExecutor`.
**Суть изменения:** executor повторяет двухфазный input существующих super roof strategies: сначала `RoofJumpRequest`, затем через половину `DoubleJumpDetector.DoubleJumpThreshold` вызывает `SuperRoofJumpRequest`, если состояние допускает upgrade. Завершение - возврат в `RoofRun`.

### 16. Зарегистрировать стратегии
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
**Что меняется:** `CreateStrategies()` и using-директивы.
**Суть изменения:** добавить ordinary и super roof-to-roof strategies рядом с существующими roof-from families, чтобы они участвовали в generation, execution, simulation, retained validation и in-progress projection.

### 17. Обновить project files после новых scripts
**Файл:** `LostCyberHamster/Assembly-CSharp.csproj`
**Что меняется:** `<Compile Include="...">` entries для новых `.cs`.
**Суть изменения:** если Unity еще не импортировала новые скрипты, добавить compile entries вручную. `.meta` для новых scripts не писать руками; их должен создать Unity import.

### 18. Проверить целевой сценарий
**Файл:** `docs/Planning/level_design_jump_constraints.md`
**Что меняется:** только при необходимости уточнить статус `Super Jump From Roof On Roof`.
**Суть изменения:** ручная runtime-проверка остается за пользователем на уровне `01_New_York/Morning/test_jump_from_roof_to_roof`; агент после правок проверяет только статический diff/project-file consistency, если пользователь явно не попросит Unity recompile/autotest.
