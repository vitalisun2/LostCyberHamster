# Jump Outcome Fire Window Refactor Plan

Дата: 2026-04-29
Статус: реализовано, ожидает review

## Итог реализации

- `JumpOutcomeFireWindowCalculator` удалён.
- Concrete calculators владеют orchestration своих сценариев: ground jump-over, super jump-over, jump-on-roof, super-jump-on-roof.
- В `Shared/JumpPlanning` остались маленькие building blocks: obstacle projection, outcome matching, fire-shift scan, scheduled fire-shift extraction, diagnostics и узкий retained-validation contract.
- `OutcomeCalculator` больше не раскрывается из concrete calculators.
- `BotStrategyFactory` удалён; список strategies создаётся в `RuntimeBotController`.
- `invoke_run_all_test_levels.ps1` берёт test levels из `Locations` и печатает semantic action summary по каждому уровню.

## Цель

Разобрать `JumpOutcomeFireWindowCalculator` как monster-class и вернуть ответственность туда, где она естественно принадлежит: уникальная логика конкретной ситуации живёт в calculator конкретной strategy, а `Shared/JumpPlanning` содержит только небольшие переиспользуемые building blocks с одним понятным смыслом.

Рефакторинг должен сохранить текущее поведение бота. Это архитектурная чистка, а не изменение физики прыжков, runtime resolver'ов или игровых констант.

## Текущая проблема

`LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/JumpOutcomeFireWindowCalculator.cs` сейчас смешивает несколько уровней ответственности:

- orchestration поиска нового `fireShift`;
- retained validation уже запланированного `PlannedAction`;
- расчёт и логирование fire-window;
- дискретный scan по окну с шагом `0.005`;
- преобразование `WorldSnapshot` в `JumpObstacleData` и shift препятствий;
- вызов runtime resolver'а (`JumpOutcomeResolver` / `SuperJumpOutcomeResolver`);
- проверку exact expected `HamsterState`;
- special-case для road-small chain over result;
- диагностические сообщения.

Из-за этого concrete calculators (`JumpOverFireWindowCalculator`, `SuperJumpOverFireWindowCalculator`, `JumpOnRoofFireWindowCalculator`, `SuperJumpOnRoofFireWindowCalculator`) стали почти пустыми обёртками, хотя именно они должны владеть ситуационно-специфичной логикой.

## Принципы решения

- Strategy-specific логика остаётся рядом со strategy.
- В shared остаётся только реально повторяемая механика, а не универсальный "бог-калькулятор".
- Каждый shared building block отвечает за один смысл: окно, scan, outcome matching, obstacle projection, pre-fire safety, diagnostics.
- Не плодить сущности ради формального SOLID. Новая сущность допустима только если она убирает реальную смешанную ответственность или повторение.
- Не копировать runtime outcome logic в planning: runtime resolver'ы остаются источником истины для исхода прыжка.
- Рефакторинг делать маленькими behavior-preserving шагами, чтобы после каждого шага можно было валидировать test levels.

## Target ownership

### Concrete strategy calculators

Каждый concrete calculator должен стать настоящим владельцем use-case своей strategy:

- `JumpOverFireWindowCalculator`
  - ground jump-over;
  - expected state: `HamsterStateEnum.JumpOver`;
  - resolver: `JumpOutcomeResolver.ResolveJump`;
  - `damageBigAliveWithoutYByReach: true`;
  - обычный выбор interior fire-shift.

- `SuperJumpOverFireWindowCalculator`
  - ground super-jump-over;
  - expected state: `HamsterStateEnum.SuperJumpOver`;
  - resolver: `SuperJumpOutcomeResolver.ResolveSuperJump`;
  - `damageBigAliveWithoutYByReach: false`;
  - обычный выбор interior fire-shift.

- `JumpOnRoofFireWindowCalculator`
  - landing на roof obstacle;
  - expected state: `HamsterStateEnum.JumpOnRoof`;
  - resolver: `JumpOutcomeResolver.ResolveJump`;
  - `damageBigAliveWithoutYByReach: true`;
  - roof landing search window;
  - ground-contact pre-fire safety;
  - latest-shift selection для `BlockingObstacleWithRoofLanding`.

