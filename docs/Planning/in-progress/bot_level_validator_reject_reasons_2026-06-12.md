# Bot level validator reject reasons — 2026-06-12

## Goal

Сделать бота валидатором уровней: если безопасная ветка не строится, бот должен не молча ехать до damage, а фиксировать planning dead-end с понятной причиной для level designer.

Текущий код стратегий пока не возвращает typed reason: большинство проверок заканчивается `return false`. Этот документ описывает контракт причин, который нужно внедрять следующим этапом.

## Current planning path

1. `ActionGenerator.Generate` строит projected snapshot и ищет decision point на текущей и соседней линии.
2. Каждая `IPlanningStrategy.CollectActions` либо добавляет `PlannedAction`, либо молча отказывается.
3. Отказ обычно происходит в одном из узлов:
   - specification / applicability;
   - action resolver;
   - fire-window calculator;
   - runtime-equivalent outcome check;
   - during/post-action safety.
4. `PlanningGraphBuilder` не добавляет leaf, если candidates пустые и unresolved situation осталась.

## Classes

### Skip

Strategy не относится к текущей ситуации. Это нормальный фон, не level issue и не должен шуметь в отчете.

Примеры из кода:
- `JumpOverStrategy.TryResolveBlockingThreat`: первый chain element не `BlockingThreat`.
- `JumpOnStrategy`: в chain нет ground target для `JumpOn`.
- `RoofJumpOverActionResolver`: текущая ситуация не `RoofOccupantHazard`.
- `PassiveRoofExitPlanner`: hamster не в `RoofRun`.

### Reject

Strategy релевантна к текущей ситуации, но конкретный action не может быть построен. Эти причины надо накапливать как evidence.

### DeadEnd

Для ближайшей unresolved situation все релевантные strategies вернули `Reject`, и ни один action не построился. Это основной сигнал валидатора уровня.

Формат dead-end отчета:
- level id / pattern index, если доступны;
- projected hamster state/lane/energy;
- first obstacle id/type/lane/x interval/roles;
- chain left/right/count;
- список tried strategies с `RejectReasonCode` и числовыми деталями.

## Reject reason codes

### `MissingPlanningData`

Не хватает обязательных данных для расчета: snapshot, hamster, obstacle, chain, travel, collider/runtime-equivalent input.

Источники:
- null/empty checks в `JumpOnActionChainResolver`, `JumpOverChainCalculator`, `RoofJumpOverChainCalculator`;
- `TryGetTravel` в policy-классах, если не найден animation clip travel.

Класс: обычно bug/config issue, не level design issue.

### `InvalidHamsterState`

Action невозможен из текущего состояния: не `Run`, не `RoofRun`, уже `IsShifting`, `IsOnRoof` не соответствует strategy.

Источники:
- `JumpOnSpecification`, `JumpOverSpecification`, `JumpOnRoofSpecification`;
- `JumpFromRoofSpecification`, `RoofJumpOverSpecification`;
- `SwitchLaneSpecification`, `PassiveRoofExitPlanner.CanExitRoofPassively`.

Класс: `Skip`, если strategy не для этого state; `Reject`, если это единственный ожидаемый route.

### `InsufficientEnergy`

Геометрически action мог бы быть кандидатом, но `hamster.Energy < policy.EnergyCost`.

Источники:
- ground jump specs (`JumpOnSpecification`, `JumpOverSpecification`, roof landing specs);
- roof jump specs;
- executors также проверяют energy перед fire.

Класс: level validator issue, если участок требует energy, которой уровень не дает перед ним.

### `UnsupportedObstacleType`

Strategy применима по роли, но policy не умеет безопасно обработать тип obstacle.

Источники:
- `policy.CanJumpOverObstacle`;
- `ObstacleClassifier.CanJumpOnGroundObstacle`;
- `ObstacleClassifier.IsObstacleWithRoof`;
- roof/road filters в `JumpFromRoofActionResolver`.

Класс: обычно `Skip`; становится `Reject`, если это ближайшая обязательная угроза и другой strategy нет.

### `WrongLane`

Obstacle не на линии, для которой strategy строит action, либо target lane не совпадает с hamster lane.

Источники:
- lane checks в specifications;
- `JumpOnActionChainResolver.TryCreateActionElement`;
- `SwitchLane` target-lane checks.

Класс: чаще `Skip`.

### `NoRelevantTarget`

Decision point есть, но strategy не нашла нужный target внутри chain или расширенной action-chain.

Источники:
- `JumpOnActionChainResolver.TryFindGroundTarget`;
- `JumpOnRoofActionResolver.TryResolve`;
- `RoofJumpOverActionResolver.TryResolve`;
- `JumpFromRoofActionResolver.TryResolve`.

Класс: `Skip` или `Reject` в зависимости от роли первой угрозы.

### `TargetOutOfReach`

Target найден, но до него нельзя построить окно по travel/reach.

Источники:
- `JumpOnActionChainResolver.GetMaxReachableTargetLeftX`;
- `JumpOnWindowCalculator.TryGetOpenWindow`;
- `JumpOnRoofFireWindowFinder.TryGetRoofLandingWindow`;
- roof/from-roof chain calculators.

Класс: level design issue, если это ближайший обязательный route.

### `NoFireWindowTooClose`

Action был бы возможен раньше, но сейчас obstacle/chain уже слишком близко: latest fire shift <= 0 или окно схлопнулось справа.

