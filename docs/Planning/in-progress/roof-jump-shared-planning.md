# Shared Jump On Roof Planning

## Цель
Вынести общую planning-логику `JumpOnRoof` и `SuperJumpOnRoof` в shared-слой `Shared/JumpPlanning`, оставив runtime execution в конкретных стратегиях. Общие классы получают префикс `JumpOnRoof`, чтобы явно показывать домен «запрыгивание на крышу», а различия между обычным jump и super jump передаются через policy.

## Шаги реализации

### 1. Создать policy-контракт jump-on-roof
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/IJumpOnRoofPolicy.cs`  
**Что меняется:** добавляется новый контракт `IJumpOnRoofPolicy`.  
**Суть изменения:** контракт описывает различия между `JumpOnRoof` и `SuperJumpOnRoof`: `BotActionKind`, `EnergyCost`, diagnostic tag, description prefix, expected roof state, флаг `damageBigAliveWithoutYByReach`, расчёт travel и вызов runtime resolver. Shared-классы зависят от этого контракта, а не от конкретных стратегий.

### 2. Добавить policy обычного jump-on-roof
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofPolicy.cs`  
**Что меняется:** добавляется конкретная policy для `JumpOnRoof`.  
**Суть изменения:** policy возвращает `BotActionKind.JumpOnRoof`, `EnergyCost = 10`, expected state `HamsterStateEnum.JumpOnRoof`, `damageBigAliveWithoutYByReach: true`, считает travel по клипу `transform_jump` и вызывает `JumpOutcomeResolver.ResolveJump`.

### 3. Добавить policy super-jump-on-roof
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofPolicy.cs`  
**Что меняется:** добавляется конкретная policy для `SuperJumpOnRoof`.  
**Суть изменения:** policy возвращает `BotActionKind.SuperJumpOnRoof`, `EnergyCost = 20`, expected state `HamsterStateEnum.SuperJumpOnRoof`, `damageBigAliveWithoutYByReach: false`, считает travel по `transform_super_jump + upgradeDelayTravel` и вызывает `SuperJumpOutcomeResolver.ResolveSuperJump`.

### 4. Вынести общий fire-window finder
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOnRoofFireWindowFinder.cs`  
**Что меняется:** добавляется shared finder на основе текущих `JumpOnRoofFireWindowFinder` и `SuperJumpOnRoofFireWindowFinder`.  
**Суть изменения:** finder содержит общий алгоритм: найти первую roof target в chain, проверить damaging roof occupant, посчитать `first/last fire shift`, сузить окно через `JumpPlanningConstants.FireWindowBoundaryMargin`, выбрать fire shift и проверить runtime outcome через `IJumpOnRoofPolicy`. Конкретные finder-классы в стратегиях после переноса становятся не нужны. На время миграции учитывать конфликт простых имён с текущим `Strategies/JumpOnRoof/JumpOnRoofFireWindowFinder`: в strategy использовать namespace/alias до удаления старого класса.

### 5. Вынести общий retained action validator
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOnRoofRetainedActionValidator.cs`  
**Что меняется:** добавляется shared validator вместо дублирующихся `JumpOnRoofRetainedActionValidator` и `SuperJumpOnRoofRetainedActionValidator`.  
**Суть изменения:** validator принимает `IJumpOnRoofPolicy` и shared `JumpOnRoofFireWindowFinder`, проверяет `ActionKind`, текущую roof target в chain, оставшийся shift до trigger obstacle и runtime outcome. Поведение сохраняется, но код существует в одной реализации.

### 6. Вынести общий simulator
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOnRoofSimulator.cs`  
**Что меняется:** добавляется shared simulator вместо дублирующихся simulator-классов.  
**Суть изменения:** simulator принимает `IJumpOnRoofPolicy`, проверяет `policy.ActionKind` и выполняет общий planning-переход через `PlanningStateTransition.ApplyRoofRunAfterLanding`, `AdvanceAfterRoofLanding` и `InProgressProjectionHelper.Project`.