- `SuperJumpOnRoofFireWindowCalculator`
  - super-jump landing на roof obstacle;
  - expected state: `HamsterStateEnum.SuperJumpOnRoof`;
  - resolver: `SuperJumpOutcomeResolver.ResolveSuperJump`;
  - `damageBigAliveWithoutYByReach: false`;
  - roof landing search window;
  - ground-contact pre-fire safety;
  - latest-shift selection для `BlockingObstacleWithRoofLanding`.

Concrete calculator может использовать shared helpers, но не должен отдавать наружу внутренний shared calculator через `OutcomeCalculator`.

### Shared/JumpPlanning building blocks

В `Shared/JumpPlanning` оставить только небольшие блоки, организованные по смыслу:

- **Search window policies**: вычисляют физический диапазон `[firstFireShift, lastFireShift]` для семейства ситуаций.
  - ground jump-over window;
  - roof landing window.

- **Pre-fire safety**: проверяет, может ли hamster безопасно дожить до fire moment до старта action.

- **Obstacle projection/data mapping**: строит `JumpObstacleData` из projected `WorldSnapshot` и умеет применить `fireShift` к obstacle coordinates.

- **Exact outcome matching**: вызывает runtime resolver, сравнивает resolved state/target и содержит правило допустимого road-small chain over result.

- **Fire-shift interval scan/selection**: перебирает окно, собирает интервалы exact outcome и выбирает точку внутри интервала с учётом latest-fire safety budget.

- **Diagnostics**: опционально пишет диагностические события, но не смешивается с самим scan/match алгоритмом.

Если building block используется только одним concrete calculator и не упрощает чтение, он не обязан оставаться отдельным shared-классом.

## План реализации

### 0. Baseline и ограничения

- Зафиксировать текущие затронутые уровни для проверки: `test_switch_lane`, `test_jump_over`, `test_superjump_over`, `test_jump_on_roof`, `test_super_jump_on_roof`.
- Не менять `JumpOutcomeResolver`, `SuperJumpOutcomeResolver`, `JumpResolveContext`, `JumpClipTravel`, animation clip travel и физические константы.
- Не менять selection-семантику: `preferLatestFireShift` остаётся только roof-entry сценарием.
- Не менять special-case road-small chain matching без отдельного gameplay-решения.

### 1. Убрать лишний `BotStrategyFactory`

- Перенести создание списка strategies прямо в `RuntimeBotController.Awake()` или приватный метод `RuntimeBotController`.
- Удалить `LostCyberHamster/Assets/Scripts/Bot/Strategies/BotStrategyFactory.cs` и `.meta`.
- Обновить `Assembly-CSharp.csproj` после Unity regenerate.

Причина: класс используется в одном месте и не добавляет вариативности, DI или test seam. Сейчас это лишняя навигационная сущность.

### 2. Выделить shared helpers из monster-class без смены поведения

Сначала вынести механические части из `JumpOutcomeFireWindowCalculator`, не меняя public API:

- obstacle data mapping и shifted obstacle build;
- exact outcome matching через resolver;
- road-small chain target match rule;
- fire-shift interval scan/selection;
- diagnostics adapter/null diagnostics.

После шага monster-class ещё может существовать, но его методы должны стать тонкой orchestration-обвязкой над helpers. Это уменьшит риск перед переносом логики в concrete calculators.

### 3. Сделать concrete calculators настоящими владельцами use-case

Мигрировать по одному calculator'у за раз:

1. `JumpOverFireWindowCalculator`
2. `SuperJumpOverFireWindowCalculator`
3. `JumpOnRoofFireWindowCalculator`
4. `SuperJumpOnRoofFireWindowCalculator`

Для каждого:

- перенести strategy-specific параметры из конструктора общего calculator в сам concrete calculator;
- заменить `OutcomeCalculator` на собственные методы concrete calculator;
- оставить только вызовы shared helpers для повторяемой механики;
- проверить, что strategy вызывает тот же внешний `TryFindFireShift` контракт.

