# JumpOn target-focus switchlane regression analysis

## Scope

Регресс: при активных role-based стратегиях `SwitchLane`, `JumpOver`, `SuperJumpOver`, `JumpOn`, `SuperJumpOn` бот не перестраивается на линию ближайшего ground `Target` и не строит `JumpOn`/`SuperJumpOn`; визуально хомяк продолжает бежать без действия.

Точный уровень/скорость из пользовательского сообщения не указаны. Анализ ниже основан на active runtime code path и доступном tail `BOT/STAB/ECO` логов.

Ожидаемое поведение: если при `Run` и достаточной энергии есть huntable target на другой линии, planner строит branch вида `SwitchLane -> JumpOn` или `SwitchLane -> SuperJumpOn`, evaluator выбирает target-oriented branch.

Фактическое поведение по code path: для off-lane target detector строит focus-chain на линии target, но все активные ground strategies отбрасывают эту situation, поэтому graph не получает candidates.

## Источники данных

- `docs/rules/AGENTS.md`
- `docs/rules/bug_regression_analysis_workflow.md`
- `docs/rules/iteration_cycle.md`
- `docs/rules/agent_tools.md`
- `docs/Planning/in-progress/role_based_planning_00_overview.md`
- `docs/Planning/in-progress/role_based_strategy_jump_on_roof.md`
- `docs/Planning/in-progress/role_based_strategy_jump_on_from_roof.md`
- `docs/Planning/in-progress/role_based_strategy_super_jump_on_roof.md`
- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilderNew.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGeneratorNew.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilderNew.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPointsNew/DecisionPointDetectorNew.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPointsNew/ObstacleChainBuilderNew.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPointsNew/ObstacleRoleClassifierNew.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/ObstacleClassifier.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/JumpOnObjectiveRules.cs`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/SwitchLane/*`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/JumpOver/*`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/SuperJumpOver/*`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/JumpOn/*`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/SuperJumpOn/*`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/Shared/JumpOn/*`
- `LostCyberHamster/Assets/Scripts/Bot/StrategiesNew/Shared/JumpOver/*`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointDetector.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/Builders/GroundJumpOnTargetChainBuilder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/Shared/GroundJumpOnTargetChainComposer.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneSpecification.cs`
- `tools/read_log_channel.ps1 -Channel BOT/STAB/ECO`

## Гипотезы

1. `JumpOn` strategies не подключены в runtime.
   - Подтверждение: отсутствуют в `RuntimeBotController` strategy list.
   - Опровержение: `RuntimeBotController.Awake` содержит `new JumpOnStrategyNew()` и `new SuperJumpOnStrategyNew()`.
   - Статус: опровергнута.

2. Detector выбирает target lane, но `SwitchLaneStrategyNew` не умеет использовать off-lane focus-chain как причину перестроения.
   - Подтверждение: `DecisionPointDetectorNew.ResolveFocusBottomLine` возвращает lane ближайшего target, а `SwitchLaneSpecificationNew` требует `obstacle.IsBottomLine == hamster.IsOnBottomLine`.
   - Опровержение: отдельный path генерирует `SwitchLane` к focus lane без same-lane blocking threat.
   - Статус: подтверждена по коду.

3. `JumpOn`/`SuperJumpOn` сами должны создавать action на off-lane target.
   - Подтверждение: specification допускает target lane != hamster lane.
   - Опровержение: `JumpOnSpecificationNew` требует `obstacle.IsBottomLine == hamster.IsOnBottomLine`.
   - Статус: опровергнута.

4. Другие active strategies создают обходной branch, поэтому пустого плана быть не должно.
   - Подтверждение: `JumpOver`, `SuperJumpOver` или `SwitchLane` проходят specification на off-lane focus-chain.
   - Опровержение: `JumpOverSpecificationNew` и `SwitchLaneSpecificationNew` требуют obstacle на текущей линии.
   - Статус: опровергнута.

## Факты по коду

- Active runtime strategies в `RuntimeBotController.Awake`: `SwitchLaneStrategyNew`, `JumpOverStrategyNew`, `SuperJumpOverStrategyNew`, `JumpOnStrategyNew`, `SuperJumpOnStrategyNew`.
- `DecisionPointDetectorNew.ResolveFocusBottomLine` включает target-focus только если `CanUseTargetFocus`: state `Run` или `RoofRun`, not shifting, `JumpOnObjectiveRules.HasEnergyForJumpOnObjective`.
- `JumpOnObjectiveRules.HighPriorityEnergyThreshold = 40`.
- Для `Run` huntable target определяется только через `ObstacleClassifier.CanJumpOnGroundObstacle`, а сейчас это только `smallAlive`.
- `ObstacleRoleClassifierNew` помечает `smallAlive` одновременно как `BlockingThreat` и `Target`, потому что `DamagesOnGroundContact(smallAlive)` и `CanJumpOnGroundObstacle(smallAlive)` оба true.
- `ObstacleChainBuilderNew` строит one-line chain только для выбранной `focusBottomLine`.
- Если ближайший target находится на другой линии, decision point chain будет на другой линии.
- `JumpOnStrategyNew.TryResolveTarget` и `SuperJumpOnStrategyNew.TryResolveTarget` найдут target в такой chain, но `JumpOnSpecificationNew` затем вернет false, потому что требует `obstacle.IsBottomLine == hamster.IsOnBottomLine`.
- `SwitchLaneStrategyNew.TryResolveBlockingThreat` найдет `smallAlive` как `BlockingThreat` в off-lane focus-chain, но `SwitchLaneSpecificationNew` вернет false, потому что требует `obstacle.IsBottomLine == hamster.IsOnBottomLine`.
- `JumpOverStrategyNew` и `SuperJumpOverStrategyNew` также отфильтруют off-lane first obstacle через `JumpOverSpecificationNew`, где есть та же lane-проверка.
- Если все strategies вернули 0 actions, `PlanningGraphBuilderNew.ExploreNode` не добавит leaf branch, пока `HasUnresolvedPlanningSituation` остается true.
- При пустом branch list `PlanEvaluator.SelectBest` возвращает null, а `PlanBuilderNew.Build` возвращает `BotPlan.Empty`.
- В старом path `DecisionPointDetector.DetectOptionalDecisionPoints` добавлял `OtherLaneGroundJumpOnTargetChainBuilder`, когда `CanSearchJumpOnObjective` true.
- `OtherLaneGroundJumpOnTargetChainBuilder` специально искал obstacle на другой линии, строил target-chain и создавал optional `DecisionPointKind.GroundJumpOnTarget`.
- Старый `SwitchLaneSpecification.IsSatisfiedBy(planningState, decisionPoint, ...)` не требовал, чтобы `decisionPoint.Chain.FirstObstacle` был на текущей линии; он проверял только state/shifting и `DamagesOnGroundContact`.
- Старый `SwitchLaneStrategy` для optional objective использовал `decisionPoint.UsesObjectiveSwitchLaneTiming` и ранний sampling ratio, затем строил `SwitchLane` к opposite lane.

## Факты по логам

- Доступный tail `BOT` показывает старые успешные прогоны с `SuperJumpOver` и `SwitchLane`, но не содержит строк `JumpOn`/`SuperJumpOn` и не содержит свежего воспроизведения описанного регресса.
- `STAB` tail содержит только `WIN level=16 stars=3`.
- `ECO` tail пуст.
- Следовательно, runtime-логами конкретный регресс пока не подтвержден; текущий root cause выведен из полного code path.

## Статус гипотез

- Root cause по коду: подтвержден с высокой уверенностью для сценария "target на другой линии".
- Остается неопределенность: не доказано логами, что конкретный пользовательский прогон был именно off-lane `smallAlive` при `Energy >= 40`; для этого нужен свежий `BOT` tail или точечный verbose лог decision point.

## Корень проблемы

`DecisionPointDetectorNew` уже переключает focus-chain на lane target-а, но `SwitchLaneStrategyNew` по-прежнему трактует выбранный `BlockingThreat` как same-lane threat и требует, чтобы obstacle был на текущей линии. В target-focus сценарии это неверный контракт: obstacle находится на target lane, а action должен быть `SwitchLane` на эту lane как подготовка к последующему `JumpOn`.

Регрессионный сдвиг относительно старого path: old optional off-lane target chain мог использовать obstacle другой линии как anchor для `SwitchLane`; new `SwitchLaneSpecificationNew` добавила same-lane check и закрыла именно этот use case.

Из-за этого root node не получает `SwitchLane` candidate. `JumpOn`/`SuperJumpOn` также не могут стартовать, потому что их specification требует, чтобы хомяк уже был на target lane. Graph остается без actions и не строит branch `SwitchLane -> JumpOn`.

## Решение

Архитектурно нужно разделить два сценария `SwitchLane` в role-based path:

1. Same-lane avoidance: текущая логика `BlockingThreat` на линии хомяка.
2. Target-focus lane acquisition: focus-chain на другой линии, первый target/threat находится на целевой линии, а `SwitchLane` должен планироваться по safe-window target lane и после симуляции дать graph следующую decision point уже на target lane.

Правка не должна ослаблять `JumpOnSpecificationNew`: `JumpOn` должен оставаться действием только с текущей линии. Исправлять нужно в `SwitchLaneStrategyNew`/specification или в явном helper-е applicability для off-lane target-focus, чтобы branch `SwitchLane -> JumpOn` создавался до target-window.

## Проверка

Требуется после фикса:

- Прогнать уровень с off-lane `smallAlive` target при `Energy >= 40`.
- Проверить в `BOT`, что появляется branch/head `SwitchLane` к target lane, затем `JumpOn` или `SuperJumpOn`.
- Контроль: same-lane blocking threat по-прежнему строит avoidance actions.
- Контроль: off-lane non-target threat не вызывает target-hunt switch.