### 7. Вынести общую specification
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOnRoofSpecification.cs`  
**Что меняется:** добавляется shared specification вместо двух одинаковых классов с разным energy cost.  
**Суть изменения:** specification принимает `IJumpOnRoofPolicy` и проверяет общий набор условий: hamster не на крыше, не shifting, не damaged и energy не меньше `policy.EnergyCost`.

### 8. Обновить JumpOnRoofStrategy как тонкий адаптер
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofStrategy.cs`  
**Что меняется:** strategy больше не хранит strategy-local `JumpOnRoofSpecification`, `JumpOnRoofFireWindowFinder`, `JumpOnRoofSimulator`; вместо них использует shared `JumpOnRoofSpecification`, `JumpOnRoofFireWindowFinder`, `JumpOnRoofSimulator`, `JumpOnRoofRetainedActionValidator` из namespace `Shared.JumpPlanning` с `JumpOnRoofPolicy`.  
**Суть изменения:** strategy сохраняет конкретный executor `JumpOnRoofExecutor` и публичную роль `IPlanningStrategy`, но planning-логика переиспользуется из shared-слоя.

### 9. Обновить SuperJumpOnRoofStrategy как тонкий адаптер
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofStrategy.cs`  
**Что меняется:** strategy подключает те же shared planning-классы, но передаёт `SuperJumpOnRoofPolicy` и сохраняет `SuperJumpOnRoofExecutor`.  
**Суть изменения:** executor остаётся единственным крупным отличием, потому что именно он реализует двухфазный runtime input: `JumpRequest`, задержка и `SuperJumpRequest`.

### 10. Удалить заменённые классы стратегий
**Файлы:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofFireWindowFinder.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofRetainedActionValidator.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofSimulator.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnRoof/JumpOnRoofSpecification.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofFireWindowFinder.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofRetainedActionValidator.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofSimulator.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnRoof/SuperJumpOnRoofSpecification.cs`  
**Что меняется:** удаляются классы, чья логика полностью перенесена в shared-слой.  
**Суть изменения:** удаление безопасно после переключения стратегий на shared-классы; необходимо обновить `Assembly-CSharp.csproj` и дождаться генерации/удаления Unity `.meta` файлов через Editor, если Unity участвует в cleanup.

### 11. Проверить references и compile errors
**Файлы:** `LostCyberHamster/Assembly-CSharp.csproj`, все затронутые `.cs` файлы в `JumpOnRoof`, `SuperJumpOnRoof`, `Shared/JumpPlanning`.  
**Что меняется:** ссылки на удалённые классы, `using`, namespace и project compile include.  
**Суть изменения:** после переноса не должно остаться ссылок на старые concrete finder/validator/simulator/specification. Новые shared-файлы должны быть включены в `.csproj`, а `using` должны использовать обычные namespace без `global::`.

## Риски и проверки

- Главный риск — слишком толстая `IJumpOnRoofPolicy`. Если policy начнёт содержать не только различия, но и общий алгоритм, её нужно разделить на smaller contracts.
- Второй риск — потеря читаемости concrete strategies. `JumpOnRoofStrategy` и `SuperJumpOnRoofStrategy` должны остаться понятными входными точками с явным executor и policy.
- На время миграции возможны конфликты простых имён (`JumpOnRoofFireWindowFinder`, `JumpOnRoofSpecification` и т.д.) между strategy-local namespace и shared namespace. Решение: сначала подключать shared-типы через namespace alias/полную квалификацию, затем удалить заменённые local-классы.
- Executor не переносить в shared-слой: однофазный и двухфазный runtime input имеют разное поведение.
- После создания новых `.cs` файлов нужно обновить `Assembly-CSharp.csproj`; Unity `.meta` вручную не писать.
- Unity recompile запускать только по явному запросу пользователя; для создания плана compile не требуется.