Критерий готовности шага: concrete calculator читается как сценарий своей strategy, без необходимости открывать shared monster-class для понимания уникального поведения.

### 4. Разорвать retained validation зависимость от общего calculator

Текущая проблема: `JumpOutcomeRetainedValidator` зависит от `JumpOutcomeFireWindowCalculator`, поэтому concrete wrappers вынуждены отдавать `OutcomeCalculator` наружу.

Целевое состояние:

- `JumpOutcomeRetainedValidator` зависит от узкого способа проверить scheduled action, а не от monster-class;
- concrete calculator предоставляет retained-validation поведение для своей strategy;
- `OutcomeCalculator` property удалён из concrete calculators.

Практический вариант без лишних сущностей: передавать в retained validator method/delegate concrete calculator'а. Если делегат ухудшит читаемость, тогда ввести маленький контракт только для retained fire-shift validation.

### 5. Удалить `JumpOutcomeFireWindowCalculator`

Когда все consumers переведены:

- удалить `JumpOutcomeFireWindowCalculator.cs`;
- удалить устаревшие using/namespace references;
- проверить, что shared helpers имеют имена по смыслу и не образуют новый monster-layer;
- удалить helpers, которые остались single-use и не улучшают читаемость.

### 6. Cleanup структуры `Shared/JumpPlanning`

Проверить итоговую структуру папки:

- `Policies/` — только политики расчёта window/safety, если они реально переиспользуются;
- data/projection helpers — отдельно от policies;
- scan/selection helpers — отдельно от outcome matching;
- runtime resolver delegate оставить только если он реально нужен после переноса;
- diagnostics — отдельным небольшим компонентом или убрать, если логирование можно оставить локально в concrete calculator.

Цель: по названию файла должно быть понятно, какой один смысл он обслуживает.

### 7. Validation plan

После C# изменений по явному запросу пользователя:

- `invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_jump_over' -TimeoutSeconds 120 -TimeScale 2`
- `invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_superjump_over' -TimeoutSeconds 120 -TimeScale 2`
- `invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_jump_on_roof' -TimeoutSeconds 120 -TimeScale 2`
- `invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_super_jump_on_roof' -TimeoutSeconds 120 -TimeScale 2`
- `invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_switch_lane' -TimeoutSeconds 120 -TimeScale 2`

Для финального контроля полного набора из `Locations`:

- `invoke_run_all_test_levels.ps1 -TimeoutSeconds 120`

Для project-file sanity после удаления/создания `.cs`:

- Unity regenerate project files через automation bridge;
- `dotnet msbuild .\LostCyberHamster\Assembly-CSharp.csproj -nologo -t:ResolveReferences -p:Configuration=Debug -p:Platform=AnyCPU -v:minimal`.

## Риски и инварианты

- `TriggerObstacleInstanceId` и `TargetObstacleInstanceId` должны сохранять текущую семантику roof-entry actions.
- `actionTravel` и `postFireWorldShift` нельзя смешивать: первый описывает outcome travel, второй нужен retained/in-progress projection.
- `damageBigAliveWithoutYByReach` различается между обычным jump и super jump.
- `GroundContactPreFireSafetyPolicy` нужен roof-entry сценариям, чтобы bot не выбирал fire shift, до которого hamster погибает на земле.
- Road-small chain over matching — отдельное domain rule, его нельзя потерять при выделении matcher'а.
- Диагностика не должна менять результат расчёта и не должна становиться причиной зависимости concrete calculators от общего monster-class.

## Definition of Done

- `JumpOutcomeFireWindowCalculator` удалён или превращён в ненужный и затем удалён.
- Concrete fire-window calculators содержат strategy-specific orchestration и больше не являются пустыми wrappers.
- В `Shared/JumpPlanning` нет класса, который одновременно занимается window search, retained validation, outcome matching, projection и diagnostics.
- `OutcomeCalculator` property удалён из concrete calculators.
- `BotStrategyFactory` удалён, если к моменту реализации всё ещё используется только в одном месте.
- Все изменённые `.cs` попали в `Assembly-CSharp.csproj` после Unity regenerate.
- Быстрый набор bot test levels проходит без regression.