Источники:
- `SwitchLaneFireWindowCalculator.TryGetLatestFireShift`;
- `JumpOverChainCalculator.TryGetOpenWindow`;
- `JumpOnWindowCalculator.TryGetOpenWindow`;
- `RoofJumpOverChainCalculator.TryGetOpenWindow`.

Класс: level design issue или missed timing issue.

### `NoFireWindowByMargin`

Геометрическое окно почти есть, но safety margin (`JumpPlanningConstants.FireWindowBoundaryMargin`) съел допустимый диапазон.

Источники:
- jump-over / jump-on / roof jump calculators, где `firstFireShift += margin`, `lastFireShift -= margin`.

Класс: важный validator сигнал: участок слишком плотный для безопасной игры.

### `NoSafeSwitchLaneInterval`

SwitchLane имеет deadline, но все fire intervals на target lane unsafe.

Источники:
- `SwitchLaneFireWindowCalculator.CollectUnsafeFireIntervals`;
- roof-support intersection for roof lane switch.

Класс: level design issue, если смена линии была единственным route.

### `RuntimeOutcomeMismatch`

Аналитическое окно найдено, но runtime-equivalent resolver дает не ожидаемый outcome или не тот target index.

Источники:
- `JumpOverFireWindowFinder.CheckRuntimeOutcomeAtFireShift`;
- `JumpOnFireWindowFinder.CheckRuntimeOutcomeAtFireShift`;
- `JumpOnRoofFireWindowFinder.CheckRuntimeOutcomeAtFireShift`;
- `JumpFromRoofFireWindowFinder.CheckRuntimeOutcomeAtFireShift`;
- `RoofJumpOverFireWindowFinder.TryGetRuntimeOutcomeAtFireShift`.

Класс: может быть level geometry issue или mismatch planning/runtime model.

### `UnsafeDuringAction`

Во время перехода action пересекает damaging obstacle.

Источники:
- `RoofExitSafety.IsSafeDuringRunFromRoof`;
- roof jump-over continuation support checks;
- SwitchLane unsafe intervals during decision travel.

Класс: level design issue или strategy gap.

### `UnsafeAfterAction`

Action сам успешен, но после completion hamster возвращается в `Run`/`RoofRun` в опасной зоне или без guard до следующего окна.

Источники:
- `TargetRemovalPostActionSafety.IsSafeAfterCompletion`;
- post-roof support checks in roof jump-over.

Класс: прямой validator signal. Текущий `JumpOn -> next smallAlive too close` относится сюда или к следующему `NoSafeContinuation`, в зависимости от того, на каком уровне диагностировать.

### `NoSafeContinuation`

Retained/current action возможен, но после его simulation остается unresolved situation, а tail plan не строится.

Источники:
- `PlanningGraphBuilder`: candidates empty + unresolved situation => no leaf;
- будущий validator должен фиксировать это явно на уровне graph/controller, а не превращать в silent fallback.

Класс: основной dead-end signal для level designer.

### `PlanningDepthLimit`

Достигнут `MaxSearchDepth = 6`, но ситуация все еще unresolved, поэтому branch не становится leaf.

Источники:
- `PlanningGraphBuilder.ExploreNode`.

Класс: planner limitation. Не считать level design issue без дополнительного подтверждения.

### `ExecutionContractFailed`

План был построен, но runtime executor отменил head action.

Источники:
- `ActionTriggerGate`: trigger obstacle missing, window closed, trigger passed;
- executors: invalid state, insufficient energy, missing target id.

Класс: runtime validation issue. Для level validator полезно связывать с последним planned action и snapshot.

## Proposed implementation shape

Минимальная модель без over-engineering:

```csharp
internal enum StrategyRejectClass
{
    Skip,
    Reject,
    DeadEnd
}

internal enum StrategyRejectReasonCode
{
    MissingPlanningData,
    InvalidHamsterState,
    InsufficientEnergy,
    UnsupportedObstacleType,
    WrongLane,
    NoRelevantTarget,
    TargetOutOfReach,
    NoFireWindowTooClose,
    NoFireWindowByMargin,
    NoSafeSwitchLaneInterval,
    RuntimeOutcomeMismatch,
    UnsafeDuringAction,
    UnsafeAfterAction,
    NoSafeContinuation,
    PlanningDepthLimit,
    ExecutionContractFailed
}
```

`CollectActions` можно расширять не сразу глобально, а через lightweight collector:

```csharp
void CollectActions(
    PlanningState planningState,
    WorldSnapshot worldSnapshot,
    DecisionPoint decisionPoint,
    List<PlannedAction> actions,
    StrategyRejectCollector rejects);
```

На первом этапе collector нужен только в bot validator/test mode. В обычном runtime он может быть `null`, чтобы не шуметь и не аллоцировать лишние детали.

## Validator behavior

1. При `NO_ACTIONS` для unresolved decision point собрать rejects от релевантных strategies.
2. Если все релевантные strategies отказались, создать `DeadEnd` report.
3. Остановить level validation run и вывести summary:
   - first obstacle;
   - distances/window values;
   - top 3 reject reasons by relevance;
   - recommended owner: `level-design`, `strategy-gap`, `planner-limit`, `runtime-model-mismatch`, `config-bug`.

## Notes

- `Skip` не должен засорять отчет: он нужен только для объяснения, почему strategy не участвовала.
- `Reject` должен хранить числа: `firstFireShift`, `lastFireShift`, `margin`, `energy`, `requiredEnergy`, obstacle ids.
- `DeadEnd` не должен автоматически чинить plan. Его задача — явно показать непроходимый участок уровня.
