# Jump on Roof small not-alive analysis — 2026-06-07

## Scope

- Регресс: на тестовом уровне `01_New_York/Morning/test_jump_on_roof` бот выбирает `JumpOn`, хотя в кейсе присутствует связка `small_not_alive` на дороге и roof.
- Дополнительный симптом: runtime почему-то не наносит damage.
- Цель: доказать корень проблемы по коду, данным уровня и логам, затем предложить архитектурное решение без подбора magic threshold под один кейс.

## Источники данных

- Уровень: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_jump_on_roof/test_jump_on_roof.json`.
- Паттерны: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Runtime trace: `LostCyberHamster/EditorLogs/diagnostic_log.txt`, запись от `2026-06-07 16:58:03`.
- Active bot path:
  - `RuntimeBotController`
  - `PlanBuilderNew`
  - `PlanningGraphBuilderNew`
  - `ActionGeneratorNew`
  - `DecisionPointDetectorNew`
  - `ObstacleChainBuilderNew`
  - `ObstacleRoleClassifierNew`
  - `JumpOnRoofStrategyNew`
  - `JumpOnRoofActionResolver`
  - `JumpOnRoofFireWindowFinderNew`
  - `RoofRunProjection`
- Runtime damage path:
  - `JumpMechanics`
  - `JumpOutcomeResolver`
  - `CollisionController`
  - `HamsterAnimationEventsMechanics`
  - `TakeDamageMechanics`
  - `RuntimeBotEventTracker`
- Specs/plans:
  - `docs/Planning/hamster-obstacle-runtime-spec.md`
  - `docs/Planning/done/role_based_strategy_jump_on_roof.md`
  - `docs/Planning/done/role_based_strategy_roof_jump_over.md`

## Гипотезы

### H1. Planner классифицирует `small_not_alive` на дороге как валидную цель для `JumpOn`.

- Подтверждает: call path `test level -> snapshot -> decision point/action generation -> JumpOn` допускает `SMALL_NOTALIVE` как target или не учитывает, что road obstacle не alive.
- Опровергает: target-фильтр явно запрещает `SMALL_NOTALIVE`, а выбранный `JumpOn` относится к другому obstacle.

### H2. Planner выбирает `JumpOn` не на road obstacle, а на roof support, но ситуация требует другого действия из-за опасности road/roof chain.

- Подтверждает: action target в логе/коде указывает на roof obstacle; safety/evaluator не учитывает одновременный road `small_not_alive`.
- Опровергает: planner target указывает на road obstacle или safety явно учитывает road/roof conflict.

### H3. Runtime не дамажит из-за collider/lane/state guard, который считает столкновение безопасным при `JumpOn`/roof-state.

- Подтверждает: runtime damage path от collision/trigger до health guard отбрасывает obstacle из-за состояния хомяка, layer/lane, category или action state.
- Опровергает: damage path вызывается и применяет damage, а симптом связан только с логом/визуалом.

### H4. Тестовый уровень/паттерн не соответствует ожидаемому описанию: obstacle type, lane, roof flag или collider отличается от предположения.

- Подтверждает: JSON/asset содержит другой obstacle category/location/bounds, чем "small not alive road and roof".
- Опровергает: данные уровня прямо соответствуют описанию.

## Факты по коду

### Данные уровня

- `test_jump_on_roof.json` запускает sequence: `test_jump_on_roof_01`, `relief`, `test_jump_on_roof_02`, `relief_energy`, `test_jump_on_roof_03`, `relief`, `test_jump_on_roof_04`, `relief`, `test_jump_on_roof_05`.
- `test_jump_on_roof_04` имеет description `should not jump on roof`.
- В `test_jump_on_roof_04`:
  - `mediumNotAlive` id `17`, x `-5.0`, y `-1.8`, примерный X interval `[-6.72, -3.28]`;
  - `smallNotAliveRoadAndRoof` id `19`, x `-5.4`, y `-0.3`, примерный X interval `[-6.10, -4.70]`;
  - `mediumNotAlive` id `18`, x `-1.4`, y `-1.8`, примерный X interval `[-3.12, 0.32]`;
  - `bigAlive` id `16`, x `-5.8`, y `-2.8`, примерный X interval `[-6.30, -5.30]`.
- `ObstacleLaneResolver` считает roof anchors частью той же линии: `roadY`, `roadY + BIG_NOTALIVE_HEIGHT_UNITS + RoofOffset`, `roadY + MEDIUM_NOTALIVE_HEIGHT_UNITS + RoofOffset`.
- Следовательно `smallNotAliveRoadAndRoof` на y `-0.3` относится к top lane как occupant на `mediumNotAlive`, а не как отдельная bottom-lane дорожная цель.

### Полный active call path выбора action

- `RuntimeBotController.Awake()` подключает new pipeline: `ActionGeneratorNew`, `TransitionSimulatorNew`, `PlanEvaluator`, `RetainedActionRevalidatorNew`, `ActionInProgressProjectorNew` и стратегии `StrategiesNew/*`.
- `TickBot()` строит `WorldSnapshot`, тикает executor и вызывает `TrySetNewPlan()`.
- `PlanBuilderNew.Build()` создает root `PlanningState.FromSnapshot()`, затем `PlanningGraphBuilderNew.BuildBranches()`, затем `PlanEvaluator.SelectBest()`.
- `ActionGeneratorNew.Generate()` строит projected snapshot и вызывает `DecisionPointDetectorNew.TryDetect()` для current lane и opposite lane.
- `DecisionPointDetectorNew` строит one-line chain через `ObstacleChainBuilderNew`.
- `ObstacleRoleClassifierNew.GetRoles()`:
  - `smallNotAliveRoadAndRoof` получает `BlockingThreat` через `ObstacleClassifier.DamagesOnGroundContact()`;
  - `smallNotAliveRoadAndRoof` не получает `Target`, потому что `CanJumpOnGroundObstacle()` возвращает true только для `smallAlive`, а `CanJumpOnFromRoofObstacle()` только для `smallAlive` и `bigAlive`;
  - `RoofOccupantHazard` вычисляется через `RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath()`, который требует roof-state (`hamster.IsOnRoof && RoofSupportInstanceId.HasValue`), поэтому до посадки на roof этот occupant не становится roof hazard.
- `JumpOnRoofStrategyNew.CollectActions()`:
  - `JumpOnRoofActionResolver.TryResolve()` выбирает первый `RoofSupport` в chain;
  - `JumpOnRoofSpecificationNew.IsSatisfiedBy()` проверяет только `Run`, not roof, not shifting, energy и same lane;
  - `JumpOnRoofFireWindowFinderNew.TryFindFireShift()` проверяет landing window и runtime outcome.
- `JumpOnRoofFireWindowFinderNew.CalculateLastFireShift()` учитывает только `chain` до `roofChainIndex`. Если выбранная крыша является первым `RoofSupport`, более поздний `smallNotAliveRoadAndRoof` на этой же крыше не ограничивает окно.
- `TryFindEarliestResolverValidFireShift()` принимает fire shift, если resolver возвращает `ExpectedRoofState` и `targetObstacleIndex`.
- Resolver уже учитывает `smallNotAliveRoadAndRoof` на выбранной roof: `JumpOutcomeResolver.HandleRoofObstacle()` вызывает `TryFindDamagingRoofOccupantOnRoof()` и возвращает `JumpOnRoofDamage`, если в resolver-точке есть X-overlap с occupant.
- Поэтому текущий planner не игнорирует runtime resolver. Недостающая часть - не сам факт roof occupant, а safety вне одной sampled outcome-точки: swept contact во время `JumpOnRoof` и/или held contact после перехода в `RoofRun`.
- `role_based_strategy_jump_on_roof.md` требует: "Если посадка близко к occupant/hazard небезопасна, action не добавляется". В текущем `JumpOnRoofFireWindowFinderNew` такой отдельной проверки нет.

### Runtime damage path

- `JumpMechanics.OnJump()` вызывает `CalculateJumpState()`, затем выставляет `HamsterState`.
- `JumpOutcomeResolver.ResolveJump()` для `bigNotAlive`/`mediumNotAlive` вызывает `HandleRoofObstacle()`.
- `HandleRoofObstacle()` может вернуть `JumpOnRoofDamage`, если `TryFindDamagingRoofOccupantOnRoof()` нашел `smallNotAliveRoadAndRoof` на этой roof и в resolver-точке есть X-overlap с occupant.
- `HamsterAnimationEventsMechanics` на `transform_jump_on_roof_end` вызывает `DamageEvent`, если state `JumpOnRoofDamage` или `SuperJumpOnRoofDamage`, затем переводит state в `RoofRun`.
- `TakeDamageMechanics.OnDamageEvent()` уменьшает `Lives`, включает blink и ставит `IsDamaged = true`.
- `RuntimeBotEventTracker.OnDamage()` пишет `[Bot DAMAGE]`.
- `CollisionController.ProcessTriggerEnter()` в `JumpOnRoof` наносит damage только для `bigAlive` через `HasCollisionWithBigAliveInJumpState()`. `smallNotAliveRoadAndRoof` во время `JumpOnRoof` не дамажит на enter.
- `CollisionController.OnTriggerStay2D()` запускает `ProcessTriggerStay()` только если state `Run` или `NeedCheckCollisionInRunFromRoofAfterShift`. `RoofRun` здесь отсутствует.
- При этом `HasCollisionInRunState()` уже содержит ветку для `RoofRun`: в `RoofRun` damage должен наноситься любым non-roof obstacle.
- Runtime spec `hamster-obstacle-runtime-spec.md` подтверждает intended behavior: в `RoofRun` урон получает любой same-line non-roof obstacle.

### Числовые константы

- `Consts.PIXELS_TO_UNITS_RATIO = 0.01`.
- `MEDIUM_NOTALIVE_WIDTH = 344`, значит ширина `3.44`.
- `SMALL_NOTALIVE_WIDTH = 140`, значит ширина `1.40`.
- `BIG_ALIVE_WIDTH = 100`, значит ширина `1.00`.
- `ObstacleRoofY0Pos = -1.8 + 1.72 + 0.1 = 0.02` для big roof.
- Medium roof anchor из `ObstacleLaneResolver` равен `-1.8 + 1.40 + 0.1 = -0.30`, что совпадает с y `smallNotAliveRoadAndRoof` в pattern 04.

## Факты по логам

- Актуальный `diagnostic_log.txt` содержит свежий trace от `16:58`, но `EditorLogs/automation/test_level_response.json` старее (`updatedAtUtc=2026-06-07T13:19:42Z`) и не использовался как подтверждение уровня.
- В trace есть результат:
  - `[16:58:45.217] [DIAG][CH=STAB] [TEST RESULT] WIN level=6 stars=3`
  - `[16:58:45.220] [DIAG][CH=BOT] [TEST FINISH] state=FINISHED lives=3`
- В trace есть проблемная последовательность:
  - `[16:58:24.872] [Bot EXEC] FIRE kind=SwitchLane ... targetLane=bottom desc=Switch lane before smallNotAliveRoad`
  - `[16:58:25.456] [Bot EXEC] FIRE kind=JumpOnRoof ... desc=Jump on roof mediumNotAlive`
  - `[16:58:26.470] [Bot EXEC] COMPLETE kind=JumpOnRoof state=RoofRun desc=Jump on roof mediumNotAlive`
  - дальше damage log отсутствует, а lives остаются `3`.
- В trace нет `[Bot DAMAGE]` и нет `[CollisionController] damage`, значит `DamageEvent` не был вызван в этом прогоне.
- Verbose window logs (`[JumpOnRoof WINDOW]`) в файле отсутствуют, поэтому точный selected `fireShift` по текущему trace не восстановлен.

## Статус гипотез

- H1: опровергнута. `smallNotAliveRoadAndRoof` не классифицируется как jump-on target; выбранное действие из лога - `JumpOnRoof mediumNotAlive`, то есть target roof support, а не small obstacle.
- H2: подтверждена с уточнением. Planner выбирает `RoofSupport`; runtime resolver outcome уже отбрасывает candidate, если occupant пересекается в resolver-точке. Но до roof-state occupant не получает `RoofOccupantHazard`, а `JumpOnRoof` не проверяет swept/handoff safety вокруг посадки.
- H3: подтверждена как runtime lifecycle gap для held overlap. `smallNotAliveRoadAndRoof` во время `JumpOnRoof` игнорируется на enter, а `OnTriggerStay2D` не обрабатывает `RoofRun`, хотя `HasCollisionInRunState()` и runtime spec требуют damage для non-roof obstacle в `RoofRun`. Если intended damage должен возникать уже от swept contact во время `JumpOnRoof`, owner этой семантики - runtime resolver, а не специальная ветка `CollisionController`.
- H4: опровергнута для problem pattern. `test_jump_on_roof_04` действительно содержит `smallNotAliveRoadAndRoof` на roof-Y anchor поверх `mediumNotAlive` и помечен как `should not jump on roof`.

## Корень проблемы

Корень проблемы состоит из двух связанных дефектов.

1. Planner/runtime-outcome boundary defect: `JumpOnRoof` safety проверяет только sampled resolver outcome посадки на выбранный `RoofSupport`. Resolver учитывает `smallNotAliveRoadAndRoof` на этой roof, но только если есть overlap в resolver-точке. Role `RoofOccupantHazard` появляется только после перехода в roof-state, поэтому pre-landing выбор `JumpOnRoof` может пройти через `JumpOnRoofActionResolver` и `JumpOnRoofFireWindowFinderNew`, хотя pattern явно `should not jump on roof`.

2. Runtime held-collision defect: если collision с `smallNotAliveRoadAndRoof` начинается во время `JumpOnRoof`, `ProcessTriggerEnter()` его не дамажит. Если overlap удерживается после перехода в `RoofRun`, он тоже не проверяется, потому что `OnTriggerStay2D()` не включает `RoofRun` в `shouldCheckHeldRunCollision`. Поэтому неправильный/недостаточно безопасный planner choice может не привести к expected damage и уровень заканчивается `WIN lives=3`.

## Решение

### Planner / resolver

- Сначала решить desired runtime semantics:
  - если contact с roof occupant во время `JumpOnRoof` должен давать damage, расширить `JumpOutcomeResolver`/`SuperJumpOutcomeResolver` с endpoint-overlap до swept-overlap по `smallNotAliveRoadAndRoof` на выбранной roof. Тогда planner автоматически унаследует fix, потому что `JumpOnRoofFireWindowFinderNew` уже использует runtime resolver outcome;
  - если damage должен возникать только после перехода в `RoofRun`, оставить resolver как point outcome и добавить post-landing handoff safety в shared `JumpOnRoof` path.
- Для planner-only handoff safety в shared `JumpOnRoof` path (`JumpOnRoofFireWindowFinderNew` или отдельный helper рядом с ним) добавить post-landing/near-landing roof hazard safety для `JumpOnRoof` и `SuperJumpOnRoof`.
- Проверка должна работать от выбранной roof support и выбранного fire shift:
  - построить projected world на момент landing/completion;
  - построить future `RoofRun` state с `ResultRoofSupportInstanceId`;
  - найти `smallNotAliveRoadAndRoof`, который лежит на passive roof path выбранной support;
  - запретить fire shift/action, если occupant уже перекрывает хомяка на landing или находится настолько близко, что до следующего planning/execution шага collision lifecycle будет небезопасен.
- Не решать это через magic margin под pattern 04. Владелец инварианта - `JumpOnRoof` safety, потому что именно эта стратегия утверждает "посадка на крышу безопасна".
- Если хочется разрешить сложные варианты "прыгнуть на крышу и сразу безопасно roof-jump-over", это должно быть доказано graph-level веткой с корректной retained/execution safety. Для `test_jump_on_roof_04` expected label `should not jump on roof` говорит, что минимальный fix должен отбрасывать landing на occupied roof.

### Runtime

- В `CollisionController.OnTriggerStay2D()` включить `RoofRun` в held collision check, например через helper `ShouldCheckHeldRunCollision()`:
  - `Run`;
  - `RoofRun`;
  - `NeedCheckCollisionInRunFromRoofAfterShift`.
- Это согласует `OnTriggerStay2D()` с уже существующим `HasCollisionInRunState()`, где `RoofRun` damage для non-roof obstacle уже реализован.
- Дополнительно рассмотреть точечную проверку на `transform_jump_on_roof_end`: если после перехода в `RoofRun` уже есть held non-roof overlap, damage не должен ждать нового trigger enter. Но минимально достаточная runtime-правка - добавить `RoofRun` в stay path.

## Проверка

- Автопрогон не запускался: задача была на анализ, код не менялся.
- После реализации:
  - `01_New_York/Morning/test_jump_on_roof` должен перестать выбирать `JumpOnRoof` на pattern `test_jump_on_roof_04`.
  - В отдельной runtime-проверке forced/manual collision `RoofRun` + `smallNotAliveRoadAndRoof` held overlap должен давать `[CollisionController] damage` и `[Bot DAMAGE]`.
  - Регрессии: `test_jump_on_roof_01`, `02`, `03`, `05`, `test_super_jump_on_roof`, roof occupant hazard / roof jump over scenarios.
