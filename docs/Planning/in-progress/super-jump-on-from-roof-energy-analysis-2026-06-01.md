# Super Jump On From Roof: energy analysis 2026-06-01

## Scope

Уровень: `01_New_York/Morning/test_super_jump_on_from_roof`, первый паттерн `test_super_jump_on_from_roof_01`.

Наблюдение пользователя: в первом паттерне бот выбирает `JumpOnFromRoof`, хотя вручную можно дождаться автоматического схода с крыши (`RunFromRoof -> Run`) и затем выполнить обычный `JumpOn`. Для super-вариантов эта же идея должна давать экономию энергии, если passive exit безопасен и не закрывает окно ground-действия.

## Источники

- `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_super_jump_on_from_roof/test_super_jump_on_from_roof.json`
- `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointDetector.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/Shared/JumpOnFromRoofTargetChainComposer.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanEvaluator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOnFromRoof/*`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOnFromRoof/*`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOn/*`
- `LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/RoofRunMechanics.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/JumpMechanics.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/RoofJumpMechanics.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/SuperRoofJumpMechanics.cs`
- `LostCyberHamster/Assets/Scripts/Consts.cs`
- `LostCyberHamster/Assets/Animations/Hamster/*.anim`

## Факты

- `test_super_jump_on_from_roof_01` содержит `bigNotAlive` (`type:4`) на `x=-6.4` и `smallAlive` (`type:0`) на `x=1.2`.
- Свежий ручной прогон от `2026-06-01 23:52` показывает, что для `_01` выбран не super-вариант: `SwitchLane -> JumpOnRoof(bigNotAlive) -> JumpOnFromRoof(smallAlive)`, затем `JumpOnFromRoof` завершается в `Run`.
- Свежий ручной прогон от `2026-06-01 23:58` показывает, что `PassiveRoofExit` для `_01` уже генерируется: `candidate target=smallAlive ... exitStart=4.93 completion=6.83 projection=8.55`, но selected plan остаётся `SwitchLane -> JumpOnRoof -> JumpOnFromRoof`.
- В том же прогоне `SuperJumpOnFromRoof` выбран в следующей связке уровня, соответствующей `test_super_jump_on_from_roof_02`: `JumpOnRoof(mediumNotAlive) -> SuperJumpOnFromRoof(smallAlive)`.
- `test_super_jump_on_from_roof_02` содержит `mediumNotAlive` (`type:11`) и road-chain из `smallNotAliveRoad` перед `smallAlive`: bottom-lane obstacles `type:2` на `x=-4.4`, `x=-2.8`, `x=-1.2`, затем `smallAlive` на `x=0.4`.
- В свежем логе `PassiveRoofExit` для `_02` режется `unsafeRunFromRoof`: при `decision=JumpOnFromRoofTarget` первый obstacle chain — `smallNotAliveRoad`, а окно passive exit — `exitStart=0.00`, `completion=1.90`.
- `JumpOnObjectiveRules.HighPriorityEnergyThreshold = 40`; при энергии не ниже порога planner целенаправленно охотится за jump-on target.
- `DecisionPointDetector` при roof-state пропускает passive roof-chain через `RoofRunProjection.TryFindLastPassiveRoof`, затем required builders идут в порядке: `RoofOccupantHazardChainBuilder`, `JumpOnFromRoofTargetChainBuilder`, `CurrentLaneGroundJumpOnTargetChainBuilder`, `BlockingThreatChainBuilder`.
- Если `JumpOnFromRoofTargetChainBuilder` нашёл road target после крыши, `CurrentLaneGroundJumpOnTargetChainBuilder` уже не вызывается для этого же state.
- `JumpOnFromRoofStrategy` и `SuperJumpOnFromRoofStrategy` обрабатывают только `DecisionPointKind.JumpOnFromRoofTarget`.
- `JumpOnStrategy` обрабатывает только `DecisionPointKind.GroundJumpOnTarget` и не строит action из roof-state.
- В registered strategies добавлен `PassiveRoofExitStrategy`, поэтому текущий регресс уже не в отсутствии candidate как такового.
- `JumpOnExecutor` уже умеет ждать `RunFromRoof`: если head action `JumpOn` пришёл раньше, executor возвращает `Waiting` до перехода в `Run`.
- `ActionGenerator.RemoveSuperJumpOnCandidatesCoveredByOrdinaryJumpOn` удаляет `SuperJumpOnFromRoof`, если для того же target есть ordinary `JumpOnFromRoof`. Значит super-вариант выбирается только когда ordinary roof-to-road candidate отсутствует или невалиден.
- `PlanEvaluator` сравнивает ветки по jump-on objective, затем по `TotalEnergyCost`, затем `TapCount`, затем таймингам. Если дешёвая ветка была бы в графе с тем же target objective, она должна выигрывать у 20-energy super action.
- Runtime energy: обычный `Jump` / `RoofJump` требуют 10 энергии; super-upgrade требует ещё 10. Bot policy кодирует `JumpOn`/`JumpOnFromRoof` как 10, `SuperJumpOnFromRoof` как 20.
- Travel из `.anim` и `Consts.GameSpeedBase = 3.8`: `run_from_roof = 1.9`, `jump = 3.8`, `jump_from_roof ~= 5.07`, `super_jump_from_roof ~= 4.94`, `super_roof_jump = 4.56`.
- `PassiveRoofExitSimulator` сейчас моделирует только завершённое состояние после полного `RunFromRoof`: `RoofRun -> Run` и `ProjectionWorldShift += exitStartShift + runFromRoofTravel`.
- `JumpOnStrategy` строит ground `JumpOn` только из уже ground-state (`!hamster.IsOnRoof`) и только при `DecisionPointKind.GroundJumpOnTarget`.
- Следовательно, текущая модель не может запланировать `JumpOn`, чей trigger-window начинается во время `RunFromRoof`: action появится только после полной симуляции `PassiveRoofExit`.
- Добавлен targeted trace `[PassiveRoofExit TRACE]`, который печатает только selected ветку, ветки с `PassiveRoofExit` и их `energy/taps/objective/finalProjection/sequence`.
- Ручной прогон от `2026-06-02 00:02` опроверг гипотезу, что passive-ветка не доходит до ground `JumpOn`: trace содержит `SwitchLane -> JumpOnRoof -> PassiveRoofExit -> JumpOn`.
- Для первого паттерна selected branch: `SwitchLane -> JumpOnRoof -> JumpOnFromRoof`, `energy=20`, `taps=1`, `objectiveCount=1`, `firstObjective=1`, `finalProjection=22.71`.
- Лучшая passive branch: `SwitchLane -> JumpOnRoof -> PassiveRoofExit -> JumpOn`, `energy=20`, `taps=1`, `objectiveCount=1`, `firstObjective=1`, `finalProjection=22.62`.
- Так как objective, energy, taps, first trigger и final next obstacle совпадают, `PlanEvaluator` выбирает active roof branch по большему `FinalProjectionWorldShift` (`22.71 > 22.62`).

## Вывод

Изначальный planner умел два разных класса решений:

1. Из `RoofRun`: активный `JumpOnFromRoof` / `SuperJumpOnFromRoof` по target-chain после крыши.
2. Из `Run`: ground `JumpOn`.

После добавления `PassiveRoofExit` третий сценарий представлен в search graph и доходит до полноценной ветки `PassiveRoofExit -> JumpOn`. Root cause текущего выбора для `_01`: при равной objective/energy/taps evaluator считает чуть больший `FinalProjectionWorldShift` более важным, чем предпочтение безопасного пассивного схода и обычного ground action.

## Архитектурное направление

Не добавлять special-case под `test_super_jump_on_from_roof_01`. Возможные архитектурные направления после trace:

- Новый decision-point builder имеет смысл только если мы хотим сделать policy на уровне detector'а: safe `PassiveRoofExit -> GroundJumpOnTarget` preempts `JumpOnFromRoofTarget`. Это похоже на существующий `RoofJumpOnTargetChainBuilder`, который строит future roof-state и target-chain заранее.
- Но по trace ветка уже есть; минимальное и более локальное решение — добавить явный tie-breaker в `PlanEvaluator`: при одинаковом jump-on objective, energy и taps предпочитать ветку с `PassiveRoofExit + JumpOn` перед веткой с `JumpOnFromRoof`/`SuperJumpOnFromRoof` для того же target, только если passive safety уже доказана candidate'ом.

Открытый риск: queued ground action нельзя разрешать, если strict safety gate для интервала `RunFromRoof` не доказывает безопасность same-lane non-roof damaging obstacles.
